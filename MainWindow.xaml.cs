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
                #lol-idle-floating-controls svg { width:16px; height:16px; fill:none; stroke:currentColor;
                    stroke-width:1.8; stroke-linecap:round; stroke-linejoin:round; pointer-events:none; }
                #lol-idle-settings svg path { fill:#fff; stroke:none; }
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
                <button id='lol-idle-missions' title='Mostrar missões' aria-label='Missões'>
                    <svg viewBox='0 0 24 24'><path d='M4 4h12a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2z'/><path d='M4 16a2 2 0 0 1 2-2h12'/><path d='M8 8h6M8 12h6'/></svg>
                </button>

                <button id='lol-idle-close' title='Fechar janela flutuante' aria-label='Fechar'>
                    <svg viewBox='0 0 24 24'><path d='M18 6 6 18M6 6l12 12'/></svg>
                </button>`;
            if (window.__lolIdleMissionsActive) panel.querySelector('#lol-idle-missions')?.classList.add('active');
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
            panel.querySelector('#lol-idle-missions').addEventListener('click', () => send('missions'));
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
        const last = { legendary: 0, epic: 0, mission: 0, inventory: 0, level: 0 };
        let prevLevel = null;
        const send = (kind) => {
            const now = Date.now();
            if (now - last[kind] < 3000) return;
            last[kind] = now;
            try { window.chrome.webview.postMessage('lol-idle-notify:' + kind); } catch (_) {}
        };
        const checkSaqueNode = (n) => {
            const isSaque = (el) => el && el.matches && el.matches('.saque');
            let el = n;
            if (n.nodeType !== 1) return;
            if (!isSaque(el)) {
                // sobe até .saque (added node pode ser filho interno do aviso)
                while (el && el !== document.documentElement) {
                    if (isSaque(el)) break;
                    el = el.parentElement;
                }
                if (!isSaque(el)) {
                    if (n.parentElement?.matches?.('#avisos-saque')) el = n;
                    else return;
                }
            }
            const cls = (el.className || '').toString().toLowerCase();
            const txtSaque = (el.textContent || '').toLowerCase();
            // So drops: ignora avisos do Arsenal (Nova estrela no Arsenal) que tambem usam classe lendario mas sem imagem do item
            if (txtSaque.includes('arsenal')) return;
            if ((cls.includes('lendario') || cls.includes('mitico')) && el.querySelector('img')) { send('legendary'); return; }
            if (cls.includes('epico') && el.querySelector('img')) { send('epic'); return; }
            if (cls.includes('alerta') || inventoryRe.test(txtSaque)) { send('inventory'); return; }
        };
        const checkText = (t) => {
            const s = (t || '').toLowerCase();
            if (!s.trim()) return;
            if (inventoryRe.test(s)) { send('inventory'); return; }
            if (missionRe.test(s)) send('mission');
        };
        let lastBolsa = '';
        const checkBolsa = () => {
            try {
                const el = document.getElementById('hud-bolsa-n');
                const qtd = document.getElementById('mochila-qtd')?.textContent?.trim() || '';
                const cur = (el?.textContent || '') + '|' + qtd + '|' + (document.getElementById('bolsa-qtd')?.textContent || '');
                if (cur === lastBolsa) return;
                lastBolsa = cur;
                const m = cur.match(/(\d+)\s*\/\s*(\d+)/);
                if (m && parseInt(m[1], 10) >= parseInt(m[2], 10) && parseInt(m[2], 10) > 0) { send('inventory'); return; }
                if (/chei|lotad/i.test(cur)) send('inventory');
            } catch (_) {}
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
                checkBolsa();
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

    private const string HeroBarScript = @"(() => {
        if (window.__lolIdleHeroBarInstalled) return;
        window.__lolIdleHeroBarInstalled = true;
        const send = () => {
            try {
                const g = (id) => document.getElementById(id)?.textContent?.trim() || '';
                const w = (id) => {
                    const el = document.getElementById(id);
                    const s = el?.style?.width || '';
                    const m = s.match(/[\d.]+%/) || s.match(/[\d.]+/);
                    return m ? parseFloat(m[0]) || 0 : 0;
                };
                const cnt = (id) => document.getElementById(id)?.children?.length ?? 0;
                const data = {
                    nivel: g('hud-nivel'),
                    vida: g('hud-vida-txt'), vidaP: Math.round(w('hud-vida')),
                    mana: g('hud-mana-txt'), manaP: Math.round(w('hud-mana')),
                    xp: g('hud-xpb-txt'), xpP: Math.round(w('hud-xpb-barra')),
                    habs: g('hud-habs').slice(0, 20), habsN: cnt('hud-habs'),
                    buffs: cnt('hud-buffs'), bolsa: g('hud-bolsa-n'),
                    pocoes: g('hud-pocoes').slice(0, 20), pocoesN: cnt('hud-pocoes')
                };
                window.chrome.webview.postMessage('lol-idle-hero:' + JSON.stringify(data));
            } catch (_) {}
        };
        const observer = new MutationObserver(() => send());
        const start = () => {
            if (!document.body) { setTimeout(start, 500); return; }
            observer.observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ['style'] });
            send();
            setInterval(send, 500);
        };
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
        else start();
    })();";

    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _trayTrayMuteItem;
    private Forms.ToolStripMenuItem? _trayHeroItem;
    private Window? _heroWindow;
    private bool _floatingOpened;
    private TextBlock? _heroLevel;
    private System.Windows.Controls.ProgressBar? _heroVida;
    private System.Windows.Controls.ProgressBar? _heroMana;
    private System.Windows.Controls.ProgressBar? _heroXp;
    private TextBlock? _heroVidaTxt;
    private TextBlock? _heroManaTxt;
    private TextBlock? _heroXpTxt;
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
    private const string VoteButtonScript = @"(() => {
        if (window.__lolIdleVoteButtonInstalled) return;
        window.__lolIdleVoteButtonInstalled = true;
        const bind = () => {
            document.querySelectorAll('button, a, [role=button]').forEach(el => {
                const txt = (el.textContent || '').toLowerCase();
                const title = (el.getAttribute('title') || el.getAttribute('aria-label') || '').toLowerCase();
                if (txt.includes('voto') || txt.includes('bônus do voto') || txt.includes('bonus do voto') || title.includes('voto') || title.includes('topidle')) {
                    el.style.cursor = 'pointer';
                    el.addEventListener('click', (e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        window.chrome.webview.postMessage('lol-idle-vote-button');
                    }, true);
                }
            });
        };
        bind();
        new MutationObserver(bind).observe(document.documentElement, { childList: true, subtree: true });
    })();";

    // Janela separada de voto (TopIdle) - independente da janela flutuante.
    private Window? _voteWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _voteView;
    private Window? _preparedVoteWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _preparedVoteView;
    private bool _isPreparingVoteView;
    private const string MissionsScript = @"(() => {
        if (window.__lolIdleMissionsInstalled) return;
        window.__lolIdleMissionsInstalled = true;
        const g = (id) => document.getElementById(id)?.textContent?.trim() || '';
        const send = () => {
            try {
                const gt = (id) => {
                    const el = document.getElementById(id);
                    const t = el?.textContent?.trim();
                    return t ? t : (el?.getAttribute('title') || '').trim();
                };
                const analiseRaw = gt('rastreio-analise') || g('analise-corpo') || (document.getElementById('janela-analise')?.textContent?.trim() || '');
                const data = {
                    missoes: g('rastreio-n'), eventos: g('rastreio-ev-n'),
                    passe: gt('rastreio-passe'), tarefa: gt('rastreio-tarefa'),
                    analise: analiseRaw, diaria: gt('rastreio-diaria')
                };
                const hasAny = Object.values(data).some(v => v && v.trim());
                if (!hasAny) return;
                window.chrome.webview.postMessage('lol-idle-missions:' + JSON.stringify(data));
            } catch (_) {}
        };
        const observer = new MutationObserver(() => send());
        const start = () => {
            if (!document.body) { setTimeout(start, 500); return; }
            const rast = document.getElementById('rastreador') || document.body;
            observer.observe(rast, { childList: true, subtree: true, characterData: true });
            send(); setInterval(send, 5000);
        };
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
        else start();
    })();";

    private Window? _missionsWindow;
    private StackPanel? _missionsRoot;
    private TextBlock? _missionsTitle;
    private TextBlock? _missionsBody;
    private System.Windows.Controls.ProgressBar? _missionsPasseBar;
    private System.Windows.Controls.ProgressBar? _missionsTarefaBar;
    private TextBlock? _missionsPasseTxt;
    private TextBlock? _missionsTarefaTxt;

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
        public bool ShowHeroBar { get; set; }
        public bool ShowMissions { get; set; }
        public bool HasMissionsBounds { get; set; }
        public double MissionsWidth { get; set; } = 360;
        public double MissionsHeight { get; set; } = 320;
        public double MissionsLeft { get; set; } = 200;
        public double MissionsTop { get; set; } = 200;
        public double MissionsOpacity { get; set; } = 1.0;
        public bool IsMaximized { get; set; }
        public bool RememberWindowState { get; set; } = true;
        public bool HeroTopmost { get; set; }
        public bool HasHeroBounds { get; set; }
        public double HeroLeft { get; set; } = 100;
        public double HeroTop { get; set; } = 100;
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

    private void Window_SourceInitialized(object? sender, EventArgs e)
    { }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

            if (settings.RememberWindowState && settings.IsMaximized)
            {
                _restoreLeft = Left; _restoreTop = Top;
                _restoreWidth = Width; _restoreHeight = Height;
                var work = SystemParameters.WorkArea;
                Left = work.Left; Top = work.Top; Width = work.Width; Height = work.Height;
                ResizeMode = ResizeMode.NoResize;
                _isCustomMaximized = true;
            }
        }

        _settingsReady = true;
        UpdateMaximizeButton();
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

            if (_isCustomMaximized)
            {
                _settings.IsMaximized = true;
                // mantém _normalWidth/_normalHeight como tamanho restaurável e Left/Top salvos
            }
            else if (WindowState != WindowState.Minimized)
            {
                _settings.IsMaximized = false;
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

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized && !_isCustomMaximized)
        {
            // Converte o maximizar nativo (fullscreen) para o nosso (área útil)
            var rb = RestoreBounds;
            if (rb.Width > 0) { _normalWidth = rb.Width; _normalHeight = rb.Height; }
            var work = SystemParameters.WorkArea;
            WindowState = WindowState.Normal;
            _isCustomMaximized = true;
            _settings.IsMaximized = true;
            Left = work.Left; Top = work.Top; Width = work.Width; Height = work.Height;
        }
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

    private Microsoft.Web.WebView2.Wpf.WebView2? _heroView;

    private void ShowHeroBar()
    {
        if (_heroWindow != null)
        {
            _heroWindow.Show();
            _heroWindow.Activate();
            if (_trayHeroItem != null) _trayHeroItem.Checked = true;
            return;
        }
        var win = new Window
        {
            Title = "LoL Idle - Personagem",
            Width = 900,
            Height = 110,
            MinWidth = 700,
            MinHeight = 110,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.CanResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = _settings.HeroTopmost,
            Background = new SolidColorBrush(Color.FromRgb(14, 16, 22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(48, 52, 61)),
            BorderThickness = new Thickness(1)
        };
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = _settings.HasHeroBounds ? _settings.HeroLeft : 100;
        win.Top = _settings.HasHeroBounds ? _settings.HeroTop : 100;
        var root = new DockPanel { Margin = new Thickness(8) };
        win.Content = root;
        // Coluna esquerda: retrato + nível (estilo HUD do jogo)
        var left = new StackPanel { Width = 78, VerticalAlignment = VerticalAlignment.Center };
        var emblem = new Border { Width = 62, Height = 62, CornerRadius = new CornerRadius(30), Background = Brushes.DarkSlateGray, BorderBrush = new SolidColorBrush(Color.FromRgb(200, 168, 112)), BorderThickness = new Thickness(2), HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        _heroLevel = new TextBlock { Text = "25", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        emblem.Child = _heroLevel;
        var nvTxt = new TextBlock { Text = "Nível", Foreground = Brushes.White, FontSize = 10, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        left.Children.Add(emblem); left.Children.Add(nvTxt);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);
        // Centro: barras vida/mana/XP
        var bars = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        _heroVida = new System.Windows.Controls.ProgressBar { Height = 18, Margin = new Thickness(0, 2, 0, 2), Foreground = new SolidColorBrush(Color.FromRgb(76, 210, 60)), Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)) };
        _heroMana = new System.Windows.Controls.ProgressBar { Height = 18, Margin = new Thickness(0, 2, 0, 2), Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)) };
        _heroXp = new System.Windows.Controls.ProgressBar { Height = 14, Margin = new Thickness(0, 2, 0, 2), Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)), Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)) };
        _heroVidaTxt = new TextBlock { Text = "-", Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) };
        _heroManaTxt = new TextBlock { Text = "-", Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) };
        _heroXpTxt = new TextBlock { Text = "-", Foreground = Brushes.White, FontSize = 10 };
        var topRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 4) };
        var pinBtn = new Button { Content = "📌", Width = 20, Height = 20, Margin = new Thickness(0, 0, 4, 0) };
        pinBtn.ToolTip = "Manter na frente (fixar)";
        if (_settings.HeroTopmost) pinBtn.Foreground = new SolidColorBrush(Color.FromRgb(232, 207, 143));
        pinBtn.Click += (_, _) => { win.Topmost = !win.Topmost; _settings.HeroTopmost = win.Topmost; SaveWindowSettings(); pinBtn.Foreground = win.Topmost ? new SolidColorBrush(Color.FromRgb(232, 207, 143)) : Brushes.White; };
        topRow.Children.Add(pinBtn);
        var closeBtn = new Button { Content = "×", Width = 20, Height = 20 };
        closeBtn.Click += (_, _) => { win.Hide(); _settings.ShowHeroBar = false; SaveWindowSettings(); if (_trayHeroItem != null) _trayHeroItem.Checked = false; };
        topRow.Children.Add(closeBtn);
        DockPanel.SetDock(topRow, Dock.Top);
        root.Children.Add(topRow);
        bars.Children.Add(_heroVida); bars.Children.Add(_heroVidaTxt);
        bars.Children.Add(_heroMana); bars.Children.Add(_heroManaTxt);
        bars.Children.Add(_heroXp); bars.Children.Add(_heroXpTxt);
        root.Children.Add(bars);
        root.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler((_, _) => { if (Mouse.PrimaryDevice.DirectlyOver is Border || Mouse.PrimaryDevice.DirectlyOver is StackPanel) win.DragMove(); }));
        win.Content = root;
        win.LocationChanged += (_, _) =>
        {
            _settings.HeroLeft = win.Left;
            _settings.HeroTop = win.Top;
            _settings.HasHeroBounds = true;
            SaveWindowSettings();
        };
        win.Closing += (_, ev) => { ev.Cancel = true; win.Hide(); _settings.ShowHeroBar = false; SaveWindowSettings(); if (_trayHeroItem != null) _trayHeroItem.Checked = false; };
        _heroWindow = win;
        win.Show();
        if (_trayHeroItem != null) _trayHeroItem.Checked = true;
    }

    private void UpdateHeroBar(string json)
    {
        if (_heroWindow == null && !_floatingOpened) return;
        if (_heroWindow == null) ShowHeroBar();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (_heroLevel != null) _heroLevel.Text = (r.TryGetProperty("nivel", out var n) ? n.GetString() : "—");
            if (_heroVida != null && r.TryGetProperty("vidaP", out var vp)) _heroVida.Value = vp.GetDouble();
            if (_heroMana != null && r.TryGetProperty("manaP", out var mp)) _heroMana.Value = mp.GetDouble();
            if (_heroXp != null && r.TryGetProperty("xpP", out var xp)) _heroXp.Value = xp.GetDouble();
            if (_heroVidaTxt != null && r.TryGetProperty("vida", out var vt)) _heroVidaTxt.Text = vt.GetString();
            if (_heroManaTxt != null && r.TryGetProperty("mana", out var mt)) _heroManaTxt.Text = mt.GetString();
            if (_heroXpTxt != null && r.TryGetProperty("xp", out var xt)) _heroXpTxt.Text = xt.GetString();
        }
        catch { }
    }

    private void ShowMissions()
    {
        if (_missionsWindow != null)
        {
            _missionsWindow.Show();
            _missionsWindow.Activate();
            return;
        }
        var win = new Window
        {
            Title = "LoL Idle - Missões",
            Width = _settings.MissionsWidth >= 300 ? _settings.MissionsWidth : 360,
            Height = _settings.MissionsHeight >= 200 ? _settings.MissionsHeight : 320,
            MinWidth = 300,
            MinHeight = 200,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.CanResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = false,
            Background = new SolidColorBrush(Color.FromRgb(25, 28, 35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(242, 192, 58)),
            BorderThickness = new Thickness(1)
        };
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = _settings.HasMissionsBounds ? _settings.MissionsLeft : 200;
        win.Top = _settings.HasMissionsBounds ? _settings.MissionsTop : 200;
        win.LocationChanged += (_, _) => { _settings.MissionsLeft = win.Left; _settings.MissionsTop = win.Top; _settings.HasMissionsBounds = true; SaveWindowSettings(); };
        win.SizeChanged += (_, _) => { _settings.MissionsWidth = win.Width; _settings.MissionsHeight = win.Height; _settings.HasMissionsBounds = true; SaveWindowSettings(); };
        var root = new StackPanel { Margin = new Thickness(10), Background = new SolidColorBrush(Color.FromRgb(25, 28, 35)) };
        _missionsRoot = root;
        var top = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 6) };
        var pinBtn = new Button { Content = "📌", Width = 20, Height = 20, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        pinBtn.Click += (_, _) =>
        {
            win.Topmost = !win.Topmost;
            pinBtn.Foreground = win.Topmost ? new SolidColorBrush(Color.FromRgb(232, 207, 143)) : Brushes.White;
        };
        win.MouseEnter += (_, _) => { if (win.Topmost) pinBtn.Foreground = new SolidColorBrush(Color.FromRgb(232, 207, 143)); };
        top.Children.Add(pinBtn);
        var closeBtn = new Button { Content = "×", Width = 20, Height = 20, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        closeBtn.Click += (_, _) => { win.Hide(); _settings.ShowMissions = false; SaveWindowSettings(); };
        top.Children.Add(closeBtn);
        root.Children.Add(top);
        _missionsTitle = new TextBlock { Text = "Missões", Foreground = new SolidColorBrush(Color.FromRgb(232, 207, 143)), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        root.Children.Add(_missionsTitle);
        _missionsPasseTxt = new TextBlock { Text = "Passe", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) };
        _missionsPasseBar = new System.Windows.Controls.ProgressBar { Height = 14, Margin = new Thickness(0, 0, 0, 10), Foreground = new LinearGradientBrush { GradientStops = new GradientStopCollection { new GradientStop(Color.FromRgb(180, 132, 26), 0), new GradientStop(Color.FromRgb(242, 192, 58), 1) }, StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) }, Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)) };
        _missionsTarefaTxt = new TextBlock { Text = "Tarefa", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) };
        _missionsTarefaBar = new System.Windows.Controls.ProgressBar { Height = 14, Margin = new Thickness(0, 0, 0, 10), Foreground = new LinearGradientBrush { GradientStops = new GradientStopCollection { new GradientStop(Color.FromRgb(30, 100, 180), 0), new GradientStop(Color.FromRgb(52, 152, 219), 1) }, StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) }, Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)) };
        root.Children.Add(_missionsPasseTxt); root.Children.Add(_missionsPasseBar);
        root.Children.Add(_missionsTarefaTxt); root.Children.Add(_missionsTarefaBar);
        _missionsBody = new TextBlock { Text = "Carregando missões...", Foreground = Brushes.White, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        root.Children.Add(_missionsBody);
        root.MouseLeftButtonDown += (_, _) => win.DragMove();
        var layout = new DockPanel();
        var grip = new Border { Width = 18, Height = 18, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = System.Windows.VerticalAlignment.Bottom, Background = Brushes.Transparent, Cursor = Cursors.SizeNWSE };
        grip.Child = new TextBlock { Text = "◢", Foreground = Brushes.DimGray, FontSize = 10, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(grip, Dock.Bottom);
        layout.Children.Add(grip);
        layout.Children.Add(root);
        bool resizing = false;
        System.Windows.Point resizeStart = new System.Windows.Point();
        double startW = 0, startH = 0;
        grip.MouseLeftButtonDown += (_, e) =>
        {
            resizing = true;
            resizeStart = e.GetPosition(win);
            startW = win.Width; startH = win.Height;
            grip.CaptureMouse();
            e.Handled = true;
        };
        win.MouseMove += (_, e) =>
        {
            if (!resizing) return;
            var p = e.GetPosition(win);
            win.Width = Math.Max(win.MinWidth, startW + (p.X - resizeStart.X));
        };
        win.MouseLeftButtonUp += (_, _) => { resizing = false; try { grip.ReleaseMouseCapture(); } catch { } };
        win.Content = layout;
        win.Closing += (_, e) => { e.Cancel = true; win.Hide(); _settings.ShowMissions = false; SaveWindowSettings(); };
        win.GotFocus += (_, _) => win.BorderThickness = new Thickness(1);
        win.LostFocus += (_, _) => win.BorderThickness = new Thickness(0);
        win.Opacity = 1.0;
        win.MouseEnter += (_, _) => { root.Background = new SolidColorBrush(Color.FromRgb(25, 28, 35)); };
        win.MouseLeave += (_, _) => { root.Background = new SolidColorBrush(Color.FromRgb(25, 28, 35)); };
        _missionsWindow = win;
        win.Show();
        var pollTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        win.Closing += (_, _) => pollTimer.Stop();
        pollTimer.Tick += async (_, _) =>
        {
            if (_missionsWindow == null || !_missionsWindow.IsVisible) return;
            try
            {
                var expr = @"(() => { const g = (id) => document.getElementById(id)?.textContent?.trim() || ''; const gt = (id) => { const el = document.getElementById(id); const t = el?.textContent?.trim(); return t ? t : (el?.getAttribute('title') || '').trim(); }; return JSON.stringify({ missoes: g('rastreio-n'), eventos: g('rastreio-ev-n'), passe: gt('rastreio-passe'), tarefa: gt('rastreio-tarefa'), analise: gt('rastreio-analise'), diaria: gt('rastreio-diaria') }); })()";
                string best = "";
                foreach (var core in new[] { _floatingView?.CoreWebView2, GameView?.CoreWebView2 })
                {
                    if (core == null) continue;
                    var raw = await core.ExecuteScriptAsync(expr);
                    if (string.IsNullOrEmpty(raw)) continue;
                    var json = System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? "";
                    if (json.Length > best.Length) best = json;
                }
                if (string.IsNullOrEmpty(best) || best == "{}") return;
                try
                {
                    using var d2 = System.Text.Json.JsonDocument.Parse(best);
                    var hasAny = d2.RootElement.EnumerateObject().Any(p => !string.IsNullOrWhiteSpace(p.Value.GetString()));
                    if (!hasAny) return;
                }
                catch { }
                if (!string.IsNullOrEmpty(best)) UpdateMissions(best, fromFloating: true);
            }
            catch { }
        };
        pollTimer.Start();
    }

    private static void AddBoldLine(TextBlock tb, string text, string[] bolds)
    {
        var remaining = text;
        while (remaining.Length > 0)
        {
            var next = -1; var nextKey = "";
            foreach (var k in bolds)
            {
                var i = remaining.IndexOf(k, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && (next < 0 || i < next)) { next = i; nextKey = k; }
            }
            if (next < 0) { tb.Inlines.Add(new System.Windows.Documents.Run(remaining)); break; }
            if (next > 0) tb.Inlines.Add(new System.Windows.Documents.Run(remaining.Substring(0, next)));
            tb.Inlines.Add(new System.Windows.Documents.Run(nextKey) { FontWeight = FontWeights.Bold });
            remaining = remaining.Substring(next + nextKey.Length);
        }
    }

    private static void SetProgress(System.Windows.Controls.ProgressBar? bar, string text)
    {
        if (bar == null || string.IsNullOrEmpty(text)) return;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*/\s*(\d+)");
        if (!m.Success) return;
        if (double.TryParse(m.Groups[1].Value, out var a) && double.TryParse(m.Groups[2].Value, out var b) && b > 0)
            bar.Value = Math.Clamp(a / b * 100.0, 0, 100);
    }

    private void UpdateMissions(string json, bool fromFloating = false)
    {
        // Evita sobrescrita pela sessão principal quando a flutuante está ativa
        if (!fromFloating && IsFloatingVisible()) return;
        if (!_settings.ShowMissions) return;
        if (_missionsWindow == null && !_floatingOpened) return;
        if (_missionsWindow == null) ShowMissions();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var r = doc.RootElement;
            string Get(string k) => r.TryGetProperty(k, out var v) ? (v.GetString() ?? "") : "";
            var passe = Get("passe"); var tarefa = Get("tarefa");
            var analise = Get("analise"); var diaria = Get("diaria");
            if (_missionsBody != null)
            {
                _missionsBody.Inlines.Clear();
                AddBoldLine(_missionsBody, analise, new[] { "Caçada", "XP/h", "ouro/h" });
                _missionsBody.Inlines.Add(new System.Windows.Documents.LineBreak());
                AddBoldLine(_missionsBody, diaria, new[] { "Missões de hoje:", "Tarefa:" });
            }
            if (_missionsTitle != null) _missionsTitle.Text = "Missões " + Get("missoes") + "  •  Eventos " + Get("eventos");
            if (_missionsPasseTxt != null)
            {
                _missionsPasseTxt.Inlines.Clear();
                AddBoldLine(_missionsPasseTxt, passe, new[] { "Missões de hoje:", "Passe" });
            }
            if (_missionsTarefaTxt != null)
            {
                _missionsTarefaTxt.Inlines.Clear();
                AddBoldLine(_missionsTarefaTxt, tarefa, new[] { "Tarefa:" });
            }
            SetProgress(_missionsPasseBar, passe); SetProgress(_missionsTarefaBar, tarefa);
        }
        catch { }
    }

    private void ExitApplication()
    {
        SaveWindowSettings();
        _allowExit = true;
        try { _heroWindow?.Close(); } catch { }
        _heroWindow = null;
        try { _missionsWindow?.Close(); } catch { }
        _missionsWindow = null;

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
        if (_voteWindow != null)
        {
            var vote = _voteWindow;
            _voteWindow = null;
            _voteView = null;
            vote.Close();
        }
        if (_preparedVoteWindow != null)
        {
            var preparedVote = _preparedVoteWindow;
            _preparedVoteWindow = null;
            _preparedVoteView = null;
            preparedVote.Close();
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
            _ = PrepareVoteWebViewAsync(this);
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

            LogFloatingWindowEvent($"NewWindowRequested: blank={isBlankWindow}, http={hasHttpUri}, host={(hasHttpUri ? requestedUri!.Host : "")}, userInitiated={args.IsUserInitiated}");

            if (!hasHttpUri && !isBlankWindow)
            {
                args.Handled = true;
                return;
            }

            if (hasHttpUri)
                _lastFloatingUri = requestedUri!.ToString();

            // Popup de voto (TopIdle/OAuth): abre no navegador real,
            // porque Google/Discord bloqueiam login em WebView2.
            // Blank (about:blank) e os demais popups continuam na janela
            // flutuante, como antes.
            string host = hasHttpUri ? requestedUri!.Host.ToLowerInvariant() : "";
            string urlLower = hasHttpUri ? requestedUri!.ToString().ToLowerInvariant() : "";
            bool isOAuthHost = host.Contains("accounts.google.com") || host.Contains("discord.com") || host.Contains("discordapp") || host.Contains("discord.gg") || host.Contains("kick.com") || host.Contains("twitch.tv");
            bool isVoteHost = host.Contains("topidle");
            bool isExternalHost = hasHttpUri && host.Length > 0 && !host.Equals(GameHost, StringComparison.OrdinalIgnoreCase) && !host.EndsWith("." + GameHost, StringComparison.OrdinalIgnoreCase) && (host.Contains("kick.com") || host.Contains("twitch.tv") || host.Contains("youtube.com") || host.Contains("tiktok.com") || host.Contains("instagram.com") || host.Contains("discord.gg"));
            bool isGamePulse = hasHttpUri && (host.Equals(GameHost, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + GameHost, StringComparison.OrdinalIgnoreCase) || urlLower.Contains("/jogar") || isBlankWindow);
            // Links de Discord e demais externos: direto no navegador (sem flutuante).
            bool isDiscordLink = hasHttpUri && (host.Contains("discord.com") || host.Contains("discordapp") || host.Contains("discord.gg") || urlLower.Contains("discord"));
            // Popup de voto (TopIdle/OAuth): abre no navegador real,
            // porque Google/Discord bloqueiam login em WebView2.
            // Blank (about:blank) e os demais popups continuam na janela
            // flutuante, como antes.
            if (isDiscordLink)
            {
                args.Handled = true;
                LogFloatingWindowEvent($"PopupVote: ({host}) redirecionado ao navegador real.");
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = requestedUri!.ToString(),
                            UseShellExecute = true
                        });
                }
                catch { }
                return;
            }
            bool useVoteWindow = hasHttpUri && !isGamePulse && (isVoteHost || isOAuthHost || isExternalHost || urlLower.Contains("topidle") || urlLower.Contains("/voto"));
            if (useVoteWindow)
            {
                args.Handled = true;
                LogFloatingWindowEvent($"PopupVote: ({host}) redirecionado ao navegador real.");
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = requestedUri!.ToString(),
                            UseShellExecute = true
                        });
                }
                catch { }
                return;
            }

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

            try
            {
                var seed = $"window.__lolIdleMissionsActive = {_settings.ShowMissions.ToString().ToLower()};";
                _ = view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(seed);
            }
            catch { }
            args.NewWindow = core;
            args.Handled = true;
            LogFloatingWindowEvent("Popup: Core pré-inicializado entregue ao jogo.");

            ShowFloatingWindow(window);
            SaveWindowSettings();
            _isHiddenToTray = true;
            Hide();
            _ = ApplyAudioSettingsAsync();
            _floatingOpened = true;
            if (_settings.ShowMissions)
            {
                ShowMissions();
                var c2 = view.CoreWebView2;
                if (c2 != null) _ = c2.ExecuteScriptAsync("document.querySelector('#lol-idle-missions')?.classList.add('active');");
            }
            LogFloatingWindowEvent("Popup: janela exibida. Janela principal fechada (sem bandeja).");

            _ = PrepareFloatingWebViewAsync(this);
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"Popup: erro ao entregar WebView preparado: {ex}");
        }
    }

    // Mostra a janela separada de voto com decoração normal do Windows,
    // em estilo mobile (estreita, vertical, centrada).
    private void ShowVoteWindow(Window window)
    {
        window.Title = "Votar no LoL Idle — TopIdle";
        window.WindowState = WindowState.Normal;
        window.WindowStyle = WindowStyle.SingleBorderWindow;
        window.ResizeMode = ResizeMode.CanResize;
        window.ShowInTaskbar = true;
        window.ShowActivated = true;
        window.Topmost = false;
        window.Opacity = 1;
        window.Width = 420;
        window.Height = 800;
        window.MinWidth = 320;
        window.MinHeight = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (!window.IsVisible)
            window.Show();
        window.Activate();
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

        window.Title = "LoL Idle - Janela flutuante";
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
                    _missionsWindow?.Hide();
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
            view.CoreWebView2.NavigationStarting += (_, navArgs) =>
            {
                if (Uri.TryCreate(navArgs.Uri, UriKind.Absolute, out var navUri) &&
                    (navUri.Scheme == "http" || navUri.Scheme == "https"))
                {
                    var lh = navUri.Host.ToLowerInvariant();
                    var lu = navUri.ToString().ToLowerInvariant();
                    if (lh.Contains("topidle") || lu.Contains("topidle"))
                    {
                        navArgs.Cancel = true;
                        LogFloatingWindowEvent($"FloatingNav: voto detectado ({navUri.Host}) redirecionado ao navegador real.");
                        try
                        {
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = navUri.ToString(),
                                    UseShellExecute = true
                                });
                        }
                        catch { }
                        return;
                    }
                    // Discord/Kick/Twitch/youtube/etc: abre no navegador real
                    if (lh.Length > 0 && !lh.Equals(GameHost, StringComparison.OrdinalIgnoreCase) && !lh.EndsWith("." + GameHost, StringComparison.OrdinalIgnoreCase) &&
                        (lh.Contains("discord.com") || lh.Contains("discordapp") || lh.Contains("discord.gg") || lh.Contains("kick.com") || lh.Contains("twitch.tv") || lh.Contains("youtube.com") || lh.Contains("tiktok.com") || lh.Contains("instagram.com") || lh.Contains("accounts.google.com")))
                    {
                        navArgs.Cancel = true;
                        LogFloatingWindowEvent($"FloatingNav: externo ({navUri.Host}) redirecionado ao navegador.");
                        try
                        {
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = navUri.ToString(),
                                    UseShellExecute = true
                                });
                        }
                        catch { }
                        if (ReferenceEquals(_floatingWindow, popupWindow))
                            _ = Dispatcher.InvokeAsync(() => popupWindow.Close());
                    }
                }
            };
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(AudioBootstrapScript);
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(TrayNotifyScript);
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(MissionsScript);
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(VoteButtonScript);
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
                    else if (message == "lol-idle-vote-button")
                    {
                        LogFloatingWindowEvent("VoteButton: abrir TopIdle no navegador.");
                        try
                        {
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "https://topidle.com/voto",
                                    UseShellExecute = true
                                });
                        }
                        catch { }
                    }
                    else if (message == "lol-idle-control:missions")
                    {
                        _settings.ShowMissions = !_settings.ShowMissions;
                        SaveWindowSettings();
                        if (_settings.ShowMissions) ShowMissions();
                        else _missionsWindow?.Hide();
                        var core2 = _floatingView?.CoreWebView2;
                        if (core2 != null) _ = core2.ExecuteScriptAsync($"document.querySelector('#lol-idle-missions')?.classList.toggle('active', {_settings.ShowMissions.ToString().ToLower()});");
                    }
                    else if (message != null && message.StartsWith("lol-idle-notify:"))
                    {
                        PlayTrayNotification(message["lol-idle-notify:".Length..]);
                    }
                    else if (message != null && message.StartsWith("lol-idle-missions:"))
                    {
                        UpdateMissions(message["lol-idle-missions:".Length..], fromFloating: true);
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
                    try
                    {
                        var finalUrl = view.CoreWebView2.Source ?? "";
                        if (Uri.TryCreate(finalUrl, UriKind.Absolute, out var finUri) &&
                            (finUri.Scheme == "http" || finUri.Scheme == "https"))
                        {
                            var lh = finUri.Host.ToLowerInvariant();
                            var isGame = lh.Equals(GameHost, StringComparison.OrdinalIgnoreCase) || lh.EndsWith("." + GameHost, StringComparison.OrdinalIgnoreCase) || lh.Contains("lolidle.com");
                            if (!isGame)
                            {
                                LogFloatingWindowEvent($"FloatingNavDone: externo ({finUri.Host}) fechando flutuante e abrindo navegador.");
                                _ = Dispatcher.InvokeAsync(() =>
                                {
                                    try
                                    {
                                        System.Diagnostics.Process.Start(
                                            new System.Diagnostics.ProcessStartInfo { FileName = finUri.ToString(), UseShellExecute = true });
                                    }
                                    catch { }
                                    popupWindow.Close();
                                });
                            }
                        }
                    }
                    catch { }
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

    // Prepara a janela separada de voto (TopIdle) — decoração normal,
    // sem scripts de arraste/controles da flutuante.
    private async Task PrepareVoteWebViewAsync(Window? owner)
    {
        if (_webViewEnvironment == null || _isPreparingVoteView ||
            (_preparedVoteWindow != null && _preparedVoteView?.CoreWebView2 != null))
            return;

        _isPreparingVoteView = true;
        Window? voteWindow = null;
        try
        {
            LogFloatingWindowEvent("PrepareVote: iniciando criação da janela separada de voto.");
            voteWindow = new Window
            {
                Title = "Votar no LoL Idle — TopIdle",
                Width = 420,
                Height = 800,
                MinWidth = 320,
                MinHeight = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = true,
                ShowActivated = true,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.CanResize,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(17, 19, 24))
            };
            // Ajusta se a tela for menor que 800px de altura
            voteWindow.MaxWidth = SystemParameters.VirtualScreenWidth * 0.9;
            voteWindow.MaxHeight = SystemParameters.VirtualScreenHeight * 0.9;
            var root = new Grid();
            var view = new Microsoft.Web.WebView2.Wpf.WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(17, 19, 24)
            };
            root.Children.Add(view);
            voteWindow.Content = root;
            _preparedVoteWindow = voteWindow;
            _preparedVoteView = view;
            voteWindow.Closed += (_, _) =>
            {
                bool wasActive = ReferenceEquals(_voteWindow, voteWindow);
                bool wasPrepared = ReferenceEquals(_preparedVoteWindow, voteWindow);
                if (wasPrepared)
                {
                    _preparedVoteWindow = null;
                    _preparedVoteView = null;
                    LogFloatingWindowEvent("PrepareVote: host preparado de voto fechado.");
                }
                if (wasActive)
                {
                    _voteWindow = null;
                    _voteView?.Dispose();
                    _voteView = null;
                    LogFloatingWindowEvent("PrepareVote: janela separada de voto fechada.");
                    _ = PrepareVoteWebViewAsync(this);
                }
            };
            voteWindow.Show();
            await voteWindow.Dispatcher.InvokeAsync(() => { },
                System.Windows.Threading.DispatcherPriority.Loaded);
            await view.EnsureCoreWebView2Async(_webViewEnvironment);
            view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            view.CoreWebView2.Settings.IsStatusBarEnabled = false;
            view.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
            view.CoreWebView2.NavigationStarting += (_, navArgs) =>
            {
                if (Uri.TryCreate(navArgs.Uri, UriKind.Absolute, out var nu) &&
                    (nu.Host.Contains("accounts.google.com") || nu.Host.Contains("accounts.google") || nu.Host.Contains("discord.com") || nu.Host.Contains("discordapp")))
                {
                    navArgs.Cancel = true;
                    LogFloatingWindowEvent($"PopupVote: OAuth ({nu.Host}) redirecionado ao navegador real.");
                    try
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = nu.ToString(),
                                UseShellExecute = true
                            });
                    }
                    catch { }
                }
            };
            view.CoreWebView2.NewWindowRequested += (_, popupArgs) =>
            {
                if (Uri.TryCreate(popupArgs.Uri, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    popupArgs.Handled = true;
                    LogFloatingWindowEvent($"PopupVote: janela aninhada ({uri.Host}) reutilizando WebView de voto.");
                    var voteViewRef = view;
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (_voteWindow != null && !_voteWindow.IsVisible) _voteWindow.Show();
                        _voteWindow?.Activate();
                        var target = uri.ToString();
                        _ = Task.Run(() => voteViewRef.CoreWebView2.Navigate(target));
                    });
                }
                else popupArgs.Handled = true;
            };
            voteWindow.Hide();
            LogFloatingWindowEvent("PrepareVote: janela separada de voto pronta e oculta.");
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"PrepareVote: falha ao inicializar: {ex.Message}");
            if (ReferenceEquals(_preparedVoteWindow, voteWindow))
            {
                _preparedVoteWindow = null;
                _preparedVoteView = null;
            }
            voteWindow?.Close();
        }
        finally
        {
            _isPreparingVoteView = false;
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
            "epic" => ("LoL Idle", "Item épico obtido!", "Assets/notify_epic.wav"),
            "mission" => ("LoL Idle", "Missão concluída!", "Assets/notify_mission.wav"),
            "inventory" => ("LoL Idle", "Inventário cheio!", "Assets/notify_inventory.wav"),
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
                _floatingOpened = true;
                if (_settings.ShowMissions) ShowMissions();
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
            _floatingOpened = true;
            if (_settings.ShowMissions) ShowMissions();
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

    private bool _isCustomMaximized;
    private double _restoreLeft;
    private double _restoreTop;
    private double _restoreWidth;
    private double _restoreHeight;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCustomMaximized)
        {
            _restoreLeft = Left; _restoreTop = Top;
            _restoreWidth = Width; _restoreHeight = Height;
            var work = SystemParameters.WorkArea;
            Left = work.Left; Top = work.Top; Width = work.Width; Height = work.Height;
            ResizeMode = ResizeMode.NoResize;
            _isCustomMaximized = true;
            _settings.IsMaximized = true;
            SaveWindowSettings();
        }
        else
        {
            _isCustomMaximized = false;
            _settings.IsMaximized = false;
            ResizeMode = ResizeMode.CanResize;
            Left = _restoreLeft; Top = _restoreTop;
            Width = _restoreWidth; Height = _restoreHeight;
            SaveWindowSettings();
        }
        UpdateMaximizeButton();
    }

    private void UpdateMaximizeButton()
    {
        if (MaximizeButton != null)
        {
            MaximizeButton.Content = _isCustomMaximized ? "❐" : "□";
            MaximizeButton.ToolTip = _isCustomMaximized ? "Restaurar" : "Maximizar";
        }
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
        else if (e.LeftButton == MouseButtonState.Pressed && !_isCustomMaximized)
            DragMove();
    }
}
