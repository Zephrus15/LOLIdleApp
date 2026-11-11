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

        private bool _hoverSegundoPlano, _hoverSair;

        private void AtualizarCardSegundoPlano()
        {
            bool sel = _result.Selected == 1;
            bool hover = _hoverSegundoPlano;
            System.Windows.Media.Color border = sel ? (hover ? System.Windows.Media.Color.FromRgb(0x2B, 0xB5, 0x5C) : VerdeAtivo)
                                                    : (hover ? System.Windows.Media.Color.FromRgb(0x3A, 0x42, 0x52) : CinzaInativo);
            System.Windows.Media.Color bg = sel ? (hover ? System.Windows.Media.Color.FromRgb(0x1E, 0x33, 0x2A) : System.Windows.Media.Color.FromRgb(0x17, 0x29, 0x2A))
                                                : (hover ? System.Windows.Media.Color.FromRgb(0x22, 0x26, 0x31) : System.Windows.Media.Color.FromRgb(0x1B, 0x1E, 0x27));

            OptionSegundoPlano.BorderBrush = new System.Windows.Media.SolidColorBrush(border);
            OptionSegundoPlano.BorderThickness = new Thickness(sel ? 1.5 : 1);
            OptionSegundoPlano.Background = new System.Windows.Media.SolidColorBrush(bg);
        }

        private void AtualizarCardSair()
        {
            bool sel = _result.Selected == 2;
            bool hover = _hoverSair;
            System.Windows.Media.Color border = sel ? (hover ? System.Windows.Media.Color.FromRgb(0xF0, 0x6D, 0x5E) : System.Windows.Media.Color.FromRgb(0xE0, 0x5B, 0x4B))
                                                    : (hover ? System.Windows.Media.Color.FromRgb(0x3A, 0x42, 0x52) : CinzaInativo);
            System.Windows.Media.Color bg = sel ? (hover ? System.Windows.Media.Color.FromRgb(0x31, 0x24, 0x28) : System.Windows.Media.Color.FromRgb(0x26, 0x1C, 0x20))
                                                : (hover ? System.Windows.Media.Color.FromRgb(0x22, 0x26, 0x31) : System.Windows.Media.Color.FromRgb(0x1B, 0x1E, 0x27));

            OptionSair.BorderBrush = new System.Windows.Media.SolidColorBrush(border);
            OptionSair.BorderThickness = new Thickness(sel ? 1.5 : 1);
            OptionSair.Background = new System.Windows.Media.SolidColorBrush(bg);
        }

        private void SelecionarSegundoPlano()
        {
            _result.Selected = 1;
            AtualizarCardSegundoPlano();
            AtualizarCardSair();

            RadioSegundoPlano.IsChecked = true;
            RadioSair.IsChecked = false;

            ConfirmButton.Content = "Fechar e continuar rodando";
            ConfirmButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2B, 0xD4, 0x6A));
            ConfirmButton.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x0A, 0x2E, 0x19));
        }

        private void SelecionarSair()
        {
            _result.Selected = 2;
            AtualizarCardSegundoPlano();
            AtualizarCardSair();

            RadioSegundoPlano.IsChecked = false;
            RadioSair.IsChecked = true;

            ConfirmButton.Content = "Sair e desligar";
            ConfirmButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE0, 0x5B, 0x4B));
            ConfirmButton.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2E, 0x0E, 0x0A));
        }

        private void CardSegundoPlano_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hoverSegundoPlano = true;
            AtualizarCardSegundoPlano();
        }

        private void CardSegundoPlano_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hoverSegundoPlano = false;
            AtualizarCardSegundoPlano();
        }

        private void CardSair_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hoverSair = true;
            AtualizarCardSair();
        }

        private void CardSair_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hoverSair = false;
            AtualizarCardSair();
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
