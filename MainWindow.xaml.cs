
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
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

    private double _normalWidth = 1280;
    private double _normalHeight = 800;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
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

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "LoL Idle Desktop",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                RestoreFromTray();
        };
    }

    // Esconde a janela sem exibir notificações do Windows.
    private void HideToTray()
    {
        Hide();
    }

    // Restaura a janela sem recarregar o site.
    private void RestoreFromTray()
    {
        Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }

    // Encerra realmente o aplicativo.
    private void ExitApplication()
    {
        _allowExit = true;

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        Close();
    }

    // Alterna entre tamanho normal e compacto.
    private void Compact_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCompact)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;

            _normalWidth = Width;
            _normalHeight = Height;

            Width = 720;
            Height = 480;

            _isCompact = true;
        }
        else
        {
            Width = _normalWidth;
            Height = _normalHeight;

            _isCompact = false;
        }
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

    // Minimiza normalmente para a barra de tarefas do Windows.
    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    // O X esconde o aplicativo, não encerra o jogo.
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    // Fechamentos que não sejam pelo botão X não enviam o app à bandeja.
    // O botão X chama HideToTray diretamente; Alt + F4 apenas minimiza.
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
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
