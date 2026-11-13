using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;

namespace LoLIdleDesktop
{
    public partial class AjustesWindow : Window
    {
        private readonly NotifySettingsPanel _notifyPanel;
        private readonly CloseBehaviorPanel _closePanel;

        public AjustesWindow(MainWindow.WindowSettings settings,
                             IEnumerable<string> accountKeys,
                             Func<string, string> displayNameSelector,
                             Action saveAction,
                             Window? owner)
        {
            Owner = owner;

            _notifyPanel = new NotifySettingsPanel(settings, accountKeys, displayNameSelector, saveAction);
            _closePanel = new CloseBehaviorPanel(settings, saveAction);

            InitializeComponent();
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed &&
                    e.OriginalSource is not Button &&
                    e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
                    DragMove();
            };

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
            AjustesTabs.SelectedIndex = 0;
        }

        private void CloseHeader_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
