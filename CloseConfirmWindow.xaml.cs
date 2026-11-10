using System;
using System.Windows;
using System.Windows.Input;

namespace LoLIdleDesktop
{
    // Resultado do diálogo de fechamento:
    // 0 = perguntar novamente, 1 = continuar em segundo plano, 2 = sair
    public sealed class CloseConfirmResult
    {
        public int Selected { get; set; } = 1;
        public bool Remember { get; set; }
    }

    public partial class CloseConfirmWindow : Window
    {
        private static readonly System.Windows.Media.Color VerdeAtivo =
            System.Windows.Media.Color.FromRgb(0x1F, 0x8A, 0x4C);
        private static readonly System.Windows.Media.Color CinzaInativo =
            System.Windows.Media.Color.FromRgb(0x2A, 0x2E, 0x38);
        private static readonly System.Windows.Media.Color VerdeRadio =
            System.Windows.Media.Color.FromRgb(0x37, 0xD0, 0x67);
        private static readonly System.Windows.Media.Color CinzaRadio =
            System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80);

        private readonly CloseConfirmResult _result = new CloseConfirmResult();

        public CloseConfirmResult Result => _result;

        public CloseConfirmWindow(Window? owner)
        {
            Owner = owner;
            InitializeComponent();
            SelecionarSegundoPlano();
            Loaded += (_, _) =>
            {
                MouseLeftButtonDown += (_, e) =>
                {
                    // Não arrasta a janela quando o clique é numa opção/botão/checkbox
                    if (e.OriginalSource is not Button &&
                        e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase &&
                        e.OriginalSource is not CheckBox &&
                        e.OriginalSource is not System.Windows.Controls.Border)
                        DragMove();
                };
            };
        }

        private void SelecionarSegundoPlano()
        {
            _result.Selected = 1;
            OptionSegundoPlano.BorderBrush = new System.Windows.Media.SolidColorBrush(VerdeAtivo);
            OptionSegundoPlano.BorderThickness = new Thickness(1.5);
            OptionSegundoPlano.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x17, 0x29, 0x2A));

            OptionSair.BorderBrush = new System.Windows.Media.SolidColorBrush(CinzaInativo);
            OptionSair.BorderThickness = new Thickness(1);
            OptionSair.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x1B, 0x1E, 0x27));

            RadioSegundoPlano.Stroke = new System.Windows.Media.SolidColorBrush(VerdeRadio);
            RadioSegundoPlano.Fill = new System.Windows.Media.SolidColorBrush(VerdeRadio);
            RadioSair.Stroke = new System.Windows.Media.SolidColorBrush(CinzaRadio);
            RadioSair.Fill = System.Windows.Media.Brushes.Transparent;

            ConfirmButton.Content = "Fechar e continuar rodando";
            ConfirmButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2B, 0xD4, 0x6A));
            ConfirmButton.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x0A, 0x2E, 0x19));
        }

        private void SelecionarSair()
        {
            _result.Selected = 2;
            OptionSair.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE0, 0x5B, 0x4B));
            OptionSair.BorderThickness = new Thickness(1.5);
            OptionSair.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x26, 0x1C, 0x20));

            OptionSegundoPlano.BorderBrush = new System.Windows.Media.SolidColorBrush(CinzaInativo);
            OptionSegundoPlano.BorderThickness = new Thickness(1);
            OptionSegundoPlano.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x1B, 0x1E, 0x27));

            RadioSegundoPlano.Stroke = new System.Windows.Media.SolidColorBrush(CinzaRadio);
            RadioSegundoPlano.Fill = System.Windows.Media.Brushes.Transparent;
            RadioSair.Stroke = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE0, 0x5B, 0x4B));
            RadioSair.Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE0, 0x5B, 0x4B));

            ConfirmButton.Content = "Sair e desligar";
            ConfirmButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE0, 0x5B, 0x4B));
            ConfirmButton.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2E, 0x0E, 0x0A));
        }

        private void OptionSegundoPlano_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
            SelecionarSegundoPlano();

        private void OptionSair_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
            SelecionarSair();

        private void CloseHeader_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            _result.Remember = RememberCheckbox.IsChecked == true;
            DialogResult = true;
            Close();
        }
    }
}
