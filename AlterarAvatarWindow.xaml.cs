using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace LoLIdleDesktop
{
    // Janela modal "Alterar avatar" (estilo Facebook): preview quadrado com bordas
    // arredondadas, arrastar para reposicionar o recorte, slider de zoom (1x a 4x).
    // Só persiste a imagem/posição/zoom ao clicar em Aplicar; Cancelar desfaz tudo.
    public partial class AlterarAvatarWindow : Window
    {
        private const double PreviewSize = 300;

        private readonly MainWindow.WindowSettings _settings;
        private readonly string _accountKey;
        private readonly string _sourceImagePath;
        private readonly string? _destFileName;
        private readonly Action _save;

        // Estado anterior para restaurar no Cancelar
        private readonly bool _hadImagemAnterior;
        private readonly string? _imagemAnterior;
        private readonly bool _hadOffsetAnterior;
        private readonly string? _offsetAnterior;
        private readonly double _escalaAnterior;

        private BitmapImage? _bmp;
        private double _bmpW = 1, _bmpH = 1;
        private Canvas? _canvas;
        private Image? _previewImage;

        // Arraste (recorte)
        private Point _dragStart;
        private double _offsetInicioX, _offsetInicioY;
        private double _sobraX, _sobraY;
        private bool _arrastando;

        // destFileName: nome final do arquivo na pasta Avatars (opcional; padrão: <slug>.<ext>)
        public AlterarAvatarWindow(MainWindow.WindowSettings settings,
                                  string accountKey,
                                  string sourceImagePath,
                                  Action saveAction,
                                  Window? owner,
                                  string? destFileName = null)
        {
            _settings = settings;
            _accountKey = accountKey;
            _sourceImagePath = sourceImagePath;
            _destFileName = destFileName;
            _save = saveAction;
            Owner = owner;

            // Snapshot do estado atual ANTES de mexer em qualquer coisa
            string? imgAnt = null;
            _hadImagemAnterior = _settings.ContasAvatarImagePath != null &&
                _settings.ContasAvatarImagePath.TryGetValue(_accountKey, out imgAnt);
            _imagemAnterior = imgAnt;
            string? offAnt = null;
            _hadOffsetAnterior = _settings.ContasAvatarImageOffsets != null &&
                _settings.ContasAvatarImageOffsets.TryGetValue(_accountKey, out offAnt);
            _offsetAnterior = offAnt;
            _escalaAnterior = _settings.ContasAvatarImageScale;

            InitializeComponent();

            // Só arrasta a janela quando o clique NÃO foi em um controle clicável
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed &&
                    e.OriginalSource is not Button &&
                    e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase &&
                    e.OriginalSource is not Slider &&
                    e.OriginalSource is not System.Windows.Controls.Border)
                    DragMove();
            };

            // Imagem nova: recorte volta ao centro (zoom atual é mantido).
            // A alteração só vai para o disco no Aplicar; Cancelar restaura.
            _settings.ContasAvatarImagePath ??= new Dictionary<string, string>();
            _settings.ContasAvatarImagePath[_accountKey] = _sourceImagePath;
            _settings.ContasAvatarImageOffsets?.Remove(_accountKey);

            if (!CarregarImagem())
            {
                ApplyButton.IsEnabled = false;
                AtualizarPreviewSidebar();
                PreviewBorder.Child = new TextBlock
                {
                    Text = "⚠ Não foi possível carregar a imagem",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    TextAlignment = System.Windows.TextAlignment.Center,
                    Margin = new Thickness(20)
                };
                return;
            }

            // Monta o preview (Canvas + Image) uma única vez; RenderPreview só reposiciona
            _canvas = new Canvas { Width = PreviewSize, Height = PreviewSize, ClipToBounds = true };
            _previewImage = new Image { Source = _bmp, Stretch = System.Windows.Media.Stretch.Fill };
            Canvas.SetLeft(_previewImage, 0);
            Canvas.SetTop(_previewImage, 0);
            _canvas.Children.Add(_previewImage);
            PreviewBorder.Child = _canvas;
            AtualizarPreviewSidebar();

            // Slider reflete a escala atual (sem disparar ValueChanged... na verdade deixa disparar, é idempotente)
            double escala = _settings.ContasAvatarImageScale;
            if (!double.IsFinite(escala) || escala < 1.0) escala = 1.0;
            else if (escala > 4.0) escala = 4.0;
            ZoomSlider.Value = escala;

            RenderPreview();
        }

        private bool CarregarImagem()
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(_sourceImagePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                _bmp = bmp;
                _bmpW = bmp.PixelWidth > 0 ? bmp.PixelWidth : 1;
                _bmpH = bmp.PixelHeight > 0 ? bmp.PixelHeight : 1;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Não foi possível usar esta imagem:\n{ex.Message}",
                    "Alterar avatar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        // Recria o recorte no preview (mesma matemática de MainWindow.CriarAvatarVisual)
        private void RenderPreview()
        {
            if (_previewImage == null || _bmp == null)
                return;

            var (offsetX, offsetY) = MainWindow.LerAvatarImageOffset(_settings, _accountKey);
            double escala = _settings.ContasAvatarImageScale;

            var (left, top, w, h) = MainWindow.CalcularPosicaoAvatar(
                _bmpW, _bmpH, PreviewSize, escala, offsetX, offsetY);

            _previewImage.Width = w;
            _previewImage.Height = h;
            Canvas.SetLeft(_previewImage, left);
            Canvas.SetTop(_previewImage, top);

            _sobraX = Math.Max(0, w - PreviewSize);
            _sobraY = Math.Max(0, h - PreviewSize);

            double exibido = (double.IsFinite(escala) && escala > 0) ? escala : 1.0;
            ZoomValueText.Text = $"{exibido:0.0}x";

            AtualizarPreviewSidebar();
        }

        // Nome exibido idêntico ao da sidebar (nome do DOM / personalizado, com fallback igual ao do MainWindow)
        private string NomeExibicaoSidebar()
        {
            var nomes = _settings.ContasNomes;
            if (nomes != null &&
                nomes.TryGetValue(_accountKey, out var nome) &&
                !string.IsNullOrWhiteSpace(nome))
                return nome;

            return string.Equals(_accountKey, MainWindow.DefaultAccountName, StringComparison.OrdinalIgnoreCase)
                ? MainWindow.DefaultAccountName
                : $"{_accountKey} (sem login)";
        }

        // Recria o mini-chip "Pré-visualização na barra lateral" usando o mesmo helper do chip real.
        // Chamado na abertura, em cada arraste e em cada mudança de zoom.
        private void AtualizarPreviewSidebar()
        {
            if (SidebarPreviewHost == null)
                return;

            string display = NomeExibicaoSidebar();
            SidebarPreviewHost.Children.Clear();
            SidebarPreviewHost.Children.Add(
                MainWindow.CriarAvatarVisual(_settings, _accountKey, display, 40, 10, 24));
            SidebarPreviewName.Text = display;

            bool destacado = string.Equals(
                _settings.ContaAtiva ?? MainWindow.DefaultAccountName,
                _accountKey,
                StringComparison.OrdinalIgnoreCase);

            SidebarPreviewChip.Background = new System.Windows.Media.SolidColorBrush(
                destacado
                    ? System.Windows.Media.Color.FromRgb(0x14, 0x26, 0x23)
                    : System.Windows.Media.Color.FromRgb(0x1F, 0x22, 0x2B));
            SidebarPreviewChip.BorderBrush = new System.Windows.Media.SolidColorBrush(
                destacado
                    ? System.Windows.Media.Color.FromRgb(0x2B, 0xD4, 0x6A)
                    : System.Windows.Media.Colors.Transparent);
            SidebarPreviewChip.BorderThickness = destacado
                ? new Thickness(1)
                : new Thickness(0);
            SidebarPreviewSubtitle.Text = destacado ? "na frente" : "segundo plano";
            SidebarPreviewSubtitle.Foreground = new System.Windows.Media.SolidColorBrush(
                destacado
                    ? System.Windows.Media.Color.FromRgb(0x37, 0xD0, 0x67)
                    : System.Windows.Media.Color.FromRgb(0x6E, 0x9C, 0xD8));
        }

        private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_bmp == null)
                return;

            _arrastando = true;
            _dragStart = e.GetPosition(PreviewBorder);
            var (offX, offY) = MainWindow.LerAvatarImageOffset(_settings, _accountKey);
            _offsetInicioX = offX;
            _offsetInicioY = offY;
            PreviewBorder.CaptureMouse();
            e.Handled = true;
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_arrastando)
                return;

            Point pos = e.GetPosition(PreviewBorder);
            double dx = pos.X - _dragStart.X;
            double dy = pos.Y - _dragStart.Y;

            // 1 px percorrido no mouse = 1 px percorrido na imagem (offset em fração da sobra)
            double novoX = _offsetInicioX;
            double novoY = _offsetInicioY;
            if (_sobraX > 0.5)
                novoX = _offsetInicioX - 2 * dx / _sobraX;
            if (_sobraY > 0.5)
                novoY = _offsetInicioY - 2 * dy / _sobraY;

            MainWindow.SalvarAvatarImageOffset(_settings, _accountKey, novoX, novoY);
            RenderPreview();
        }

        private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_arrastando)
                return;

            _arrastando = false;
            PreviewBorder.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_bmp == null)
                return;

            _settings.ContasAvatarImageScale = e.NewValue;
            RenderPreview();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Copia o arquivo para a pasta de avatares (mesma lógica anterior do "Escolher arquivo…")
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LoLIdleDesktop", "Avatars");
                Directory.CreateDirectory(dir);

                string slug = MainWindow.AvatarSlug(_accountKey);
                string ext = Path.GetExtension(_sourceImagePath);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".png";
                ext = ext.ToLowerInvariant();

                // Remove cópias antigas do avatar desta conta (qualquer extensão)
                foreach (var old in Directory.EnumerateFiles(dir, slug + ".*"))
                {
                    try { File.Delete(old); } catch { /* arquivo em uso: a cópia abaixo sobrescreve */ }
                }

                // Remove cópias antigas de versões "campeão" desta conta (ex.: <slug>-champion-*.png)
                foreach (var old in Directory.EnumerateFiles(dir, slug + "-champion-*.png"))
                {
                    try { File.Delete(old); } catch { /* arquivo em uso */ }
                }

                string dest = Path.Combine(dir, string.IsNullOrWhiteSpace(_destFileName) ? slug + ext : _destFileName);
                File.Copy(_sourceImagePath, dest, true);

                _settings.ContasAvatarImagePath ??= new Dictionary<string, string>();
                _settings.ContasAvatarImagePath[_accountKey] = dest;

                // Persiste imagem + posição + zoom
                _save();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Não foi possível usar esta imagem:\n{ex.Message}",
                    "Alterar avatar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RestaurarEstadoAnterior()
        {
            _settings.ContasAvatarImagePath ??= new Dictionary<string, string>();
            if (_hadImagemAnterior && _imagemAnterior != null)
                _settings.ContasAvatarImagePath[_accountKey] = _imagemAnterior;
            else
                _settings.ContasAvatarImagePath.Remove(_accountKey);

            _settings.ContasAvatarImageOffsets ??= new Dictionary<string, string>();
            if (_hadOffsetAnterior && _offsetAnterior != null)
                _settings.ContasAvatarImageOffsets[_accountKey] = _offsetAnterior;
            else
                _settings.ContasAvatarImageOffsets.Remove(_accountKey);

            _settings.ContasAvatarImageScale = _escalaAnterior;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            RestaurarEstadoAnterior();
            DialogResult = false;
            Close();
        }

        private void CloseHeader_Click(object sender, RoutedEventArgs e)
        {
            RestaurarEstadoAnterior();
            DialogResult = false;
            Close();
        }
    }
}
