using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
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
        const syncGameButtons = (prefs) => {
            const sync = (id, muted) => {
                try {
                    const b = document.getElementById(id);
                    if (!b) return;
                    const curMuted = b.getAttribute('aria-pressed') === 'false';
                    if (curMuted !== !!muted) b.click();
                } catch (_) {}
            };
            sync('bt-musica', prefs.musicMuted);
            sync('bt-efeitos', prefs.effectsMuted);
        };
        window.__lolIdleSetAudioPrefs = async (prefs) => {
            window.__lolIdleAudioPrefs = prefs;
            syncGameButtons(prefs);
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
                    } else if (!prefs.masterMuted && context.state === 'suspended') {
                        pausedByUs.delete(context);
                        await context.resume().catch(() => {});
                    }
                } catch (_) {}
            }
        };
        const resumeAfterGesture = () => {
            const prefs = window.__lolIdleAudioPrefs;
            if (prefs && !prefs.masterMuted) {
                for (const context of contexts) {
                    if (context.state === 'suspended') context.resume().catch(() => {});
                }
            }
        };
        document.addEventListener('pointerdown', resumeAfterGesture, true);
        document.addEventListener('keydown', resumeAfterGesture, true);
    })();";

    // Controles são desenhados dentro do DOM do jogo para ficarem acima do WebView2.
    private const string FloatingControlsScript = @"(() => {
        if (window.__lolIdleFloatingControlsInstalled) return;
        window.__lolIdleFloatingControlsInstalled = true;
        const install = () => {
            if (!document.documentElement || document.getElementById('lol-idle-floating-controls')) return;
            const style = document.createElement('style');
            style.textContent = `
                #lol-idle-floating-controls { position:fixed; top:10px; right:12px; z-index:2147483647;
                    display:flex; gap:6px; padding:0; user-select:none; }
                #lol-idle-floating-controls button { width:30px; height:30px; padding:0;
                    border:none; border-radius:50%;
                    color:#fff; background:#191c23; cursor:pointer;
                    display:grid; place-items:center; }
                #lol-idle-floating-controls button:hover { background:#2a2e38; }
                #lol-idle-floating-controls button.active { color:#e8cf8f; }
                #lol-idle-floating-controls svg { width:16px; height:16px; fill:#fff; stroke:#fff;
                    stroke-width:1.8; stroke-linecap:round; stroke-linejoin:round; pointer-events:none; }
                #lol-idle-floating-controls button.active svg { fill:#e8cf8f; stroke:#e8cf8f; }
            `;
            document.head.appendChild(style);
            const panel = document.createElement('div');
            panel.id = 'lol-idle-floating-controls';
            panel.innerHTML = `
                <button id='lol-idle-audio' title='Silenciar todo o áudio' aria-label='Som'>
                    <svg viewBox='0 0 24 24'><path d='M11 5 6 9H3v6h3l5 4z'/><path d='M15.5 8.5a5 5 0 0 1 0 7'/><path d='M18.5 5.5a9 9 0 0 1 0 13'/></svg>
                </button>
                <button id='lol-idle-pin' title='Ativar janela sempre no topo' aria-label='Fixar'>
                    <svg viewBox='0 0 24 24'><path d='m16 3 5 5-4 1-4 4 2 2-2 2-4-4-5 5'/><path d='m8 8 8 8'/></svg>
                </button>
                <button id='lol-idle-close' title='Fechar janela flutuante' aria-label='Fechar'>
                    <svg viewBox='0 0 24 24'><path d='M18 6 6 18M6 6l12 12'/></svg>
                </button>`;
            document.documentElement.appendChild(panel);
            const send = action => window.chrome.webview.postMessage('lol-idle-control:' + action);
            const audio = panel.querySelector('#lol-idle-audio');
            const pin = panel.querySelector('#lol-idle-pin');
            audio.addEventListener('click', () => {
                audio.classList.toggle('active');
                const muted = audio.classList.contains('active');
                audio.title = muted ? 'Ativar todo o áudio' : 'Silenciar todo o áudio';
                audio.innerHTML = muted
                    ? `<svg viewBox='0 0 24 24'><path d='M11 5 6 9H3v6h3l5 4z'/><path d='m16 9 5 6m0-6-5 6'/></svg>`
                    : `<svg viewBox='0 0 24 24'><path d='M11 5 6 9H3v6h3l5 4z'/><path d='M15.5 8.5a5 5 0 0 1 0 7'/><path d='M18.5 5.5a9 9 0 0 1 0 13'/></svg>`;
                send('audio');
            });
            pin.addEventListener('click', () => {
                pin.classList.toggle('active');
                const pinned = pin.classList.contains('active');
                pin.title = pinned ? 'Desativar janela sempre no topo' : 'Ativar janela sempre no topo';
                send('pin');
            });
            panel.querySelector('#lol-idle-close').addEventListener('click', () => send('close'));
        };
        if (document.readyState === 'loading')
            document.addEventListener('DOMContentLoaded', install, { once:true });
        else install();
    })();";

    // Arrastar pelo jogo, exceto quando o clique começa nos controles flutuantes.
    private const string FloatingDragScript = @"(() => {
        if (window.__lolIdleFloatingDragInstalled) return;
        window.__lolIdleFloatingDragInstalled = true;
        document.addEventListener('pointerdown', event => {
            if (event.button !== 0 || event.target.closest('#lol-idle-floating-controls')) return;
            try { window.chrome.webview.postMessage('lol-idle-begin-window-drag'); } catch (_) {}
        }, true);
    })();";

    // Observa #avisos-saque (loot real do jogo) + #log (missões) para notificar na bandeja.
    private const string TrayNotifyScript = @"(() => {
        if (window.__lolIdleTrayNotifyInstalled) return;
        window.__lolIdleTrayNotifyInstalled = true;
        const missionRe = /(miss[aã]o.*(conclu|complet)|mission.*(complete|conclu)|quest.*(complete|conclu)|conclu.*miss[aã]o|tarefa.*(conclu|complet))/i;
        const inventoryRe = /(mochila.*chei|invent[áa]rio.*(cheio|lotado|cheia)|inventory.*(full|lotado)|bolsa.*(cheia|lotada)|sem espa[cç]o|no space|not enough space|bag.*full)/i;
        const last = { legendary: 0, mission: 0, inventory: 0, level: 0 };
        let prevLevel = null;
        const send = (kind) => {
            const now = Date.now();
            if (now - last[kind] < 3000) return;
            last[kind] = now;
            try { window.chrome.webview.postMessage('lol-idle-notify:' + kind); } catch (_) {}
        };
        const checkSaqueNode = (n) => {
            let el = n;
            if (n.nodeType !== 1) return;
            // Sobem até o bloco .saque (o jogo faz prepend em #avisos-saque)
            while (el && el !== document.documentElement) {
                if (el.matches && el.matches('.saque')) break;
                el = el.parentElement;
            }
            if (!el || el === n.parentElement && !n.matches?.('.saque')) {
                // Fallback: se o nó é um filho direto de #avisos-saque, considera ele mesmo
                if (!n.parentElement?.matches?.('#avisos-saque')) return;
                el = n;
            }
            const cls = (el.className || '').toString().toLowerCase();
            const txtSaque = (el.textContent || '').toLowerCase();
            // So drops: ignora avisos do Arsenal (Nova estrela no Arsenal) que tambem usam classe lendario mas sem imagem do item
            if (txtSaque.includes('arsenal')) return;
            if ((cls.includes('lendario') || cls.includes('mitico')) && el.querySelector('img')) { send('legendary'); return; }
            if (cls.includes('alerta') || inventoryRe.test(txtSaque)) { send('inventory'); return; }
        };
        const checkText = (t) => {
            const s = (t || '').toLowerCase();
            if (!s.trim()) return;
            if (inventoryRe.test(s)) send('inventory');
            else if (missionRe.test(s)) send('mission');
        };
        const observer = new MutationObserver((mutations) => {
            for (const m of mutations) {
                for (const n of m.addedNodes || []) {
                    if (n.nodeType === 1) {
                        checkSaqueNode(n);
                        // Log de missões (canal Servidor #log) e fallback genérico
                        if (n.textContent) checkText(n.textContent);
                    } else if (n.nodeType === 3) checkText(n.nodeValue);
                }
                if (m.type === 'characterData' && m.target) {
                    checkText(m.target.textContent);
                    // Nível: observa #hud-nivel (o jogo usa som 'nivel' ao upar)
                    const pel = m.target.parentElement;
                    const hud = (m.target.closest ? m.target.closest('#hud-nivel') : null) || (pel?.closest ? pel.closest('#hud-nivel') : null) || (pel?.id === 'hud-nivel' ? pel : null);
                    if (hud) {
                        const v = parseInt((m.target.textContent || '').replace(/\D/g, ''), 10);
                        if (!isNaN(v)) {
                            if (prevLevel == null) prevLevel = v;
                            else if (v > prevLevel) { send('level'); prevLevel = v; }
                            else prevLevel = v;
                        }
                    }
                }
                // Também checa mudança direta do #hud-nivel via childList (texto substituído)
                for (const n of m.addedNodes || []) {
                    if (n.nodeType === 1 && (n.id === 'hud-nivel' || n.querySelector?.('#hud-nivel'))) {
                        const t = (n.textContent || '').replace(/\D/g, '');
                        const v = parseInt(t, 10);
                        if (!isNaN(v)) { if (prevLevel == null) prevLevel = v; else if (v > prevLevel) { send('level'); prevLevel = v; } else prevLevel = v; }
                    }
                }
            }
        });
        const start = () => {
            if (!document.body) { setTimeout(start, 500); return; }
            observer.observe(document.body, { childList: true, subtree: true, characterData: true });
        };
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
        else start();
    })();";

    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _trayTrayMuteItem;
    private bool _allowExit;
    private bool _isHiddenToTray;
    private bool _settingsReady;
    private System.Windows.Threading.DispatcherTimer? _settingsSaveTimer;
    private System.Drawing.Icon? _appIcon;
    private CoreWebView2Environment? _webViewEnvironment;
    private string _lastFloatingUri = GameUrl;
    private Window? _floatingWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _floatingView;
    private Window? _preparedFloatingWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _preparedFloatingView;
    private bool _isPreparingFloatingView;
    private Window? _floatingSettingsWindow;
    private WindowSettings _settings = new();
    private string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LoLIdleDesktop", "window-settings.json");

    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

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
        public bool MusicMuted { get; set; }
        public bool EffectsMuted { get; set; }
        public bool MasterMuted { get; set; }
        public bool TrayMuted { get; set; } = true;
        public bool TrayNotify { get; set; } = true;
        public bool TrayBalloon { get; set; } = true;
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
        SyncTrayMenuChecks();
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

    // Configuração do ícone e menu da bandeja do Windows (estilo Discord).
    private void InitializeTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add(
            new Forms.ToolStripMenuItem(
                "Restaurar",
                null,
                (_, _) => RestoreFromTray()));

        var soundMenu = new Forms.ToolStripMenuItem("Som");

        var trayMuteItem = new Forms.ToolStripMenuItem("Som do jogo", null, (_, _) =>
        {
            _settings.TrayMuted = !_settings.TrayMuted;
            SaveWindowSettings();
            _ = ApplyAudioSettingsAsync();
            SyncTrayMenuChecks();
        });
        trayMuteItem.CheckOnClick = true;
        soundMenu.DropDownItems.Add(trayMuteItem);
        _trayTrayMuteItem = trayMuteItem;

        menu.Items.Add(soundMenu);

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

    private bool IsFloatingVisible() => _floatingWindow != null && _floatingWindow.IsVisible;

    private void SyncTrayMenuChecks()
    {
        if (_trayTrayMuteItem != null) _trayTrayMuteItem.Checked = !_settings.TrayMuted;
    }

    private void HideToTray()
    {
        SaveWindowSettings();
        _isHiddenToTray = true;

        if (_trayIcon != null)
            _trayIcon.Visible = true;

        Hide();
        _ = ApplyAudioSettingsAsync();
    }

    private void RestoreFromTray()
    {
        _isHiddenToTray = false;
        Show();

        if (_trayIcon != null)
            _trayIcon.Visible = false;

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        _ = ApplyAudioSettingsAsync();
    }

    private void ExitApplication()
    {
        SaveWindowSettings();
        _allowExit = true;

        _floatingSettingsWindow?.Close();

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

        try
        {
            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoLIdleDesktop", "WebView2");

            Directory.CreateDirectory(userData);

            var envOptions = new CoreWebView2EnvironmentOptions();
            envOptions.AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required";
            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userData, options: envOptions);

            await GameView.EnsureCoreWebView2Async(_webViewEnvironment);
            await GameView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(AudioBootstrapScript);
            await GameView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(TrayNotifyScript);
            GameView.CoreWebView2.WebMessageReceived += (_, args) =>
            {
                try
                {
                    var msg = args.TryGetWebMessageAsString();
                    if (msg != null && msg.StartsWith("lol-idle-notify:"))
                        PlayTrayNotification(msg["lol-idle-notify:".Length..]);
                }
                catch { }
            };

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
            SaveWindowSettings();
            _isHiddenToTray = true;
            Hide();
            _ = ApplyAudioSettingsAsync();
            LogFloatingWindowEvent("Popup: janela exibida. Janela principal fechada (sem bandeja).");

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

    private static System.Windows.Shapes.Path CreateToolbarIcon(string name, System.Windows.Media.Brush color)
    {
        var data = name switch
        {
            "volume" => "M3,9 L7,9 L12,5 L12,19 L7,15 L3,15 Z M15,9 C17.5,11 17.5,13 15,15 M17.5,6.5 C21.5,10 21.5,14 17.5,17.5",
            "mute" => "M3,9 L7,9 L12,5 L12,19 L7,15 L3,15 Z M16,9 L21,15 M21,9 L16,15",
            "pin" => "M9,3 L15,3 L14,8 L17,11 L17,13 L13,13 L12,21 L10,13 L6,13 L6,11 L9,8 Z",
            "pin-filled" => "M9,3 L15,3 L14,8 L17,11 L17,13 L13,13 L12,21 L10,13 L6,13 L6,11 L9,8 Z",
            _ => "M6,6 L18,18 M18,6 L6,18"
        };

        return new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Fill = name == "pin-filled" ? color : Brushes.Transparent,
            Stroke = color,
            StrokeThickness = 1.7,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 17,
            Height = 17,
            Stretch = System.Windows.Media.Stretch.Uniform,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };
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

            var view = new Microsoft.Web.WebView2.Wpf.WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(17, 19, 24)
            };
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
                        {
                            // Ao fechar a janela flutuante, traz o app de volta
                            // da bandeja para a janela principal.
                            RestoreFromTray();
                            LogFloatingWindowEvent("FloatingWindow: janela principal restaurada após fechar o modo flutuante.");
                            _ = PrepareFloatingWebViewAsync(this);
                        }
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
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(TrayNotifyScript);
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(FloatingControlsScript);
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(FloatingDragScript);
            view.CoreWebView2.WebMessageReceived += (_, messageArgs) =>
            {
                try
                {
                    var message = messageArgs.TryGetWebMessageAsString();
                    if (message == "lol-idle-begin-window-drag" &&
                        popupWindow.IsVisible && popupWindow.WindowState == WindowState.Normal)
                    {
                        var handle = new WindowInteropHelper(popupWindow).Handle;
                        ReleaseCapture();
                        SendMessage(handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
                    }
                    else if (message == "lol-idle-control:audio")
                    {
                        _settings.MasterMuted = !_settings.MasterMuted;
                        SaveWindowSettings();
                        _ = ApplyAudioSettingsAsync();
                    }
                    else if (message == "lol-idle-control:pin")
                    {
                        popupWindow.Topmost = !popupWindow.Topmost;
                    }
                    else if (message == "lol-idle-control:close")
                    {
                        popupWindow.Close();
                    }
                    else if (message != null && message.StartsWith("lol-idle-notify:"))
                    {
                        PlayTrayNotification(message["lol-idle-notify:".Length..]);
                    }
                }
                catch (InvalidOperationException)
                {
                    // O ponteiro pode soltar antes de o Windows iniciar o movimento.
                }
            };
            LogFloatingWindowEvent("Prepare: EnsureCoreWebView2Async concluído, áudio e arraste instalados.");

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
            Text = "BANDEJA",
            Foreground = new SolidColorBrush(Color.FromRgb(232, 207, 143)),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Margin = new Thickness(0, 14, 0, 10)
        });

        AddAudioSetting(stack, "Mutar jogo na bandeja", _settings.TrayMuted, value =>
        {
            _settings.TrayMuted = value;
            SaveWindowSettings();
            _ = ApplyAudioSettingsAsync();
        });
        AddAudioSetting(stack, "Som de notificação na bandeja", _settings.TrayNotify, value =>
        {
            _settings.TrayNotify = value;
            SaveWindowSettings();
        });
        AddAudioSetting(stack, "Balão de notificação na bandeja", _settings.TrayBalloon, value =>
        {
            _settings.TrayBalloon = value;
            SaveWindowSettings();
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Na bandeja, o jogo fica mutado e só toca um som (com balão opcional) quando: item lendário, missão concluída, inventário cheio ou up de nível.",
            Foreground = new SolidColorBrush(Color.FromRgb(145, 153, 168)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
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

    private void PlayTrayNotification(string kind)
    {
        if (!_settings.TrayNotify && !_settings.TrayBalloon)
            return;

        var (title, message, sound) = kind switch
        {
            "legendary" => ("LoL Idle", "Item lendário obtido!", "Assets/notify_legendary.wav"),
            "mission" => ("LoL Idle", "Missão concluída!", "Assets/notify_mission.wav"),
            "inventory" => ("LoL Idle", "Inventário cheio!", "Assets/notify_mission.wav"),
            "level" => ("LoL Idle", "Subiu de nível!", "Assets/notify_level.wav"),
            _ => ("LoL Idle", "Evento importante no jogo!", "Assets/notify_mission.wav")
        };

        if (_settings.TrayNotify)
        {
            try
            {
                var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,," + "/" + sound));
                if (resource != null)
                {
                    // Playback síncrono via arquivo temporário (SoundPlayer.Play() assíncrono não audível quando oculto)
                    var tmp = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(sound) + "_" + Environment.ProcessId + ".wav");
                    using (var fs = File.Create(tmp))
                    using (var rs = resource.Stream)
                        rs.CopyTo(fs);
                    using (var player = new System.Media.SoundPlayer(tmp))
                    {
                        player.Load();
                        player.PlaySync();
                    }
                    try { File.Delete(tmp); } catch { }
                    LogFloatingWindowEvent($"TrayNotify: som tocado ({kind}) com {sound}");
                }
                else System.Media.SystemSounds.Asterisk.Play();
            }
            catch (Exception ex)
            {
                LogFloatingWindowEvent($"TrayNotify: falha no som ({kind}): {ex.Message}");
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            }
        }

        if (_settings.TrayBalloon && _trayIcon != null)
        {
            try { _trayIcon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Info); } catch { }
        }

        if (!_isHiddenToTray)
            return;
        LogFloatingWindowEvent($"TrayNotify: {kind}");
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

    private async Task ApplyAudioSettingsAsync()
    {
        // Só muta de fato na bandeja (não na janela flutuante)
        bool trulyHidden = _isHiddenToTray && !IsFloatingVisible();
        bool effectiveMasterMuted = _settings.MasterMuted || (trulyHidden && _settings.TrayMuted);
        var preferences = JsonSerializer.Serialize(new
        {
            musicMuted = _settings.MusicMuted,
            effectsMuted = _settings.EffectsMuted,
            masterMuted = effectiveMasterMuted
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
                    attributeFilter: ['src', 'id', 'class', 'data-name', 'aria-label', 'aria-pressed']
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
                SaveWindowSettings();
                _isHiddenToTray = true;
                Hide();
                _ = ApplyAudioSettingsAsync();
                LogFloatingWindowEvent("ReopenButton: janela flutuante reaberta. Janela principal fechada (sem bandeja).");
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
            SaveWindowSettings();
            _isHiddenToTray = true;
            Hide();
            _ = ApplyAudioSettingsAsync();
            LogFloatingWindowEvent("ReopenButton: janela flutuante aberta. Janela principal fechada (sem bandeja).");

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
