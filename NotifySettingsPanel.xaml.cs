using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;

namespace LoLIdleDesktop
{
    public partial class NotifySettingsPanel : UserControl
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
            // "potion" (master) e as sub-chaves de poção ficam no XAML (grupo indentado).
        };

        private static readonly NotifyTypeEntry[] PotionSubTypes =
        {
            new("potion_vida", "Poções de Vida"),
            new("potion_vidaF", "Poções de Vida Forte"),
            new("potion_vidaS", "Poções de Vida Suprema"),
            new("potion_mana", "Poções de Mana"),
            new("potion_manaF", "Poções de Mana Forte")
        };

        public NotifySettingsPanel(MainWindow.WindowSettings settings,
                                   IEnumerable<string> accountKeys,
                                   Func<string, string> displayNameSelector,
                                   Action saveAction)
        {
            _settings = settings;
            _save = saveAction;
            _allKeys = accountKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            DisplayNameSelector = displayNameSelector;

            InitializeComponent();

            RebuildAccountsList();
            RebuildTypesList();
            RebuildPotionList();
            NotifyEnabledCheck.IsChecked = _settings.NotifyEnabled;
            ApplyNotifyEnabledState(_settings.NotifyEnabled);
        }

        // Sub-chaves de poção (na ordem do XAML).
        private CheckBox[] PotionSubChecks => new[]
        {
            PotionVidaCheck, PotionVidaForteCheck, PotionVidaSupremaCheck,
            PotionManaCheck, PotionManaForteCheck
        };

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

        // Carrega master + sub-chaves de poção a partir de NotifyTypesDisabled.
        // Compatibilidade: "potion" na lista = master OFF → silencia todas as poções;
        // as sub-chaves continuam refletindo suas próprias chaves, mas ficam desabilitadas visualmente.
        private void RebuildPotionList()
        {
            PotionMasterCheck.IsChecked = !_settings.NotifyTypesDisabled.Contains("potion", StringComparer.OrdinalIgnoreCase);

            var subs = PotionSubChecks;
            for (int i = 0; i < PotionSubTypes.Length; i++)
            {
                var kind = PotionSubTypes[i].Kind;
                subs[i].IsChecked = !_settings.NotifyTypesDisabled.Contains(kind, StringComparer.OrdinalIgnoreCase);
            }

            SyncPotionSubState();
        }

        // Master OFF → sub-chaves cinza/desabilitadas (mas preservam o IsChecked salvo).
        private void SyncPotionSubState()
        {
            bool masterOn = PotionMasterCheck.IsChecked == true;
            foreach (var sub in PotionSubChecks)
            {
                sub.IsEnabled = masterOn;
                sub.Opacity = masterOn ? 1.0 : 0.4;
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

            // Master de poções alterado → atualiza o visual/estado das sub-chaves.
            if (string.Equals(kind, "potion", StringComparison.OrdinalIgnoreCase))
                SyncPotionSubState();

            _save();
        }

        private void NotifyEnabledCheck_Changed(object sender, RoutedEventArgs e)
        {
            _settings.NotifyEnabled = NotifyEnabledCheck.IsChecked == true;
            ApplyNotifyEnabledState(_settings.NotifyEnabled);
            _save();
        }

        private void ApplyNotifyEnabledState(bool enabled)
        {
            ContasListPanel.IsEnabled = enabled;
            TypesListPanel.IsEnabled = enabled;
            PotionMasterCheck.IsEnabled = enabled;
            PotionSubPanel.IsEnabled = enabled;
            MultiAccountHint.IsHitTestVisible = enabled;
            ContasListPanel.Opacity = enabled ? 1.0 : 0.4;
            TypesListPanel.Opacity = enabled ? 1.0 : 0.4;
            PotionMasterCheck.Opacity = enabled ? 1.0 : 0.4;
            PotionSubPanel.Opacity = enabled ? 1.0 : 0.4;
            MultiAccountHint.Opacity = enabled ? 1.0 : 0.4;

            // Master desligado globalmente ou desmarcado → sub-chaves cinza/desabilitadas.
            SyncPotionSubState();
        }
    }
}
