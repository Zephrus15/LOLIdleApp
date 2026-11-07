using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using Cursors = System.Windows.Input.Cursors;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Panel = System.Windows.Controls.Panel;
using CheckBox = System.Windows.Controls.CheckBox;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace LoLIdleDesktop;

public partial class MainWindow : Window
{
    private const string GameUrl = "https://lolidle.com/jogar";
    private const string GameHost = "lolidle.com";

    // Instalado antes dos scripts do jogo para acompanhar AudioContext desde sua criação.
    private const string AudioBootstrapScript = @"(() => {
        if (window.__lolIdleAudioControlInstalled) return;
        window.__lolIdleAudioControlInstalled = true;
        const contexts = new Set();
        const pausedByUs = new WeakSet();
        const NativeAudioContext = window.AudioContext || window.webkitAudioContext;
        if (NativeAudioContext) {
            const WrappedAudioContext = new Proxy(NativeAudioContext, {
                construct(target, args, newTarget) {
                    const context = Reflect.construct(target, args, newTarget);
                    contexts.add(context);
                    return context;
                }
            });
            try {
                if (window.AudioContext) window.AudioContext = WrappedAudioContext;
                if (window.webkitAudioContext) window.webkitAudioContext = WrappedAudioContext;
            } catch (_) {}
        }
        window.__lolIdleSetAudioPrefs = async (prefs) => {
            window.__lolIdleAudioPrefs = prefs;
            document.querySelectorAll('audio,video').forEach(el => {
                if (!el.__lolIdleOriginalMutedSaved) {
                    el.__lolIdleOriginalMuted = el.muted;
                    el.__lolIdleOriginalMutedSaved = true;
                }
                const hint = [el.currentSrc, el.src, el.id, el.className,
                    el.getAttribute('aria-label'), el.getAttribute('data-name')]
                    .filter(Boolean).join(' ').toLowerCase();
                const isMusic = /(music|bgm|background|theme|trilha)/.test(hint);
                const isEffect = /(sfx|effect|effects|sound|click|attack|hit)/.test(hint);
                const channelMuted = isMusic ? prefs.musicMuted : isEffect ? prefs.effectsMuted : false;
                el.muted = !!(prefs.masterMuted || channelMuted || el.__lolIdleOriginalMuted);
            });
            for (const context of contexts) {
                try {
                    if (prefs.masterMuted && context.state === 'running') {
                        pausedByUs.add(context);
                        await context.suspend();
                    } else if (!prefs.masterMuted && pausedByUs.has(context)) {
                        pausedByUs.delete(context);
                        await context.resume();
                    }
                } catch (_) {}
            }
        };
    })();";

    private Forms.NotifyIcon? _trayIcon;
    private bool _allowExit;
    private bool _settingsReady;
    private System.Windows.Threading.DispatcherTimer? _settingsSaveTimer;
    private System.Windows.Threading.DispatcherTimer? _panelRefreshTimer;
    private System.Drawing.Icon? _appIcon;
    private CoreWebView2Environment? _webViewEnvironment;
    private string _lastFloatingUri = GameUrl;
    private Window? _floatingWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _floatingView;
    private Window? _preparedFloatingWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _preparedFloatingView;
    private bool _isPreparingFloatingView;
    private Window? _floatingSettingsWindow;
    private readonly Dictionary<string, AuxiliaryPanelWindow> _panelWindows = new();
    private WindowSettings _settings = new();
    private string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LoLIdleDesktop", "window-settings.json");

    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    private double _normalWidth = 1280;
    private double _normalHeight = 800;

    private sealed class WindowSettings
    {
        public double NormalWidth { get; set; } = 1280;
        public double NormalHeight { get; set; } = 800;
        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public bool IsAlwaysOnTop { get; set; }
        public bool HasFloatingBounds { get; set; }
        public double FloatingWidth { get; set; } = 900;
        public double FloatingHeight { get; set; } = 650;
        public double FloatingLeft { get; set; } = 100;
        public double FloatingTop { get; set; } = 100;
        public bool ShowQuestPanel { get; set; }
        public bool ShowSidebarPanel { get; set; }
        public bool ShowCharacterPanel { get; set; }
        public double QuestPanelOpacity { get; set; } = 0.30;
        public double SidebarPanelOpacity { get; set; } = 0.60;
        public double CharacterPanelOpacity { get; set; } = 0.80;
        public bool MusicMuted { get; set; }
        public bool EffectsMuted { get; set; }
        public bool MasterMuted { get; set; }
    }

    public MainWindow()
    {
        InitializeComponent();
        _settingsSaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _settingsSaveTimer.Tick += (_, _) =>
        {
            _settingsSaveTimer.Stop();
            SaveWindowSettings();
        };
        _panelRefreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _panelRefreshTimer.Tick += async (_, _) => await RefreshPanelDataAsync();
        InitializeTrayIcon();
        SourceInitialized += Window_SourceInitialized;
    }

    // Faz a janela sem bordas respeitar a área de trabalho ao maximizar.
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WM_GETMINMAXINFO)
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    info.ptMaxPosition.x = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
                    info.ptMaxPosition.y = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;
                    info.ptMaxSize.x = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
                    info.ptMaxSize.y = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
                    Marshal.StructureToPtr(info, lParam, true);
                    handled = true;
                }
            }
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // Lê as preferências e recupera a posição/tamanho usados anteriormente.
    private void RestoreWindowSettings()
    {
        WindowSettings? settings = null;

        try
        {
            if (File.Exists(SettingsPath))
            {
                settings = JsonSerializer.Deserialize<WindowSettings>(
                    File.ReadAllText(SettingsPath));
            }
        }
        catch
        {
            // Um arquivo de preferências inválido não impede o jogo de abrir.
        }

        if (settings != null)
        {
            _settings = settings;
            if (settings.NormalWidth >= 480 && settings.NormalWidth <= 10000)
                _normalWidth = settings.NormalWidth;

            if (settings.NormalHeight >= 320 && settings.NormalHeight <= 10000)
                _normalHeight = settings.NormalHeight;

            Width = _normalWidth;
            Height = _normalHeight;

            double minLeft = SystemParameters.VirtualScreenLeft;
            double minTop = SystemParameters.VirtualScreenTop;
            double maxLeft = minLeft + SystemParameters.VirtualScreenWidth - 120;
            double maxTop = minTop + SystemParameters.VirtualScreenHeight - 80;

            if (double.IsFinite(settings.Left) && double.IsFinite(settings.Top))
            {
                Left = Math.Clamp(settings.Left, minLeft, Math.Max(minLeft, maxLeft));
                Top = Math.Clamp(settings.Top, minTop, Math.Max(minTop, maxTop));
            }

        }

        _settingsReady = true;
    }

    // Salva as preferências no perfil local do Windows.
    private void SaveWindowSettings()
    {
        if (!_settingsReady)
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

            if (WindowState != WindowState.Minimized)
            {
                _settings.NormalWidth = _normalWidth;
                _settings.NormalHeight = _normalHeight;
                _settings.Left = Left;
                _settings.Top = Top;
            }
            CaptureFloatingWindowBounds();

            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }
        catch
        {
            // Falha ao salvar preferências não deve interromper o aplicativo.
        }
    }

    private void QueueWindowSettingsSave()
    {
        if (!_settingsReady || _settingsSaveTimer == null)
            return;

        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        QueueWindowSettingsSave();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_settingsReady && WindowState == WindowState.Normal)
        {
            _normalWidth = Width;
            _normalHeight = Height;
        }

        QueueWindowSettingsSave();
    }

    // Configuração do ícone e menu da bandeja do Windows.
    private void InitializeTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add(
            new Forms.ToolStripMenuItem(
                "Abrir LoL Idle",
                null,
                (_, _) => RestoreFromTray()));

        menu.Items.Add(new Forms.ToolStripSeparator());

        menu.Items.Add(
            new Forms.ToolStripMenuItem(
                "Sair",
                null,
                (_, _) => ExitApplication()));

        try
        {
            var iconResource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/lol-idle.ico"));
            if (iconResource != null)
            {
                using (iconResource.Stream)
                    _appIcon = new System.Drawing.Icon(iconResource.Stream);
            }
        }
        catch
        {
            _appIcon?.Dispose();
            _appIcon = null;
        }

        _appIcon ??= (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "LoL Idle Desktop",
            Icon = _appIcon,
            ContextMenuStrip = menu,
            Visible = false
        };

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                RestoreFromTray();
        };
    }

    private void HideToTray()
    {
        SaveWindowSettings();

        if (_trayIcon != null)
            _trayIcon.Visible = true;

        Hide();
    }

    private void RestoreFromTray()
    {
        Show();

        if (_trayIcon != null)
            _trayIcon.Visible = false;

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }

    private void ExitApplication()
    {
        SaveWindowSettings();
        _allowExit = true;

        _floatingSettingsWindow?.Close();
        foreach (var panel in new List<AuxiliaryPanelWindow>(_panelWindows.Values))
            panel.Close();
        _panelWindows.Clear();

        // Fecha a janela flutuante junto com o processo quando o usuário escolhe Sair.
        if (_floatingWindow != null)
        {
            var floating = _floatingWindow;
            _floatingWindow = null;
            _floatingView = null;
            floating.Close();
        }
        if (_preparedFloatingWindow != null)
        {
            var prepared = _preparedFloatingWindow;
            _preparedFloatingWindow = null;
            _preparedFloatingView = null;
            prepared.Close();
        }

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _appIcon?.Dispose();
        _appIcon = null;

        Close();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowSettings();
        RestorePanelWindows();

        try
        {
            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoLIdleDesktop", "WebView2");

            Directory.CreateDirectory(userData);

            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userData);

            await GameView.EnsureCoreWebView2Async(_webViewEnvironment);
            await GameView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(AudioBootstrapScript);

            GameView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            GameView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            GameView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            GameView.CoreWebView2.Settings.IsZoomControlEnabled = true;

            GameView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) &&
                    uri.Scheme is "http" or "https" &&
                    !uri.Host.Equals(GameHost, StringComparison.OrdinalIgnoreCase) &&
                    !uri.Host.EndsWith("." + GameHost, StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;

                    try
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = uri.ToString(),
                                UseShellExecute = true
                            });
                    }
                    catch { }
                }

                StatusText.Text = "Carregando LoL Idle...";
            };

            GameView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                StatusText.Text = args.IsSuccess
                    ? "LoL Idle pronto  •  Sua sessão fica salva neste computador"
                    : "Não foi possível carregar. Verifique sua conexão e tente atualizar.";
                if (args.IsSuccess)
                {
                    _ = ApplyAudioSettingsAsync();
                    _panelRefreshTimer?.Start();
                    _ = RefreshPanelDataAsync();
                }
            };

            GameView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                HandleNewWindowRequested(args);
            };

            GameView.Source = new Uri(GameUrl);
            _ = PrepareFloatingWebViewAsync(this);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erro ao iniciar o WebView2. Consulte o LEIA-ME.";

            MessageBox.Show(
                "Não foi possível iniciar o componente WebView2.\n\n" +
                ex.Message +
                "\n\nInstale o Microsoft Edge WebView2 Runtime e abra o aplicativo novamente.",
                "LoL Idle - erro de inicialização",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // Usa um WebView2 previamente inicializado. Nenhum EnsureCoreWebView2Async
    // é executado dentro do evento NewWindowRequested.
    private void HandleNewWindowRequested(CoreWebView2NewWindowRequestedEventArgs args)
    {
        try
        {
            var requestedUrl = args.Uri;
            var hasHttpUri = Uri.TryCreate(requestedUrl, UriKind.Absolute, out var requestedUri) &&
                requestedUri!.Scheme is "http" or "https";
            var isBlankWindow = string.IsNullOrWhiteSpace(requestedUrl) ||
                (Uri.TryCreate(requestedUrl, UriKind.Absolute, out var blankUri) &&
                 blankUri!.AbsoluteUri.Equals("about:blank", StringComparison.OrdinalIgnoreCase));

            LogFloatingWindowEvent($"NewWindowRequested: blank={isBlankWindow}, http={hasHttpUri}, userInitiated={args.IsUserInitiated}");

            if (!hasHttpUri && !isBlankWindow)
            {
                args.Handled = true;
                return;
            }

            if (hasHttpUri)
                _lastFloatingUri = requestedUri!.ToString();

            if (_preparedFloatingWindow == null || _preparedFloatingView?.CoreWebView2 == null)
            {
                LogFloatingWindowEvent("Popup: WebView pré-inicializado indisponível; mantendo comportamento padrão do WebView2.");
                return;
            }

            var window = _preparedFloatingWindow;
            var view = _preparedFloatingView;
            var core = view.CoreWebView2;

            _preparedFloatingWindow = null;
            _preparedFloatingView = null;
            _floatingWindow = window;
            _floatingView = view;

            args.NewWindow = core;
            args.Handled = true;
            LogFloatingWindowEvent("Popup: Core pré-inicializado entregue ao jogo.");

            ShowFloatingWindow(window);
            LogFloatingWindowEvent("Popup: janela exibida.");

            _ = PrepareFloatingWebViewAsync(this);
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"Popup: erro ao entregar WebView preparado: {ex}");
        }
    }

    // Restaura a última geometria da janela flutuante sem deixá-la fora da tela.
    private void ShowFloatingWindow(Window window)
    {
        var areaLeft = SystemParameters.VirtualScreenLeft;
        var areaTop = SystemParameters.VirtualScreenTop;
        var areaRight = areaLeft + SystemParameters.VirtualScreenWidth;
        var areaBottom = areaTop + SystemParameters.VirtualScreenHeight;
        var width = _settings.HasFloatingBounds ? _settings.FloatingWidth : 900;
        var height = _settings.HasFloatingBounds ? _settings.FloatingHeight : 650;
        width = Math.Clamp(width, 360, Math.Max(360, SystemParameters.VirtualScreenWidth));
        height = Math.Clamp(height, 240, Math.Max(240, SystemParameters.VirtualScreenHeight));

        window.WindowState = WindowState.Normal;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.CanResize;
        window.ShowInTaskbar = true;
        window.ShowActivated = true;
        window.Opacity = 1;
        window.Width = width;
        window.Height = height;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = _settings.HasFloatingBounds
            ? Math.Clamp(_settings.FloatingLeft, areaLeft, Math.Max(areaLeft, areaRight - width))
            : areaLeft + Math.Max(0, (SystemParameters.VirtualScreenWidth - width) / 2);
        window.Top = _settings.HasFloatingBounds
            ? Math.Clamp(_settings.FloatingTop, areaTop, Math.Max(areaTop, areaBottom - height))
            : areaTop + Math.Max(0, (SystemParameters.VirtualScreenHeight - height) / 2);

        if (!window.IsVisible)
            window.Show();

        if (!window.Tag?.Equals("floating-bounds-tracked") ?? true)
        {
            window.Tag = "floating-bounds-tracked";
            window.LocationChanged += (_, _) => QueueWindowSettingsSave();
            window.SizeChanged += (_, _) => QueueWindowSettingsSave();
            window.Closing += (_, _) =>
            {
                CaptureFloatingWindowBounds();
                SaveWindowSettings();
            };
        }

        // Garante que a janela recém-revelada fique acima do jogo e receba foco.
        window.Topmost = true;
        window.Activate();
        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            window.Topmost = false;
            window.Activate();
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void CaptureFloatingWindowBounds()
    {
        if (_floatingWindow == null || !_floatingWindow.IsVisible ||
            _floatingWindow.WindowState != WindowState.Normal)
            return;

        var window = _floatingWindow;
        if (window.Width < 360 || window.Height < 240 ||
            !double.IsFinite(window.Left) || !double.IsFinite(window.Top))
            return;

        _settings.FloatingWidth = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        _settings.FloatingHeight = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        _settings.FloatingLeft = window.Left;
        _settings.FloatingTop = window.Top;
        _settings.HasFloatingBounds = true;
    }

    // Log técnico mínimo, sem gravar URLs completas, cookies ou tokens.
    private static void LogFloatingWindowEvent(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoLIdleDesktop");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "floating-window.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [PID={Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch
        {
            // O log nunca deve interferir no funcionamento do jogo.
        }
    }

    // Prepara uma janela invisível antes de qualquer pedido do jogo.
    private async Task PrepareFloatingWebViewAsync(Window? owner)
    {
        if (_webViewEnvironment == null || _isPreparingFloatingView ||
            (_preparedFloatingWindow != null && _preparedFloatingView?.CoreWebView2 != null))
            return;

        _isPreparingFloatingView = true;
        Window? popupWindow = null;
        try
        {
            LogFloatingWindowEvent("Prepare: iniciando criação do host oculto.");
            popupWindow = new Window
            {
                Title = "LoL Idle - Janela flutuante",
                Width = 2,
                Height = 2,
                Left = -32000,
                Top = -32000,
                MinWidth = 360,
                MinHeight = 240,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Owner = owner,
                Opacity = 0,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(17, 19, 24))
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(25, 28, 35)),
                Padding = new Thickness(8, 0, 8, 0)
            };
            var toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            toolbarGrid.Children.Add(new TextBlock
            {
                Text = "LoL Idle",
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var topmostButton = new Button
            {
                Content = "◇",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(216, 220, 228)),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Ativar janela sempre no topo"
            };
            topmostButton.Click += (_, _) =>
            {
                popupWindow.Topmost = !popupWindow.Topmost;
                topmostButton.Content = popupWindow.Topmost ? "◆" : "◇";
                topmostButton.Foreground = popupWindow.Topmost
                    ? System.Windows.Media.Brushes.Gold
                    : new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(216, 220, 228));
                topmostButton.ToolTip = popupWindow.Topmost
                    ? "Desativar janela sempre no topo"
                    : "Ativar janela sempre no topo";
            };
            var audioMuteButton = new Button
            {
                Content = _settings.MasterMuted ? "🔇" : "🔊",
                Foreground = _settings.MasterMuted
                    ? new SolidColorBrush(Color.FromRgb(232, 207, 143))
                    : new SolidColorBrush(Color.FromRgb(216, 220, 228)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 15,
                Cursor = Cursors.Hand,
                ToolTip = _settings.MasterMuted ? "Ativar todo o áudio" : "Silenciar todo o áudio"
            };
            audioMuteButton.Click += (_, _) =>
            {
                _settings.MasterMuted = !_settings.MasterMuted;
                audioMuteButton.Content = _settings.MasterMuted ? "🔇" : "🔊";
                audioMuteButton.Foreground = _settings.MasterMuted
                    ? new SolidColorBrush(Color.FromRgb(232, 207, 143))
                    : new SolidColorBrush(Color.FromRgb(216, 220, 228));
                audioMuteButton.ToolTip = _settings.MasterMuted
                    ? "Ativar todo o áudio"
                    : "Silenciar todo o áudio";
                SaveWindowSettings();
                _ = ApplyAudioSettingsAsync();
            };
            Grid.SetColumn(audioMuteButton, 5);
            toolbarGrid.Children.Add(audioMuteButton);

            Grid.SetColumn(topmostButton, 6);
            toolbarGrid.Children.Add(topmostButton);

            var settingsButton = new Button
            {
                Content = "⚙",
                Foreground = new SolidColorBrush(Color.FromRgb(216, 220, 228)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = Cursors.Hand,
                ToolTip = "Configurações dos painéis e áudio"
            };
            settingsButton.Click += (_, _) => ShowFloatingSettings();
            Grid.SetColumn(settingsButton, 1);
            toolbarGrid.Children.Add(settingsButton);

            var questToggleButton = CreatePanelToggleButton("Missões", "quest", 2);
            var sidebarToggleButton = CreatePanelToggleButton("Barra lateral", "sidebar", 3);
            var characterToggleButton = CreatePanelToggleButton("Herói", "character", 4);
            toolbarGrid.Children.Add(questToggleButton);
            toolbarGrid.Children.Add(sidebarToggleButton);
            toolbarGrid.Children.Add(characterToggleButton);

            var closeButton = new Button
            {
                Content = "×",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(216, 220, 228)),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 22,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Fechar janela flutuante"
            };
            closeButton.Click += (_, _) => popupWindow.Close();
            Grid.SetColumn(closeButton, 7);
            toolbarGrid.Children.Add(closeButton);

            toolbar.MouseLeftButtonDown += (_, args) =>
            {
                // O clique nos botões percorre a árvore visual até o Border.
                // Não iniciar DragMove nesses casos, senão o botão perde o clique.
                if (IsInsideButton(args.OriginalSource as DependencyObject))
                    return;

                if (args.LeftButton == MouseButtonState.Pressed)
                    popupWindow.DragMove();
            };

            toolbar.Child = toolbarGrid;
            Grid.SetRow(toolbar, 0);
            root.Children.Add(toolbar);

            var view = new Microsoft.Web.WebView2.Wpf.WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(17, 19, 24)
            };
            Grid.SetRow(view, 1);
            root.Children.Add(view);
            popupWindow.Content = root;

            _preparedFloatingWindow = popupWindow;
            _preparedFloatingView = view;
            popupWindow.Closed += (_, _) =>
            {
                var wasActiveFloatingWindow = ReferenceEquals(_floatingWindow, popupWindow);
                var wasPreparedWindow = ReferenceEquals(_preparedFloatingWindow, popupWindow);

                if (wasPreparedWindow)
                {
                    _preparedFloatingWindow = null;
                    _preparedFloatingView = null;
                }

                if (wasActiveFloatingWindow)
                {
                    CaptureFloatingWindowBounds();
                    SaveWindowSettings();
                    var closedView = _floatingView;
                    _floatingWindow = null;
                    _floatingView = null;

                    // Notifica ao WebView2 que o popup foi realmente fechado,
                    // para que o jogo atualize o estado de window.closed.
                    // O controle WPF implementa Dispose; CoreWebView2 não expõe Close().
                    try { closedView?.Dispose(); } catch { }

                    LogFloatingWindowEvent("FloatingWindow: janela fechada; referências liberadas e popup encerrado.");
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_allowExit && _floatingWindow == null)
                            _ = PrepareFloatingWebViewAsync(this);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else if (wasPreparedWindow)
                {
                    LogFloatingWindowEvent("FloatingWindow: host preparado fechado; referências liberadas.");
                }
            };

            // Criar e mostrar o host invisível garante que o controle WPF tenha
            // seu HWND antes de inicializar o runtime do WebView2.
            popupWindow.Show();
            await popupWindow.Dispatcher.InvokeAsync(() => { },
                System.Windows.Threading.DispatcherPriority.Loaded);

            LogFloatingWindowEvent("Prepare: antes de EnsureCoreWebView2Async.");
            await view.EnsureCoreWebView2Async(_webViewEnvironment);
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(AudioBootstrapScript);
            LogFloatingWindowEvent("Prepare: EnsureCoreWebView2Async concluído e controle de áudio instalado.");

            view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            view.CoreWebView2.Settings.IsStatusBarEnabled = false;
            view.CoreWebView2.Settings.AreDevToolsEnabled = false;
            view.CoreWebView2.NewWindowRequested += (_, popupArgs) =>
                HandleNewWindowRequested(popupArgs);
            view.CoreWebView2.NavigationCompleted += (_, navArgs) =>
            {
                if (navArgs.IsSuccess)
                {
                    _ = ApplyAudioSettingsAsync();
                    _panelRefreshTimer?.Start();
                    _ = RefreshPanelDataAsync();
                }
            };

            popupWindow.Hide();
            popupWindow.Opacity = 1;
            popupWindow.ShowInTaskbar = true;
            popupWindow.WindowStyle = WindowStyle.None;
            popupWindow.ResizeMode = ResizeMode.CanResize;
            popupWindow.Width = 900;
            popupWindow.Height = 650;
            popupWindow.MinWidth = 360;
            popupWindow.MinHeight = 240;
            popupWindow.WindowStartupLocation = owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner;

            LogFloatingWindowEvent("Prepare: janela flutuante pronta e oculta.");
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"Prepare: falha ao inicializar WebView flutuante: {ex}");
            if (ReferenceEquals(_preparedFloatingWindow, popupWindow))
            {
                _preparedFloatingWindow = null;
                _preparedFloatingView = null;
            }
            popupWindow?.Close();
        }
        finally
        {
            _isPreparingFloatingView = false;
        }
    }


    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Button)
                return true;

            if (element is System.Windows.Media.Visual ||
                element is System.Windows.Media.Media3D.Visual3D)
            {
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    private Button CreatePanelToggleButton(string label, string key, int column)
    {
        var isEnabled = key switch
        {
            "quest" => _settings.ShowQuestPanel,
            "sidebar" => _settings.ShowSidebarPanel,
            _ => _settings.ShowCharacterPanel
        };

        var button = new Button
        {
            Content = isEnabled ? label + " ✓" : label,
            Foreground = isEnabled
                ? new SolidColorBrush(Color.FromRgb(232, 207, 143))
                : new SolidColorBrush(Color.FromRgb(155, 164, 178)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 11,
            Padding = new Thickness(2, 0, 2, 0),
            Cursor = Cursors.Hand,
            ToolTip = isEnabled ? "Desativar painel " + label : "Ativar painel " + label
        };
        button.Click += (_, _) =>
        {
            var currentlyEnabled = key switch
            {
                "quest" => _settings.ShowQuestPanel,
                "sidebar" => _settings.ShowSidebarPanel,
                _ => _settings.ShowCharacterPanel
            };
            SetPanelVisibility(key, !currentlyEnabled);
            button.Content = !currentlyEnabled ? label + " ✓" : label;
            button.Foreground = !currentlyEnabled
                ? new SolidColorBrush(Color.FromRgb(232, 207, 143))
                : new SolidColorBrush(Color.FromRgb(155, 164, 178));
            button.ToolTip = !currentlyEnabled
                ? "Desativar painel " + label
                : "Ativar painel " + label;
        };
        Grid.SetColumn(button, column);
        return button;
    }

    // Abre o centro de controle dos painéis independentes e do áudio.
    private void ShowFloatingSettings()
    {
        if (_floatingSettingsWindow != null)
        {
            _floatingSettingsWindow.Activate();
            return;
        }

        var window = new Window
        {
            Title = "LoL Idle - Configurações",
            Width = 430,
            Height = 650,
            MinWidth = 380,
            MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = _floatingWindow ?? this,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.FromRgb(17, 19, 24)),
            Foreground = Brushes.White
        };
        _floatingSettingsWindow = window;
        window.Closed += (_, _) => _floatingSettingsWindow = null;

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(18) };
        scroll.Content = stack;

        stack.Children.Add(new TextBlock
        {
            Text = "PAINÉIS INDEPENDENTES",
            Foreground = new SolidColorBrush(Color.FromRgb(232, 207, 143)),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 12)
        });

        AddPanelSetting(stack, "Missões", "quest", _settings.ShowQuestPanel, _settings.QuestPanelOpacity);
        AddPanelSetting(stack, "Barra lateral", "sidebar", _settings.ShowSidebarPanel, _settings.SidebarPanelOpacity);
        AddPanelSetting(stack, "Personagem", "character", _settings.ShowCharacterPanel, _settings.CharacterPanelOpacity);

        stack.Children.Add(new Separator { Margin = new Thickness(0, 18, 0, 16), Background = new SolidColorBrush(Color.FromRgb(55, 61, 72)) });
        stack.Children.Add(new TextBlock
        {
            Text = "ÁUDIO",
            Foreground = new SolidColorBrush(Color.FromRgb(232, 207, 143)),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 10)
        });

        AddAudioSetting(stack, "Silenciar música", _settings.MusicMuted, value =>
        {
            _settings.MusicMuted = value;
            SaveWindowSettings();
            _ = ApplyAudioSettingsAsync();
        });
        AddAudioSetting(stack, "Silenciar efeitos sonoros", _settings.EffectsMuted, value =>
        {
            _settings.EffectsMuted = value;
            SaveWindowSettings();
            _ = ApplyAudioSettingsAsync();
        });
        AddAudioSetting(stack, "Silenciar todo o áudio", _settings.MasterMuted, value =>
        {
            _settings.MasterMuted = value;
            SaveWindowSettings();
            _ = ApplyAudioSettingsAsync();
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Música e efeitos separados só podem ser controlados quando o jogo identifica cada canal. O silenciamento geral também pausa os contextos Web Audio detectados; sons criados antes da instalação do controle ou por mecanismos externos podem ter limitações.",
            Foreground = new SolidColorBrush(Color.FromRgb(145, 153, 168)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        });

        window.Content = scroll;
        window.Show();
        window.Activate();
    }

    private void AddPanelSetting(Panel parent, string title, string key, bool visible, double opacity)
    {
        var group = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var toggle = new CheckBox
        {
            Content = "Ativar " + title,
            IsChecked = visible,
            Foreground = Brushes.White,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 7)
        };
        toggle.Checked += (_, _) => SetPanelVisibility(key, true);
        toggle.Unchecked += (_, _) => SetPanelVisibility(key, false);
        group.Children.Add(toggle);

        var slider = new Slider
        {
            Minimum = 15,
            Maximum = 100,
            Value = Math.Clamp(opacity * 100, 15, 100),
            TickFrequency = 5,
            IsSnapToTickEnabled = false,
        };
        var valueText = new TextBlock
        {
            Text = $"Opacidade: {Math.Round(slider.Value)}%",
            Foreground = new SolidColorBrush(Color.FromRgb(155, 164, 178)),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        };
        slider.ValueChanged += (_, _) =>
        {
            valueText.Text = $"Opacidade: {Math.Round(slider.Value)}% (ao passar o mouse: 100%)";
            SetPanelOpacity(key, slider.Value / 100.0);
        };
        group.Children.Add(slider);
        group.Children.Add(valueText);
        parent.Children.Add(group);
    }

    private static void AddAudioSetting(Panel parent, string title, bool initialValue, Action<bool> changed)
    {
        var check = new CheckBox
        {
            Content = title,
            IsChecked = initialValue,
            Foreground = Brushes.White,
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 8)
        };
        check.Checked += (_, _) => changed(true);
        check.Unchecked += (_, _) => changed(false);
        parent.Children.Add(check);
    }

    private void SetPanelVisibility(string key, bool visible)
    {
        switch (key)
        {
            case "quest": _settings.ShowQuestPanel = visible; break;
            case "sidebar": _settings.ShowSidebarPanel = visible; break;
            case "character": _settings.ShowCharacterPanel = visible; break;
        }

        if (visible)
        {
            var panel = GetOrCreatePanel(key);
            panel.SetPanelVisible(true);
        }
        else if (_panelWindows.TryGetValue(key, out var panel))
        {
            panel.SetPanelVisible(false);
        }

        SaveWindowSettings();
    }

    private void SetPanelOpacity(string key, double opacity)
    {
        switch (key)
        {
            case "quest": _settings.QuestPanelOpacity = opacity; break;
            case "sidebar": _settings.SidebarPanelOpacity = opacity; break;
            case "character": _settings.CharacterPanelOpacity = opacity; break;
        }

        if (_panelWindows.TryGetValue(key, out var panel))
            panel.SetPanelOpacity(opacity);

        QueueWindowSettingsSave();
    }

    private void RestorePanelWindows()
    {
        if (_settings.ShowQuestPanel)
            GetOrCreatePanel("quest").SetPanelVisible(true);
        if (_settings.ShowSidebarPanel)
            GetOrCreatePanel("sidebar").SetPanelVisible(true);
        if (_settings.ShowCharacterPanel)
            GetOrCreatePanel("character").SetPanelVisible(true);
    }

    private sealed class PanelSnapshot
    {
        public string Quest { get; set; } = "";
        public string Sidebar { get; set; } = "";
        public string Character { get; set; } = "";
        public CharacterPanelStats CharacterStats { get; set; } = new();
        public List<PanelActionItem> Actions { get; set; } = new();
    }

    // Lê somente texto presente no DOM do jogo. Não estima nem inventa atributos.
    private async Task RefreshPanelDataAsync()
    {
        if (!_panelWindows.Values.Any(panel => panel.IsVisible))
            return;

        var floatingCore = _floatingView?.CoreWebView2;
        var source = floatingCore != null &&
            !string.Equals(floatingCore.Source, "about:blank", StringComparison.OrdinalIgnoreCase)
                ? floatingCore
                : GameView?.CoreWebView2;
        if (source == null)
            return;

        const string script = @"(() => {
            const lines = (document.body?.innerText || '')
                .split(/\n+/).map(x => x.trim()).filter(Boolean);
            const collect = (pattern) => {
                const found = [];
                for (let i = 0; i < lines.length; i++) {
                    if (pattern.test(lines[i])) {
                        for (let j = Math.max(0, i - 1); j <= Math.min(lines.length - 1, i + 2); j++)
                            found.push(lines[j]);
                    }
                }
                return [...new Set(found)].slice(0, 28).join('\n');
            };
            const visible = (el) => {
                const r = el.getBoundingClientRect();
                const st = getComputedStyle(el);
                return r.width > 0 && r.height > 0 && st.display !== 'none' &&
                    st.visibility !== 'hidden' && Number(st.opacity || 1) > 0;
            };
            let nextId = Number(window.__lolIdleNextActionId || 0);
            const actions = [];
            const candidates = document.querySelectorAll('button,[role=""button""],a,[onclick],summary');
            for (const el of candidates) {
                if (!visible(el)) continue;
                const label = (el.innerText || el.getAttribute('aria-label') ||
                    el.getAttribute('title') || el.getAttribute('alt') ||
                    el.getAttribute('data-tooltip') || el.value || '')
                    .replace(/\s+/g, ' ').trim().slice(0, 90);
                if (!label) continue;
                const region = el.closest('aside,section,article,[role=""dialog""],li') || el.parentElement;
                const context = (region?.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 180);
                const hay = (label + ' ' + context + ' ' + (el.getAttribute('class') || '')).toLowerCase();
                let category = 'sidebar';
                if (/quest|mission|miss[aã]o|objetivo|recompensa|daily|weekly|claim reward/.test(hay))
                    category = 'quest';
                else if (/skill|ability|habilidade|personagem|character|hero|mana|health|vida|atributo|level|nível/.test(hay))
                    category = 'character';
                else if (/item|invent[aá]rio|mochila|equip|po[cç][aã]o|shop|loja|craft|upgrade|comprar|vender|usar/.test(hay))
                    category = 'sidebar';
                if (!el.hasAttribute('data-lolidle-action-id')) {
                    nextId++;
                    el.setAttribute('data-lolidle-action-id', String(nextId));
                }
                actions.push({
                    id: el.getAttribute('data-lolidle-action-id'),
                    label,
                    category,
                    context
                });
            }
            const statElements = [...document.querySelectorAll('body *')].filter(el =>
                visible(el) && (el.children.length === 0 || el.matches('progress,[role=""progressbar""]')));
            const parseStat = (kind, words) => {
                const re = new RegExp(words, 'i');
                for (const el of statElements) {
                    const hint = [el.innerText, el.getAttribute('aria-label'), el.getAttribute('title'),
                        el.getAttribute('data-tooltip'), el.id, el.className,
                        el.getAttribute('role')].filter(Boolean).join(' ').replace(/\\s+/g, ' ').trim();
                    if (!re.test(hint)) continue;
                    const nearby = (el.innerText || el.parentElement?.innerText ||
                        el.parentElement?.parentElement?.innerText || '').replace(/\\s+/g, ' ').trim();
                    const raw = el.getAttribute('aria-valuenow') ?? el.value;
                    const maxRaw = el.getAttribute('aria-valuemax') ?? el.max;
                    const number = v => {
                        if (v == null || v === '') return null;
                        const n = Number(String(v).replace(',', '.').replace(/[^0-9.%-]/g, '').replace('%',''));
                        return Number.isFinite(n) ? n : null;
                    };
                    let current = number(raw), maximum = number(maxRaw), percent = null;
                    const pair = nearby.match(/(\\d[\\d.,]*)\\s*(?:\\/|de)\\s*(\\d[\\d.,]*)/);
                    if (pair) { current ??= number(pair[1]); maximum ??= number(pair[2]); }
                    if (current != null && maximum != null && maximum > 0)
                        percent = Math.max(0, Math.min(100, current / maximum * 100));
                    else if (el.tagName === 'PROGRESS' && maximum > 0)
                        percent = Math.max(0, Math.min(100, current / maximum * 100));
                    else {
                        const width = (el.getAttribute('style') || '').match(/width\\s*:\\s*([\\d.]+)%/i);
                        if (width) percent = Math.max(0, Math.min(100, Number(width[1])));
                    }
                    if (current != null || percent != null) {
                        return { label: kind, current: current == null ? '' : String(current),
                            maximum: maximum == null ? '' : String(maximum), percent };
                    }
                }
                return null;
            };
            const levelLine = lines.find(x => /(?:nível|nivel|level)\\s*[:#-]?\\s*\\d+/i.test(x)) || '';
            const skillNames = [];
            for (const el of statElements) {
                const hint = [el.getAttribute('aria-label'), el.getAttribute('title'),
                    el.getAttribute('alt'), el.getAttribute('data-tooltip'), el.className, el.id]
                    .filter(Boolean).join(' ');
                if (!/skill|ability|spell|habilidade|passiva/i.test(hint)) continue;
                const name = (el.getAttribute('aria-label') || el.getAttribute('title') ||
                    el.getAttribute('alt') || el.innerText || '').replace(/\\s+/g, ' ').trim();
                if (name && !skillNames.includes(name)) skillNames.push(name.slice(0, 70));
                if (skillNames.length >= 8) break;
            }
            window.__lolIdleNextActionId = nextId;
            return {
                quest: collect(/missões|missao|quest|progresso|objetivo|recompensa/i),
                sidebar: collect(/mochila|equipamento|itens|poções|pocões|poção|runas|cartas|loja|inventário/i),
                character: collect(/nível|nivel|experiência|experiencia|habilidades|atributos|dano|armadura|poder|mana|crítico|critico|esquiva|abates|mortes|vida|saúde/i),
                characterStats: {
                    hp: parseStat('Vida', 'hp|health|vida|saúde'),
                    mp: parseStat('Mana', 'mp|mana'),
                    xp: parseStat('XP', 'xp|experience|experiência|experiencia'),
                    level: levelLine,
                    skills: skillNames
                },
                actions
            };
        })();";

        try
        {
            var json = await source.ExecuteScriptAsync(script);
            var snapshot = JsonSerializer.Deserialize<PanelSnapshot>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (snapshot == null)
                return;

            if (_panelWindows.TryGetValue("quest", out var quest) && quest.IsVisible)
            {
                quest.SetPanelContent(snapshot.Quest);
                quest.SetPanelActions(snapshot.Actions.Where(x => x.Category == "quest"),
                    action => _ = ExecutePanelActionAsync(action));
            }
            if (_panelWindows.TryGetValue("sidebar", out var sidebar) && sidebar.IsVisible)
            {
                sidebar.SetPanelContent(snapshot.Sidebar);
                sidebar.SetPanelActions(snapshot.Actions.Where(x => x.Category == "sidebar"),
                    action => _ = ExecutePanelActionAsync(action));
            }
            if (_panelWindows.TryGetValue("character", out var character) && character.IsVisible)
            {
                character.SetPanelContent(snapshot.Character);
                character.SetCharacterStats(snapshot.CharacterStats);
                character.SetPanelActions(snapshot.Actions.Where(x => x.Category == "character"),
                    action => _ = ExecutePanelActionAsync(action));
            }
        }
        catch
        {
            // Uma leitura indisponível não deve interromper o jogo.
        }
    }

    private async Task ExecutePanelActionAsync(PanelActionItem action)
    {
        if (Regex.IsMatch(action.Label,
            @"buy|purchase|upgrade|sell|claim|collect|use|equip|enhance|unlock|comprar|vender|coletar|resgatar|usar|equipar|melhorar|desbloquear|forjar|gastar",
            RegexOptions.IgnoreCase))
        {
            var answer = MessageBox.Show(
                $"Deseja executar esta ação dentro do LoL Idle?\n\n{action.Label}",
                "Confirmar ação do jogo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
                return;
        }

        var floatingCore = _floatingView?.CoreWebView2;
        var source = floatingCore != null &&
            !string.Equals(floatingCore.Source, "about:blank", StringComparison.OrdinalIgnoreCase)
                ? floatingCore
                : GameView?.CoreWebView2;
        if (source == null)
            return;

        try
        {
            var id = JsonSerializer.Serialize(action.Id);
            var result = await source.ExecuteScriptAsync(
                $"(() => {{ const el = document.querySelector('[data-lolidle-action-id=' + {id} + ']'); if (!el || !el.isConnected) return false; el.click(); return true; }})()");
            var executed = string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
            if (_panelWindows.TryGetValue(action.Category, out var panel))
                panel.SetActionStatus(executed
                    ? $"Ação enviada ao jogo: {action.Label}"
                    : "Esse controle mudou ou não está mais disponível. Atualizando a lista...");
            await Task.Delay(180);
            await RefreshPanelDataAsync();
        }
        catch (Exception ex)
        {
            if (_panelWindows.TryGetValue(action.Category, out var panel))
                panel.SetActionStatus($"Não foi possível executar a ação: {ex.Message}");
        }
    }

    private AuxiliaryPanelWindow GetOrCreatePanel(string key)
    {
        if (_panelWindows.TryGetValue(key, out var existing))
            return existing;

        var (title, description, opacity) = key switch
        {
            "quest" => ("Missões", "Aguardando informações visíveis na interface do jogo.", _settings.QuestPanelOpacity),
            "sidebar" => ("Barra lateral", "Aguardando informações visíveis na interface do jogo.", _settings.SidebarPanelOpacity),
            _ => ("Herói", "Aguardando informações visíveis na interface do jogo.", _settings.CharacterPanelOpacity)
        };

        var panel = new AuxiliaryPanelWindow(title, description, key != "character", opacity);
        panel.Closed += (_, _) => _panelWindows.Remove(key);
        _panelWindows[key] = panel;
        return panel;
    }

    private async Task ApplyAudioSettingsAsync()
    {
        var preferences = JsonSerializer.Serialize(new
        {
            musicMuted = _settings.MusicMuted,
            effectsMuted = _settings.EffectsMuted,
            masterMuted = _settings.MasterMuted
        });

        var script = @"(() => {
            const prefs = " + preferences + @";
            if (typeof window.__lolIdleSetAudioPrefs === 'function') {
                window.__lolIdleSetAudioPrefs(prefs);
            } else {
                document.querySelectorAll('audio,video').forEach(el => {
                    if (!el.__lolIdleOriginalMutedSaved) {
                        el.__lolIdleOriginalMuted = el.muted;
                        el.__lolIdleOriginalMutedSaved = true;
                    }
                    const hint = [el.currentSrc, el.src, el.id, el.className,
                        el.getAttribute('aria-label'), el.getAttribute('data-name')]
                        .filter(Boolean).join(' ').toLowerCase();
                    const isMusic = /(music|bgm|background|theme|trilha)/.test(hint);
                    const isEffect = /(sfx|effect|effects|sound|click|attack|hit)/.test(hint);
                    const channelMuted = isMusic ? prefs.musicMuted : isEffect ? prefs.effectsMuted : false;
                    el.muted = !!(prefs.masterMuted || channelMuted || el.__lolIdleOriginalMuted);
                });
            }
            if (!window.__lolIdleAudioObserver) {
                window.__lolIdleAudioObserver = new MutationObserver(() => {
                    if (typeof window.__lolIdleSetAudioPrefs === 'function')
                        window.__lolIdleSetAudioPrefs(window.__lolIdleAudioPrefs || prefs);
                });
                window.__lolIdleAudioObserver.observe(document.documentElement, {
                    childList: true, subtree: true, attributes: true,
                    attributeFilter: ['src', 'id', 'class', 'data-name', 'aria-label']
                });
            }
            return { mediaElements: document.querySelectorAll('audio,video').length,
                webAudioControl: typeof window.__lolIdleSetAudioPrefs === 'function' };
        })();";

        foreach (var core in new[] { GameView?.CoreWebView2, _floatingView?.CoreWebView2 })
        {
            if (core == null)
                continue;
            try { await core.ExecuteScriptAsync(script); }
            catch (Exception ex) { LogFloatingWindowEvent($"Audio: não foi possível aplicar preferências: {ex.Message}"); }
        }
    }

    // Abre a janela já preparada sem inicializar WebView2 durante o clique.
    private async void ReopenFloatingWindow_Click(object sender, RoutedEventArgs e)
    {
        LogFloatingWindowEvent("ReopenButton: clique recebido.");
        try
        {
            if (_floatingWindow != null)
            {
                ShowFloatingWindow(_floatingWindow);
                return;
            }

            if (_preparedFloatingWindow == null || _preparedFloatingView?.CoreWebView2 == null)
                await PrepareFloatingWebViewAsync(this);

            if (_preparedFloatingWindow == null || _preparedFloatingView?.CoreWebView2 == null)
            {
                LogFloatingWindowEvent("ReopenButton: WebView preparado indisponível.");
                return;
            }

            var window = _preparedFloatingWindow;
            var view = _preparedFloatingView;
            var core = view.CoreWebView2;
            _preparedFloatingWindow = null;
            _preparedFloatingView = null;
            _floatingWindow = window;
            _floatingView = view;

            ShowFloatingWindow(window);

            var target = Uri.TryCreate(_lastFloatingUri, UriKind.Absolute, out var savedUri) &&
                         savedUri.Scheme is "http" or "https"
                ? savedUri.ToString()
                : GameUrl;
            if (string.IsNullOrWhiteSpace(core.Source) || core.Source == "about:blank")
                core.Navigate(target);

            _ = PrepareFloatingWebViewAsync(this);
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"ReopenButton: falha: {ex}");
            MessageBox.Show(
                "Não foi possível abrir a janela flutuante.\\n\\n" + ex.Message,
                "LoL Idle - janela flutuante",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        SaveWindowSettings();
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowSettings();

        if (!_allowExit)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }
    }

    private void TitleBar_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            Maximize_Click(sender, e);
        else if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
