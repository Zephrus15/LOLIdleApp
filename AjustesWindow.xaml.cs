using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LoLIdleDesktop
{
    public partial class AjustesWindow : Window
    {
        private readonly NotifySettingsPanel _notifyPanel;
        private readonly CloseBehaviorPanel _closePanel;
        private readonly AudioSettingsPanel _audioPanel;
        private readonly Window? _owner;

        public AjustesWindow(MainWindow.WindowSettings settings,
                             IEnumerable<string> accountKeys,
                             Func<string, string> displayNameSelector,
                             Action saveAction,
                             Window? owner,
                             Func<Task>? onAudioChanged = null,
                             MainWindow? updateHost = null)
        {
            _owner = owner;

            _notifyPanel = new NotifySettingsPanel(settings, accountKeys, displayNameSelector, saveAction);
            _closePanel = new CloseBehaviorPanel(settings, saveAction);
            _audioPanel = new AudioSettingsPanel(settings, saveAction, onAudioChanged);

            InitializeComponent();
            Deactivated += (_, _) => { try { Close(); } catch { } };

            AjustesTabs.Items.Add(new TabItem
            {
                Header = "🔔 Notificações",
                Style = (Style)FindResource("AjustesTabItem"),
                Content = _notifyPanel
            });
            AjustesTabs.Items.Add(new TabItem
            {
                Header = "⚙ Inicialização",
                Style = (Style)FindResource("AjustesTabItem"),
                Content = _closePanel
            });
            AjustesTabs.Items.Add(new TabItem
            {
                Header = "🎵 Áudio",
                Style = (Style)FindResource("AjustesTabItem"),
                Content = _audioPanel
            });
            // Aba "Sobre" só faz sentido com o MainWindow como fonte do estado da checagem.
            MainWindow? updateHostResolved = updateHost ?? owner as MainWindow;
            if (updateHostResolved != null)
            {
                AjustesTabs.Items.Add(new TabItem
                {
                    Header = "ℹ️ Sobre",
                    Style = (Style)FindResource("AjustesTabItem"),
                    Content = new UpdateSettingsPanel(updateHostResolved)
                });
            }
            else
            {
                MainWindow.LogFloatingWindowEvent("AjustesWindow: updateHost/owner não é um MainWindow — aba Sobre não será exibida");
            }
            AjustesTabs.SelectedIndex = 0;
        }

        private void CloseHeader_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
