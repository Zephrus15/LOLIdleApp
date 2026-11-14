using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Button = System.Windows.Controls.Button;

namespace LoLIdleDesktop;

// Popup próprio de notificação (card ardósia+cobre, canto inferior direito).
// O toast do Windows continua sendo enviado à parte (silencioso, como registro
// na Central de Ações); esta janela é o único elemento visível para o usuário.
public partial class NotificationPopupWindow : Window
{
    private readonly DispatcherTimer _autoHideTimer;
    private readonly string? _conta;
    private readonly string? _kind;
    private readonly string? _context;
    private readonly Action<string?, string?, string?>? _onClick;
    private bool _closed;
    private bool _closing;
    // Âncora calculada no Show (canto inferior direito), usada como fallback
    // de posição inicial da animação de saída se Left estiver indefinido (NaN).
    private double? _memoryAnchor;

    public NotificationPopupWindow(MainWindow.WindowSettings settings,
                                   string? conta,
                                   string? kind,
                                   string title,
                                   string message,
                                   Action<string?, string?, string?>? onClick,
                                   string? context = null)
    {
        _conta = conta;
        _kind = kind;
        _context = context;
        _onClick = onClick;

        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;

        // Avatar da conta (mesmo visual do chip/histórico). Qualquer falha → omite o avatar.
        try
        {
            string display = string.IsNullOrWhiteSpace(title) ? (conta ?? "LoL Idle") : title;
            var avatar = MainWindow.CriarAvatarVisual(settings, conta ?? string.Empty, display, 40, 7, 24);
            AvatarHost.Content = avatar;
        }
        catch (Exception ex)
        {
            MainWindow.LogFloatingWindowEvent($"NotificationPopup: avatar falhou ({ex.Message}) — exibindo sem avatar");
            AvatarHost.Visibility = Visibility.Collapsed;
        }

        Opacity = 0;

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _autoHideTimer.Tick += (_, _) =>
        {
            // Um tick já enfileirado pode rodar após o fechamento — ignora com segurança.
            if (_closed) return;
            CloseWithAnimation();
        };

        Loaded += (_, _) =>
        {
            _autoHideTimer.Start();
            // A janela sobe invisível; o fade visível acontece no Root (Opacity + deslize da direita).
            Opacity = 1;
            // Entrada = espelho da saída: nasce fora da tela pela direita (largura + 40 px).
            // ActualWidth pode ser 0/NaN antes do primeiro layout — cai para Width e, por fim, 400.
            double width = ActualWidth;
            if (double.IsNaN(width) || width <= 0)
                width = Width;
            double effectiveWidth = (double.IsNaN(width) || width <= 0 ? 360 : width);
            double initialX = effectiveWidth + 40;
            _memoryAnchor = SystemParameters.WorkArea.Right - effectiveWidth - 16;
            RootTranslate.X = initialX;
            var entry = TimeSpan.FromMilliseconds(450);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            // Opacidade espelhada da saída: segura 0 até ~50% do deslize e só revela no final,
            // quando o card já está praticamente na posição.
            var fade = new DoubleAnimationUsingKeyFrames { Duration = entry };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(Root.Opacity, KeyTime.FromPercent(0.0)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.5)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
            Root.BeginAnimation(UIElement.OpacityProperty, fade);
            var slide = new DoubleAnimation(initialX, 0, entry) { EasingFunction = ease };
            RootTranslate.BeginAnimation(TranslateTransform.XProperty, slide);
        };

        Closed += (_, _) =>
        {
            _closed = true;
            _autoHideTimer.Stop();
        };
    }

