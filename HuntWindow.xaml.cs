using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace LoLIdleDesktop
{
    // Janela flutuante compacta do "Hunt for LoL Idle". Hospeda um WebView2 que aponta
    // para o side panel da extensão (chrome-extension://<id>/painel.html), usando o MESMO
    // environment da conta ativa — assim o painel lê exatamente a sessão certa.
    public partial class HuntWindow : Window
    {
        private readonly MainWindow _host;
        private readonly CoreWebView2Environment _sharedEnv;
        private DispatcherTimer? _installWatchTimer;
        private bool _navigationError;
        private bool _updating;

        // Ambiente com o qual a janela foi criada (para o MainWindow detectar troca de conta).
        internal CoreWebView2Environment SharedEnvironment => _sharedEnv;

        public HuntWindow(MainWindow host, CoreWebView2Environment sharedEnv, Window? owner)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _sharedEnv = sharedEnv ?? throw new ArgumentNullException(nameof(sharedEnv));

            InitializeComponent();

            if (owner != null)
                Owner = owner;
            Topmost = host.HuntTopmostSetting;
            HuntView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(23, 26, 33);

            Closed += (_, _) =>
            {
                _installWatchTimer?.Stop();
                _installWatchTimer = null;
                try { HuntView.Dispose(); } catch { }
            };

            Loaded += HuntWindow_Loaded;
        }

        private async void HuntWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reaproveita o environment já criado para a conta ativa (não criamos outro).
                await HuntView.EnsureCoreWebView2Async(_sharedEnv);
            }
            catch (Exception ex)
            {
                MainWindow.LogFloatingWindowEvent(
                    $"HuntWindow: EnsureCoreWebView2Async falhou — {ex.GetType().Name}: {ex.Message}");
                ShowFatal("Não consegui iniciar o painel aqui. Fecha a janela e tenta de novo.");
                return;
            }

            HuntView.NavigationStarting += (_, args) =>
            {
                if (args.Uri != null &&
                    args.Uri.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
                    HideLoading();
            };

            // Obs.: o checkbox "Avisos" do painel do Hunt fica inerte de propósito —
            // o manifest.json local tem a permissão "notifications" removida no
            // install (SanitizeManifest em HuntExtensionInstaller), então
            // chrome.notifications não existe e nada notifica, independente do checkbox.
            HuntView.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                {
                    _navigationError = false;
                    HideLoading();
                    MainWindow.LogFloatingWindowEvent("HuntWindow: painel carregado.");
                }
                else if (!_navigationError)
                {
                    _navigationError = true;
                    MainWindow.LogFloatingWindowEvent(
                        $"HuntWindow: NavigationCompleted com falha (webErrorStatus={args.WebErrorStatus}).");
                    ShowFatal("Não consegui abrir o painel do Hunt. Fecha a janela e clica no botão de novo.");
                }
            };

            NavigateIfReady();

            // Checagem de update só na abertura do painel — em background, nunca trava a janela.
            _ = CheckForStoreUpdateAsync();
        }

        // Navega para o painel quando o ID da extensão é conhecido; senão, mostra a moldura
        // de "Instalando…" e fica de olho (a instalação pode estar em andamento).
        private void NavigateIfReady()
        {
            string? id = _host.HuntExtensionIdSetting;
            if (string.IsNullOrWhiteSpace(id))
            {
                ShowLoading();
                EnsureInstallWatch();
                return;
            }

            StopInstallWatch();
            try
            {
                HuntView.CoreWebView2?.Navigate(
                    $"chrome-extension://{id}/{HuntExtensionInstaller.PanelPage}");
            }
            catch (Exception ex)
            {
                MainWindow.LogFloatingWindowEvent(
                    $"HuntWindow: Navigate falhou — {ex.GetType().Name}: {ex.Message}");
                ShowFatal("Não consegui abrir o painel do Hunt. Fecha a janela e clica no botão de novo.");
            }
        }

        private void EnsureInstallWatch()
        {
            if (_installWatchTimer != null)
                return;

            _installWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _installWatchTimer.Tick += (_, _) => NavigateIfReady();
            _installWatchTimer.Start();
        }

        private void StopInstallWatch()
        {
            _installWatchTimer?.Stop();
            _installWatchTimer = null;
        }

        private void ShowLoading()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            InstallProgress.Visibility = Visibility.Visible;
            LoadingText.Text = "Instalando Hunt for LoL Idle…";
        }

        private void ShowFatal(string message)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            InstallProgress.Visibility = Visibility.Collapsed;
            LoadingText.Text = message;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        // Consulta em background a versão publicada na Chrome Web Store e, se for maior que a
        // local (manifest.json sanitizado), mostra o botão "↻ Atualizar" no cabeçalho.
        // Qualquer falha só loga — o botão simplesmente não aparece e a janela segue normal.
        private async Task CheckForStoreUpdateAsync()
        {
            try
            {
                string? verLoja = await HuntExtensionInstaller.CheckStoreVersionAsync();
                string? verLocal = HuntExtensionInstaller.ReadLocalVersion();
                if (verLoja == null || verLocal == null)
                    return; // já logado dentro dos helpers

                if (Version.TryParse(verLoja, out var vLoja) &&
                    Version.TryParse(verLocal, out var vLocal) &&
                    vLoja > vLocal)
                {
                    HuntUpdateButton.Visibility = Visibility.Visible;
                    HuntUpdateButton.ToolTip = $"Atualizar Hunt for LoL Idle (versão {verLocal} → {verLoja})";
                    MainWindow.LogFloatingWindowEvent($"Hunt: atualização disponível ({verLocal} → {verLoja}).");
                }
                else
                {
                    MainWindow.LogFloatingWindowEvent($"Hunt: extensão já atualizada ({verLocal}).");
                }
            }
            catch (Exception ex)
            {
                MainWindow.LogFloatingWindowEvent($"Hunt: falha ao checar atualização — {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Clique em "↻ Atualizar": força o ciclo completo (download da loja → sanitize → re-registro
        // nos perfis) via host e recarrega o painel. Cliques repetidos durante a operação são ignorados.
        private async void HuntUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_updating)
                return;
            _updating = true;
            try
            {
                HuntUpdateButton.IsEnabled = false;
                var (ok, error) = await _host.UpdateHuntExtensionAsync();
                if (ok)
                {
                    string? nova = HuntExtensionInstaller.ReadLocalVersion();
                    MainWindow.LogFloatingWindowEvent(
                        $"Hunt: atualizado para a versão {nova ?? "?"} (id={_host.HuntExtensionIdSetting ?? "?"}).");
                    HuntUpdateButton.Visibility = Visibility.Collapsed;
                    NavigateIfReady();
                }
                else
                {
                    MainWindow.LogFloatingWindowEvent($"HuntUpdate: falha — {error}");
                    ShowFatal("Não consegui atualizar a extensão. Fecha e tenta de novo.");
                    // Botão permanece visível para nova tentativa.
                }
            }
            catch (Exception ex)
            {
                MainWindow.LogFloatingWindowEvent($"HuntUpdate: exceção — {ex.GetType().Name}: {ex.Message}");
                ShowFatal("Não consegui atualizar a extensão. Fecha e tenta de novo.");
            }
            finally
            {
                _updating = false;
                HuntUpdateButton.IsEnabled = true;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Arrastar a janela pelo cabeçalho — nunca quando o clique começa no ✕.
            if (e.LeftButton == MouseButtonState.Pressed &&
                e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
            {
                try { DragMove(); }
                catch (InvalidOperationException) { /* clique fora do botão esquerdo */ }
            }
        }

        private void CloseHuntButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
