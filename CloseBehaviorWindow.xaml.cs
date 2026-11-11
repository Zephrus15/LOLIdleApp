using System;
using System.Windows;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;

namespace LoLIdleDesktop
{
    public partial class CloseBehaviorWindow : Window
    {
        private readonly MainWindow.WindowSettings _settings;
        private readonly Action _save;
        private bool _syncing;

        public CloseBehaviorWindow(MainWindow.WindowSettings settings,
                                   Action saveAction,
                                   Window? owner)
        {
            _settings = settings;
            _save = saveAction;
            Owner = owner;

            InitializeComponent();
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed &&
                    e.OriginalSource is not Button &&
                    e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
                    DragMove();
            };

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