    // Fecha a janela com animação de saída (~350 ms, QuadraticEase.EaseIn):
    // a PRÓPRIA janela desliza para fora da tela pela direita (Left → Left atual
    // + largura + 48 px) enquanto a opacidade se mantém ~1 durante a maior
    // parte do deslize e só cai para 0 no final; ao terminar, Close().
    // Animar o Left da Window (em vez do TranslateTransform interno) evita o
    // recorte do conteúdo na borda da janela. Se Left não puder ser animado,
    // cai para o fallback do RootTranslate (com log de qual caminho foi usado).
    // Idempotente: chamadas subsequentes (duplo clique, tick concorrente) são ignoradas.
    // _closed é setado já no INÍCIO da saída para impedir tick/clique duplicado.
    internal void CloseWithAnimation()
    {
        if (_closing) return;
        _closing = true;
        _closed = true;
        _autoHideTimer.Stop();
        IsHitTestVisible = false; // não recebe mais cliques durante a saída

        double width = ActualWidth;
        if (double.IsNaN(width) || width <= 0)
            width = Width;
        if (double.IsNaN(width) || width <= 0)
            width = 360;
        // Left pode ser NaN se a janela ainda não foi posicionada — cai para a
        // âncora calculada no Show (canto inferior direito) ou para o cálculo direto.
        double initialLeft = double.IsNaN(Left)
            ? (_memoryAnchor ?? SystemParameters.WorkArea.Right - width - 16)
            : Left;
        double targetLeft = initialLeft + width + 48;

        var exit = TimeSpan.FromMilliseconds(350);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };

        // Opacidade: segura ~1 até 55% do deslize e só esmaece no final,
        // quando o card já está praticamente fora da tela.
        var fade = new DoubleAnimationUsingKeyFrames { Duration = exit };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(Root.Opacity, KeyTime.FromPercent(0.0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.55)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));
        fade.Completed += (_, _) =>
        {
            try { Close(); } catch { }
        };
        // Tenta animar o Left da própria janela; se o WPF recusar/clampar, cai para
        // o TranslateTransform interno (comportamento antigo).
        bool leftAnimated = false;
        try
        {
            var slideLeft = new DoubleAnimation(initialLeft, targetLeft, exit) { EasingFunction = ease };
            BeginAnimation(Window.LeftProperty, slideLeft);
            leftAnimated = true;
            MainWindow.LogFloatingWindowEvent($"Popup saída: Left → {targetLeft:0} (animada)");
        }
        catch (Exception ex)
        {
            MainWindow.LogFloatingWindowEvent($"Popup saída: fallback transform ({ex.Message})");
        }
        if (!leftAnimated)
        {
            double targetX = width + 48;
            var slide = new DoubleAnimation(RootTranslate.X, targetX, exit) { EasingFunction = ease };
            RootTranslate.BeginAnimation(TranslateTransform.XProperty, slide);
        }
        Root.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    // Hover pausa o auto-hide; ao sair, o timer recomeça do zero (6s).
    private void Root_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_closing) return;
        _autoHideTimer.Stop();
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_closing) return;
        _autoHideTimer.Stop();
        _autoHideTimer.Interval = TimeSpan.FromSeconds(6);
        _autoHideTimer.Start();
    }

    // ✕ fecha SÓ o popup (sem executar a ação da notificação).
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closing) return;
        MainWindow.LogFloatingWindowEvent($"NotificationPopup: fechado pelo ✕ (conta='{_conta}', kind='{_kind}')");
        CloseWithAnimation();
    }

    // Clique em qualquer área do card (menos o ✕): fecha e executa a ação da notificação.
    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_closing) return;
        if (IsWithinCloseButton(e.OriginalSource as DependencyObject) || CloseButton.IsMouseOver)
            return;

        MainWindow.LogFloatingWindowEvent($"NotificationPopup: clique no card (conta='{_conta}', kind='{_kind}')");
        CloseWithAnimation();
        try
        {
            _onClick?.Invoke(_conta, _kind, _context);
        }
        catch (Exception ex)
        {
            MainWindow.LogFloatingWindowEvent($"NotificationPopup: ação do clique falhou: {ex.Message}");
        }
    }

    // OriginalSource numa área clicável do ✕ costuma ser um filho do botão (TextBlock/Border),
    // então sobe a árvore visual para ver se o clique caiu dentro do CloseButton.
    private bool IsWithinCloseButton(DependencyObject? source)
    {
        try
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, CloseButton))
                    return true;
                current = current is Visual visual
                    ? VisualTreeHelper.GetParent(visual)
                    : null;
            }
        }
        catch
        {
            // Se a árvore visual não puder ser percorrida, cai para o teste por IsMouseOver no chamador.
        }
        return false;
    }
}
