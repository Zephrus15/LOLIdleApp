using System;
using System.Threading.Tasks;
using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace LoLIdleDesktop
{
    public partial class AudioSettingsPanel : UserControl
    {
        private readonly MainWindow.WindowSettings _settings;
        private readonly Action _save;
        private readonly Func<Task>? _onAudioChanged;
        private bool _syncing;

        public AudioSettingsPanel(MainWindow.WindowSettings settings,
                                  Action saveAction,
                                  Func<Task>? onAudioChanged = null)
        {
            _settings = settings;
            _save = saveAction;
            _onAudioChanged = onAudioChanged;

            InitializeComponent();

            LoadCurrentOption();
        }

        private void LoadCurrentOption()
        {
            _syncing = true;
            try
            {
                MusicMutedCheck.IsChecked = !_settings.MusicMuted;
                EffectsMutedCheck.IsChecked = !_settings.EffectsMuted;
            }
            finally
            {
                _syncing = false;
            }
        }

        // Salva a preferência e reaplica as preferências de áudio na WebView
        // (via callback do MainWindow, que chama ApplyAudioSettingsAsync).
        private void ApplyAudioChange()
        {
            _save();
            if (_onAudioChanged != null)
                _ = _onAudioChanged();
        }

        private void MusicMuted_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _settings.MusicMuted = MusicMutedCheck.IsChecked != true;
            ApplyAudioChange();
        }

        private void EffectsMuted_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _settings.EffectsMuted = EffectsMutedCheck.IsChecked != true;
            ApplyAudioChange();
        }

        // "Restaurar padrão (som ligado)": ambos os canais em false (toggles ligados).
        private void RestoreDefault_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _settings.MusicMuted = false;
            _settings.EffectsMuted = false;

            _syncing = true;
            try
            {
                MusicMutedCheck.IsChecked = true;
                EffectsMutedCheck.IsChecked = true;
            }
            finally
            {
                _syncing = false;
            }

            ApplyAudioChange();
        }
    }
}
