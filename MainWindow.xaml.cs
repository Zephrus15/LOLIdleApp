using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace LoLIdleDesktop;

public partial class MainWindow : Window
{
    private const string GameUrl = "https://lolidle.com/jogar";
    private const string GameHost = "lolidle.com";

    private Forms.NotifyIcon? _trayIcon;
    private bool _allowExit;
    private bool _settingsReady;
    private System.Windows.Threading.DispatcherTimer? _settingsSaveTimer;
    private System.Drawing.Icon? _appIcon;
    private CoreWebView2Environment? _webViewEnvironment;
    private string _lastFloatingUri = GameUrl;
    private Window? _floatingWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _floatingView;
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
        if (!_settingsReady || WindowState == WindowState.Minimized)
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

            var settings = new WindowSettings
            {
                NormalWidth = _normalWidth,
                NormalHeight = _normalHeight,
                Left = Left,
                Top = Top,
            };

            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions
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

        // Fecha a janela flutuante junto com o processo quando o usuário escolhe Sair.
        if (_floatingWindow != null)
        {
            var floating = _floatingWindow;
            _floatingWindow = null;
            _floatingView = null;
            floating.Close();
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

        try
        {
            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoLIdleDesktop", "WebView2");

            Directory.CreateDirectory(userData);

            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userData);

            await GameView.EnsureCoreWebView2Async(_webViewEnvironment);
            FloatingReopenButton.IsEnabled = true;

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
            };

