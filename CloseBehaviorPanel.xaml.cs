using System;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using Microsoft.Win32;

namespace LoLIdleDesktop
{
    public partial class CloseBehaviorPanel : UserControl
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "LoLIdleDesktop";

        private readonly MainWindow.WindowSettings _settings;
        private readonly Action _save;
        private bool _syncing;

        public CloseBehaviorPanel(MainWindow.WindowSettings settings,
                                  Action saveAction)
        {
            _settings = settings;
            _save = saveAction;

            InitializeComponent();

            LoadCurrentOption();
        }

        private void LoadCurrentOption()
        {
            _syncing = true;
            try
            {
                // Fechamento: chave única — ligada = 1 (segundo plano),
                // desligada = 2 (sair). Qualquer valor != 2 cai no padrão ligado.
                BackgroundCheck.IsChecked = _settings.CloseBehavior != 2;

                // Pré-carregamento: padrão ligado (vale na próxima abertura do app).
                PrecarregarContasCheck.IsChecked = _settings.PrecarregarContas;

                AskCloseCheck.IsChecked = _settings.PerguntarAoFechar;
                UpdateAskVisibility();

                StartWithWindowsCheck.IsChecked = _settings.IniciarComWindows;
                StartTrayCheck.IsChecked = _settings.IniciarNaBandejaAoIniciar;
                StartTrayRow.Visibility = _settings.IniciarComWindows
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            finally
            {
                _syncing = false;
            }
        }

        // Pré-carregamento de contas no startup: só afeta a próxima abertura do app.
        private void Precarregar_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _settings.PrecarregarContas = PrecarregarContasCheck.IsChecked == true;
            _save();
        }

        // Inicialização com o Windows: grava/apaga a chave HKCU ...\Run\LoLIdleDesktop.
        private static void SetRunValue(bool enabled)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (key == null)
                    return;

                if (enabled)
                {
                    string exe = Environment.ProcessPath
                        ?? System.Windows.Forms.Application.ExecutablePath;
                    // Caminho entre aspas (o nome do executável contém espaço: "LoL Idle.exe").
                    key.SetValue(RunValueName, $"\"{exe}\"");
                }
                else
                {
                    key.DeleteValue(RunValueName, throwOnMissingValue: false);
                }
            }
            catch
            {
                // Falha ao gravar/apagar a chave não deve derrubar o app;
                // a preferência fica salva em _settings mesmo assim.
            }
        }

        private void Startup_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            bool on = StartWithWindowsCheck.IsChecked == true;
            _settings.IniciarComWindows = on;
            SetRunValue(on);
            StartTrayRow.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            _save();
        }

        private void StartTray_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _settings.IniciarNaBandejaAoIniciar = StartTrayCheck.IsChecked == true;
            _save();
        }

        // "Perguntar ao fechar": só tem efeito com CloseBehavior = 2 (sair);
        // pergunta a cada × entre continuar em segundo plano ou sair.
        private void AskClose_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _settings.PerguntarAoFechar = AskCloseCheck.IsChecked == true;
            _save();
        }

        // Mostra a pergunta apenas no modo "sair" (CloseBehavior = 2) com a chave
        // de segundo plano desligada; caso contrário, o check some.
        private void UpdateAskVisibility()
        {
            bool show = BackgroundCheck.IsChecked != true && _settings.CloseBehavior == 2;
            AskCloseRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        // Fechamento: chave única — ON = continuar em segundo plano (bandeja),
        // OFF = encerrar o app completamente ao clicar em Fechar.
        private void CloseBehavior_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            bool on = BackgroundCheck.IsChecked == true;
            _settings.CloseBehavior = on ? 1 : 2;
            // ON: a pergunta não faz sentido (× vai direto pra bandeja).
            // OFF: volta a perguntar a cada ×.
            _settings.PerguntarAoFechar = !on;
            _syncing = true;
            try { AskCloseCheck.IsChecked = !on; }
            finally { _syncing = false; }
            UpdateAskVisibility();
            _save();
        }
    }
}
