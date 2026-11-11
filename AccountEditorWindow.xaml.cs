using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace LoLIdleDesktop
{
    public partial class AccountEditorWindow : Window
    {
        private readonly MainWindow.WindowSettings _settings;
        private readonly string _accountKey;
        private readonly Action _save;
        private bool _carregando;

        private static readonly string[] Colors =
        {
            "#8B5CF6", "#3B82F6", "#06B6D4", "#14B8A6", "#22C55E",
            "#EAB308", "#EF4444", "#EC4899", "#6E4FD8", "#64748B", "#000000"
        };

        // Borda sutil para swatches muito escuros (ex.: o preto), visíveis no painel #161922.
        private static readonly SolidColorBrush BordaSwatchEscuro =
            new SolidColorBrush(Color.FromRgb(0x2F, 0x35, 0x40));

        private static bool SwatchEscuro(string hex)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex)!;
                double lum = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
                return lum < 32;
            }
            catch { return false; }
        }

        private static System.Windows.Media.Brush BordaPadraoDoSwatch(string hex) =>
            SwatchEscuro(hex) ? BordaSwatchEscuro : Brushes.Transparent;

        private static readonly string[] Icons =
        {
            "⚔️", "🛡️", "🏹", "✨", "⭐", "🎮",
            "🏆", "🔥", "💎", "🌙", "⚡", "🎯"
        };

        private static readonly string[] Champions =
        {
            "Darius", "Ezreal", "Lux", "Yasuo", "Katarina", "Taric",
            "Zac", "Ryze", "Rengar", "Shaco", "Sona", "Aphelios"
        };

        public AccountEditorWindow(MainWindow.WindowSettings settings,
                                  string accountKey,
                                  Action saveAction,
                                  Window? owner)
        {
            _settings = settings;
            _accountKey = accountKey;
            _save = saveAction;
            Owner = owner;

            InitializeComponent();
            // Só arrasta a janela quando o clique NÃO foi em um controle clicável/seleção
            // (botão, checkbox, TextBox, ou qualquer Border/Ellipse/Shape de seleção do diálogo).
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed &&
                    e.OriginalSource is not Button &&
                    e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase &&
                    e.OriginalSource is not System.Windows.Controls.TextBox &&
                    e.OriginalSource is not System.Windows.Controls.Border &&
                    e.OriginalSource is not System.Windows.Shapes.Shape &&
                    e.OriginalSource is not System.Windows.Controls.Panel &&
                    e.OriginalSource is not System.Windows.Controls.ContentPresenter)
                    DragMove();
            };

            HeaderSubtitle.Text = _accountKey;

            _carregando = true;
            NameBox.Text = NomeExibicao();
            _carregando = false;

            BuildColors();
            BuildIcons();
            BuildChampions();
            SelecionarAba(false);
            AtualizarTudo();
        }

        private string NomeExibicao()
        {
            var nomes = _settings.ContasNomes;
            if (nomes != null &&
                nomes.TryGetValue(_accountKey, out var nome) &&
                !string.IsNullOrWhiteSpace(nome))
                return nome;

            return _accountKey;
        }

        // Nome editado manualmente: vazio ou igual à chave -> volta ao padrão;
        // caso contrário, salva como nome personalizado (o DOM não sobrescreve).
        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregando) return;

            string texto = (NameBox.Text ?? string.Empty).Trim();

            _settings.ContasNomes ??= new Dictionary<string, string>();
            var personalizados = _settings.ContasNomesPersonalizados ??= new List<string>();

            if (texto.Length == 0 ||
                string.Equals(texto, _accountKey, StringComparison.Ordinal))
            {
                // Volta ao rótulo padrão ("Conta N" / nome do DOM)
                _settings.ContasNomes.Remove(_accountKey);
                personalizados.RemoveAll(c => c.Equals(_accountKey, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                _settings.ContasNomes[_accountKey] = texto;
                if (!personalizados.Contains(_accountKey, StringComparer.OrdinalIgnoreCase))
                    personalizados.Add(_accountKey);
            }

            _save();
            AtualizarPreview();
        }

        private void BuildColors()
        {
            ColorsPanel.Children.Clear();
            for (int i = 0; i < Colors.Length; i++)
            {
                var hex = Colors[i];
                Color cor;
                try { cor = (Color)ColorConverter.ConvertFromString(hex)!; }
                catch { cor = Color.FromRgb(0x6E, 0x4F, 0xD8); }

                var circle = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Background = new SolidColorBrush(cor),
                    BorderBrush = BordaPadraoDoSwatch(hex),
                    BorderThickness = new Thickness(2),
                    Cursor = Cursors.Hand,
                    Tag = hex,
                    Margin = new Thickness(0, 0, i < Colors.Length - 1 ? 6 : 0, 0),
                    ToolTip = hex
                };
                circle.MouseLeftButtonUp += ColorCircle_MouseLeftButtonUp;
                ColorsPanel.Children.Add(circle);
            }
        }

        private void BuildIcons()
        {
            IconsPanel.Children.Clear();
            foreach (var icon in Icons)
            {
                var cell = new Border
                {
                    Height = 36,
                    Margin = new Thickness(0, 0, 6, 6),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1E, 0x27)),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = icon,
                    Child = new TextBlock
                    {
                        Text = icon,
                        FontSize = 17,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)),
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI, Tahoma"),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false
                    }
                };
                cell.MouseLeftButtonUp += IconCell_MouseLeftButtonUp;
                IconsPanel.Children.Add(cell);
            }
        }

        // Carrega um PNG embutido (EmbeddedResource) como BitmapImage.
        private static System.Windows.Media.Imaging.BitmapImage? CarregarCampeaoBitmap(string championName)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string suffix = ".Champions." + championName + ".png";
                string? resourceName = null;
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }
                if (resourceName == null)
                    return null;

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return null;

                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 96;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        // Extrai um PNG embutido para um arquivo temporário (usado como origem na janela "Alterar avatar").
        private static string ExtrairCampeaoParaTemp(string championName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string suffix = ".Champions." + championName + ".png";
            string? resourceName = null;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = name;
                    break;
                }
            }
            if (resourceName == null)
                throw new FileNotFoundException($"Recurso embutido do campeão '{championName}' não encontrado.");

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Recurso embutido do campeão '{championName}' não encontrado.");

            string temp = Path.Combine(Path.GetTempPath(), "lol-idle-champion-" + championName + ".png");
            using var fs = File.Create(temp);
            stream.CopyTo(fs);
            return temp;
        }

        private void BuildChampions()
        {
            ChampionsPanel.Children.Clear();
            foreach (var champion in Champions)
            {
                var cell = new Border
                {
                    Height = 60,
                    Margin = new Thickness(0, 0, 6, 6),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1E, 0x27)),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = champion,
                    ToolTip = champion
                };

                var bmp = CarregarCampeaoBitmap(champion);
                if (bmp != null)
                {
                    // Moldura com cantos arredondados para a foto ficar arredondada
                    var championImage = new System.Windows.Controls.Image
                    {
                        Source = bmp,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        IsHitTestVisible = false
                    };
                    System.Windows.Media.RenderOptions.SetBitmapScalingMode(
                        championImage, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
                    cell.Child = new Border
                    {
                        Width = 52,
                        Height = 52,
                        CornerRadius = new CornerRadius(8),
                        ClipToBounds = true,
                        IsHitTestVisible = false,
                        Child = championImage
                    };
                }
                else
                {
                    cell.Child = new TextBlock
                    {
                        Text = champion,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        IsHitTestVisible = false
                    };
                }

                cell.MouseLeftButtonUp += ChampionCell_MouseLeftButtonUp;
                ChampionsPanel.Children.Add(cell);
            }
        }

        private void SelecionarAba(bool campeoes)
        {
            IconsPanel.Visibility = campeoes ? Visibility.Collapsed : Visibility.Visible;
            ChampionsPanel.Visibility = campeoes ? Visibility.Visible : Visibility.Collapsed;

            var ativo = new SolidColorBrush(Color.FromRgb(0x26, 0x2B, 0x3D));
            var inativo = new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x2B));
            var bordaAtiva = new SolidColorBrush(Color.FromRgb(0x2B, 0xD4, 0x6A));
            var bordaInativa = new SolidColorBrush(Color.FromRgb(0x2F, 0x35, 0x40));
            var textoAtivo = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));
            var textoInativo = new SolidColorBrush(Color.FromRgb(0x92, 0x98, 0xA5));

            IconsTabButton.Background = campeoes ? inativo : ativo;
            IconsTabButton.BorderBrush = campeoes ? bordaInativa : bordaAtiva;
            IconsTabButton.Foreground = campeoes ? textoInativo : textoAtivo;

            ChampionsTabButton.Background = campeoes ? ativo : inativo;
            ChampionsTabButton.BorderBrush = campeoes ? bordaAtiva : bordaInativa;
            ChampionsTabButton.Foreground = campeoes ? textoAtivo : textoInativo;
        }

        private void IconsTab_Click(object sender, RoutedEventArgs e) => SelecionarAba(false);

        private void ChampionsTab_Click(object sender, RoutedEventArgs e) => SelecionarAba(true);

        private void ColorCircle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border circle || circle.Tag is not string hex)
                return;

            _settings.ContasAvatarColor ??= new Dictionary<string, string>();
            // Cor é fundo do avatar: não apaga a imagem (sprites de campeão são PNG transparente)
            _settings.ContasAvatarColor[_accountKey] = hex;

            _save();
            AtualizarTudo();
        }

        private void IconCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border cell || cell.Tag is not string icon)
                return;

            _settings.ContasAvatarIcon ??= new Dictionary<string, string>();
            _settings.ContasAvatarImagePath?.Remove(_accountKey); // ícone assume o avatar
            _settings.ContasAvatarImageOffsets?.Remove(_accountKey);
            _settings.ContasAvatarIcon[_accountKey] = icon;

            _save();
            AtualizarTudo();
        }

        private void ChampionCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border cell || cell.Tag is not string champion)
                return;

            try
            {
                // Campeão entra DIRETO no avatar (sem abrir "Alterar avatar"):
                // extrai o PNG embutido, copia para %LOCALAPPDATA%\LoLIdleDesktop\Avatars
                // e zera recorte/zoom da conta (mesmo reset do fluxo de escolher imagem).
                string temp = ExtrairCampeaoParaTemp(champion);
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LoLIdleDesktop", "Avatars");
                Directory.CreateDirectory(dir);

                string dest = Path.Combine(dir, $"{MainWindow.AvatarSlug(_accountKey)}-champion-{champion}.png");
                File.Copy(temp, dest, true);

                _settings.ContasAvatarImagePath ??= new Dictionary<string, string>();
                _settings.ContasAvatarImagePath[_accountKey] = dest;
                _settings.ContasAvatarImageOffsets?.Remove(_accountKey);
                _settings.ContasAvatarImageScale = 1.0;

                _save();
                AtualizarTudo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Não foi possível usar o campeão '{champion}':\n{ex.Message}",
                    "Avatar da conta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Mesma rotina usada pelo botão "Escolher arquivo…": abre a janela modal "Alterar avatar".
        private void AbrirEditorDeAvatar(string caminhoImagem, string? destFileName = null)
        {
            try
            {
                var janela = new AlterarAvatarWindow(
                    _settings, _accountKey, caminhoImagem, _save, this, destFileName);
                janela.ShowDialog();
                AtualizarTudo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Não foi possível usar esta imagem:\n{ex.Message}",
                    "Avatar da conta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ChooseImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Escolher imagem para o avatar",
                Filter = "Imagem (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
                return;

            // Abre a janela modal "Alterar avatar" (arrastar + zoom).
            // A imagem só é copiada/salva ao clicar em Aplicar; Cancelar desfaz tudo.
            AbrirEditorDeAvatar(dialog.FileName);
        }

        // Atualiza apenas o preview ao vivo (usado a cada tecla no campo Nome).
        private void AtualizarPreview()
        {
            PreviewHost.Child = MainWindow.CriarAvatarVisual(
                _settings, _accountKey, NomeExibicao(), 64, 14, 34);
        }

        // Passo de deslocamento do recorte por clique (20% da sobra de cada lado)
        private const double OffsetStep = 0.2;

        private void AjustarOffset(double dx, double dy)
        {
            var (x, y) = MainWindow.LerAvatarImageOffset(_settings, _accountKey);
            MainWindow.SalvarAvatarImageOffset(_settings, _accountKey, x + dx, y + dy);
            _save();
            AtualizarTudo();
        }

        // ◀ mostra mais a parte esquerda da imagem
        private void OffsetLeft_Click(object sender, RoutedEventArgs e) => AjustarOffset(-OffsetStep, 0);

        // ▶ mostra mais a parte direita da imagem
        private void OffsetRight_Click(object sender, RoutedEventArgs e) => AjustarOffset(+OffsetStep, 0);

        // ▲ mostra mais a parte de cima da imagem
        private void OffsetUp_Click(object sender, RoutedEventArgs e) => AjustarOffset(0, -OffsetStep);

        // ▼ mostra mais a parte de baixo da imagem
        private void OffsetDown_Click(object sender, RoutedEventArgs e) => AjustarOffset(0, +OffsetStep);

        private void OffsetReset_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.SalvarAvatarImageOffset(_settings, _accountKey, 0, 0);
            _save();
            AtualizarTudo();
        }

        // Atualiza preview, destaques de seleção e dica da imagem.
        private void AtualizarTudo()
        {
            // Preview ao vivo (64x64)
            AtualizarPreview();

            // Destaque da cor selecionada
            string? corSelecionada = null;
            _settings.ContasAvatarColor?.TryGetValue(_accountKey, out corSelecionada);
            foreach (var child in ColorsPanel.Children)
            {
                if (child is Border circle && circle.Tag is string hex)
                {
                    bool selected = string.Equals(hex, corSelecionada, StringComparison.OrdinalIgnoreCase);
                    circle.BorderBrush = selected
                        ? new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2))
                        : BordaPadraoDoSwatch(hex);
                }
            }

            // Destaque do ícone selecionado
            string? iconeSelecionado = null;
            _settings.ContasAvatarIcon?.TryGetValue(_accountKey, out iconeSelecionado);
            foreach (var child in IconsPanel.Children)
            {
                if (child is Border cell && cell.Tag is string icon)
                {
                    bool selected = string.Equals(icon, iconeSelecionado, StringComparison.Ordinal);
                    cell.Background = new SolidColorBrush(selected
                        ? Color.FromRgb(0x26, 0x2B, 0x3D)
                        : Color.FromRgb(0x1B, 0x1E, 0x27));
                    cell.BorderBrush = selected
                        ? new SolidColorBrush(Color.FromRgb(0x2B, 0xD4, 0x6A))
                        : Brushes.Transparent;
                }
            }

            // Dica da imagem atual + visibilidade dos botões de posicionamento
            string? imagePath = null;
            _settings.ContasAvatarImagePath?.TryGetValue(_accountKey, out imagePath);
            bool temImagem = !string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath);
            ImageHint.Text = temImagem
                ? Path.GetFileName(imagePath)
                : "Nenhuma imagem escolhida";
            ImageOffsetPanel.Visibility = temImagem ? Visibility.Visible : Visibility.Collapsed;

            // Destaque do campeão selecionado (quando o avatar salvo é uma cópia "-champion-")
            string slug = MainWindow.AvatarSlug(_accountKey);
            string? nomeArquivo = temImagem ? Path.GetFileName(imagePath!) : null;
            foreach (var child in ChampionsPanel.Children)
            {
                if (child is Border cell && cell.Tag is string champion)
                {
                    bool selected = nomeArquivo != null &&
                        string.Equals(nomeArquivo, $"{slug}-champion-{champion}.png", StringComparison.OrdinalIgnoreCase);
                    cell.Background = new SolidColorBrush(selected
                        ? Color.FromRgb(0x26, 0x2B, 0x3D)
                        : Color.FromRgb(0x1B, 0x1E, 0x27));
                    cell.BorderBrush = selected
                        ? new SolidColorBrush(Color.FromRgb(0x2B, 0xD4, 0x6A))
                        : Brushes.Transparent;
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
