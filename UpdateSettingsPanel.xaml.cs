using System;
using System.Globalization;
using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace LoLIdleDesktop
{
    // Aba "Sobre" dos Ajustes: identidade do app (nome, versão, stack, créditos) e a
    // seção de Atualização — checar manualmente, abrir a página da release e ignorar a versão atual.
    public partial class UpdateSettingsPanel : UserControl
    {
        private readonly MainWindow _host;
        private bool _checking;

        public UpdateSettingsPanel(MainWindow host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            InitializeComponent();

            _host.UpdateStateChanged += OnUpdateStateChanged;
            Unloaded += (_, _) => _host.UpdateStateChanged -= OnUpdateStateChanged;

            Refresh();
        }

        private void OnUpdateStateChanged(object? sender, EventArgs e) => Refresh();

        // Repinta a tela a partir do estado atual no MainWindow (fonte única da verdade).
        private void Refresh()
        {
            InstalledVersionText.Text = _host.InstalledVersionDisplay ?? "desconhecida";

            if (_host.LatestReleaseVersion != null)
            {
                string tag = _host.LatestReleaseVersion;
                string? name = _host.LatestReleaseName;
                LatestReleaseText.Text = string.IsNullOrWhiteSpace(name) ||
                                         string.Equals(name, tag, StringComparison.OrdinalIgnoreCase)
                    ? tag
                    : $"{tag} — {name}";

                if (TryFormatPublishedAt(_host.LatestReleasePublishedAt, out string published))
                {
                    PublishedAtText.Text = published;
                    PublishedAtRow.Visibility = Visibility.Visible;
                }
                else
                {
                    PublishedAtRow.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                LatestReleaseText.Text = "—";
                PublishedAtRow.Visibility = Visibility.Collapsed;
            }

            LastCheckText.Text = _host.LastUpdateCheckAt.HasValue
                ? $"Última verificação: {_host.LastUpdateCheckAt.Value:dd/M/yyyy HH:mm}"
                : "Última verificação: nunca";

            if (_host.HasUpdate)
            {
                StatusText.Text = _host.VelopackUpdateDownloadFailed
                    ? "Falha ao baixar a atualização — verifique sua conexão e tente de novo."
                    : $"Atualização disponível — {_host.LatestReleaseVersion}";
            }
            else if (_host.LatestReleaseIgnored)
            {
                StatusText.Text = $"Atualização {_host.LatestReleaseVersion} ignorada";
            }
            else if (_host.LatestReleaseVersion != null)
            {
                StatusText.Text = _host.InstalledVersionUnknown
                    ? "Versão instalada desconhecida — não foi possível comparar."
                    : "Já temos a versão mais recente";
            }
            else if (_host.LastUpdateCheckFailed)
            {
                StatusText.Text = "Não foi possível verificar agora (offline/erro) — tente de novo.";
            }
            else
            {
                StatusText.Text = _host.LastUpdateCheckAt.HasValue
                    ? "Você está na versão mais recente"
                    : "Verificação ainda não concluída.";
            }

            // "Abrir release no navegador": visível sempre que há release conhecida (mesmo ignorada).
            // "Ignorar esta versão": só quando há atualização disponível.
            bool showOpen = _host.LatestReleaseVersion != null;
            bool showIgnore = _host.HasUpdate;
            OpenReleaseButton.Visibility = showOpen ? Visibility.Visible : Visibility.Collapsed;
            IgnoreButton.Visibility = showIgnore ? Visibility.Visible : Visibility.Collapsed;
            UpdateActionsPanel.Visibility = (showOpen || showIgnore) ? Visibility.Visible : Visibility.Collapsed;

            // Botão reflete checagem em andamento (startup ou clique aqui): desabilitado + "Procurando...".
            bool busy = _host.IsUpdateCheckInFlight || _checking;
            CheckNowButton.IsEnabled = !busy;
            CheckNowButton.Content = busy ? "Procurando..." : "Procurar atualização agora";

            // "Atualizar agora": só em instalação Velopack com update disponível.
            // Durante o download o botão mostra a porcentagem e a barra enche.
            bool showUpdateNow = _host.VelopackMode && _host.HasUpdate;
            UpdateNowButton.Visibility = showUpdateNow ? Visibility.Visible : Visibility.Collapsed;
            if (_host.VelopackDownloadInFlight)
            {
                UpdateNowButton.IsEnabled = false;
                UpdateNowButton.Content = $"Baixando… {_host.VelopackDownloadProgress}%";
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = _host.VelopackDownloadProgress;
            }
            else
            {
                UpdateNowButton.IsEnabled = showUpdateNow;
                UpdateNowButton.Content = "Atualizar agora";
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateProgressBar.Value = 0;
            }
        }

        private static bool TryFormatPublishedAt(string? iso, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(iso))
                return false;

            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                text = parsed.ToLocalTime().ToString("dd/M/yyyy HH:mm", CultureInfo.InvariantCulture);
                return true;
            }

            text = iso.Trim(); // formato inesperado: mostra cru, ainda informativo
            return true;
        }

        private async void CheckNow_Click(object sender, RoutedEventArgs e)
        {
            if (_checking)
                return;

            _checking = true;
            CheckNowButton.IsEnabled = false;
            CheckNowButton.Content = "Procurando...";
            try
            {
                await _host.CheckForUpdatesAsync("manual");
            }
            catch
            {
                // CheckForUpdatesAsync já captura e loga tudo; nada a propagar para a UI.
            }
            finally
            {
                _checking = false;
                Refresh();
            }
        }

        private async void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (_host.VelopackDownloadInFlight)
                return;

            UpdateNowButton.IsEnabled = false;
            try
            {
                await _host.DownloadVelopackUpdateAsync(progress =>
                {
                    // Progresso chega de outra thread: repassa para a UI com segurança.
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UpdateNowButton.Content = $"Baixando… {progress}%";
                            UpdateProgressBar.Visibility = Visibility.Visible;
                            UpdateProgressBar.Value = progress;
                        });
                    }
                    catch { /* UI de progresso nunca derruba o download */ }
                });
                // Sucesso encerra o processo (ApplyUpdatesAndRestart); se voltamos aqui,
                // o download/apply falhou e o log no host já registra a causa.
                Refresh();
            }
            catch
            {
                // DownloadVelopackUpdateAsync já captura e loga tudo; nada a propagar.
                Refresh();
            }
        }

        private void Ignore_Click(object sender, RoutedEventArgs e)
        {
            // O evento UpdateStateChanged dispara o refresh.
            _host.IgnoreLatestRelease();
        }

        private void OpenRelease_Click(object sender, RoutedEventArgs e)
        {
            _host.OpenLatestReleaseInBrowser();
        }

        private void OpenRepo_Click(object sender, RoutedEventArgs e)
        {
            _host.OpenRepositoryInBrowser();
        }
    }
}