            GameView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                // Usa o deferral oficial do WebView2: a solicitação permanece válida
                // enquanto inicializamos o novo controle, sem bloquear a interface.
                _ = HandleNewWindowRequestedAsync(args, this);
            };

            GameView.Source = new Uri(GameUrl);
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

    // Atende pop-ups sem reutilizar um CoreWebView2 que já recebeu navegação.
    private async Task HandleNewWindowRequestedAsync(
        CoreWebView2NewWindowRequestedEventArgs args,
        Window? owner)
    {
        LogFloatingWindowEvent("Handler: evento recebido.");
        using (args.GetDeferral())
        {
            LogFloatingWindowEvent("Handler: deferral obtido.");
            try
            {
                var requestedUrl = args.Uri;
                LogFloatingWindowEvent("Handler: argumentos lidos.");
                var hasHttpUri = Uri.TryCreate(
                    requestedUrl, UriKind.Absolute, out var requestedUri) &&
                    requestedUri!.Scheme is "http" or "https";
                var isBlankWindow = string.IsNullOrWhiteSpace(requestedUrl) ||
                    (Uri.TryCreate(requestedUrl, UriKind.Absolute, out var blankUri) &&
                     blankUri!.AbsoluteUri.Equals("about:blank", StringComparison.OrdinalIgnoreCase));

                LogFloatingWindowEvent(
                    $"NewWindowRequested: blank={isBlankWindow}, http={hasHttpUri}, " +
                    $"userInitiated={args.IsUserInitiated}");

                // window.open() pode começar sem URL (ou com about:blank) e
                // receber o conteúdo depois, via JavaScript. Isso também é válido.
                if (!hasHttpUri && !isBlankWindow)
                {
                    LogFloatingWindowEvent("Solicitação ignorada: esquema de URL não permitido.");
                    args.Handled = true;
                    return;
                }

                if (hasHttpUri)
                {
                    _lastFloatingUri = requestedUri!.ToString();
                    FloatingReopenButton.IsEnabled = true;
                }

                if (_floatingWindow != null && _floatingView?.CoreWebView2 != null)
                {
                    _floatingWindow.Activate();
                    if (hasHttpUri)
                    {
                        _floatingView.CoreWebView2.Navigate(requestedUri!.ToString());
                        args.Handled = true;
                    }
                    else
                    {
                        // Não responder com WindowProxy fictício para uma abertura vazia.
                        // O WebView2 segue o comportamento padrão se já houver popup ativo.
                        LogFloatingWindowEvent("Popup em branco recebido com janela já aberta; tratamento padrão mantido.");
                    }
                    return;
                }

                LogFloatingWindowEvent("Handler: antes de criar WebView flutuante.");
                var newCore = await CreateFloatingWebViewAsync(owner);
                LogFloatingWindowEvent("Handler: WebView flutuante criado.");
                args.NewWindow = newCore;
                LogFloatingWindowEvent("Handler: CoreWebView2 atribuído a NewWindow.");
                args.Handled = true;

                // Só exibe a janela depois que o CoreWebView2 foi entregue ao evento.
                // Assim o popup não aparece antes de o WebView2 assumir a solicitação.
                LogFloatingWindowEvent("Handler: antes de exibir janela.");
                _floatingWindow?.Show();
                LogFloatingWindowEvent("Handler: janela exibida.");
            }
            catch (Exception ex)
            {
                LogFloatingWindowEvent($"Falha ao criar janela: {ex}");
                MessageBox.Show(
                    "Não foi possível abrir a janela flutuante.\n\n" + ex.Message,
                    "LoL Idle - janela flutuante",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                args.Handled = true;
            }
        }
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
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // O log nunca deve interferir no funcionamento do jogo.
        }
    }

    // Mantém uma única janela flutuante e limpa a referência quando ela é fechada.
    // O WebView2 compartilha o mesmo ambiente para preservar cookies e sessão.
    private async Task<CoreWebView2> CreateFloatingWebViewAsync(Window? owner)
    {
        if (_webViewEnvironment == null)
            throw new InvalidOperationException("O ambiente WebView2 ainda não foi inicializado.");

        if (_floatingWindow != null && _floatingView?.CoreWebView2 != null)
        {
            _floatingWindow.Activate();
            return _floatingView.CoreWebView2;
        }

        LogFloatingWindowEvent("CreateFloating: iniciando criação da janela WPF.");
        var popupWindow = new Window
        {
            Title = "LoL Idle - Janela flutuante",
            Width = 900,
            Height = 650,
            MinWidth = 360,
            MinHeight = 240,
            WindowStartupLocation = owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            Owner = owner,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(17, 19, 24))
        };

        popupWindow.WindowStyle = WindowStyle.SingleBorderWindow;

        LogFloatingWindowEvent("CreateFloating: janela WPF criada.");
        var popupRoot = new Grid();
        popupRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        popupRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var popupToolbar = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(25, 28, 35)),
            Padding = new Thickness(8, 0, 8, 0)
        };
        var popupToolbarGrid = new Grid();
        popupToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        popupToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

        var popupTitle = new TextBlock
        {
            Text = "LoL Idle",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        popupToolbarGrid.Children.Add(popupTitle);

        var popupTopmostButton = new Button
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
        popupTopmostButton.Click += (_, _) =>
        {
            popupWindow.Topmost = !popupWindow.Topmost;
            popupTopmostButton.Content = popupWindow.Topmost ? "◆" : "◇";
            popupTopmostButton.Foreground = popupWindow.Topmost
                ? System.Windows.Media.Brushes.Gold
                : new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(216, 220, 228));
            popupTopmostButton.ToolTip = popupWindow.Topmost
                ? "Desativar janela sempre no topo"
                : "Ativar janela sempre no topo";
        };
        Grid.SetColumn(popupTopmostButton, 1);
        popupToolbarGrid.Children.Add(popupTopmostButton);
        popupToolbar.Child = popupToolbarGrid;
        Grid.SetRow(popupToolbar, 0);
        popupRoot.Children.Add(popupToolbar);

        LogFloatingWindowEvent("CreateFloating: antes de criar controle WebView2.");
        var popupView = new Microsoft.Web.WebView2.Wpf.WebView2
        {
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(17, 19, 24)
        };
        Grid.SetRow(popupView, 1);
        popupRoot.Children.Add(popupView);

        _floatingWindow = popupWindow;
        _floatingView = popupView;

        popupWindow.Closed += (_, _) =>
        {
            // Não deixar uma referência morta bloquear a próxima abertura.
            if (ReferenceEquals(_floatingWindow, popupWindow))
            {
                _floatingWindow = null;
                _floatingView = null;
            }
        };

        popupWindow.Content = popupRoot;
        LogFloatingWindowEvent("CreateFloating: conteúdo da janela configurado.");

        try
        {
            // Inicializa o controle antes de exibir a janela, evitando a janela preta
            // causada por entregar ao WebView2 um CoreWebView2 ainda não pronto.
            LogFloatingWindowEvent("CreateFloating: antes de EnsureCoreWebView2Async.");
            await popupView.EnsureCoreWebView2Async(_webViewEnvironment);
            LogFloatingWindowEvent("CreateFloating: EnsureCoreWebView2Async concluído.");
            popupView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            popupView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            popupView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            popupView.CoreWebView2.NewWindowRequested += (_, popupArgs) =>
            {
                _ = HandleNewWindowRequestedAsync(popupArgs, popupWindow);
            };
            LogFloatingWindowEvent("CreateFloating: configurações e eventos aplicados.");

            // A janela será exibida pelo chamador depois que args.NewWindow
            // receber esta instância, ou imediatamente no fluxo de abertura manual.
            return popupView.CoreWebView2;
        }
        catch
        {
            if (ReferenceEquals(_floatingWindow, popupWindow))
            {
                _floatingWindow = null;
                _floatingView = null;
            }
            popupWindow.Close();
            throw;
        }
    }

    // Reabre a janela se ela foi fechada, sem reutilizar um WebView2 já descartado.
    private async void ReopenFloatingWindow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_floatingWindow != null && _floatingView?.CoreWebView2 != null)
            {
                _floatingWindow.Activate();
                return;
            }

            var floatingCore = await CreateFloatingWebViewAsync(this);
            _floatingWindow?.Show();

            var target = Uri.TryCreate(_lastFloatingUri, UriKind.Absolute, out var savedUri) &&
                         savedUri.Scheme is "http" or "https"
                ? savedUri.ToString()
                : GameUrl;

            // O botão é uma abertura explícita, então a nova janela recebe o jogo
            // somente depois que o WebView2 terminou sua inicialização.
            if (floatingCore.Source == "about:blank" ||
                string.IsNullOrWhiteSpace(floatingCore.Source))
            {
                floatingCore.Navigate(target);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Não foi possível abrir a janela flutuante.\n\n" + ex.Message,
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
