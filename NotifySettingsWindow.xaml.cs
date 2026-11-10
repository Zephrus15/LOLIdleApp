using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;

namespace LoLIdleDesktop
{
    public partial class NotifySettingsWindow : Window
    {
        private readonly MainWindow.WindowSettings _settings;
        private readonly Action _save;
        private readonly List<string> _allKeys;

        private sealed record NotifyTypeEntry(string Kind, string Label);

        private static readonly NotifyTypeEntry[] NotifyTypes =
        {
            new("legendary", "Item lendário"),
            new("epic", "Item épico"),
            new("mission", "Missão concluída"),
            new("inventory", "Inventário cheio"),
            new("level", "Subiu de nível")
        };

        public NotifySettingsWindow(MainWindow.WindowSettings settings,
                                    IEnumerable<string> accountKeys,
                                    Func<string, string> displayNameSelector,
                                    Action saveAction,
                                    Window? owner)
        {
            _settings = settings;
            _save = saveAction;
            _allKeys = accountKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            DisplayNameSelector = displayNameSelector;
            Owner = owner;

            InitializeComponent();
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed &&
                    e.OriginalSource is not Button &&
                    e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
                    DragMove();
            };

            RebuildAccountsList();
            RebuildTypesList();
            NotifyEnabledCheck.IsChecked = _settings.NotifyEnabled;
        }

        private Func<string, string> DisplayNameSelector { get; }

        private void RebuildAccountsList()
        {
            ContasListPanel.Children.Clear();

            // Percorre a lista completa (principal + extras)
            foreach (var key in _allKeys)
            {
                bool enabled = !_settings.NotifyAccountsDisabled.Contains(key, StringComparer.OrdinalIgnoreCase);
                var check = new CheckBox
                {
                    Content = DisplayNameSelector(key),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 6),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = enabled,
                    Tag = key,
                    Style = (Style)FindResource("DarkCheckBox")
                };
                check.Checked += AccountCheck_Changed;
                check.Unchecked += AccountCheck_Changed;
                ContasListPanel.Children.Add(check);
            }
        }

        private void RebuildTypesList()
        {
            TypesListPanel.Children.Clear();

            foreach (var entry in NotifyTypes)
            {
                bool enabled = !_settings.NotifyTypesDisabled.Contains(entry.Kind, StringComparer.OrdinalIgnoreCase);
                var check = new CheckBox
                {
                    Content = entry.Label,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 6),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = enabled,
                    Tag = entry.Kind,
                    Style = (Style)FindResource("DarkCheckBox")
                };
                check.Checked += TypeCheck_Changed;
                check.Unchecked += TypeCheck_Changed;
                TypesListPanel.Children.Add(check);
            }
        }

        private void AccountCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not string key) return;

            if (cb.IsChecked == true)
                _settings.NotifyAccountsDisabled.RemoveAll(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            else if (!_settings.NotifyAccountsDisabled.Contains(key, StringComparer.OrdinalIgnoreCase))
                _settings.NotifyAccountsDisabled.Add(key);

            _save();
        }

        private void TypeCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not string kind) return;

            if (cb.IsChecked == true)
                _settings.NotifyTypesDisabled.RemoveAll(k => k.Equals(kind, StringComparison.OrdinalIgnoreCase));
            else if (!_settings.NotifyTypesDisabled.Contains(kind, StringComparer.OrdinalIgnoreCase))
                _settings.NotifyTypesDisabled.Add(kind);

            _save();
        }

        private void NotifyEnabledCheck_Changed(object sender, RoutedEventArgs e)
        {
            _settings.NotifyEnabled = NotifyEnabledCheck.IsChecked == true;
            ContasListPanel.IsEnabled = _settings.NotifyEnabled;
            TypesListPanel.IsEnabled = _settings.NotifyEnabled;
            MultiAccountHint.IsHitTestVisible = _settings.NotifyEnabled;
            ContasListPanel.Opacity = _settings.NotifyEnabled ? 1.0 : 0.4;
            TypesListPanel.Opacity = _settings.NotifyEnabled ? 1.0 : 0.4;
            MultiAccountHint.Opacity = _settings.NotifyEnabled ? 1.0 : 0.4;
            _save();
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
