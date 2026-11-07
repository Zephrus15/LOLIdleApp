using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
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
    private bool _isCompact;
    private bool _wasMaximizedBeforeCompact;
    private System.Drawing.Icon? _appIcon;

    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    private readonly string _settingsPath;
    private double _normalWidth = 1280;
    private double _normalHeight = 800;
    private double _normalLeft = 100;
    private double _normalTop = 100;

    public MainWindow()
    {
        InitializeComponent();

        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LoLIdleDesktop",
            "window-settings.json");

        LoadWindowSettings();
        InitializeTrayIcon();
        SourceInitialized += Window_SourceInitialized;
    }

    // Restaura as preferências da janela sem interferir na sessão do jogo.
    private void LoadWindowSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var settings = JsonSerializer.Deserialize<WindowSettings>(
                File.ReadAllText(_settingsPath));

            if (settings == null)
                return;

            _normalWidth = Math.Max(MinWidth, settings.NormalWidth);
            _normalHeight = Math.Max(MinHeight, settings.NormalHeight);
            _normalLeft = settings.NormalLeft;
            _normalTop = settings.NormalTop;

            Width = settings.IsCompact ? 680 : _normalWidth;
            Height = settings.IsCompact ? 420 : _normalHeight;
            Left = settings.Left;
            Top = settings.Top;
            _isCompact = settings.IsCompact;
            Topmost = settings.AlwaysOnTop;

            if (settings.IsMaximized && !_isCompact)
                WindowState = WindowState.Maximized;

            UpdateAlwaysOnTopButton();
            KeepWindowOnScreen();
        }
        catch
        {
            // Configurações inválidas não devem impedir a abertura do aplicativo.
        }
    }

    private void SaveWindowSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

            var settings = new WindowSettings
            {
                Left = WindowState == WindowState.Normal ? Left : _lastLeft,
                Top = WindowState == WindowState.Normal ? Top : _lastTop,
                NormalWidth = _normalWidth,
                NormalHeight = _normalHeight,
                NormalLeft = _normalLeft,
                NormalTop = _normalTop,
                IsCompact = _isCompact,
                IsMaximized = WindowState == WindowState.Maximized,
                AlwaysOnTop = Topmost
            };

            File.WriteAllText(
                _settingsPath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Falha ao salvar preferências não deve fechar ou travar o jogo.
        }
    }

    private double _lastLeft = 100;
    private double _lastTop = 100;

    private sealed class WindowSettings
    {
        public WindowSettings() { }

        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public double NormalWidth { get; set; } = 1280;
        public double NormalHeight { get; set; } = 800;
        public double NormalLeft { get; set; } = 100;
        public double NormalTop { get; set; } = 100;
        public bool IsCompact { get; set; }
        public bool IsMaximized { get; set; }
        public bool AlwaysOnTop { get; set; }
    }

    private void KeepWindowOnScreen()
    {
        if (Left + 100 < SystemParameters.VirtualScreenLeft ||
            Left > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100)
        {
            Left = SystemParameters.VirtualScreenLeft + 40;
        }

        if (Top < SystemParameters.VirtualScreenTop ||
            Top > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80)
        {
            Top = SystemParameters.VirtualScreenTop + 40;
        }

        _lastLeft = Left;
        _lastTop = Top;
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

    // Alterna entre a janela normal e a janela compacta.
    private void Compact_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCompact)
        {
            _wasMaximizedBeforeCompact = WindowState == WindowState.Maximized;

            if (_wasMaximizedBeforeCompact)
                WindowState = WindowState.Normal;

            _normalWidth = Width;
            _normalHeight = Height;
            _normalLeft = Left;
            _normalTop = Top;

            Width = 680;
            Height = 420;
            _isCompact = true;
        }
        else
        {
            Width = _normalWidth;
            Height = _normalHeight;
            Left = _normalLeft;
            Top = _normalTop;
            _isCompact = false;

            if (_wasMaximizedBeforeCompact)
                WindowState = WindowState.Maximized;
        }

        KeepWindowOnScreen();
        SaveWindowSettings();
    }

    // Fixa ou libera a janela sobre os outros aplicativos.
    private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdateAlwaysOnTopButton();
        SaveWindowSettings();
    }

    private void UpdateAlwaysOnTopButton()
    {
        if (AlwaysOnTopButton == null)
            return;

        AlwaysOnTopButton.Content = Topmost ? "📌" : "◇";
        AlwaysOnTopButton.ToolTip = Topmost
            ? "Desativar janela sempre no topo"
            : "Ativar janela sempre no topo";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoLIdleDesktop", "WebView2");

            Directory.CreateDirectory(userData);

            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userData);

            await GameView.EnsureCoreWebView2Async(environment);

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
                args.Handled = true;

                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
                {
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

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        SaveWindowSettings();
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _lastLeft = Left;
            _lastTop = Top;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
        }

        SaveWindowSettings();
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
        {
            DragMove();
            if (WindowState == WindowState.Normal)
            {
                _lastLeft = Left;
                _lastTop = Top;
            }
            SaveWindowSettings();
        }
    }
}
