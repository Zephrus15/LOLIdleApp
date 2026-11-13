using System;
using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;

namespace LoLIdleDesktop
{
    public partial class CloseBehaviorPanel : UserControl
    {
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
                // Qualquer valor fora de 1/2 cai no padrão "Sempre perguntar"
                AskCheck.IsChecked = _settings.CloseBehavior != 1 && _settings.CloseBehavior != 2;
                BackgroundCheck.IsChecked = _settings.CloseBehavior == 1;
                ExitCheck.IsChecked = _settings.CloseBehavior == 2;
            }
            finally
            {
                _syncing = false;
            }
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _syncing = true;
            try
            {
                if (ReferenceEquals(sender, BackgroundCheck))
                {
                    AskCheck.IsChecked = false;
                    ExitCheck.IsChecked = false;
                    _settings.CloseBehavior = 1;
                }
                else if (ReferenceEquals(sender, ExitCheck))
                {
                    AskCheck.IsChecked = false;
                    BackgroundCheck.IsChecked = false;
                    _settings.CloseBehavior = 2;
                }
                else
                {
                    BackgroundCheck.IsChecked = false;
                    ExitCheck.IsChecked = false;
                    _settings.CloseBehavior = 0;
                }

                _save();
            }
            finally
            {
                _syncing = false;
            }
        }

        private void Option_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            // Comportamento de rádio: a opção ativa não pode ser desmarcada
            if (sender is CheckBox cb)
            {
                _syncing = true;
                try
                {
                    cb.IsChecked = true;
                }
                finally
                {
                    _syncing = false;
                }
            }
        }
    }
}
