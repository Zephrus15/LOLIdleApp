using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LoLIdleDesktop;

// Janela HUD compacta: overlay com os stats do jogo que existem em HTML
// (ouro e a tarefa atual rastreada). Nada de HP/mana/XP (canvas do jogo, inacessíveis).
// Reusa a instância — não destrói; nasce com Topmost=True (pinado na frente).
public partial class HudCompactaWindow : Window
{
    private bool _closing;
    private bool _pinned = true;
    private readonly MainWindow.WindowSettings _settings;
    private bool _restoredFromSettings;

    public HudCompactaWindow(MainWindow.WindowSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        InitializeComponent();
        OuroText.Text = "—";
        TarefaText.Text = "—";
        MochilaText.Text = "—";
        ApplyPinVisual();
    }

    // Define os textos exibidos. Valores vazios viram "—". Sem escrita no jogo.
    public void SetStats(string ouro, string tarefa, string mochila)
    {
        OuroText.Text = string.IsNullOrWhiteSpace(ouro) ? "—" : ouro.Trim();
        TarefaText.Text = string.IsNullOrWhiteSpace(tarefa) ? "—" : tarefa.Trim();
        MochilaText.Text = string.IsNullOrWhiteSpace(mochila) ? "—" : mochila.Trim();
    }

    // Mostra a janela com fade 0→1 (~180ms), posicionada na borda direita da work area,
    // verticalmente centralizada. Não aceita foco (ShowActivated=False) nem aparece na taskbar.
    public void ShowAnimated()
    {
        try
        {
            var area = SystemParameters.WorkArea;
            double areaLeft = area.Left;
            double areaTop = area.Top;
            if (_settings.HasHudBounds)
            {
                // Restaura a última posição conhecida, com clamp na work area.
                Left = Math.Clamp(_settings.HudLeft, areaLeft, Math.Max(areaLeft, area.Right - Width));
                Top = Math.Clamp(_settings.HudTop, areaTop, Math.Max(areaTop, area.Bottom - 100));
                _restoredFromSettings = true;
            }
            else
            {
                // Default: borda direita da work area, verticalmente centralizada.
                Left = area.Right - Width - 16;
                Top = area.Top + Math.Max(0, (area.Height - (ActualHeight > 0 && !double.IsNaN(ActualHeight) ? ActualHeight : 140)) / 2.0);
                _restoredFromSettings = false;
            }
            Opacity = 1;
            IsHitTestVisible = true;
            Show();
            // Após o primeiro layout, recalcula Top com a altura real — SÓ quando a posição
            // não veio do setting (não sobrescrever a posição restaurada de HasHudBounds).
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_restoredFromSettings) return;
                try { Top = area.Top + Math.Max(0, (area.Height - ActualHeight) / 2.0); } catch { }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Root.BeginAnimation(UIElement.OpacityProperty, fade);
            MainWindow.LogFloatingWindowEvent("HUD: exibida");
        }
        catch (Exception ex)
        {
            MainWindow.LogFloatingWindowEvent($"HUD: falha exibindo: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Esconde a janela com fade 1→0 (~150ms) e chama Hide() ao fim. Não destrói a instância.
    public void HideAnimated()
    {
        if (_closing) return; // já em saída
        _closing = true;
        IsHitTestVisible = false;

        var fade = new DoubleAnimation(Root.Opacity, 0.0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            try { Hide(); } catch { }
            Opacity = 1;
            _closing = false;
            MainWindow.LogFloatingWindowEvent("HUD: escondida");
        };
        Root.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    // Clique dentro do X ou do pino não arrasta; qualquer outro ponto do header arrasta normalmente.
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src &&
            (HudCloseButton.IsAncestorOf(src) || HudPinButton.IsAncestorOf(src)))
            return; // clique no X ou no pino não arrasta
        try { DragMove(); } catch { }
    }

    private void HudCloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        // Fecha e pede o stop do poll imediatamente (o MainWindow decide quando precisa).
        HideAnimated();
        CloseRequested?.Invoke();
    }

    // Alterna o Topmost ("fixar na frente"): pino laranja = fixada; cinza = solta.
    private void HudPinButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _pinned = !_pinned;
        Topmost = _pinned;
        ApplyPinVisual();
        MainWindow.LogFloatingWindowEvent($"HUD: pino {(_pinned ? "fixado" : "solto")}");
    }

    private void ApplyPinVisual()
    {
        if (PinPath != null)
            PinPath.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_pinned ? "#D97B36" : "#93A0B4"));
        if (HudPinButton != null)
            HudPinButton.ToolTip = _pinned ? "Soltar da frente" : "Fixar na frente";
    }

    // Callback simples para o dono pedir o stop do poll sem acoplamento forte.
    public event Action? CloseRequested;
}
