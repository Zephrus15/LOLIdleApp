using System.Globalization;
using System.Collections.Generic;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using ColorConverter = System.Windows.Media.ColorConverter;
using Image = System.Windows.Controls.Image;
using Panel = System.Windows.Controls.Panel;
using CheckBox = System.Windows.Controls.CheckBox;
using System.Runtime.InteropServices;
using System.Management;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Toolkit.Uwp.Notifications;

namespace LoLIdleDesktop;

public partial class MainWindow : Window
{
    private const string GameUrl = "https://lolidle.com/jogar";
    private const string GameHost = "lolidle.com";
    // Rótulo da conta padrão/principal
    internal const string DefaultAccountName = "Conta principal";

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
                    } else if (!prefs.masterMuted && context.state === 'suspended' && pausedByUs.has(context)) {
                        try {
                            await context.resume();
                            pausedByUs.delete(context);
                        } catch (_) { /* bloqueado (ex.: autoplay): mantém no set para o gesture tentar de novo */ }
                    }
                } catch (_) {}
            }
        };
        const resumeAfterGesture = () => {
            const prefs = window.__lolIdleAudioPrefs;
            if (prefs && !prefs.masterMuted) {
                for (const context of contexts) {
                    if (context.state === 'suspended' && pausedByUs.has(context)) {
                        context.resume().then(() => pausedByUs.delete(context)).catch(() => {});
                    }
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
                    color:#fff; background:#1C2129; cursor:pointer;
                    display:grid; place-items:center; }
                #lol-idle-floating-controls button:hover { background:#2B313F; }
                #lol-idle-floating-controls button.active { color:#E8903F; }
                #lol-idle-floating-controls svg { width:16px; height:16px; fill:none; stroke:currentColor;
                    stroke-width:1.8; stroke-linecap:round; stroke-linejoin:round; pointer-events:none; }
                #lol-idle-settings svg path { fill:#fff; stroke:none; }
                #lol-idle-floating-controls button.active svg { fill:none; stroke:#E8903F; }
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
            if (window.__lolIdleFloatingTopmost) {
                const p = panel.querySelector('#lol-idle-pin');
                p?.classList.add('active');
                if (p) p.title = 'Desativar janela sempre no topo';
            }
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
        const last = { legendary: 0, epic: 0, mission: 0, inventory: 0, level: 0, potion_hp: 0, potion_mana: 0 };
        let prevLevel = null;
        const POCOES_FAM = {
            potion_hp: ['vida', 'vidaF', 'vidaS'],
            potion_mana: ['mana', 'manaF']
        };
        const somaPocao = (ids) => {
            let s = 0;
            for (const id of ids) {
                const b = document.querySelector('#hud-pocoes .hud-pocao[data-pocao=' + id + '] b');
                s += b ? (parseInt(b.textContent, 10) || 0) : 0;
            }
            return s;
        };
        let prevFamilias = { potion_hp: null, potion_mana: null };
        let _ultimaPocoes = 0;
        let _pocoesTimer = null;
        const checkPocoes = () => {
            try {
                const hud = document.querySelector('#hud-pocoes');
                if (!hud || hud.querySelectorAll('.hud-pocao').length === 0) return; // ausente ≠ zero
                const now = Date.now();
                if (_ultimaPocoes && (now - _ultimaPocoes) < 400) {
                    if (!_pocoesTimer) _pocoesTimer = setTimeout(() => { _pocoesTimer = null; checkPocoes(); }, 400 - (now - _ultimaPocoes) + 1);
                    return;
                }
                _ultimaPocoes = now;
                for (const kind of Object.keys(POCOES_FAM)) {
                    const cur = somaPocao(POCOES_FAM[kind]);
                    if (prevFamilias[kind] === null) { prevFamilias[kind] = cur; continue; }
                    if (prevFamilias[kind] > 0 && cur === 0) send(kind);
                    prevFamilias[kind] = cur;
                }
            } catch (_) {}
        };
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
        // So executa a deteccao em areas de notificacao reais (#log, #avisos-saque).
        // Fora delas (ex.: painel de configuracoes da engrenagem), o texto pode
        // conter missao/concluida sem ser evento real e gerava falso positivo.
        const inNotifyArea = (node) => {
            try {
                let el = node && node.nodeType === 1 ? node : (node && node.parentElement) || null;
                if (!el || !el.closest) return false;
                return !!(el.closest('#log') || el.closest('#avisos-saque'));
            } catch (_) { return false; }
        };
        const observer = new MutationObserver((mutations) => {
            for (const m of mutations) {
                for (const n of m.addedNodes || []) {
                    if (n.nodeType === 1) {
                        checkSaqueNode(n);
                        // Log de missões (canal Servidor #log); ignora painéis fora dele.
                        if ((n.textContent) && inNotifyArea(n)) checkText(n.textContent);
                    } else if (n.nodeType === 3 && inNotifyArea(n)) checkText(n.nodeValue);
                }
                if (m.type === 'characterData' && m.target) {
                    if (inNotifyArea(m.target)) checkText(m.target.textContent);
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
            checkPocoes();
        });
        const start = () => {
            if (!document.body) { setTimeout(start, 500); return; }
            observer.observe(document.body, { childList: true, subtree: true, characterData: true });
            setTimeout(checkPocoes, 800);
        };
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
        else start();
    })();";

    private Forms.NotifyIcon? _trayIcon;
    private string? _lastBalloonAccount;
    private string? _lastBalloonKind;
    private System.Windows.Threading.DispatcherTimer? _balloonRestoreTimer;
    private Forms.ToolStripMenuItem? _trayTrayMuteItem;
    private bool _floatingOpened;
    private bool _allowExit;
    // True depois que o MainWindow dispara Closed (ex.: via ExitApplication):
    // Show()/Activate() em janela fechada lança InvalidOperationException.
    private bool _mainWindowClosed;
    private bool _isHiddenToTray;
    private bool _settingsReady;

    // Notificações recentes (só memória, não persistidas): últimas 4, mais recente primeiro.
    private sealed class NotificacaoItem
    {
        public string Conta = "";
        public string Kind = "";
        public DateTime Quando;
    }
    private readonly List<NotificacaoItem> _ultimasNotificacoes = new();
    private readonly HashSet<string> _contasNaoLidas = new(StringComparer.OrdinalIgnoreCase);
    private bool ContaNaoLida(string conta) => _contasNaoLidas.Contains(conta);
    private System.Windows.Threading.DispatcherTimer? _settingsSaveTimer;
    private System.Drawing.Icon? _appIcon;
    private const string AccountBrowserArguments = "--autoplay-policy=no-user-gesture-required" +
        " --disable-background-timer-throttling" +
        " --disable-backgrounding-occluded-windows" +
        " --disable-renderer-backgrounding";
    // Uma WebView2 (sessão/partição própria) por conta. GameView aponta para a padrão.
    private readonly Dictionary<string, Microsoft.Web.WebView2.Wpf.WebView2> _accountViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CoreWebView2Environment> _accountEnvs = new(StringComparer.OrdinalIgnoreCase);
    // Referência para a conta padrão (compatibilidade com o restante do código).
    private Microsoft.Web.WebView2.Wpf.WebView2? GameView;
    // WebView da conta atualmente ativa (fallback: conta padrão).
    private Microsoft.Web.WebView2.Wpf.WebView2? ActiveView =>
        (_settings.ContaAtiva != null &&
         _accountViews.TryGetValue(_settings.ContaAtiva, out var ativa))
            ? ativa
            : GameView;
    private CoreWebView2Environment? _webViewEnvironment;
    private string _lastFloatingUri = GameUrl;
    private Window? _floatingWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _floatingView;
    // Janela Hunt (painel da extensão Hunt for LoL Idle) e guarda de instalação concorrente.
    private HuntWindow? _huntWindow;
    private bool _huntInstalling;
    private readonly System.Threading.SemaphoreSlim _huntInstallLock = new(1, 1);
    // Conta dona da flutuante no momento da criação (evita rótulo errado em notificação)
    private string _floatingAccountKey = DefaultAccountName;
    private Window? _preparedFloatingWindow;
    private Microsoft.Web.WebView2.Wpf.WebView2? _preparedFloatingView;
    private bool _isPreparingFloatingView;
    // Coalesced one-shot: pedido de recriação do host feito enquanto uma
    // preparação estava em andamento. Aplicado ao final da preparação atual.
    private bool _prepareRestartRequested;
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
    private TextBlock? _missionsTitle;
    private TextBlock? _missionsBody;
    private System.Windows.Controls.ProgressBar? _missionsPasseBar;
    private System.Windows.Controls.ProgressBar? _missionsTarefaBar;
    private TextBlock? _missionsPasseTxt;
    private TextBlock? _missionsTarefaTxt;
    private Button? _missionsPinBtn;
    private string? _lastMissionsJson;
    private System.Windows.Threading.DispatcherTimer? _missionsPollTimer;

    // Brush do tema (ardósia+cobre) por chave, com fallback para o hex original caso o recurso não exista.
    private static SolidColorBrush ThemeBrush(string key, string fallbackHex)
        => System.Windows.Application.Current?.Resources[key] as SolidColorBrush
           ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));

    // Painel "Consumo total": RAM/CPU totais + barra/pill (via WMI + WorkingSet64), RAM/CPU por conta e limpeza de cache.
    private System.Windows.Threading.DispatcherTimer? _usoTimer;
    private readonly Dictionary<int, TimeSpan> _ultimoCpu = new();
    private DateTime _ultimoCpuSample = DateTime.MinValue;
    private bool _usoColetando;
    private bool _usoEncerrando;
    private CancellationTokenSource? _usoCts;
    // Totais globais (processo principal + todas as WebViews2) usados pela barra de status
    private double? _footerCpuPercent;
    private long _footerRamBytes;

    // Linhas visuais do painel (criadas uma única vez; a cada tick só o texto muda).
    private sealed class LinhaUsoVisual
    {
        public required Grid Root { get; init; }
        public required TextBlock Nome { get; init; }
        public required TextBlock Valor { get; init; }
    }

    private readonly List<LinhaUsoVisual> _usoLinhasVisual = new();

    // Meta de referência de RAM do dispositivo/app (bloco "Consumo total").
    // 200 MB para o app + 1,0 GB por conta ABERTA (WebView instanciada).
    private double MetaRamBytes()
    {
        double baseBytes = 200.0 * 1024.0 * 1024.0;          // 200 MB para o app em si
        double porConta  = (_accountViews?.Count ?? 1) * 1024.0 * 1024.0 * 1024.0; // 1 GB por WebView instanciada
        return baseBytes + porConta;
    }
    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
    private System.Windows.Threading.DispatcherTimer? _consumoRevertTimer;

    // DTO pequeno devolvido pela coleta de uso (fora da UI thread).
    private sealed class ContasUsoEntry
    {
        public string Conta { get; init; } = "";
        public long RamBytes { get; init; }
        public double? CpuPercent { get; init; }
    }

    // Resultado agregado da coleta (entradas por conta + totais do aplicativo inteiro).
    private sealed class ContasUsoResultado
    {
        public List<ContasUsoEntry> Contas { get; } = new();
        public long TotalRamBytes { get; set; }
        public double? FooterCpuPercent { get; set; }
    }

    private WindowSettings _settings = new();
    private string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LoLIdleDesktop", "window-settings.json");

    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    // SetWindowPos com SWP_NOACTIVATE: reordena a z-order SEM ativar a janela
    // (BringWindowToTop ativava a Missões a cada clique no jogo, roubando o foco).
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

    private double _normalWidth = 1280;
    private double _normalHeight = 800;

    public sealed class WindowSettings
    {
        public double NormalWidth { get; set; } = 1280;
        public double NormalHeight { get; set; } = 800;
        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        // Persistência do "Sempre no topo" por janela (A).
        // IsAlwaysOnTop (legado, sem uso) foi substituído por estas duas preferências.
        public bool FloatingTopmost { get; set; } = false;
        public bool MissionsTopmost { get; set; } = false;
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
        public bool ShowMissions { get; set; }
        public bool SidebarVisible { get; set; } = true;
        public double SidebarWidth { get; set; } = 260;
        public bool HasMissionsBounds { get; set; }
        public double MissionsWidth { get; set; } = 360;
        public double MissionsHeight { get; set; } = 320;
        public double MissionsLeft { get; set; } = 200;
        public double MissionsTop { get; set; } = 200;
        public bool IsMaximized { get; set; }
        // Comportamento ao fechar (0 = perguntar, 1 = continuar em segundo plano, 2 = sair)
        public int CloseBehavior { get; set; } = 0;
        public List<string> ContasAdicionadas { get; set; } = new();
        public string ContaAtiva { get; set; } = "Conta principal";
        public Dictionary<string, string> ContasNomes { get; set; } = new();
        // Chaves cujo rótulo foi escolhido manualmente (o DOM não sobrescreve)
        public List<string> ContasNomesPersonalizados { get; set; } = new();
        // Avatar da conta: cor de fundo (hex), ícone (emoji) e caminho da imagem personalizada
        public Dictionary<string, string> ContasAvatarColor { get; set; } = new();
        public Dictionary<string, string> ContasAvatarIcon { get; set; } = new();
        public Dictionary<string, string> ContasAvatarImagePath { get; set; } = new();
        // Deslocamento do recorte da imagem do avatar ("x,y" de -1 a 1; 0 = centralizado)
        public Dictionary<string, string> ContasAvatarImageOffsets { get; set; } = new();
        // Zoom da imagem do avatar (multiplicador de 1.0 a 4.0 sobre a escala "fill" do recorte)
        public double ContasAvatarImageScale { get; set; } = 1.0;
        // Notificações: master switch e filtros por conta e por tipo (vazio = todas ativas)
        public bool NotifyEnabled { get; set; } = true;
        // Chaves de contas DESATIVADAS para notificações
        public List<string> NotifyAccountsDisabled { get; set; } = new();
        // Tipos de notificação desativados (legendary / epic / mission / inventory / level)
        public List<string> NotifyTypesDisabled { get; set; } = new();
        // Hunt for LoL Idle (extensão opcional, instalada só no clique do botão Hunt)
        public string? HuntExtensionId { get; set; }
        public bool HuntTopmost { get; set; } = true;
    }

    public MainWindow()
    {
        ToastNotificationManagerCompat.OnActivated += ToastActivated;
        Closed += (_, _) => _mainWindowClosed = true;
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
    }

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

            // Normaliza a lista de contas persistidas
            if (_settings.ContasAdicionadas == null)
                _settings.ContasAdicionadas = new List<string>();
            _settings.ContasAdicionadas = _settings.ContasAdicionadas
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (_settings.ContasNomes == null)
                _settings.ContasNomes = new Dictionary<string, string>();
            if (_settings.ContasNomesPersonalizados == null)
                _settings.ContasNomesPersonalizados = new List<string>();
            if (_settings.ContasAvatarColor == null)
                _settings.ContasAvatarColor = new Dictionary<string, string>();
            if (_settings.ContasAvatarIcon == null)
                _settings.ContasAvatarIcon = new Dictionary<string, string>();
            if (_settings.ContasAvatarImagePath == null)
                _settings.ContasAvatarImagePath = new Dictionary<string, string>();
            if (_settings.ContasAvatarImageOffsets == null)
                _settings.ContasAvatarImageOffsets = new Dictionary<string, string>();
            if (!double.IsFinite(_settings.ContasAvatarImageScale) || _settings.ContasAvatarImageScale < 1.0)
                _settings.ContasAvatarImageScale = 1.0;
            else if (_settings.ContasAvatarImageScale > 4.0)
                _settings.ContasAvatarImageScale = 4.0;
            if (_settings.NotifyAccountsDisabled == null)
                _settings.NotifyAccountsDisabled = new List<string>();
            if (_settings.NotifyTypesDisabled == null)
                _settings.NotifyTypesDisabled = new List<string>();
            if (string.IsNullOrWhiteSpace(_settings.ContaAtiva))
                _settings.ContaAtiva = DefaultAccountName;

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

            if (settings.IsMaximized)
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
        ApplySidebarLayout();
        UpdateMaximizeButton();
        SyncTrayMenuChecks();
        _ = AtualizarContaConectadaUIAsync(); // fire-and-forget
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

        // Clique no balão de notificação da bandeja restaura a janela e troca para a conta da notificação.
        // Os valores são capturados e LIMPOS aqui: uma notificação antiga não pode mais trocar a conta.
        _trayIcon.BalloonTipClicked += (_, _) =>
        {
            var conta = _lastBalloonAccount;
            var kind = _lastBalloonKind;
            // NULA o estado sempre: um clique fora de hora nunca reutiliza dados de outra notificação.
            _lastBalloonAccount = null;
            _lastBalloonKind = null;
            // Estado já consumido/limpo: ignora silenciosamente (balão antigo não sequestra a ação).
            if (conta == null && kind == null)
                return;
            RestoreFromTray();
            if (conta != null && !ContaExiste(conta))
            {
                LogFloatingWindowEvent($"BalloonTipClicked: conta '{conta}' não existe mais — ignorado");
                // Conta removida: descarta a linha órfã do histórico (nada de restaurar/trocar).
                RemoverUltimaNotificacao(conta, kind);
                conta = null;
            }
            if (conta != null)
                _ = SwitchAccountAsync(conta);
            // Clique no balão de poção manda o personagem de volta para a fonte
            if (conta != null && kind != null
                && kind.StartsWith("potion", StringComparison.OrdinalIgnoreCase))
                _ = VoltarParaFonteAsync(conta);
            RemoverUltimaNotificacao(conta, kind);
        };
    }

    private void ToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        try
        {
            var toastArgs = ToastArguments.Parse(e.Argument);
            string? conta = null, kind = null;
            if (toastArgs.Contains("conta")) conta = toastArgs["conta"];
            if (toastArgs.Contains("kind"))  kind  = toastArgs["kind"];
            // O evento roda numa thread COM fora da UI — despachar para a UI thread
            Dispatcher.Invoke(() =>
            {
                // Clique no toast consome/limpa também o estado do fallback balloon:
                // um balão antigo nunca sobrevive a uma interação por toast.
                // (Notificações novas re-setam o estado ao exibir.)
                _lastBalloonAccount = null;
                _lastBalloonKind = null;
                if (!ContaExiste(conta))
                {
                    LogFloatingWindowEvent($"ToastActivated: conta '{conta}' não existe mais — ignorado");
                    // Conta removida: descarta a linha órfã do histórico.
                    RemoverUltimaNotificacao(conta, kind);
                    return;
                }
                RestoreFromTray();
                if (conta != null) _ = SwitchAccountAsync(conta);
                RemoverUltimaNotificacao(conta, kind);
                if (conta != null && kind != null
                    && kind.StartsWith("potion", StringComparison.OrdinalIgnoreCase))
                    _ = VoltarParaFonteAsync(conta);
            });
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"ToastActivated: erro: {ex.Message}");
        }
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

        if (_floatingWindow != null)   // flutuante aberta: traz ELA de volta, não a principal
        {
            if (_floatingWindow.WindowState == WindowState.Minimized)
                _floatingWindow.WindowState = WindowState.Normal;
            if (!_floatingWindow.IsVisible)
                _floatingWindow.Show();
            _floatingWindow.Activate();

            if (_trayIcon != null)
                _trayIcon.Visible = false;

            if (_settings.ShowMissions)
            {
                try { ShowMissions(); } catch { }
            }
            return;
        }

        // comportamento atual (janela principal)
        // MainWindow já fechado (Closed disparado, ex.: ExitApplication):
        // Show()/Activate() em janela fechada lança InvalidOperationException.
        // Não há o que restaurar — sai sem tocar na janela.
        if (_mainWindowClosed)
        {
            LogFloatingWindowEvent("RestoreFromTray: janela principal já fechada — restauração ignorada (app encerrando).");
            return;
        }
        Show();
        if (_trayIcon != null)
            _trayIcon.Visible = false;
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        _ = ApplyAudioSettingsAsync();
        _ = AtualizarContaConectadaUIAsync(); // fire-and-forget
    }

    private void ShowMissions()
    {
        if (_missionsWindow != null)
        {
            _missionsWindow.Topmost = _settings.MissionsTopmost;
            if (_missionsPinBtn != null)
                _missionsPinBtn.Foreground = _missionsWindow.Topmost ? ThemeBrush("Accent", "#D97B36") : ThemeBrush("TextSecondary", "#93A0B4");
            _missionsWindow.Show();
            // Mantém só visível, sem forçar foco: a Missões não é stateful e o jogo deve continuar
            // com o foco (o usuário clica nela quando quiser interagir).
            try { _missionsPollTimer?.Start(); } catch { }
            SyncFloatingControls();
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
            Topmost = _settings.MissionsTopmost,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            BorderBrush = ThemeBrush("BorderSubtle", "#2B313F"),
            BorderThickness = new Thickness(1)
        };
        // Chrome invisível para resize pelas bordas (CaptionHeight=0: o arrasto é o próprio DragMove do conteúdo).
        var chrome = new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(20),
            CornerRadius = new CornerRadius(10),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
            NonClientFrameEdges = System.Windows.Shell.NonClientFrameEdges.None
        };
        System.Windows.Shell.WindowChrome.SetWindowChrome(win, chrome);
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = _settings.HasMissionsBounds ? _settings.MissionsLeft : 200;
        win.Top = _settings.HasMissionsBounds ? _settings.MissionsTop : 200;
        win.LocationChanged += (_, _) => { _settings.MissionsLeft = win.Left; _settings.MissionsTop = win.Top; _settings.HasMissionsBounds = true; SaveWindowSettings(); };
        win.SizeChanged += (_, _) => { _settings.MissionsWidth = win.Width; _settings.MissionsHeight = win.Height; _settings.HasMissionsBounds = true; SaveWindowSettings(); };
        var textPrimaryBrush = ThemeBrush("TextPrimary", "#D6DAE6");
        var textSecondaryBrush = ThemeBrush("TextSecondary", "#93A0B4");
        var accentBrush = ThemeBrush("Accent", "#D97B36");
        var accentHoverBrush = ThemeBrush("AccentHover", "#E8903F");
        var accentIceBrush = ThemeBrush("AccentIce", "#4FD1D9");
        var ghostIconButtonStyle = (Style)System.Windows.Application.Current.Resources["GhostIconButton"];
        const string rajdhani = "pack://application:,,,/Assets/Fonts/Rajdhani-SemiBold.ttf#Rajdhani";
        const string barlow = "pack://application:,,,/Assets/Fonts/Barlow-Regular.ttf#Barlow";

        // Ícones dos botões de controle: SVG 24x24 com o traço ligado ao Foreground do botão (hover/ativo do GhostIconButton).
        var strokeForegroundBinding = new System.Windows.Data.Binding("Foreground")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1)
        };
        System.Windows.Shapes.Path MakeIcon(string geometry)
        {
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(geometry),
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = System.Windows.Media.Brushes.Transparent,
                Stretch = Stretch.Uniform,
                Width = 15,
                Height = 15
            };
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, strokeForegroundBinding);
            return path;
        }

        var root = new Grid { Background = ThemeBrush("BgPanel", "#1C2129") };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Cabeçalho escuro destacado (mesmo esquema dos diálogos Ajustes/Hunt): faixa BgPanel2 com título à esquerda e pin/close à direita.
        var header = new Border
        {
            Background = ThemeBrush("BgPanel2", "#222733"),
            Padding = new Thickness(14, 10, 10, 10),
            BorderBrush = ThemeBrush("BorderSubtle", "#2B313F"),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        Grid.SetRow(header, 0);
        var headerGrid = new Grid();
        _missionsTitle = new TextBlock { Text = "📖 Missões", Foreground = textPrimaryBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, FontFamily = new System.Windows.Media.FontFamily(rajdhani), VerticalAlignment = VerticalAlignment.Center };
        headerGrid.Children.Add(_missionsTitle);
        var top = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var pinBtn = new Button
        {
            Content = MakeIcon("M16 3 L21 8 L17 9 L13 13 L15 15 L13 17 L9 13 L4 18 M8 8 L16 16"),
            Style = ghostIconButtonStyle,
            Width = 26,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = win.Topmost ? accentBrush : textSecondaryBrush,
            ToolTip = "Manter na frente (fixar)"
        };
        _missionsPinBtn = pinBtn;
        pinBtn.Click += (_, _) =>
        {
            win.Topmost = !win.Topmost;
            _settings.MissionsTopmost = win.Topmost;
            SaveWindowSettings();
            pinBtn.Foreground = win.Topmost ? accentBrush : textSecondaryBrush;
        };
        win.MouseEnter += (_, _) => { if (win.Topmost) pinBtn.Foreground = accentBrush; };
        top.Children.Add(pinBtn);
        var closeBtn = new Button
        {
            Content = MakeIcon("M6 6 L18 18 M18 6 L6 18"),
            Style = ghostIconButtonStyle,
            Width = 26,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Fechar missões"
        };
        closeBtn.Click += (_, _) => { win.Hide(); _settings.ShowMissions = false; SaveWindowSettings(); SyncFloatingControls(); };
        top.Children.Add(closeBtn);
        headerGrid.Children.Add(top);
        header.Child = headerGrid;
        root.Children.Add(header);

        var content = new StackPanel { Margin = new Thickness(10) };
        Grid.SetRow(content, 1);
        _missionsPasseTxt = new TextBlock { Text = "Passe", Foreground = textPrimaryBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, FontFamily = new System.Windows.Media.FontFamily(rajdhani), Margin = new Thickness(0, 0, 0, 4) };
        _missionsPasseBar = new System.Windows.Controls.ProgressBar { Height = 14, Margin = new Thickness(0, 0, 0, 10), Foreground = new LinearGradientBrush { GradientStops = new GradientStopCollection { new GradientStop(accentBrush.Color, 0), new GradientStop(accentHoverBrush.Color, 1) }, StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) }, Background = ThemeBrush("BgPanel3", "#262C39") };
        _missionsTarefaTxt = new TextBlock { Text = "Tarefa", Foreground = textPrimaryBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, FontFamily = new System.Windows.Media.FontFamily(rajdhani), Margin = new Thickness(0, 0, 0, 4) };
        _missionsTarefaBar = new System.Windows.Controls.ProgressBar { Height = 14, Margin = new Thickness(0, 0, 0, 10), Foreground = accentIceBrush, Background = ThemeBrush("BgPanel3", "#262C39") };
        content.Children.Add(_missionsPasseTxt); content.Children.Add(_missionsPasseBar);
        content.Children.Add(_missionsTarefaTxt); content.Children.Add(_missionsTarefaBar);
        _missionsBody = new TextBlock { Text = "Carregando missões...", Foreground = textSecondaryBrush, FontSize = 12, FontFamily = new System.Windows.Media.FontFamily(barlow), TextWrapping = TextWrapping.Wrap };
        content.Children.Add(_missionsBody);
        root.Children.Add(content);
        root.MouseLeftButtonDown += (_, _) => win.DragMove();
        var layout = new DockPanel();
        var grip = new Border { Width = 18, Height = 18, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = System.Windows.VerticalAlignment.Bottom, Background = Brushes.Transparent, Cursor = Cursors.SizeNWSE };
        grip.Child = new TextBlock { Text = "◢", Foreground = ThemeBrush("TextDim", "#6B7689"), FontSize = 10, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(grip, Dock.Bottom);
        layout.Children.Add(grip);
        layout.Children.Add(root);
        bool resizing = false;
        double resizeStartX = 0, resizeStartY = 0;
        double startW = 0, startH = 0;
        grip.MouseLeftButtonDown += (_, e) =>
        {
            resizing = true;
            // Posição em coordenadas de tela (não relativa à janela, que muda ao redimensionar).
            resizeStartX = Forms.Cursor.Position.X;
            resizeStartY = Forms.Cursor.Position.Y;
            startW = win.Width; startH = win.Height;
            grip.CaptureMouse();
            e.Handled = true;
        };
        win.MouseMove += (_, _) =>
        {
            if (!resizing) return;
            double dx = Forms.Cursor.Position.X - resizeStartX;
            double dy = Forms.Cursor.Position.Y - resizeStartY;
            win.Width = Math.Max(win.MinWidth, startW + dx);
            win.Height = Math.Max(win.MinHeight, startH + dy);
        };
        win.MouseLeftButtonUp += (_, _) =>
        {
            if (!resizing) return;
            resizing = false;
            try { grip.ReleaseMouseCapture(); } catch { }
            _settings.MissionsWidth = win.Width;
            _settings.MissionsHeight = win.Height;
            _settings.HasMissionsBounds = true;
            SaveWindowSettings();
        };
        // Moldura externa arredondada (mesmo visual da janela flutuante); o conteúdo interno fica inalterado.
        var frame = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = ThemeBrush("BgPanel", "#1C2129"),
            BorderBrush = ThemeBrush("BorderSubtle", "#2B313F"),
            BorderThickness = new Thickness(1),
            Child = layout,
            ClipToBounds = true
        };
        // ClipToBounds corta só no retângulo externo; o Clip arredondado recorta o conteúdo
        // (cabeçalho BgPanel2) pelos cantos da moldura. Atualiza junto com o resize.
        frame.SizeChanged += (_, _) =>
        {
            if (frame.ActualWidth > 0 && frame.ActualHeight > 0)
                frame.Clip = new RectangleGeometry(new Rect(0, 0, frame.ActualWidth, frame.ActualHeight), 10, 10);
        };
        win.Content = frame;
        // Na saída do app (ExitApplication, _allowExit == true) deixa fechar de verdade.
        // Cancelar sempre impedia o shutdown (OnLastWindowClose) e deixava o processo vivo invisível.
        win.Closing += (_, e) => { if (_allowExit) return; e.Cancel = true; win.Hide(); _settings.ShowMissions = false; SaveWindowSettings(); SyncFloatingControls(); };
        win.GotFocus += (_, _) => win.BorderThickness = new Thickness(1);
        win.LostFocus += (_, _) => win.BorderThickness = new Thickness(0);
        win.Opacity = 1.0;
        win.MouseEnter += (_, _) => { root.Background = ThemeBrush("BgPanel", "#1C2129"); };
        win.MouseLeave += (_, _) => { root.Background = ThemeBrush("BgPanel", "#1C2129"); };
        _missionsWindow = win;
        _lastMissionsJson = null;
        win.Show();
        var pollTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _missionsPollTimer = pollTimer;
        win.Closing += (_, _) => pollTimer.Stop();
        pollTimer.Tick += async (_, _) =>
        {
            if (_missionsWindow == null || !_missionsWindow.IsVisible) return;
            try
            {
                var expr = @"(() => { const g = (id) => document.getElementById(id)?.textContent?.trim() || ''; const gt = (id) => { const el = document.getElementById(id); const t = el?.textContent?.trim(); return t ? t : (el?.getAttribute('title') || '').trim(); }; return JSON.stringify({ missoes: g('rastreio-n'), eventos: g('rastreio-ev-n'), passe: gt('rastreio-passe'), tarefa: gt('rastreio-tarefa'), analise: gt('rastreio-analise'), diaria: gt('rastreio-diaria') }); })()";
                // Lê sempre o WebView da conta na frente (ActiveView); nunca puxa dados da outra conta.
                // Se o DOM da conta ativa ainda estiver sem o tracker, mostra vazio/dica em vez de cair na outra conta.
                string? json = null;
                var activeView = ActiveView;
                var sources = new[] { activeView?.CoreWebView2 };
                foreach (var core in sources)
                {
                    if (core == null) continue;
                    string raw;
                    try { raw = await core.ExecuteScriptAsync(expr); } catch { continue; }
                    if (string.IsNullOrEmpty(raw)) continue;
                    string candidate;
                    try { candidate = System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? ""; } catch { continue; }
                    if (string.IsNullOrWhiteSpace(candidate) || candidate == "{}") continue;
                    try
                    {
                        using var d2 = System.Text.Json.JsonDocument.Parse(candidate);
                        var hasAny = d2.RootElement.EnumerateObject().Any(p => !string.IsNullOrWhiteSpace(p.Value.GetString()));
                        if (!hasAny) continue;
                    }
                    catch { continue; }
                    json = candidate;
                    break;
                }
                if (string.IsNullOrWhiteSpace(json)) return;
                if (json == _lastMissionsJson) return;
                // No 1º carregamento permite exibir já (o guard em UpdateMissions ignoraria
                // um fromFloating: false quando a flutuante está visível).
                bool primeiroCarregamento = _lastMissionsJson == null;
                _lastMissionsJson = json;
                UpdateMissions(json, fromFloating: primeiroCarregamento);
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
            if (_missionsTitle != null) _missionsTitle.Text = "📖 Missões";
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
        _usoEncerrando = true;
        try { _usoTimer?.Stop(); } catch { }
        _usoTimer = null;
        try { _consumoRevertTimer?.Stop(); } catch { }
        try { _usoCts?.Cancel(); } catch { }
        _usoCts = null;
        try { _missionsWindow?.Close(); } catch { }
        _missionsWindow = null;

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
        // Fecha a janela Hunt junto com o processo quando o usuário escolhe Sair.
        if (_huntWindow != null)
        {
            var hunt = _huntWindow;
            _huntWindow = null;
            try { hunt.Close(); } catch { }
        }
        // Libera as WebViews de conta ao sair do aplicativo (as pastas de sessão NÃO são apagadas).
        foreach (var accountView in _accountViews.Values)
        {
            try { accountView.Dispose(); } catch { }
        }
        _accountViews.Clear();

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
        IniciarUsoRecursosTimer();

        try
        {
            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoLIdleDesktop", "WebView2");

            Directory.CreateDirectory(userData);

            var envOptions = new CoreWebView2EnvironmentOptions();
            envOptions.AdditionalBrowserArguments = AccountBrowserArguments;
            // Permite instalar/carregar extensões (Hunt for LoL Idle) nos perfis WebView2.
            envOptions.AreBrowserExtensionsEnabled = true;
            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userData, options: envOptions);
            // Registra o ambiente padrão como o da conta principal
            _accountEnvs[DefaultAccountName] = _webViewEnvironment;

            // Restaura a conta ativa; se a chave salva não existir mais, cai para a padrão.
            string active = _settings.ContaAtiva;
            if (!string.Equals(active, DefaultAccountName, StringComparison.OrdinalIgnoreCase))
            {
                var match = _settings.ContasAdicionadas
                    .FirstOrDefault(c => c.Equals(active, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    active = DefaultAccountName;
                }
                else
                {
                    active = match; // normaliza para a chave canônica persistida
                }
                if (!string.Equals(active, _settings.ContaAtiva, StringComparison.Ordinal))
                {
                    _settings.ContaAtiva = active;
                    SaveWindowSettings();
                }
            }

            await EnsureAccountViewAsync(DefaultAccountName);
            if (!string.Equals(active, DefaultAccountName, StringComparison.OrdinalIgnoreCase))
            {
                try { await EnsureAccountViewAsync(active); }
                catch
                {
                    _settings.ContaAtiva = DefaultAccountName;
                    SaveWindowSettings();
                }
            }
            ApplyAccountVisibility();
            _ = AtualizarContaConectadaUIAsync();
            _ = PrepareFloatingWebViewAsync(this, "Window_Loaded");
        }
        catch (Exception ex)
        {
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

            LogFloatingWindowEvent($"Popup: estado do host — preparedWindowNull={_preparedFloatingWindow == null}, preparedViewNull={_preparedFloatingView == null}, coreWebView2Null={_preparedFloatingView?.CoreWebView2 == null}");
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
            LogFloatingWindowEvent("Prepare: host preparado consumido nesta navegação.");

            try
            {
                var seed = $"window.__lolIdleMissionsActive = {_settings.ShowMissions.ToString().ToLower()}; window.__lolIdleFloatingTopmost = {_settings.FloatingTopmost.ToString().ToLower()};";
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

            _ = PrepareFloatingWebViewAsync(this, "HandleNewWindowRequested");
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
        var width = _settings.HasFloatingBounds ? _settings.FloatingWidth : 360;
        var height = _settings.HasFloatingBounds ? _settings.FloatingHeight : 287;
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
            // Clique/ativação na flutuante reordena a z-order da Missões para ela reaparecer junto,
            // mas SEM ativar a Missões (BringWindowToTop roubava o foco do jogo a cada clique).
            window.Activated += (_, _) =>
            {
                if (_settings.ShowMissions && _missionsWindow != null && _missionsWindow.IsVisible)
                {
                    try
                    {
                        var hwnd = new System.Windows.Interop.WindowInteropHelper(_missionsWindow).Handle;
                        SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }
                    catch { }
                }
            };
            // Minimizar/restaurar a flutuante (ex.: clique no ícone da barra de tarefas) esconde/restaura a Missões.
            // Só oculta/exibe a janela visual — _settings.ShowMissions (preferência do usuário) é mantida.
            window.StateChanged += (_, _) =>
            {
                if (window.WindowState == WindowState.Minimized || !window.IsVisible)
                {
                    _missionsWindow?.Hide();
                    return;
                }
                if (window.WindowState == WindowState.Normal
                    && _settings.ShowMissions
                    && _missionsWindow != null
                    && !_missionsWindow.IsVisible)
                {
                    _missionsWindow.Show();
                }
            };
        }

        // Restaura a preferência "Sempre no topo" da flutuante (A) e garante foco.
        window.Topmost = _settings.FloatingTopmost;
        window.Activate();
        SyncFloatingControls();
    }

    private void SyncFloatingControls()
    {
        var core = _floatingView?.CoreWebView2;
        if (core == null) return;
        var missions = _settings.ShowMissions.ToString().ToLowerInvariant();
        var topmost = _settings.FloatingTopmost.ToString().ToLowerInvariant();
        var script = $"(()=>{{const m=document.querySelector('#lol-idle-missions'); if(m){{m.classList.toggle('active',{missions});}} const p=document.querySelector('#lol-idle-pin'); if(p){{p.classList.toggle('active',{topmost}); p.title={topmost}?'Desativar janela sempre no topo':'Ativar janela sempre no topo';}} }})()";
        try { _ = core.ExecuteScriptAsync(script); } catch { }
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
    internal static void LogFloatingWindowEvent(string message)
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

    // Cria (se necessário) e retorna a WebView da conta indicada.
    // A conta padrão reutiliza a sessão salva (\LoLIdleDesktop\WebView2).
    // As demais ganham sessão isolada em \WebView2\Contas\<slug>.
    private async Task<Microsoft.Web.WebView2.Wpf.WebView2> EnsureAccountViewAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Chave de conta vazia.", nameof(key));

        if (_accountViews.TryGetValue(key, out var existing))
            return existing;

        bool isDefault = string.Equals(key, DefaultAccountName, StringComparison.OrdinalIgnoreCase);

        var view = new Microsoft.Web.WebView2.Wpf.WebView2
        {
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(23, 26, 33),
            Visibility = Visibility.Collapsed
        };
        AccountViewHost.Children.Add(view);
        _accountViews[key] = view;
        if (isDefault)
            GameView = view;

        try
        {
            CoreWebView2Environment env;
            if (isDefault)
            {
                env = _webViewEnvironment
                    ?? throw new InvalidOperationException("Ambiente WebView2 padrão não inicializado.");
            }
            else
            {
                string baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LoLIdleDesktop", "WebView2", "Contas");
                string folder = Path.Combine(baseDir, Slugify(key));
                Directory.CreateDirectory(folder);

                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = AccountBrowserArguments,
                    AreBrowserExtensionsEnabled = true // idem: extensão Hunt sob demanda
                };
                env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: folder, options: options);
            }
            _accountEnvs[key] = env;

            await view.EnsureCoreWebView2Async(env);
            await ConfigurarAccountViewAsync(view, key);
            view.Source = new Uri(GameUrl);

            return view;
        }
        catch
        {
            _accountViews.Remove(key);
            _accountEnvs.Remove(key);
            AccountViewHost.Children.Remove(view);
            if (isDefault && ReferenceEquals(GameView, view))
                GameView = null;
            try { view.Dispose(); } catch { }
            throw;
        }
    }

    // Aplica a todas as contas: scripts de áudio/notificação, handlers e settings.
    private async Task ConfigurarAccountViewAsync(Microsoft.Web.WebView2.Wpf.WebView2 view, string key)
    {
        var core = view.CoreWebView2;
        if (core == null)
            throw new InvalidOperationException("CoreWebView2 indisponível após EnsureCoreWebView2Async.");

        await core.AddScriptToExecuteOnDocumentCreatedAsync(AudioBootstrapScript);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(TrayNotifyScript);
        core.WebMessageReceived += (_, args) =>
        {
            try
            {
                var msg = args.TryGetWebMessageAsString();
                if (msg == "lol-idle-control:missions")
                {
                    LogFloatingWindowEvent($"Missões: toggle pedido (de {_settings.ShowMissions} para {!_settings.ShowMissions}).");
                    _settings.ShowMissions = !_settings.ShowMissions;
                    SaveWindowSettings();
                    if (_settings.ShowMissions)
                    {
                        LogFloatingWindowEvent("Missões: exibindo janela de Missões.");
                        ShowMissions();
                    }
                    else
                    {
                        LogFloatingWindowEvent("Missões: ocultando janela de Missões.");
                        _missionsWindow?.Hide();
                    }
                    _ = core.ExecuteScriptAsync($"document.querySelector('#lol-idle-missions')?.classList.toggle('active', {_settings.ShowMissions.ToString().ToLower()});");
                }
                else if (msg != null && msg.StartsWith("lol-idle-notify:"))
                    HandleNotification(key, msg["lol-idle-notify:".Length..]);
                else if (msg == "lol-idle-control:hunt")
                    ToggleHuntFromControl();
            }
            catch (InvalidOperationException)
            {
                LogFloatingWindowEvent("Missões: InvalidOperationException no handler da principal (provável chamada fora da UI thread).");
            }
            catch (Exception ex)
            {
                LogFloatingWindowEvent($"Missões: exceção inesperada no handler da principal: {ex.GetType().Name}: {ex.Message}");
            }
        };

        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsZoomControlEnabled = true;

        core.NavigationStarting += (_, args) =>
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
        };

        core.NavigationCompleted += (_, args) =>
        {
            if (args.IsSuccess)
            {
                _ = ApplyAudioSettingsAsync();
                if (key != null)
                    _ = RefreshAccountLabelAsync(key);
            }
        };

        core.NewWindowRequested += (_, args) =>
        {
            HandleNewWindowRequested(args);
        };

        // Se a extensão Hunt já estiver extraída, garante que ESTE profile também a tem.
        // (Este é o ponto único por onde passam TODOS os ambientes de conta + principal.)
        try
        {
            if (HuntExtensionInstaller.IsExtensionExtracted())
                await EnsureHuntInAllProfilesAsync();
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"HuntBootstrap ({key}): {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Alterna visibilidade: só a conta ativa fica visível; as demais colapsadas (continuam rodando).
    private void ApplyAccountVisibility()
    {
        foreach (var kvp in _accountViews)
            kvp.Value.Visibility = string.Equals(kvp.Key, _settings.ContaAtiva, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    // Alterna para a conta indicada, criando a WebView sob demanda na primeira troca.
    private async Task SwitchAccountAsync(string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                LogFloatingWindowEvent("SwitchAccountAsync: chave vazia — ignorado");
                return;
            }
            await EnsureAccountViewAsync(key);
            _settings.ContaAtiva = key;
            _contasNaoLidas.Remove(key);
            var itensConta = _ultimasNotificacoes
                .Where(n => string.Equals(n.Conta, key, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (itensConta.Count > 0)
                await RemoverNotificacoesComAnimacaoAsync(itensConta);
            ApplyAccountVisibility();
            SaveWindowSettings();

            // Nunca queremos flutuante com dados da conta anterior: fecha e descarta/recria
            if (_floatingWindow != null)
            {
                try { _floatingWindow.Close(); } catch { }
            }

            // O Hunt usa o environment da conta ativa; trocar de conta invalida a janela.
            if (_huntWindow != null)
            {
                try { _huntWindow.Close(); } catch { }
                _huntWindow = null;
            }

            _ = ApplyAudioSettingsAsync();
            RestartPreparedFloating();
            _lastMissionsJson = null;
            await AtualizarContaConectadaUIAsync();
            if (_accountViews.TryGetValue(key, out var view))
                view.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Não foi possível alternar para a conta.\n\n" + ex.Message,
                "Trocar conta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    // Nome de pasta seguro para a partição da conta (estável: baseado na chave, não no apelido).
    private static string Slugify(string value)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_') sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        while (s.Contains("--")) s = s.Replace("--", "-");
        return string.IsNullOrWhiteSpace(s) ? "conta" : s;
    }

    // ===== Consumo total + RAM/CPU por conta + limpeza de cache =====

    private void IniciarUsoRecursosTimer()
    {
        try
        {
            if (_usoTimer != null) return;
            _usoCts ??= new CancellationTokenSource();
            _usoTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _usoTimer.Tick += UsoTimer_Tick;
            _usoTimer.Start();
            // Coleta inicial imediata (também fora da UI thread).
            UsoTimer_Tick(null, EventArgs.Empty);
        }
        catch { }
    }

    // Tick do timer de uso: coleta em background e só atualiza a UI ao voltar.
    // Se um tick anterior ainda estiver rodando (ou o app estiver saindo), ignora.
    private async void UsoTimer_Tick(object? sender, EventArgs e)
    {
        if (_usoColetando || _usoEncerrando) return;
        _usoColetando = true;
        try
        {
            var contas = ObterContasOrdem();
            var ct = _usoCts?.Token ?? CancellationToken.None;
            var resultado = await ColetarUsoAsync(contas, ct);
            if (_usoEncerrando) return;
            AtualizarSidebarComUso(resultado);
            AtualizarFooterBarCom(resultado);
        }
        catch (OperationCanceledException)
        {
            // Saída do app: coleta cancelada — não atualiza nada.
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"UsoRecursos: falha: {ex.Message}");
        }
        finally
        {
            _usoColetando = false;
        }
    }

    private List<string> ObterContasOrdem()
    {
        var contas = new List<string> { DefaultAccountName };
        if (_settings.ContasAdicionadas != null)
        {
            foreach (var c in _settings.ContasAdicionadas)
            {
                if (!string.IsNullOrWhiteSpace(c) &&
                    !contas.Contains(c, StringComparer.OrdinalIgnoreCase))
                    contas.Add(c);
            }
        }
        return contas;
    }

    private Dictionary<string, List<int>> ColetarPidsPorConta(List<string> contas)
    {
        var resultado = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in contas)
            resultado[c] = new List<int>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='msedgewebview2.exe'");
            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    string? cmd = obj["CommandLine"] as string;
                    if (string.IsNullOrWhiteSpace(cmd)) continue;
                    int pid = Convert.ToInt32(obj["ProcessId"], System.Globalization.CultureInfo.InvariantCulture);

                    // Conta principal: pasta WebView2 do app, mas NÃO a de contas extras.
                    if (cmd.Contains(@"LoLIdleDesktop\WebView2", StringComparison.OrdinalIgnoreCase) &&
                        !cmd.Contains(@"\WebView2\Contas\", StringComparison.OrdinalIgnoreCase))
                    {
                        resultado[DefaultAccountName].Add(pid);
                        continue;
                    }

                    // Conta extra: casa pelo slug da pasta usada na criação (Slugify)
                    // e também pelo AvatarSlug, para cobrir chaves com caracteres especiais.
                    foreach (var conta in contas)
                    {
                        if (string.Equals(conta, DefaultAccountName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        string slugCriacao = Slugify(conta);
                        string slugAvatar = AvatarSlug(conta);
                        bool match =
                            cmd.Contains(@"\WebView2\Contas\" + slugCriacao, StringComparison.OrdinalIgnoreCase) ||
                            cmd.Contains(@"\WebView2\Contas\" + slugAvatar, StringComparison.OrdinalIgnoreCase);
                        if (match)
                        {
                            resultado[conta].Add(pid);
                            break;
                        }
                    }
                }
                catch { }
                finally { obj.Dispose(); }
            }
        }
        catch { }

        return resultado;
    }

    // Coleta fora da UI thread: WMI (Win32_Process) + GetProcessById + delta de CPU.
    private async Task<ContasUsoResultado> ColetarUsoAsync(List<string> contas, CancellationToken ct)
    {
        return await Task.Run(() => ColetarUsoInterno(contas, ct), ct).ConfigureAwait(false);
    }

    private ContasUsoResultado ColetarUsoInterno(List<string> contas, CancellationToken ct)
    {
        var pidsPorConta = ColetarPidsPorConta(contas);
        ct.ThrowIfCancellationRequested();
        var agora = DateTime.UtcNow;
        bool primeira = _ultimoCpuSample == DateTime.MinValue;
        double segundos = primeira ? 0 : (agora - _ultimoCpuSample).TotalSeconds;
        double segundosValidos = segundos > 0.2 ? segundos : 0;
        int nucleos = Math.Max(1, Environment.ProcessorCount);

        var novosCpu = new Dictionary<int, TimeSpan>();
        var resultado = new ContasUsoResultado();

        // Nota sobre WorkingSet64: ele soma páginas compartilhadas entre processos
        // WebView2 (ex.: chrome.dll, DLLs do Edge), portanto reflete o footprint de
        // memória total do aplicativo — não memória física exclusiva por processo.
        // É a métrica intencional aqui; não trocar por PrivateMemorySize64.
        // Acumuladores para o total global (WebViews + processo principal),
        // usado tanto pelo rodapé quanto pela linha "Total" do painel.
        long totalRamGlobal = 0;
        double totalCpuMsGlobal = 0;
        bool algumComBaseGlobal = false;

        foreach (var conta in contas)
        {
            ct.ThrowIfCancellationRequested();
            long ram = 0;
            double cpuSomaMs = 0;
            bool algumComBase = false;

            foreach (var pid in pidsPorConta[conta])
            {
                try
                {
                    using var proc = System.Diagnostics.Process.GetProcessById(pid);
                    ram += proc.WorkingSet64;
                    TimeSpan cpuNow = proc.TotalProcessorTime;
                    novosCpu[pid] = cpuNow;
                    if (segundosValidos > 0 &&
                        _ultimoCpu.TryGetValue(pid, out var anterior))
                    {
                        double deltaMs = (cpuNow - anterior).TotalMilliseconds;
                        if (deltaMs < 0) deltaMs = 0;
                        cpuSomaMs += deltaMs;
                        algumComBase = true;
                    }
                }
                catch { }
            }

            totalRamGlobal += ram;
            if (segundosValidos > 0)
            {
                totalCpuMsGlobal += cpuSomaMs;
                if (algumComBase) algumComBaseGlobal = true;
            }

            double? cpu = null;
            if (segundosValidos > 0)
                cpu = algumComBase
                    ? Math.Clamp(cpuSomaMs / (segundosValidos * 1000.0) / nucleos * 100.0, 0, 100)
                    : 0.0;

            resultado.Contas.Add(new ContasUsoEntry { Conta = conta, RamBytes = ram, CpuPercent = cpu });
        }

        // Processo principal do app (tudo que não é WebView2): entra apenas no total
        // global (linha "Total" do painel e barra de status), nunca nas linhas por conta.
        // novosCpu[self.Id] é registrado em um único lugar — aqui — e cobre o processo
        // principal no mesmo conjunto de CPUs usado pelo total agregado.
        try
        {
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            long selfRam = self.WorkingSet64;
            TimeSpan selfCpuNow = self.TotalProcessorTime;
            totalRamGlobal += selfRam;
            novosCpu[self.Id] = selfCpuNow;
            if (segundosValidos > 0 && _ultimoCpu.TryGetValue(self.Id, out var selfPrev))
            {
                double selfDeltaMs = (selfCpuNow - selfPrev).TotalMilliseconds;
                if (selfDeltaMs < 0) selfDeltaMs = 0;
                totalCpuMsGlobal += selfDeltaMs;
                algumComBaseGlobal = true;
            }
        }
        catch { }

        ct.ThrowIfCancellationRequested();
        resultado.TotalRamBytes = totalRamGlobal;
        resultado.FooterCpuPercent = (segundosValidos > 0 && algumComBaseGlobal)
            ? Math.Clamp(totalCpuMsGlobal / (segundosValidos * 1000.0) / nucleos * 100.0, 0, 100)
            : null;

        _ultimoCpu.Clear();
        foreach (var kv in novosCpu)
            _ultimoCpu[kv.Key] = kv.Value;
        _ultimoCpuSample = agora;

        return resultado;
    }

    private static string FormatarRam(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        return mb >= 1024
            ? $"{mb / 1024.0:0.0} GB"
            : $"{mb:0} MB";
    }

    // RAM do bloco "Consumo total", sempre em pt-BR (vírgula decimal).
    private static string FormatarRamPtBr(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        return mb >= 1024
            ? $"{(mb / 1024.0).ToString("0.0", PtBrCulture)} GB"
            : $"{mb.ToString("0", PtBrCulture)} MB";
    }

    // Bytes liberados pelo limpar cache (pt-BR). Nunca dizer "RAM liberada".
    private static string FormatarBytesCachePtBr(long bytes)
    {
        double gb = bytes / (1024.0 * 1024.0 * 1024.0);
        if (gb >= 1) return $"{gb.ToString("0.0", PtBrCulture)} GB";
        double mb = bytes / (1024.0 * 1024.0);
        return $"{mb.ToString("0", PtBrCulture)} MB";
    }

    private static string FormatarCpuPtBr(double percent)
        => $"{percent.ToString("0.#", PtBrCulture)}%";

    // Cria a linha visual do painel (mesma aparência de sempre) e devolve
    // referências aos TextBlocks para que os próximos ticks só atualizem texto.
    private LinhaUsoVisual CriarLinhaUsoVisual(string nome, string ram, string cpu, bool negrito, bool destaque, string? tooltip = null)
    {
        var grid = new Grid
        {
            Margin = negrito ? new Thickness(0, 4, 0, 2) : new Thickness(0, 1, 0, 1),
            ToolTip = tooltip
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nomeTb = new TextBlock
        {
            Text = nome,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            FontWeight = negrito ? FontWeights.Bold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var valorTb = new TextBlock
        {
            Text = $"{ram} · {cpu}",
            FontSize = 10,
            Foreground = new SolidColorBrush(destaque
                ? Color.FromRgb(0xE8, 0xCF, 0x8F)
                : Color.FromRgb(0x93, 0xA0, 0xB4)),
            FontWeight = negrito ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        Grid.SetColumn(valorTb, 1);
        grid.Children.Add(nomeTb);
        grid.Children.Add(valorTb);
        return new LinhaUsoVisual { Root = grid, Nome = nomeTb, Valor = valorTb };
    }

    private void AtualizarFooterBar()
    {
        try
        {
            if (FooterCpuText != null)
            {
                FooterCpuText.Text = _footerCpuPercent.HasValue
                    ? $"{_footerCpuPercent.Value:0.0}%"
                    : "—";

                if (FooterCpuFill != null)
                {
                    double pctCpu = _footerCpuPercent.HasValue
                        ? Math.Clamp(_footerCpuPercent.Value, 0, 100)
                        : 0;
                    FooterCpuFill.Width = pctCpu * 26.0 / 100.0;
                }
            }

            if (FooterRamText != null)
            {
                FooterRamText.Text = FormatarRam(_footerRamBytes);
            }
        }
        catch { }
    }

    // Atualiza a barra de status a partir da coleta (roda na UI thread, sem coleta).
    private void AtualizarFooterBarCom(ContasUsoResultado resultado)
    {
        _footerRamBytes = resultado.TotalRamBytes;
        _footerCpuPercent = resultado.FooterCpuPercent;
        AtualizarFooterBar();
    }

    // Garante que a estrutura do painel corresponde às contas atuais;
    // só recria os Grids quando a quantidade/ordem de contas muda.
    // Nos demais ticks, apenas os textos são atualizados.
    private void GarantirEstruturaSidebar(List<string> contas)
    {
        if (ContasUsoList == null) return;

        bool mesmaEstrutura =
            _usoLinhasVisual.Count == contas.Count &&
            _usoLinhasVisual.Zip(contas, (linha, conta) =>
                string.Equals(linha.Root.Tag as string, conta, StringComparison.OrdinalIgnoreCase))
                .All(ok => ok);

        if (mesmaEstrutura) return;

        ContasUsoList.Children.Clear();
        _usoLinhasVisual.Clear();

        foreach (var conta in contas)
        {
            var linha = CriarLinhaUsoVisual(
                GetAccountDisplayName(conta),
                FormatarRam(0),
                "—",
                negrito: false,
                destaque: false);
            linha.Root.Tag = conta;
            _usoLinhasVisual.Add(linha);
            ContasUsoList.Children.Add(linha.Root);
        }
    }

    // Atualiza apenas os textos dos TextBlocks já existentes (nada de Clear/Add por tick).
    private void AtualizarSidebarComUso(ContasUsoResultado resultado)
    {
        try
        {
            if (ContasUsoList == null) return;

            var contas = resultado.Contas.Select(c => c.Conta).ToList();
            GarantirEstruturaSidebar(contas);

            for (int i = 0; i < resultado.Contas.Count && i < _usoLinhasVisual.Count; i++)
            {
                var entrada = resultado.Contas[i];
                var linha = _usoLinhasVisual[i];
                linha.Nome.Text = GetAccountDisplayName(entrada.Conta);
                linha.Valor.Text = $"{FormatarRam(entrada.RamBytes)} · {(entrada.CpuPercent.HasValue ? $"{entrada.CpuPercent.Value:0.0}%" : "—")}";
            }

            // Total do app (WebViews + processo principal): reflete no bloco "Consumo total".
            AtualizarConsumoTotalVisual(resultado.TotalRamBytes, resultado.FooterCpuPercent);
        }
        catch { }
    }

    // ===== Consumo total: bloco "Consumo total" (estado normal / resultado da limpeza) =====

    // Atualiza CPU/RAM, barra e pill no mesmo tick (5s) da coleta.
    private void AtualizarConsumoTotalVisual(long totalRamBytes, double? cpuPercent)
    {
        try
        {
            var brushIce    = System.Windows.Application.Current.Resources["AccentIce"] as SolidColorBrush
                              ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FD1D9"));
            var brushDanger = System.Windows.Application.Current.Resources["Danger"]    as SolidColorBrush
                              ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E05B4B"));

            if (ConsumoCpuText != null)
            {
                ConsumoCpuText.Text = cpuPercent.HasValue
                    ? FormatarCpuPtBr(cpuPercent.Value)
                    : "—";
            }

            if (ConsumoRamText != null)
                ConsumoRamText.Text = FormatarRamPtBr(totalRamBytes);

            double metaRamBytes = MetaRamBytes();
            double ratio = Math.Clamp(totalRamBytes / metaRamBytes, 0, 1);
            bool acimaDaMeta = totalRamBytes > metaRamBytes;

            if (ConsumoMetaRefText != null)
                ConsumoMetaRefText.Text = $"Meta de referência: {FormatarRamPtBr((long)metaRamBytes)}";

            if (ConsumoRamFillScale != null)
                ConsumoRamFillScale.ScaleX = ratio;
            if (ConsumoRamFill != null)
                ConsumoRamFill.Background = acimaDaMeta ? brushDanger : brushIce;

            if (ConsumoMetaPill != null && ConsumoMetaPillText != null)
            {
                if (acimaDaMeta)
                {
                    ConsumoMetaPill.Background = new SolidColorBrush(Color.FromArgb(0x33, brushDanger.Color.R, brushDanger.Color.G, brushDanger.Color.B));
                    ConsumoMetaPillText.Foreground = brushDanger;
                    ConsumoMetaPillText.Text = "Acima da meta";
                }
                else
                {
                    ConsumoMetaPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2BD46A"));
                    ConsumoMetaPillText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2BD46A"));
                    ConsumoMetaPillText.Text = "Normal";
                }
            }
        }
        catch { }
    }

    // Troca para o estado "Limpeza de cache" (substitui temporariamente o estado normal).
    private void MostrarConsumoTotalResult(string bytesLiberados, int falhas)
    {
        try
        {
            if (ConsumoResultValorText != null)
                ConsumoResultValorText.Text = bytesLiberados;
            if (ConsumoResultNotaText != null)
            {
                ConsumoResultNotaText.Text = falhas > 0
                    ? $"Cookies, logins e progresso preservados.\n{falhas} pasta(s) em uso não puderam ser limpas."
                    : "Cookies, logins e progresso preservados.";
            }
            if (ConsumoTotalNormal != null) ConsumoTotalNormal.Visibility = Visibility.Collapsed;
            if (ConsumoTotalResult != null) ConsumoTotalResult.Visibility = Visibility.Visible;

            _consumoRevertTimer ??= new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _consumoRevertTimer.Tick -= ConsumoRevertTimer_Tick;
            _consumoRevertTimer.Tick += ConsumoRevertTimer_Tick;
            _consumoRevertTimer.Stop();
            _consumoRevertTimer.Start();
        }
        catch { }
    }

    private void ConsumoRevertTimer_Tick(object? sender, EventArgs e)
    {
        try { _consumoRevertTimer?.Stop(); } catch { }
        VoltarConsumoTotalNormal();
    }

    private void VoltarConsumoTotalNormal()
    {
        try
        {
            if (ConsumoTotalResult != null) ConsumoTotalResult.Visibility = Visibility.Collapsed;
            if (ConsumoTotalNormal != null) ConsumoTotalNormal.Visibility = Visibility.Visible;
        }
        catch { }
    }

    private void ConsumoResultOk_Click(object sender, RoutedEventArgs e)
    {
        try { _consumoRevertTimer?.Stop(); } catch { }
        VoltarConsumoTotalNormal();
    }

    private static List<string> EnumerarPastasCache(string root)
    {
        var pastas = new List<string>();
        string[] nomes = { "Cache", "Code Cache", "GPUCache" };

        void Adicionar(string defaultDir)
        {
            foreach (var nome in nomes)
            {
                string p = Path.Combine(defaultDir, nome);
                if (Directory.Exists(p))
                    pastas.Add(p);
            }
        }

        // Conta principal: <root>\EBWebView\Default\{Cache,Code Cache,GPUCache}
        Adicionar(Path.Combine(root, "EBWebView", "Default"));

        // Contas extras: <root>\Contas\<slug>\EBWebView\Default\{...}
        try
        {
            string contasDir = Path.Combine(root, "Contas");
            if (Directory.Exists(contasDir))
            {
                foreach (var dir in Directory.EnumerateDirectories(contasDir))
                    Adicionar(Path.Combine(dir, "EBWebView", "Default"));
            }
        }
        catch { }

        return pastas;
    }

    private static long CalcularTamanho(string pasta)
    {
        long total = 0;
        var stack = new Stack<string>();
        stack.Push(pasta);

        while (stack.Count > 0)
        {
            string atual = stack.Pop();
            try
            {
                foreach (var arquivo in Directory.EnumerateFiles(atual))
                {
                    try { total += new FileInfo(arquivo).Length; } catch { }
                }
                foreach (var dir in Directory.EnumerateDirectories(atual))
                    stack.Push(dir);
            }
            catch { }
        }

        return total;
    }

    // Resultado da limpeza de cache (executada fora da UI thread).
    private sealed class LimpezaCacheResultado
    {
        public long Bytes { get; init; }
        public int Removidas { get; init; }
        public int Falhas { get; init; }
    }

    private static LimpezaCacheResultado ExecutarLimpezaCache(List<string> pastas)
    {
        long tamanhoAntesTotal = 0;
        int removidas = 0;
        int falhas = 0;

        foreach (var pasta in pastas)
        {
            try
            {
                if (!Directory.Exists(pasta))
                {
                    LogFloatingWindowEvent($"LimparCache: pasta sumiu antes da limpeza, ignorada: {pasta}");
                    continue;
                }

                long tamanho = CalcularTamanho(pasta);
                LogFloatingWindowEvent($"LimparCache: {pasta} ~{tamanho / (1024.0 * 1024.0):0.0} MB — tentando remover");
                Directory.Delete(pasta, recursive: true);
                tamanhoAntesTotal += tamanho;
                removidas++;
                LogFloatingWindowEvent($"LimparCache: Directory.Delete OK em {pasta} ({tamanho / (1024.0 * 1024.0):0.0} MB)");
            }
            catch (Exception ex)
            {
                // Pasta em uso pelo WebView2 (arquivos travados): registra e segue para as demais.
                falhas++;
                LogFloatingWindowEvent($"LimparCache: falha ao deletar {pasta}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return new LimpezaCacheResultado
        {
            Bytes = tamanhoAntesTotal,
            Removidas = removidas,
            Falhas = falhas
        };
    }

    private async void LiberarEspaco_Click(object sender, RoutedEventArgs e)
    {
        bool confirmar = MostrarConfirmacaoRemover(
            "Limpar cache?",
            "Limpar o cache do WebView2?",
            "Isso remove só o cache (arquivos temporários do WebView2). Cookies, sessão e progresso do jogo ficam intactos.",
            "Limpar cache");
        if (!confirmar) return;

        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LoLIdleDesktop", "WebView2");

        var pastas = EnumerarPastasCache(root);
        LogFloatingWindowEvent($"LimparCache: root={root}; pastas encontradas={pastas.Count}; varridas: {string.Join(" | ", pastas)}");

        if (pastas.Count == 0)
        {
            LogFloatingWindowEvent("LimparCache: nenhuma pasta de cache encontrada (nada a limpar)");
            MostrarMensagemSimples(
                "Limpar cache",
                "Nada a limpar — nenhum cache encontrado (o perfil já está sem arquivos temporários, ou a limpeza anterior já os removeu).",
                "OK",
                MessageKind.Warning);
            return;
        }

        // Desabilita só durante a operação; a UI fica livre (limpeza em background).
        if (LiberarEspacoButton != null) LiberarEspacoButton.IsEnabled = false;
        LimpezaCacheResultado resultado;
        try
        {
            resultado = await Task.Run(() => ExecutarLimpezaCache(pastas));
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"LimparCache: falha inesperada: {ex.GetType().Name}: {ex.Message}");
            resultado = new LimpezaCacheResultado();
        }
        finally
        {
            if (LiberarEspacoButton != null) LiberarEspacoButton.IsEnabled = true;
        }

        LogFloatingWindowEvent($"LimparCache: concluído — liberado {resultado.Bytes / (1024.0 * 1024.0):0.0} MB em {resultado.Removidas} pasta(s); falhas={resultado.Falhas}");

        if (resultado.Removidas == 0 && resultado.Falhas == 0)
        {
            // Nada foi removido e nada falhou: não afirmar sucesso de um não-evento.
            MostrarMensagemSimples(
                "Limpar cache",
                "Nada a limpar — nenhum cache encontrado (o perfil já está sem arquivos temporários, ou a limpeza anterior já os removeu).",
                "OK",
                MessageKind.Warning);
            return;
        }

        if (resultado.Removidas > 0)
        {
            // Estado "Limpeza de cache": volta sozinho para o normal em ~4s (ou via botão Ok).
            MostrarConsumoTotalResult(FormatarBytesCachePtBr(resultado.Bytes), resultado.Falhas);
            return;
        }

        // removidas == 0 e falhas > 0: aviso honesto, sem falar em sucesso.
        MostrarMensagemSimples(
            "Limpar cache",
            $"{resultado.Falhas} pasta(s) não puderam ser limpas (em uso pelo navegador — tente após fechar o jogo).\nNada foi removido.",
            "OK",
            MessageKind.Warning);
    }

    // Próxima chave automática para conta extra: "Conta 2", "Conta 3", ...
    private string NextAccountKey()
    {
        int n = 2;
        while (true)
        {
            string candidate = $"Conta {n}";
            bool used = _settings.ContasAdicionadas.Any(
                c => c.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (!used) return candidate;
            n++;
        }
    }

    // Rótulo exibido de uma conta (nome do jogador lido do DOM, ou provisório)
    private string GetAccountDisplayName(string key)
    {
        var nomes = _settings.ContasNomes;
        if (nomes != null &&
            nomes.TryGetValue(key, out var nome) &&
            !string.IsNullOrWhiteSpace(nome))
            return nome;

        return string.Equals(key, DefaultAccountName, StringComparison.OrdinalIgnoreCase)
            ? DefaultAccountName
            : $"{key} (sem login)";
    }

    // Lê o nome do jogador da conta a partir de um CoreWebView2 específico.
    private async Task<string?> ReadPlayerNameAsync(CoreWebView2? core)
    {
        try
        {
            if (core == null) return null;

            const string script = @"(() => {
                const pegar = (id) => {
                    const el = document.getElementById(id);
                    return el ? (el.textContent || '').trim() : '';
                };
                const cls = (sel) => {
                    const el = document.querySelector(sel);
                    return el ? (el.textContent || '').trim() : '';
                };
                const candidatos = [
                    pegar('st-nome'),
                    cls('.nome-jogador'),
                    pegar('usuario'),
                    cls('.usuario')
                ];
                for (const t of candidatos) {
                    if (t) return t;
                }
                return '';
            })()";

            string raw = await core.ExecuteScriptAsync(script);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string valor = System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? "";
            valor = valor.Trim();
            return string.IsNullOrWhiteSpace(valor) ? null : valor;
        }
        catch
        {
            return null;
        }
    }

    // Indica se a conta tem nome editado manualmente (não deve ser sobrescrito pelo DOM).
    private bool NomePersonalizado(string key)
    {
        var personalizados = _settings.ContasNomesPersonalizados;
        return personalizados != null &&
               personalizados.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    // Após navegação/troca, lê o nome do jogador e atualiza o chip dinamicamente.
    // Se vazio (não logada), mantém o rótulo provisório ("Conta N (sem login)").
    private async Task RefreshAccountLabelAsync(string key)
    {
        try
        {
            if (!_accountViews.TryGetValue(key, out var view)) return;

            // O jogo é uma SPA; o nome pode demorar para aparecer no DOM.
            await Task.Delay(2000);
            var name = await ReadPlayerNameAsync(view.CoreWebView2);
            if (string.IsNullOrWhiteSpace(name))
            {
                await Task.Delay(3000);
                name = await ReadPlayerNameAsync(view.CoreWebView2);
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                // Deslogado/sem nome: remove o rótulo antigo -> chip volta ao provisório
                // (nome editado manualmente é preservado)
                if (NomePersonalizado(key))
                    return;
                var nomesAtual = _settings.ContasNomes;
                if (nomesAtual != null && nomesAtual.Remove(key))
                {
                    SaveWindowSettings();
                    _ = AtualizarContaConectadaUIAsync();
                }
                return;
            }
            if (!_accountViews.ContainsKey(key)) return;
            if (NomePersonalizado(key)) return; // nome editado manualmente tem prioridade sobre o DOM

            var nomes = _settings.ContasNomes ??= new Dictionary<string, string>();
            string? current = nomes.TryGetValue(key, out var n) ? n : null;
            if (!string.Equals(current, name, StringComparison.Ordinal))
            {
                nomes[key] = name!;
                QueueWindowSettingsSave();
                await AtualizarContaConectadaUIAsync();
            }
        }
        catch { }
    }

    // Expostos para a HuntWindow (mesmo assembly).
    internal bool HuntTopmostSetting => _settings.HuntTopmost;
    internal string? HuntExtensionIdSetting => _settings.HuntExtensionId;

    // Ambiente compartilhado pelo WebView da conta ativa (para a flutuante).
    private CoreWebView2Environment GetActiveAccountEnvironment()
    {
        var key = _settings.ContaAtiva;
        if (_accountEnvs.TryGetValue(key, out var env))
            return env;
        return _webViewEnvironment
            ?? throw new InvalidOperationException("Ambiente WebView2 padrão não inicializado.");
    }

    // Botão 🏹 Hunt na barra de título: abre/fecha o painel da extensão.
    private void HuntTitle_Click(object sender, RoutedEventArgs e) => ToggleHuntFromControl();

    // Clique no botão Hunt (barra de título): abre/fecha a janela.
    private async void ToggleHuntFromControl()
    {
        try { await ToggleHuntWindowAsync(); }
        catch (Exception ex) { LogFloatingWindowEvent($"Hunt: erro ao alternar a janela — {ex.GetType().Name}: {ex.Message}"); }
    }

    // Alterna a janela Hunt; se a conta ativa mudou desde a criação, descarta e recria.
    private async Task ToggleHuntWindowAsync()
    {
        if (_huntInstalling)
        {
            MostrarMensagemSimples(
                "Hunt for LoL Idle",
                "Ainda estou instalando a extensão, tenta de novo daqui a pouco.",
                "OK",
                MessageKind.Info);
            return;
        }

        bool precisaInstalar = !HuntExtensionInstaller.IsExtensionExtracted() ||
                               string.IsNullOrWhiteSpace(_settings.HuntExtensionId);
        if (precisaInstalar)
        {
            bool confirmar = MostrarConfirmacaoRemover(
                "Instalar Hunt for LoL Idle?",
                "O app vai baixar a extensão da Chrome Web Store (~270 KB) e instalar no perfil do app.",
                "Acontece só uma vez e o painel lê só a sessão do jogo deste app.",
                "Instalar");
            if (!confirmar) return;

            _huntInstalling = true;
            (bool ok, string? error) resultado;
            try { resultado = await InstallHuntExtensionAsync(); }
            finally { _huntInstalling = false; }

            if (!resultado.ok)
            {
                LogFloatingWindowEvent($"HuntInstall: falha — {resultado.error}");
                MostrarMensagemSimples(
                    "Não deu",
                    resultado.error ?? "Não foi possível instalar a extensão.",
                    "OK",
                    MessageKind.Error);
                return;
            }
        }
        else
        {
            // Pasta extraída + ID conhecido: confere que o profile atual tem a extensão
            // (chamada barata; se já tiver, não faz nada).
            await EnsureHuntInAllProfilesAsync();
        }

        var env = GetActiveAccountEnvironment();
        if (_huntWindow != null)
        {
            if (ReferenceEquals(_huntWindow.SharedEnvironment, env))
            {
                if (_huntWindow.IsVisible)
                {
                    try { _huntWindow.Close(); } catch { }
                    return;
                }
                _huntWindow.Show();
                _huntWindow.Activate();
                return;
            }
            // A conta ativa mudou desde a criação: o ambiente é outro — recria a janela.
            try { _huntWindow.Close(); } catch { }
            _huntWindow = null;
        }

        Window? owner = (_floatingWindow != null && _floatingWindow.IsVisible) ? _floatingWindow : (IsVisible ? this : null);
        _huntWindow = new HuntWindow(this, env, owner);
        _huntWindow.Closed += (_, _) => _huntWindow = null;
        _huntWindow.Show();
        _huntWindow.Activate();
    }

    // Fluxo completo: extrai (se preciso) e instala em TODOS os perfis criados.
    private async Task<(bool ok, string? error)> InstallHuntExtensionAsync()
    {
        LogFloatingWindowEvent("HuntInstall: iniciando instalação sob pedido.");
        if (!HuntExtensionInstaller.IsExtensionExtracted())
        {
            var extraida = await HuntExtensionInstaller.DownloadAndExtractAsync();
            if (!extraida.ok)
                return (false, extraida.error ?? "Falha ao baixar a extensão.");
        }
        return await EnsureHuntInAllProfilesAsync();
    }

    // Fluxo completo FORÇADO: baixa a versão da loja de novo (mesmo que a atual pareça OK),
    // extrai, sanitiza e re-registra em todos os perfis (removendo instalações obsoletas).
    // Usado pelo botão "↻ Atualizar" do painel do Hunt.
    internal async Task<(bool ok, string? error)> UpdateHuntExtensionAsync()
    {
        LogFloatingWindowEvent("HuntUpdate: forçando ciclo completo de atualização.");
        var extraida = await HuntExtensionInstaller.DownloadAndExtractAsync();
        if (!extraida.ok)
            return (false, extraida.error ?? "Falha ao baixar a extensão.");
        return await EnsureHuntInAllProfilesAsync();
    }

    // Adiciona a extensão aos perfis de TODAS as WebViews já criadas (conta principal + contas).
    // Antes de instalar, remove instalações obsoletas (ex.: 0.8.3 com "notifications") via RemoveAsync.
    // Perfis que já têm a extensão atualizada e nada obsoleto não são mexidos.
    private async Task<(bool ok, string? error)> EnsureHuntInAllProfilesAsync()
    {
        await _huntInstallLock.WaitAsync();
        try
        {
            string? ultimoErro = null;
            bool algumOk = false;
            foreach (var kvp in _accountViews)
            {
                var core = kvp.Value.CoreWebView2;
                if (core == null)
                    continue;
                try
                {
                    await EnsureHuntInProfileCoreAsync(core);
                    algumOk = true;
                }
                catch (Exception ex)
                {
                    ultimoErro = $"perfil '{kvp.Key}': {ex.Message}";
                    LogFloatingWindowEvent($"HuntInstall: falha ao ativar no perfil '{kvp.Key}' — {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (!algumOk && ultimoErro != null)
                return (false, "Não consegui ativar a extensão nos perfis do jogo. " + ultimoErro);

            LogFloatingWindowEvent($"HuntInstall: extensão ativa nos perfis (id={_settings.HuntExtensionId ?? "?"}).");
            return (true, null);
        }
        finally
        {
            _huntInstallLock.Release();
        }
    }

    // ID conhecido da instalação antiga (0.8.3, com permissão "notifications"), registrada
    // por versões anteriores do app a partir de ...\Extensions\hunt-for-lol-idle\0.8.3.
    // Não adianta apagar a pasta: quem manda no perfil é o WebView2 — por isso a remoção
    // acontece via CoreWebView2BrowserExtension.RemoveAsync().
    private const string LegacyHuntExtensionId = "hkaaipkllejfjgncdpkgbjgedkpkcnho";

    // Identifica uma instalação obsoleta do "Hunt for LoL Idle" no perfil:
    //  1) id legado conhecido (0.8.3);
    //  2) mesmo nome esperado, mas id diferente do que temos salvo — veio de outra
    //     pasta/versão (ex.: instalação antiga que não conhecemos).
    // Sem id salvo, só o id legado é considerado obsoleto (não removemos nada às cegas).
    private static bool IsStaleHuntExtension(CoreWebView2BrowserExtension ext, string? salvoId)
    {
        if (string.Equals(ext.Id, LegacyHuntExtensionId, StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(ext.Name) &&
            string.Equals(ext.Name, HuntExtensionInstaller.ExpectedName, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(salvoId) &&
            !string.Equals(ext.Id, salvoId, StringComparison.Ordinal))
            return true;

        return false;
    }

    // Garante a extensão num profile específico; devolve o ID efetivo da extensão.
    private async Task<string> EnsureHuntInProfileCoreAsync(CoreWebView2 core)
    {
        var profile = core.Profile;
        string? salvo = string.IsNullOrWhiteSpace(_settings.HuntExtensionId) ? null : _settings.HuntExtensionId;

        List<CoreWebView2BrowserExtension>? instaladas = null;
        try
        {
            instaladas = (await profile.GetBrowserExtensionsAsync())?.ToList() ?? new List<CoreWebView2BrowserExtension>();
        }
        catch (Exception ex)
        {
            // Não sabendo listar, tenta adicionar direto (add é idempotente para a mesma pasta).
            LogFloatingWindowEvent($"HuntInstall: GetBrowserExtensionsAsync falhou — {ex.GetType().Name}: {ex.Message}");
        }

        bool salvoRemovido = false;
        var removidas = new HashSet<string>(StringComparer.Ordinal);
        if (instaladas != null)
        {
            // 1) Remove instalações obsoletas ANTES de qualquer coisa (0.8.3 etc.).
            foreach (var ext in instaladas)
            {
                if (!IsStaleHuntExtension(ext, salvo))
                    continue;

                try
                {
                    string nome = string.IsNullOrWhiteSpace(ext.Name) ? "(nome indisponível)" : ext.Name;
                    await ext.RemoveAsync();
                    removidas.Add(ext.Id);
                    if (string.Equals(salvo, ext.Id, StringComparison.Ordinal))
                        salvoRemovido = true;
                    LogFloatingWindowEvent($"Hunt: removida instalação antiga da extensão (id={ext.Id}, nome={nome}) do perfil.");
                }
                catch (Exception ex)
                {
                    // RemoveAsync falhou: a antiga pode continuar ativa aqui. Segue o jogo
                    // e reporta no log — não aborta a instalação da versão sanitizada.
                    LogFloatingWindowEvent($"Hunt: NÃO consegui remover a instalação antiga da extensão (id={ext.Id}) — {ex.GetType().Name}: {ex.Message}. A versão antiga pode continuar ativa neste perfil.");
                }
            }

            if (salvoRemovido)
            {
                // O id salvo apontava para a instalação recém-removida: zera para não
                // reutilizar um id morto. Será regravado com o id novo logo abaixo.
                _settings.HuntExtensionId = null;
                salvo = null;
                SaveWindowSettings();
            }

            // 2) Se, após a limpeza, sobrou uma instalação atual (não obsoleta), reutiliza.
            //    A lista é um snapshot de antes da remoção: exclui explicitamente o que removemos.
            var atual = instaladas.FirstOrDefault(e =>
                !removidas.Contains(e.Id) &&
                string.Equals(e.Name, HuntExtensionInstaller.ExpectedName, StringComparison.OrdinalIgnoreCase));
            if (atual != null)
            {
                PersistHuntId(atual.Id);
                return atual.Id;
            }

            if (instaladas.Count == 0)
            {
                // A API não enxergou nada, mesmo que o perfil tenha a antiga registrada
                // (Secure Preferences). O Add abaixo reinstala a sanitizada; se a antiga
                // sobreviver, ela NÃO some por aqui — fica o aviso no log.
                LogFloatingWindowEvent($"HuntInstall: GetBrowserExtensionsAsync retornou lista vazia neste perfil; se a extensão antiga (id={LegacyHuntExtensionId}) continuar ativa, será preciso removê-la por uma via que o WebView2 exponha.");
            }
        }

        // 3) (Re)instala a versão sanitizada da pasta local.
        //    Fluxo de download/extract fica por conta de InstallHuntExtensionAsync —
        //    aqui só garantimos que a pasta está íntegra antes de registrar.
        if (!HuntExtensionInstaller.IsExtensionExtracted())
            throw new InvalidOperationException("A pasta local da extensão não está extraída/íntegra; o fluxo de instalação (download + extract + sanitize) precisa rodar antes.");

        var adicionada = await profile.AddBrowserExtensionAsync(HuntExtensionInstaller.TargetFolder);
        PersistHuntId(adicionada.Id);
        return adicionada.Id;
    }

    private void PersistHuntId(string id)
    {
        if (!string.Equals(_settings.HuntExtensionId, id, StringComparison.Ordinal))
        {
            _settings.HuntExtensionId = id;
            SaveWindowSettings();
        }
    }

    // Descarta a flutuante pré-inicializada e recria com o ambiente da conta ativa.
    // Só faz isso quando a flutuante não estiver aberta (a aberta continuará com a conta ativa).
    private void RestartPreparedFloating()
    {
        if (_floatingWindow != null)
        {
            LogFloatingWindowEvent("RestartPreparedFloating: ignorado — flutuante aberta; nada a recriar.");
            return; // flutuante aberta: não mexe
        }

        // Preparação em andamento: NÃO fecha o host pela metade nem descarta a
        // recriação silenciosamente. Enfileira uma recriação única (coalesced),
        // aplicada ao final da preparação atual.
        if (_isPreparingFloatingView)
        {
            _prepareRestartRequested = true;
            LogFloatingWindowEvent("RestartPreparedFloating: preparação em andamento; recriação será aplicada ao final dela.");
            return;
        }

        ApplyPreparedFloatingRestart();
    }

    private void ApplyPreparedFloatingRestart()
    {
        _prepareRestartRequested = false;

        if (_preparedFloatingWindow != null)
        {
            var w = _preparedFloatingWindow;
            _preparedFloatingWindow = null;
            _preparedFloatingView = null;
            LogFloatingWindowEvent("RestartPreparedFloating: host preparado descartado; recriando com a conta ativa.");
            try { w.Close(); }
            catch (Exception ex) { LogFloatingWindowEvent($"RestartPreparedFloating: erro ao fechar host preparado — {ex.Message}"); }
        }

        _ = PrepareFloatingWebViewAsync(this, "RestartPreparedFloating");
    }

    // Prepara uma janela invisível antes de qualquer pedido do jogo.
    private async Task PrepareFloatingWebViewAsync(Window? owner, string origin = "?")
    {
        LogFloatingWindowEvent($"Prepare: chamada a partir de {origin}.");
        if (_webViewEnvironment == null)
        {
            LogFloatingWindowEvent("Prepare: ignorado — ambiente WebView2 ainda não inicializado.");
            return;
        }
        if (_isPreparingFloatingView)
        {
            LogFloatingWindowEvent("Prepare: ignorado — preparação já em andamento.");
            return;
        }
        if (_preparedFloatingWindow != null && _preparedFloatingView?.CoreWebView2 != null)
        {
            LogFloatingWindowEvent("Prepare: ignorado — host já preparado.");
            return;
        }

        // Captura a conta ativa no momento da preparação (dona da flutuante/popup no jogo)
        _floatingAccountKey = _settings.ContaAtiva ?? DefaultAccountName;
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
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent // Janela transparente: a moldura arredondada é o Border externo (experimento estilo Missões/diálogos)
            };

            // Chrome invisível para habilitar resize pelas bordas (CaptionHeight=0: o jogo tem o próprio arrasto).
            var chrome = new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(26),
                CornerRadius = new CornerRadius(10),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false,
                NonClientFrameEdges = System.Windows.Shell.NonClientFrameEdges.None
            };
            System.Windows.Shell.WindowChrome.SetWindowChrome(popupWindow, chrome);

            var bgApp = ThemeBrush("BgApp", "#171A21").Color;
            var view = new Microsoft.Web.WebView2.Wpf.WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(bgApp.A, bgApp.R, bgApp.G, bgApp.B)
            };

            // Experimento: janela com AllowsTransparency=true + Background=Transparent,
            // moldura arredondada externa (Border CornerRadius=10) e o WebView2 como
            // filho direto. Risco conhecido: HwndHost em janela layered pode não
            // renderizar/receber input — aguardando teste em runtime.
            var frame = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = ThemeBrush("BgPanel", "#1C2129"),
                BorderBrush = ThemeBrush("BorderSubtle", "#2B313F"),
                BorderThickness = new Thickness(1),
                Child = view
            };

            popupWindow.Content = frame;

            // Força a borda do frame externo do Windows para o tema (DWMWA_BORDER_COLOR, Win11 22000+).
            // Sem isto, o Windows 11 pinta a borda da janela ativa com a cor de acento da sessão.
            popupWindow.SourceInitialized += (_, _) =>
            {
                try
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(popupWindow).Handle;
                    uint cor = 0xFFFFFFFE; // DWMWA_COLOR_NONE: borda invisível
                    int atributo = 34;     // DWMWA_BORDER_COLOR
                    int size = sizeof(uint);
                    int hr = DwmSetWindowAttribute(hwnd, atributo, ref cor, size);
                    // Força cantos arredondados no chrome externo (Win11 22000+).
                    // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2.
                    // Sem isto, a moldura interna arredonda mas o HWND quadrado vaza por fora.
                    uint preferencia = 2; // DWMWCP_ROUND
                    int atributoCanto = 33; // DWMWA_WINDOW_CORNER_PREFERENCE
                    int hrCanto = DwmSetWindowAttribute(hwnd, atributoCanto, ref preferencia, sizeof(uint));
                    if (hrCanto != 0)
                    {
                        LogFloatingWindowEvent($"DWM: DWMWA_WINDOW_CORNER_PREFERENCE não suportado (hr=0x{hrCanto:X8})");
                    }
                    else LogFloatingWindowEvent("DWM: cantos arredondados da janela flutuante aplicados (DWMWCP_ROUND).");

                    if (hr != 0)
                    {
                        LogFloatingWindowEvent($"DWM: DwmSetWindowAttribute(BORDER_COLOR) falhou hr=0x{hr:X8}");
                        LogFloatingWindowEvent("DWM: BORDER_COLOR não suportado neste Windows para janelas sem frame; a borda violeta é imposta pelo sistema.");
                    }
                    else LogFloatingWindowEvent("DWM: borda da janela flutuante ajustada para o tema.");
                }
                catch (Exception ex) { LogFloatingWindowEvent($"DWM: falha ao aplicar BORDER_COLOR — {ex.Message}"); }
            };

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
                    LogFloatingWindowEvent("Prepare: host preparado fechado; referências liberadas.");
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
                            _ = PrepareFloatingWebViewAsync(this, "FloatingWindow.Closed");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else if (wasPreparedWindow)
                {
                    LogFloatingWindowEvent("FloatingWindow: host preparado encerrado sem entrega ao jogo.");
                }
            };

            // Criar e mostrar o host invisível garante que o controle WPF tenha
            // seu HWND antes de inicializar o runtime do WebView2.
            popupWindow.Show();
            await popupWindow.Dispatcher.InvokeAsync(() => { },
                System.Windows.Threading.DispatcherPriority.Loaded);

            LogFloatingWindowEvent("Prepare: antes de EnsureCoreWebView2Async.");
            await view.EnsureCoreWebView2Async(GetActiveAccountEnvironment());
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
                        _settings.FloatingTopmost = popupWindow.Topmost;
                        SaveWindowSettings();
                    }
                    else if (message == "lol-idle-control:close")
                    {
                        popupWindow.Close();
                    }
                    else if (message == "lol-idle-control:hunt")
                    {
                        ToggleHuntFromControl();
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
                        LogFloatingWindowEvent($"Missões: toggle pedido (de {_settings.ShowMissions} para {!_settings.ShowMissions}).");
                        _settings.ShowMissions = !_settings.ShowMissions;
                        SaveWindowSettings();
                        if (_settings.ShowMissions)
                        {
                            LogFloatingWindowEvent("Missões: exibindo janela de Missões.");
                            ShowMissions();
                        }
                        else
                        {
                            LogFloatingWindowEvent("Missões: ocultando janela de Missões.");
                            _missionsWindow?.Hide();
                        }
                        LogFloatingWindowEvent($"Missões: toggle concluído, janela {(_missionsWindow == null ? "nula" : (_missionsWindow.IsVisible ? "visível" : "oculta"))}.");
                        var core2 = _floatingView?.CoreWebView2;
                        if (core2 != null) _ = core2.ExecuteScriptAsync($"document.querySelector('#lol-idle-missions')?.classList.toggle('active', {_settings.ShowMissions.ToString().ToLower()});");
                    }
                    else if (message != null && message.StartsWith("lol-idle-notify:"))
                    {
                        HandleNotification(_floatingAccountKey, message["lol-idle-notify:".Length..]);
                    }
                    else if (message != null && message.StartsWith("lol-idle-missions:"))
                    {
                        UpdateMissions(message["lol-idle-missions:".Length..], fromFloating: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // O ponteiro pode soltar antes de o Windows iniciar o movimento.
                    LogFloatingWindowEvent("Missões/controles: InvalidOperationException no handler da flutuante (provável chamada fora da UI thread).");
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
                LogFloatingWindowEvent("Prepare: host falho descartado; referências liberadas.");
            }
            else if (_preparedFloatingWindow == null)
            {
                LogFloatingWindowEvent("Prepare: host já havia sido descartado por outra chamada durante a preparação.");
            }
            popupWindow?.Close();
        }
        finally
        {
            _isPreparingFloatingView = false;
            // Recriação coalesced solicitada durante a preparação (ex.: troca de
            // conta enquanto o host estava sendo criado). Aplica uma única vez.
            if (_prepareRestartRequested)
            {
                LogFloatingWindowEvent("Prepare: aplicando recriação enfileirada solicitada durante a preparação.");
                ApplyPreparedFloatingRestart();
            }
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

    // Tratamento central de notificação: aplica os filtros das preferências.
    // `key` = chave da conta que gerou o evento; `kind` = tipo do evento.
    private void HandleNotification(string? key, string kind)
    {
        if (!_settings.NotifyEnabled) return;
        // kind é string não-anulável aqui; "potion_hp"/"potion_mana" caem no toggle único "potion".
        string disabledKey = kind.StartsWith("potion", StringComparison.OrdinalIgnoreCase) ? "potion" : kind;
        if (_settings.NotifyTypesDisabled.Contains(disabledKey, StringComparer.OrdinalIgnoreCase)) return;
        if (key != null && _settings.NotifyAccountsDisabled.Contains(key, StringComparer.OrdinalIgnoreCase)) return;

        // App em 1º plano + conta ativa em uso → não registra nem nada: nem som, nem histórico.
        // Contas em segundo plano continuam notificando normalmente.
        if (key != null
            && string.Equals(key, _settings.ContaAtiva, StringComparison.OrdinalIgnoreCase)
            && AppEmPrimeiroPlano())
        {
            return;
        }

        // Registra nas "Últimas notificações" e marca a conta como não lida quando o
        // usuário não está vendo (outra conta na frente, ou app oculto na bandeja).
        if (key != null)
        {
            // Marca como não lida ANTES do rebuild disparado por RegistrarNotificacao,
            // para que chips/faixas/bolinha do histórico já nasçam com o estado final.
            _contasNaoLidas.Add(key);
            RegistrarNotificacao(key, kind);
        }

        // Se mais de uma conta estiver habilitada, mostra o nome da conta no balão
        string? accountLabel = null;
        if (key != null)
        {
            var todasContas = new List<string> { DefaultAccountName };
            todasContas.AddRange(_settings.ContasAdicionadas);
            int habilitadas = todasContas.Count(c =>
                !_settings.NotifyAccountsDisabled.Contains(c, StringComparer.OrdinalIgnoreCase));
            if (habilitadas > 1)
                accountLabel = GetAccountDisplayName(key);
        }

        PlayTrayNotification(kind, accountLabel, key);
    }

    // true quando o app está visível e o usuário consegue ver o jogo:
    // janela principal visível e não minimizada (mesmo sem foco) ou janela flutuante visível.
    private bool AppEmPrimeiroPlano()
    {
        // A flutuante (janela do jogo) também conta como "no app" — não avisa a conta ativa.
        if (IsFloatingVisible()) return true;
        return !_isHiddenToTray && WindowState != WindowState.Minimized && IsVisible;
    }

    // true quando a conta ainda existe (principal ou adicionada). Usado para ignorar toasts/balões de contas removidas.
    private bool ContaExiste(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (string.Equals(key, DefaultAccountName, StringComparison.OrdinalIgnoreCase)) return true;
        return _settings.ContasAdicionadas?.Contains(key, StringComparer.OrdinalIgnoreCase) ?? false;
    }

    // Registra o evento em "Últimas notificações" (até 4, mais recente embaixo), sem marcar bolinha e sem tocar som/balão.
    private void RegistrarNotificacao(string? key, string kind)
    {
        if (key == null) return;

        // "Adiciona/atualiza": mesmo tipo de evento na mesma conta renova o horário e vai para o final (mais recente embaixo),
        // em vez de poluir a lista com entradas idênticas.
        _ultimasNotificacoes.RemoveAll(n =>
            string.Equals(n.Conta, key, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(n.Kind, kind, StringComparison.OrdinalIgnoreCase));
        _ultimasNotificacoes.Add(new NotificacaoItem { Conta = key, Kind = kind, Quando = DateTime.Now });
        if (_ultimasNotificacoes.Count > 4)
            _ultimasNotificacoes.RemoveRange(0, _ultimasNotificacoes.Count - 4);

        _ = AtualizarContaConectadaUIAsync();
    }

    // Texto/som compartilhados por tipo de notificação (balão, som e lista "Últimas notificações").
    private static (string message, string sound) ObterTextoNotificacao(string kind) => kind switch
    {
        "legendary" => ("Item lendário obtido!", "Assets/notify_legendary.wav"),
        "epic" => ("Item épico obtido!", "Assets/notify_epic.wav"),
        "mission" => ("Missão concluída!", "Assets/notify_mission.wav"),
        "inventory" => ("Inventário cheio!", "Assets/notify_inventory.wav"),
        "level" => ("Subiu de nível!", "Assets/notify_level.wav"),
        "potion_hp" => ("Poções de HP acabaram! Compre mais com o Lojista.", "Assets/notify_potion.wav"),
        "potion_mana" => ("Poções de MP acabaram! Compre mais com o Lojista.", "Assets/notify_potion.wav"),
        _ => ("Evento importante no jogo!", "Assets/notify_mission.wav")
    };

    private void PlayTrayNotification(string kind, string? accountLabel = null, string? accountKey = null)
    {
        if (!_settings.TrayNotify && !_settings.TrayBalloon)
            return;

        var title = accountLabel ?? "LoL Idle";
        var (message, sound) = ObterTextoNotificacao(kind);
        var titleFinal = title;

        // O toast é montado/exibido ANTES do bloco de áudio: o SoundPlayer (SND_ASYNC)
        // pode bloquear a thread chamadora esperando o som anterior terminar, o que
        // atrasaria o toast se ele fosse montado depois.
        if (_settings.TrayBalloon)
        {
            _lastBalloonAccount = accountKey;
            _lastBalloonKind = kind;
            try
            {
                var toastContent = new ToastContentBuilder()
                    .AddText(titleFinal)
                    .AddText(message)
                    .AddArgument("conta", accountKey ?? string.Empty)
                    .AddArgument("kind", kind)
                    .SetToastScenario(ToastScenario.Default)
                    // Silent: suprime o "ding" padrão do Windows — o app toca o .wav próprio no bloco TrayNotify.
                    .AddAudio(new ToastAudio { Silent = true });
                toastContent.Show();
                LogFloatingWindowEvent($"Toast: {kind}");
            }
            catch (Exception ex)
            {
                LogFloatingWindowEvent($"Toast falhou ({kind}): {ex.Message} — caindo no balloon");
                // Mesma semântica da regra A: app visível na janela principal OU flutuante suprime
                // notificação da conta ativa — o HandleNotification já faz esse cedo retorno,
                // então este bloco trata apenas os demais casos (outras contas, conta ativa
                // minimizada/oculta etc.). App MINIMIZADO propositalmente conta como "visível"
                // aqui: minimizado não é bandeja, então o balão pode aparecer.
                bool appOuMinimizadoVisivel = !_isHiddenToTray;
                if (appOuMinimizadoVisivel && _trayIcon != null && !_trayIcon.Visible)
                    _trayIcon.Visible = true;
                try { _trayIcon?.ShowBalloonTip(5000, titleFinal, message, Forms.ToolTipIcon.Info); } catch { }
                if (appOuMinimizadoVisivel && _trayIcon != null)
                {
                    _balloonRestoreTimer?.Stop();
                    _balloonRestoreTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(6)
                    };
                    _balloonRestoreTimer.Tick += (_, _) =>
                    {
                        _balloonRestoreTimer?.Stop();
                        if (_trayIcon != null)
                            _trayIcon.Visible = _isHiddenToTray;
                    };
                    _balloonRestoreTimer.Start();
                }
            }
        }

        if (_settings.TrayNotify)
        {
            try
            {
                var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,," + "/" + sound));
                if (resource != null)
                {
                    // Toca direto de um MemoryStream (SND_ASYNC): o BCL lê o buffer antes do
                    // playback, então não vaza arquivo temporário. O MemoryStream NÃO é
                    // disposed aqui — deve permanecer aberto até o playback terminar; o GC
                    // recolhe player e stream depois. Sem Dispose imediato do player pelo
                    // mesmo motivo (playback roda em thread separada).
                    var ms = new System.IO.MemoryStream();
                    using (var rs = resource.Stream)
                        rs.CopyTo(ms);
                    ms.Position = 0;
                    var player = new System.Media.SoundPlayer(ms);
                    player.Play();
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

        if (!_isHiddenToTray)
            return;
        LogFloatingWindowEvent($"TrayNotify: {kind}");
    }

    // Simula a tecla B ("Voltar para a Fonte") no WebView2 da conta informada.
    // O handler do jogo não checa isTrusted, então o keydown sintético funciona,
    // inclusive para contas em segundo plano; o próprio jogo valida se o recall é permitido.
    private async Task VoltarParaFonteAsync(string? contaKey)
    {
        if (string.IsNullOrWhiteSpace(contaKey))
        {
            LogFloatingWindowEvent("VoltarParaFonte: conta vazia — ignorado");
            return;
        }
        try
        {
            if (_accountViews.TryGetValue(contaKey, out var view)
                && view.CoreWebView2 != null)
            {
                await view.CoreWebView2.ExecuteScriptAsync(
                    "window.dispatchEvent(new KeyboardEvent('keydown', { key: 'b' }))");
                LogFloatingWindowEvent($"VoltarParaFonte: B enviado para a conta '{contaKey}'");
            }
        }
        catch (Exception ex) { LogFloatingWindowEvent($"VoltarParaFonte falhou ({contaKey}): {ex.Message}"); }
    }

    private async Task ApplyAudioSettingsAsync()
    {
        // Só muta de fato na bandeja (não na janela flutuante)
        bool trulyHidden = _isHiddenToTray && !IsFloatingVisible();
        bool effectiveMasterMuted = _settings.MasterMuted || (trulyHidden && _settings.TrayMuted);

        // Preferências do usuário (aplicadas à CONTA ATIVA / frente)
        var activePrefs = JsonSerializer.Serialize(new
        {
            musicMuted = _settings.MusicMuted,
            effectsMuted = _settings.EffectsMuted,
            masterMuted = effectiveMasterMuted
        });
        // Contas fora de foco ficam totalmente mudas (o áudio sai só da conta ativa)
        var inactivePrefs = JsonSerializer.Serialize(new
        {
            musicMuted = true,
            effectsMuted = true,
            masterMuted = true
        });

        var scriptTemplate = @"(() => {
            const prefs = PREFERENCES_MARKER;
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

        // Script por conta (ativa vs segundo plano)
        var scriptForActive = scriptTemplate.Replace("PREFERENCES_MARKER", activePrefs);
        var scriptForInactive = scriptTemplate.Replace("PREFERENCES_MARKER", inactivePrefs);

        foreach (var kvp in _accountViews)
        {
            var core = kvp.Value.CoreWebView2;
            if (core == null) continue;
            var script = string.Equals(kvp.Key, _settings.ContaAtiva, StringComparison.OrdinalIgnoreCase)
                ? scriptForActive : scriptForInactive;
            try { await core.ExecuteScriptAsync(script); }
            catch (Exception ex) { LogFloatingWindowEvent($"Audio: não foi possível aplicar preferências na {kvp.Key}: {ex.Message}"); }
        }

        // A flutuante segue a conta ativa (mesmo áudio)
        if (_floatingView?.CoreWebView2 != null)
        {
            try { await _floatingView.CoreWebView2.ExecuteScriptAsync(scriptForActive); }
            catch (Exception ex) { LogFloatingWindowEvent($"Audio: não foi possível aplicar preferências na flutuante: {ex.Message}"); }
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

    // Alterna a visibilidade da barra lateral de contas e persiste a preferência.
    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.SidebarVisible = !_settings.SidebarVisible;
        SaveWindowSettings();
        ApplySidebarLayout();
    }

    // Primeira aplicação do layout (inicialização) é instantânea; chamadas
    // seguintes (toggle) animam fade + deslize lateral do painel.
    private bool _sidebarLayoutInitialized;

    // Aplica a largura/visibilidade da barra lateral com base na preferência salva.
    private void ApplySidebarLayout()
    {
        if (SidebarColumn == null || SidebarPanel == null || SidebarToggleButton == null)
            return;

        bool visible = _settings.SidebarVisible;
        double width = _settings.SidebarWidth >= 160 ? _settings.SidebarWidth : 260;

        // Cancela animações pendentes do painel antes de aplicar o novo estado.
        SidebarPanel.BeginAnimation(UIElement.OpacityProperty, null);
        if (SidebarPanel.RenderTransform is TranslateTransform sidebarTt)
            sidebarTt.BeginAnimation(TranslateTransform.XProperty, null);

        if (SidebarRailColumn != null)
            SidebarRailColumn.Width = visible ? new GridLength(28) : new GridLength(52);

        if (SidebarRailPanel != null)
            SidebarRailPanel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;

        // Alterna a visibilidade dos dois ícones vetoriais
        if (SidebarToggleLeftIcon != null && SidebarToggleRightIcon != null)
        {
            SidebarToggleLeftIcon.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;   // ◀ quando escondida (clique para mostrar)
            SidebarToggleRightIcon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;   // ▶ quando visível (clique para esconder)
        }

        SidebarToggleButton.ToolTip = visible ? "Esconder barra lateral" : "Mostrar barra lateral";

        // Ao minimizar, reconstrói a faixa de avatares da borda (e a da sidebar recolhida).
        if (!visible && SidebarRailPanel != null)
            RebuildContasStrip(_settings.ContaAtiva ?? DefaultAccountName);

        if (!_sidebarLayoutInitialized)
        {
            // Inicialização: aplica o estado salvo sem animação (evita flash na abertura).
            _sidebarLayoutInitialized = true;
            SidebarColumn.Width = visible ? new GridLength(width) : new GridLength(0);
            SidebarPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            SidebarPanel.Opacity = 1;
            SidebarPanel.RenderTransform = null;
            return;
        }

        if (!visible)
        {
            if (SidebarPanel.Visibility != Visibility.Visible)
            {
                // Já está escondido: garante o estado final sem animar.
                SidebarColumn.Width = new GridLength(0);
                SidebarPanel.Visibility = Visibility.Collapsed;
                SidebarPanel.Opacity = 1;
                SidebarPanel.RenderTransform = null;
                return;
            }

            // Esconder: fade-out + deslize para a direita; só colapsa no fim.
            var panel = SidebarPanel;
            panel.RenderTransform = new TranslateTransform { X = 0 };
            panel.Opacity = 1;

            var animX = new DoubleAnimation
            {
                From = 0,
                To = 80,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            };
            animX.Completed += (_, _) =>
            {
                SidebarColumn.Width = new GridLength(0);
                panel.Visibility = Visibility.Collapsed;
                panel.Opacity = 1;
                panel.RenderTransform = null;
            };
            panel.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(160), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }, FillBehavior = FillBehavior.Stop });
            ((TranslateTransform)panel.RenderTransform).BeginAnimation(TranslateTransform.XProperty, animX);
            return;
        }

        // Mostrar: abre o espaço na hora e anima fade-in + deslize de volta.
        SidebarColumn.Width = new GridLength(width);
        SidebarPanel.Visibility = Visibility.Visible;
        SidebarPanel.Opacity = 0;
        SidebarPanel.RenderTransform = new TranslateTransform { X = 80 };

        var animShowX = new DoubleAnimation
        {
            From = 80,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animShowX.Completed += (_, _) =>
        {
            SidebarPanel.Opacity = 1;
            SidebarPanel.RenderTransform = null;
        };
        SidebarPanel.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(160), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        ((TranslateTransform)SidebarPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty, animShowX);
    }

    // Abre a janela "Ajustes" (Notificações + Opções de fechamento em abas)
    private void Ajustes_Click(object sender, RoutedEventArgs e)
    {
        var accountKeys = new List<string> { DefaultAccountName };
        accountKeys.AddRange(_settings.ContasAdicionadas);

        var window = new AjustesWindow(_settings, accountKeys, GetAccountDisplayName, SaveWindowSettings, this);
        window.ShowDialog();
    }

    // Limite total de contas (incluindo a principal): 4 = principal + 3 extras
    private const int MaxContas = 3;

    // Abre um dialog simples mostrando a conta conectada (se houver) com opção de adicionar.
    private async void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.ContasAdicionadas.Count >= MaxContas)
        {
            MostrarMensagemSimples(
                "Limite de contas",
                $"Limite de {MaxContas + 1} contas atingido. Remova uma conta existente antes de adicionar outra.",
                "Entendi",
                MessageKind.Warning);
            return;
        }

        string key = NextAccountKey();
        _settings.ContasAdicionadas.Add(key);
        _settings.ContaAtiva = key;
        SaveWindowSettings();

        try
        {
            await EnsureAccountViewAsync(key);
            ApplyAccountVisibility();
        }
        catch (Exception ex)
        {
            // Não deixa conta "quebrada" no seletor.
            _settings.ContasAdicionadas.RemoveAll(c => c.Equals(key, StringComparison.OrdinalIgnoreCase));
            _settings.ContaAtiva = DefaultAccountName;
            SaveWindowSettings();
            await AtualizarContaConectadaUIAsync();
            ApplyAccountVisibility();
            MostrarMensagemSimples(
                "Não foi possível criar",
                $"Não foi possível criar a nova conta.\n\n{ex.Message}",
                "Fechar",
                MessageKind.Error);
            return;
        }

        await AtualizarContaConectadaUIAsync();

        MostrarMensagemSimples(
            "Conta adicionada",
            $"Conta criada: {key}. Faça login na conta nesta aba; ela fica salva e o chip troca para o nome do jogador automaticamente.",
            "Entendi",
            MessageKind.Success);
    }

    // Atualiza os itens de conta exibidos na barra lateral (lista estilo IdleDeck).
    private Task AtualizarContaConectadaUIAsync()
    {
        // Limpar a lista existente
        ContasWrapPanel.Children.Clear();

        string ativa = _settings.ContaAtiva ?? DefaultAccountName;

        // Conta padrão (sempre presente, nunca removível)
        ContasWrapPanel.Children.Add(CriarChip(
            DefaultAccountName,
            destacado: string.Equals(ativa, DefaultAccountName, StringComparison.OrdinalIgnoreCase)));

        // Contas extras
        foreach (var conta in _settings.ContasAdicionadas)
        {
            ContasWrapPanel.Children.Add(CriarChip(
                conta,
                destacado: string.Equals(ativa, conta, StringComparison.OrdinalIgnoreCase)));
        }

        ContasCountText.Text = $"Contas: {_settings.ContasAdicionadas.Count + 1} / {MaxContas + 1}";

        RebuildContasStrip(ativa);
        RebuildUltimasNotificacoes();
        return Task.CompletedTask;
    }

    // Cria um item de avatar clicável (usado na faixa da sidebar recolhida e na faixa da borda quando minimizada).
    // horizontalStrip: true para a faixa horizontal (WrapPanel) da sidebar; false para a rail vertical da sidebar minimizada.
    private Border CriarAvatarStripItem(string conta, string display, bool destacado, bool horizontalStrip)
    {
        var verde = new SolidColorBrush(Color.FromRgb(0xD9, 0x7B, 0x36));
        var fundoApp = new SolidColorBrush(Color.FromRgb(0x1C, 0x21, 0x29));
        var vermelho = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

        var avatar = CriarAvatarVisual(_settings, conta, display, 34, 9, 19);
        avatar.IsHitTestVisible = false;

        var grid = new Grid
        {
            Width = 34,
            Height = 34,
            IsHitTestVisible = false
        };
        grid.Children.Add(avatar);

        if (ContaNaoLida(conta))
        {
            var bolinha = new System.Windows.Shapes.Ellipse
            {
                Width = 11,
                Height = 11,
                Fill = vermelho,
                Stroke = fundoApp,
                StrokeThickness = 2,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            grid.Children.Add(bolinha);
        }

        var item = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(2),
            BorderBrush = destacado ? verde : Brushes.Transparent,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Margin = horizontalStrip ? new Thickness(0, 0, 6, 6) : new Thickness(0, 0, 0, 6),
            Tag = conta,
            ToolTip = "Trocar para " + display,
            Child = grid
        };
        item.MouseLeftButtonUp += StripItem_Click;
        return item;
    }

    // Recria as faixas de avatares: horizontal (WrapPanel) na sidebar recolhida e vertical na rail da sidebar minimizada.
    private void RebuildContasStrip(string ativa)
    {
        ContasStripPanel.Children.Clear();
        if (SidebarRailPanel != null)
            SidebarRailPanel.Children.Clear();

        var todasContas = new List<string> { DefaultAccountName };
        todasContas.AddRange(_settings.ContasAdicionadas);

        foreach (var conta in todasContas)
        {
            bool destacado = string.Equals(ativa, conta, StringComparison.OrdinalIgnoreCase);
            string display = GetAccountDisplayName(conta);

            ContasStripPanel.Children.Add(CriarAvatarStripItem(conta, display, destacado, horizontalStrip: true));

            if (SidebarRailPanel != null)
                SidebarRailPanel.Children.Add(CriarAvatarStripItem(conta, display, destacado, horizontalStrip: false));
        }
    }

    // Clique num avatar da faixa recolhida: troca para a conta (limpa a não lida dela).
    private async void StripItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string key && !string.IsNullOrWhiteSpace(key))
            await SwitchAccountAsync(key);
    }

    // Remove a linha correspondente da lista "Últimas notificações" (ex.: após clique no toast/balão).
    private void RemoverUltimaNotificacao(string? conta, string? kind)
    {
        if (string.IsNullOrWhiteSpace(conta) || string.IsNullOrWhiteSpace(kind))
            return;
        _ultimasNotificacoes.RemoveAll(n =>
            string.Equals(n.Conta, conta, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(n.Kind, kind, StringComparison.OrdinalIgnoreCase));
        RebuildUltimasNotificacoes();
        // Rebuild dos chips/listas (dots rebuild) — fire and forget como nas outras chamadas
        _ = AtualizarContaConectadaUIAsync();
    }

    // Recria a lista "Últimas notificações" (até 4 itens, da mais antiga para a mais recente — novas aparecem embaixo).
    private void RebuildUltimasNotificacoes()
    {
        NotificacoesRecentesList.Children.Clear();

        if (_ultimasNotificacoes.Count == 0)
        {
            NotificacoesRecentesVazio.Visibility = Visibility.Visible;
            NotificacoesRecentesTituloRow.Visibility = Visibility.Collapsed;
            return;
        }

        NotificacoesRecentesVazio.Visibility = Visibility.Collapsed;
        NotificacoesRecentesTituloRow.Visibility = Visibility.Visible;

        var fundoApp = new SolidColorBrush(Color.FromRgb(0x1C, 0x21, 0x29));
        var vermelho = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

        foreach (var item in _ultimasNotificacoes.Take(4))
        {
            string display = GetAccountDisplayName(item.Conta);
            var (message, _) = ObterTextoNotificacao(item.Kind);
            bool naoLida = ContaNaoLida(item.Conta);

            var avatar = CriarAvatarVisual(_settings, item.Conta, display, 30, 8, 18);
            avatar.IsHitTestVisible = false;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(avatar);

            var textos = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                IsHitTestVisible = false
            };

            // Linha 1: nome da conta + bolinha de não lida à direita
            var linha1 = new Grid();
            linha1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linha1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            linha1.Children.Add(new TextBlock
            {
                Text = display,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (naoLida)
            {
                var bolinha = new System.Windows.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = vermelho,
                    Stroke = fundoApp,
                    StrokeThickness = 1,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                Grid.SetColumn(bolinha, 1);
                linha1.Children.Add(bolinha);
            }
            textos.Children.Add(linha1);

            // Linha 2: o que aconteceu
            textos.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0xAD, 0xBA)),
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            });

            // Linha 3: tempo relativo
            textos.Children.Add(new TextBlock
            {
                Text = FormatarTempoRelativo(item.Quando),
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x76, 0x89)),
                FontSize = 10,
                Margin = new Thickness(0, 1, 0, 0)
            });

            Grid.SetColumn(textos, 1);
            grid.Children.Add(textos);

            // Brush próprio por linha: permite animar só o fundo da linha clicada.
            var fundoLinha = new SolidColorBrush(Color.FromRgb(0x22, 0x27, 0x33));

            var linha = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = fundoLinha,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Tag = item,
                ToolTip = "Trocar para " + display,
                Child = grid
            };
            linha.MouseLeftButtonUp += NotificacaoLinha_Click;
            NotificacoesRecentesList.Children.Add(linha);
        }
    }

    // Anima a saída das linhas correspondentes (fade + deslize para a direita, escalonada),
    // remove os itens da lista e recria "Últimas notificações". Reutilizável pelos caminhos
    // de remoção (clique na linha, troca de conta pela sidebar, etc.). Falha em qualquer
    // etapa cai para a remoção direta.
    private async Task RemoverNotificacoesComAnimacaoAsync(IEnumerable<NotificacaoItem> itens)
    {
        var itensParaRemover = itens?.ToList();
        if (itensParaRemover == null || itensParaRemover.Count == 0)
            return;

        try
        {
            var linhas = NotificacoesRecentesList.Children.OfType<Border>()
                .Where(l => l.Tag is NotificacaoItem it && itensParaRemover.Contains(it))
                .ToList();

            if (linhas.Count == 0)
            {
                foreach (var it in itensParaRemover)
                    _ultimasNotificacoes.Remove(it);
                RebuildUltimasNotificacoes();
                return;
            }

            var concluida = new TaskCompletionSource<object?>();
            int ultima = linhas.Count - 1;
            for (int i = 0; i < linhas.Count; i++)
            {
                var linha = linhas[i];
                var translate = new TranslateTransform();
                linha.RenderTransform = translate;

                var beginTime = TimeSpan.FromMilliseconds(i * 45);
                var animX = new DoubleAnimation
                {
                    From = 0,
                    To = 320,
                    Duration = TimeSpan.FromMilliseconds(220),
                    BeginTime = beginTime,
                    FillBehavior = FillBehavior.Stop
                };
                var animOpacity = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    BeginTime = beginTime,
                    FillBehavior = FillBehavior.Stop
                };

                if (i == ultima)
                {
                    animOpacity.Completed += (_, _) =>
                    {
                        // Remove só os itens que estavam nas linhas animadas (por referência);
                        // notificações que chegaram durante a animação são preservadas.
                        foreach (var it in itensParaRemover)
                            _ultimasNotificacoes.Remove(it);
                        RebuildUltimasNotificacoes();
                        concluida.TrySetResult(null);
                    };
                }

                translate.BeginAnimation(TranslateTransform.XProperty, animX);
                linha.BeginAnimation(UIElement.OpacityProperty, animOpacity);
            }

            // Rede de segurança: se a animação não completar (ex.: janela sem render), remove direto
            // em vez de travar quem está esperando (ex.: troca de conta).
            var limite = Task.Delay(TimeSpan.FromMilliseconds(ultima * 45 + 220 + 1000));
            if (await Task.WhenAny(concluida.Task, limite).ConfigureAwait(true) != concluida.Task)
            {
                foreach (var it in itensParaRemover)
                    _ultimasNotificacoes.Remove(it);
                RebuildUltimasNotificacoes();
            }
        }
        catch
        {
            foreach (var it in itensParaRemover)
                _ultimasNotificacoes.Remove(it);
            RebuildUltimasNotificacoes();
        }
    }

    // Marca como lidas apenas as contas cujas notificações saíram na limpeza e que ficaram
    // SEM nenhuma notificação restante na lista. Conta que recebeu notificação durante a
    // animação (e continua na lista) mantém a bolinha.
    private void MarcarContasSemNotificacaoComoLidas(IEnumerable<NotificacaoItem> itensRemovidos)
    {
        var contas = itensRemovidos
            .Select(n => n?.Conta)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var conta in contas)
        {
            bool restante = _ultimasNotificacoes.Any(n =>
                string.Equals(n.Conta, conta, StringComparison.OrdinalIgnoreCase));
            if (!restante)
                _contasNaoLidas.Remove(conta!);
        }
    }

    // Botão "Limpar" das Últimas notificações: anima o desaparecimento das linhas (fade + deslize para a direita, escalonado)
    // e só então esvazia a lista. Também marca como lidas (_contasNaoLidas) apenas as contas cujas notificações
    // foram removidas e ficaram sem nenhuma restante; contas com notificação recém-chegada mantêm a bolinha.
    private void LimparNotificacoes_Click(object sender, RoutedEventArgs e)
    {
        if (_ultimasNotificacoes.Count == 0 && _contasNaoLidas.Count == 0)
            return;

        // Captura as linhas animadas (e seus itens) ANTES de animar: notificação recém-chegada
        // durante a animação não pode ser apagada junto. Declarado fora do try para o catch usar.
        var itensAnimados = new List<NotificacaoItem>();
        try
        {
            var linhas = NotificacoesRecentesList.Children.OfType<Border>().ToList();
            foreach (var linha in linhas)
                if (linha.Tag is NotificacaoItem it)
                    itensAnimados.Add(it);
            if (linhas.Count == 0)
            {
                _ultimasNotificacoes.Clear();
                RebuildUltimasNotificacoes();
                // Lista vazia: nenhuma conta tem notificação restante, então todas as bolinhas ficam obsoletas.
                _contasNaoLidas.Clear();
                _ = AtualizarContaConectadaUIAsync(); // fire-and-forget: recria chips/faixas sem as bolinhas
                return;
            }

            int ultima = linhas.Count - 1;
            for (int i = 0; i < linhas.Count; i++)
            {
                var linha = linhas[i];
                var translate = new TranslateTransform();
                linha.RenderTransform = translate;

                var beginTime = TimeSpan.FromMilliseconds(i * 45);
                var animX = new DoubleAnimation
                {
                    From = 0,
                    To = 320,
                    Duration = TimeSpan.FromMilliseconds(240),
                    BeginTime = beginTime,
                    FillBehavior = FillBehavior.Stop
                };
                var animOpacity = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    BeginTime = beginTime,
                    FillBehavior = FillBehavior.Stop
                };

                if (i == ultima)
                {
                    animOpacity.Completed += (_, _) =>
                    {
                        // Remove só os itens que estavam nas linhas animadas (por referência);
                        // notificações que chegaram durante a animação são preservadas.
                        foreach (var it in itensAnimados)
                            _ultimasNotificacoes.Remove(it);
                        RebuildUltimasNotificacoes();
                        // Zera bolinha só das contas que ficaram sem nenhuma notificação restante.
                        MarcarContasSemNotificacaoComoLidas(itensAnimados);
                        _ = AtualizarContaConectadaUIAsync(); // fire-and-forget: recria chips/faixas sem as bolinhas
                    };
                }

                translate.BeginAnimation(TranslateTransform.XProperty, animX);
                linha.BeginAnimation(UIElement.OpacityProperty, animOpacity);
            }
        }
        catch
        {
            // Fallback: se a animação falhar, remove direto — mas SÓ os itens capturados
            // antes da animação. Notificações que chegaram durante a tentativa são preservadas.
            foreach (var it in itensAnimados)
                _ultimasNotificacoes.Remove(it);
            RebuildUltimasNotificacoes();
            // Zera bolinha só das contas que ficaram sem nenhuma notificação restante.
            MarcarContasSemNotificacaoComoLidas(itensAnimados);
            _ = AtualizarContaConectadaUIAsync(); // fire-and-forget: recria chips/faixas sem as bolinhas
        }
    }

    // Clique numa linha de "Últimas notificações": pulso rápido no fundo da linha, anima a saída
    // (fade + deslize para a direita) e só então remove o item da lista e troca para a conta.
    private async void NotificacaoLinha_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border linha || linha.Tag is not NotificacaoItem item)
            return;

        // Conta inválida/vazia na tag: remove a linha sem trocar de conta.
        if (string.IsNullOrWhiteSpace(item.Conta))
        {
            LogFloatingWindowEvent("NotificacaoLinha_Click: conta vazia — item removido sem troca de conta");
            try { _ultimasNotificacoes.Remove(item); RebuildUltimasNotificacoes(); } catch { }
            return;
        }

        try
        {
            try
            {
                if (linha.Background is SolidColorBrush brush)
                {
                    var anim = new ColorAnimation
                    {
                        From = Color.FromRgb(0x22, 0x27, 0x33),
                        To = Color.FromRgb(0x2B, 0x3A, 0x55),
                        Duration = TimeSpan.FromMilliseconds(120),
                        AutoReverse = true,
                        FillBehavior = FillBehavior.Stop
                    };
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                }
            }
            catch
            {
                // Se o pulso falhar, segue direto para a animação de saída.
            }

            await Task.Delay(130);

            await RemoverNotificacoesComAnimacaoAsync(new[] { item });

            try
            {
                await SwitchAccountAsync(item.Conta);
                if (item.Kind.StartsWith("potion", StringComparison.OrdinalIgnoreCase))
                    _ = VoltarParaFonteAsync(item.Conta);
            }
            catch
            {
                await RemoverNotificacaoETrocarAsync(item);
            }
        }
        catch
        {
            // Falha em qualquer etapa: remove o item e troca de conta direto.
            await RemoverNotificacaoETrocarAsync(item);
        }
    }

    // Fallback direto (sem animação): remove o item, recria a lista e troca para a conta.
    private async Task RemoverNotificacaoETrocarAsync(NotificacaoItem item)
    {
        try
        {
            _ultimasNotificacoes.Remove(item);
            RebuildUltimasNotificacoes();
        }
        catch
        {
            // Lista pode já estar recriada/vazia; segue para a troca de conta.
        }
        await SwitchAccountAsync(item.Conta);
        if (item.Kind.StartsWith("potion", StringComparison.OrdinalIgnoreCase))
            _ = VoltarParaFonteAsync(item.Conta);
    }

    // Tempo relativo curto: "agora", "há X min", "há X h" ou "HH:mm" se mais velho que 24h.
    private static string FormatarTempoRelativo(DateTime quando)
    {
        var diff = DateTime.Now - quando;
        if (diff.TotalMinutes < 1) return "agora";
        if (diff.TotalMinutes < 60) return $"há {(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24) return $"há {(int)diff.TotalHours} h";
        return quando.ToString("HH:mm");
    }

    // Cria um item de conta (lista vertical):
    // Calcula a posição/tamanho da imagem dentro do recorte quadrado do avatar.
    // Escala "fill" com multiplicador de zoom (escalaMult > 1 aproxima a imagem);
    // offset -1 = borda esquerda/superior, +1 = borda direita/inferior.
    internal static (double Left, double Top, double Width, double Height) CalcularPosicaoAvatar(
        double imgW, double imgH, double tamanho, double escalaMult, double offsetX, double offsetY)
    {
        if (imgW <= 0) imgW = 1;
        if (imgH <= 0) imgH = 1;
        if (tamanho <= 0) tamanho = 1;

        double escala = Math.Max(tamanho / imgW, tamanho / imgH);
        if (double.IsFinite(escalaMult) && escalaMult > 0)
            escala *= escalaMult;

        double w = imgW * escala;
        double h = imgH * escala;

        double sobraX = Math.Max(0, w - tamanho);
        double sobraY = Math.Max(0, h - tamanho);
        double left = -sobraX * (offsetX + 1) / 2;
        double top = -sobraY * (offsetY + 1) / 2;
        return (left, top, w, h);
    }

    // Cria o visual do avatar de uma conta (usado no chip e no diálogo de avatar).
    // Prioridade: imagem do arquivo > ícone (emoji sobre a cor) > cor + iniciais > roxo padrão + iniciais.
    internal static Border CriarAvatarVisual(
        WindowSettings settings,
        string conta,
        string display,
        double tamanho,
        double cornerRadius,
        double fontSizeConteudo)
    {
        string iniciais = display.Length >= 2
            ? display[..2].ToUpperInvariant()
            : display.ToUpperInvariant();

        var roxoPadrao = Color.FromRgb(0x6E, 0x4F, 0xD8);

        string? corHex = null;
        settings.ContasAvatarColor?.TryGetValue(conta, out corHex);
        Color corFundo = roxoPadrao;
        if (!string.IsNullOrWhiteSpace(corHex))
        {
            try
            {
                if (ColorConverter.ConvertFromString(corHex) is Color parsed)
                    corFundo = parsed;
            }
            catch
            {
                // Cor inválida no settings: cai para o roxo padrão.
            }
        }

        string? imagePath = null;
        settings.ContasAvatarImagePath?.TryGetValue(conta, out imagePath);

        var border = new Border
        {
            Width = tamanho,
            Height = tamanho,
            MinWidth = tamanho,
            MaxWidth = tamanho,
            MinHeight = tamanho,
            MaxHeight = tamanho,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = new SolidColorBrush(corFundo),
            ClipToBounds = true,
            // Clip arredondado: recorta o conteúdo (imagem/canvas/texto) nos cantos,
            // já que ClipToBounds só corta no retângulo.
            Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, tamanho, tamanho),
                RadiusX = cornerRadius,
                RadiusY = cornerRadius
            },
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                var (offsetX, offsetY) = LerAvatarImageOffset(settings, conta);

                // Sprite de campeão (*-champion-*.png): mostra só a cabeça (região 16x16 do
                // sprite, como o HUD do LoL Idle), preenchendo toda a área do avatar,
                // escalada sem suavização. Offsets e escala são ignorados por completo.
                string nomeArquivo = Path.GetFileName(imagePath);
                bool isChampion = nomeArquivo.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    && nomeArquivo.Contains("-champion-", StringComparison.OrdinalIgnoreCase);
                if (isChampion)
                {
                    try
                    {
                        int head = Math.Min(16, Math.Min(bmp.PixelWidth, bmp.PixelHeight));
                        int x = Math.Max(0, (int)Math.Floor((bmp.PixelWidth - head) / 2.0) - 1);
                        int y = 0;
                        if (nomeArquivo.Contains("champion-Zac.png", StringComparison.OrdinalIgnoreCase))
                            y = 4;
                        else if (nomeArquivo.Contains("champion-Yasuo.png", StringComparison.OrdinalIgnoreCase))
                            y = 3;
                        else if (nomeArquivo.Contains("champion-Rengar.png", StringComparison.OrdinalIgnoreCase))
                            y = 2;
                        y = Math.Clamp(y, 0, Math.Max(0, bmp.PixelHeight - head));
                        x = Math.Clamp(x, 0, Math.Max(0, bmp.PixelWidth - head));

                        var crop = new CroppedBitmap(bmp, new Int32Rect(x, y, head, head));
                        var headImage = new Image
                        {
                            Source = crop,
                            Width = tamanho,
                            Height = tamanho,
                            Stretch = Stretch.Fill,
                            IsHitTestVisible = false
                        };
                        RenderOptions.SetBitmapScalingMode(headImage, BitmapScalingMode.NearestNeighbor);
                        border.Child = headImage;
                        return border;
                    }
                    catch
                    {
                        // Fallback: sprite inteiro 1:1 centralizado (comportamento anterior).
                        int spriteW = bmp.PixelWidth > 0 ? bmp.PixelWidth : 1;
                        int spriteH = bmp.PixelHeight > 0 ? bmp.PixelHeight : 1;
                        var sprite = new Image
                        {
                            Source = bmp,
                            Width = spriteW,
                            Height = spriteH,
                            Stretch = Stretch.None,
                            IsHitTestVisible = false
                        };
                        RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.NearestNeighbor);
                        var spriteCanvas = new Canvas
                        {
                            Width = tamanho,
                            Height = tamanho,
                            ClipToBounds = true,
                            IsHitTestVisible = false
                        };
                        Canvas.SetLeft(sprite, (tamanho - spriteW) / 2);
                        Canvas.SetTop(sprite, (tamanho - spriteH) / 2);
                        spriteCanvas.Children.Add(sprite);
                        border.Child = spriteCanvas;
                        return border;
                    }
                }

                // Escala "fill" (com multiplicador de zoom): a imagem cobre todo o quadrado, sobra é cortada
                double imgW = bmp.PixelWidth > 0 ? bmp.PixelWidth : 1;
                double imgH = bmp.PixelHeight > 0 ? bmp.PixelHeight : 1;
                var (left, top, w, h) = CalcularPosicaoAvatar(
                    imgW, imgH, tamanho, settings.ContasAvatarImageScale, offsetX, offsetY);

                var imagem = new Image
                {
                    Source = bmp,
                    Width = w,
                    Height = h,
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false
                };
                var canvas = new Canvas
                {
                    Width = tamanho,
                    Height = tamanho,
                    ClipToBounds = true,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(imagem, left);
                Canvas.SetTop(imagem, top);
                canvas.Children.Add(imagem);
                border.Child = canvas;
                return border;
            }
            catch
            {
                // Imagem ilegível: cai para ícone/cor/iniciais.
            }
        }

        string? icon = null;
        settings.ContasAvatarIcon?.TryGetValue(conta, out icon);

        bool temIcone = !string.IsNullOrWhiteSpace(icon);
        var emojiTextBlock = new TextBlock
        {
            Text = temIcone ? icon : iniciais,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            FontWeight = FontWeights.Bold,
            FontSize = temIcone ? fontSizeConteudo : fontSizeConteudo * 0.55,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        if (temIcone)
            emojiTextBlock.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI, Tahoma");
        border.Child = emojiTextBlock;
        return border;
    }

    // Lê o deslocamento do recorte da imagem do avatar ("x,y" de -1 a 1; 0 = centralizado).
    internal static (double X, double Y) LerAvatarImageOffset(WindowSettings settings, string conta)
    {
        double offsetX = 0, offsetY = 0;
        if (settings.ContasAvatarImageOffsets != null &&
            settings.ContasAvatarImageOffsets.TryGetValue(conta, out var offStored))
        {
            var partes = (offStored ?? string.Empty).Split(',');
            if (partes.Length == 2 &&
                double.TryParse(partes[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var ox) &&
                double.TryParse(partes[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var oy))
            {
                offsetX = Math.Clamp(ox, -1, 1);
                offsetY = Math.Clamp(oy, -1, 1);
            }
        }
        return (offsetX, offsetY);
    }

    // Salva (ou remove, quando ~zero) o deslocamento do recorte da imagem do avatar.
    internal static void SalvarAvatarImageOffset(WindowSettings settings, string conta, double offsetX, double offsetY)
    {
        offsetX = Math.Clamp(offsetX, -1, 1);
        offsetY = Math.Clamp(offsetY, -1, 1);
        settings.ContasAvatarImageOffsets ??= new Dictionary<string, string>();

        if (Math.Abs(offsetX) < 0.001 && Math.Abs(offsetY) < 0.001)
            settings.ContasAvatarImageOffsets.Remove(conta);
        else
            settings.ContasAvatarImageOffsets[conta] = string.Format(
                System.Globalization.CultureInfo.InvariantCulture, "{0:0.###},{1:0.###}", offsetX, offsetY);
    }

    // Chave de arquivo segura para a conta (usada ao copiar o avatar para a pasta Avatars).
    internal static string AvatarSlug(string conta)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in conta.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "conta" : slug;
    }

    // avatar quadrado com as iniciais, bolinha de status (verde = na frente, azul = fora),
    // nome + subtítulo, ✏️ editar (nome + avatar) e × remover (somente contas extras).
    private Border CriarChip(string conta, bool destacado = false)
    {
        bool isDefault = string.Equals(conta, DefaultAccountName, StringComparison.OrdinalIgnoreCase);
        string display = GetAccountDisplayName(conta);

        // Paleta de destaque
        var bgItem = new SolidColorBrush(Color.FromRgb(0x22, 0x27, 0x33));
        var bgItemAtivo = new SolidColorBrush(Color.FromRgb(0x3A, 0x2A, 0x1C));
        var bordaAtivo = new SolidColorBrush(Color.FromRgb(0xD9, 0x7B, 0x36));
        var bordaInativo = new SolidColorBrush(Colors.Transparent);
        var verde = Color.FromRgb(0xD9, 0x7B, 0x36);
        var azul = Color.FromRgb(0x3B, 0x9B, 0xFF);
        var fundoApp = Color.FromRgb(0x1C, 0x21, 0x29);

        // Avatar quadrado (imagem > ícone > cor > padrão)
        // Tag "avatar": clique no avatar abre o editor de conta (estilo Facebook),
        // tratado em Chip_Click via IsInsideAvatar.
        var avatar = CriarAvatarVisual(_settings, conta, display, 40, 7, 24);
        avatar.Tag = "avatar";
        avatar.Cursor = Cursors.Hand;
        avatar.ToolTip = "Editar conta";

        // Bolinha de status (canto inferior do avatar)
        var bolinha = new System.Windows.Shapes.Ellipse
        {
            Width = 11,
            Height = 11,
            Fill = new SolidColorBrush(destacado ? verde : azul),
            Stroke = new SolidColorBrush(fundoApp),
            StrokeThickness = 2,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(29, 0, 0, 0),
            IsHitTestVisible = false
        };

        var avatarStack = new Grid
        {
            Width = 40,
            Height = 40,
            VerticalAlignment = VerticalAlignment.Center
        };
        avatarStack.Children.Add(avatar);
        avatarStack.Children.Add(bolinha);

        // Bolinha vermelha de notificação não lida (canto SUPERIOR direito do avatar)
        if (ContaNaoLida(conta))
        {
            avatarStack.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 11,
                Height = 11,
                Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                Stroke = new SolidColorBrush(fundoApp),
                StrokeThickness = 2,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            });
        }

        // Nome + subtítulo
        var textos = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        textos.Children.Add(new TextBlock
        {
            Text = display,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textos.Children.Add(new TextBlock
        {
            Text = destacado ? "na frente" : "segundo plano",
            Foreground = new SolidColorBrush(destacado
                ? Color.FromRgb(0xD9, 0x7B, 0x36)
                : Color.FromRgb(0x6E, 0x9C, 0xD8)),
            FontSize = 10,
            Margin = new Thickness(0, 1, 0, 0)
        });

        // Coluna de botões à direita (editar + remover)
        var botoes = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        // Botão para recarregar apenas o jogo desta conta (como F5) — ícone de refresh estilo feather
        var strokeForegroundBinding = new System.Windows.Data.Binding("Foreground")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1)
        };

        // Ícone ópticamente centrado no Canvas 13x13: desenho ocupa ~1.5..11.5 (x) e 2.0..11.0 (y), centro ~ (6.5, 6.5)
        var reloadIcon = new Canvas { Width = 13, Height = 13 };

        var reloadArrow = new System.Windows.Shapes.Polyline
        {
            Points = new System.Windows.Media.PointCollection
            {
                new System.Windows.Point(11.5, 2.5),
                new System.Windows.Point(11.5, 5.5),
                new System.Windows.Point(8.5, 5.5)
            },
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = System.Windows.Media.Brushes.Transparent
        };
        reloadArrow.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, strokeForegroundBinding);
        reloadIcon.Children.Add(reloadArrow);

        // Arco do círculo (aprox. 295°) com o trecho final reto até o canto da seta, estilo feather "rotate-cw" recentralizado
        var reloadArc = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M10.245 8.0 a4.5 4.5 0 1 1 -1.06 -4.68 L11.5 5.5"),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = System.Windows.Media.Brushes.Transparent
        };
        reloadArc.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, strokeForegroundBinding);
        reloadIcon.Children.Add(reloadArc);

        var btnRecarregar = new Button
        {
            Content = reloadIcon,
            ToolTip = "Recarregar o jogo desta conta",
            Style = (Style)System.Windows.Application.Current.Resources["GhostIconButton"],
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            Width = 28,
            Height = 28
        };
        btnRecarregar.Click += (s, e) => RecarregarConta(conta);
        botoes.Children.Add(btnRecarregar);

        // Botão ✏️ para abrir o editor de conta (renomear + avatar)
        var btnEditar = new Button
        {
            Content = "✏️",
            ToolTip = "Editar conta",
            Style = (Style)System.Windows.Application.Current.Resources["GhostIconButton"],
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            Width = 28,
            Height = 28
        };
        btnEditar.Click += (s, e) => EditarConta(conta);
        botoes.Children.Add(btnEditar);

        // Botão × para remover (a conta padrão não pode ser removida)
        if (!isDefault)
        {
            var btnFechar = new Button
            {
                Content = "×",
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Remover conta",
                Style = (Style)System.Windows.Application.Current.Resources["GhostIconButton"]
            };
            btnFechar.Click += (s, e) => RemoverConta(conta);
            botoes.Children.Add(btnFechar);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(avatarStack);
        Grid.SetColumn(textos, 1);
        grid.Children.Add(textos);
        Grid.SetColumn(botoes, 2);
        grid.Children.Add(botoes);

        var chip = new Border
        {
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(8, 8, 8, 8),
            Background = destacado ? bgItemAtivo : bgItem,
            BorderBrush = destacado ? bordaAtivo : bordaInativo,
            BorderThickness = destacado ? new Thickness(1) : new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            Tag = conta,
            ToolTip = destacado
                ? "Conta na frente"
                : "Clique para trocar para esta conta",
            Child = grid
        };
        chip.MouseLeftButtonUp += Chip_Click;
        return chip;
    }

    // Alterna para a conta quando o usuário clica no item da lista.
    // Clique sobre o avatar abre o editor de conta (estilo Facebook) em vez de trocar.
    private async void Chip_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string key &&
            e.OriginalSource is DependencyObject src && IsInsideAvatar(src))
        {
            EditarConta(key);
            return;
        }
        if (IsInsideButton(e.OriginalSource as DependencyObject))
            return; // clique no × ou ✏️, não troca de conta
        if (sender is FrameworkElement fe2 && fe2.Tag is string k)
            await SwitchAccountAsync(k);
    }

    // Verdadeiro se a origem do clique está dentro do Border do avatar do chip (Tag == "avatar").
    private static bool IsInsideAvatar(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Border b && b.Tag is string s && s == "avatar")
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

    // Diálogo customizado de confirmação de remoção (estilo do app).
    // Retorna true se o usuário confirmar a remoção.
    private static bool MostrarConfirmacaoRemover(string titulo, string mensagem, string subMensagem, string textoConfirmar)
    {
        var janela = new Window
        {
            Title = titulo,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Colors.Transparent),
            ShowInTaskbar = false,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var moldura = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x21, 0x29)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3F)),
            BorderThickness = new Thickness(1)
        };

        var externa = new Grid();

        // Faixa de destaque (removida: ficava muito gritante no topo)

        // Cabeçalho
        var header = new Border
        {
            Padding = new Thickness(18, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x27, 0x33)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3F)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        headerGrid.Children.Add(new TextBlock
        {
            Text = titulo,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btnFechar = new Button
        {
            Content = "✕",
            Width = 26,
            Height = 26,
            ToolTip = "Cancelar",
            Style = (Style)System.Windows.Application.Current.Resources["GhostIconButton"]
        };
        btnFechar.Click += (s, e) => janela.DialogResult = false;
        Grid.SetColumn(btnFechar, 1);
        headerGrid.Children.Add(btnFechar);
        header.Child = headerGrid;

        // Corpo com ícone + mensagens
        var corpo = new StackPanel
        {
            Margin = new Thickness(20, 16, 20, 14)
        };

        var icone = new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(23),
            Background = new SolidColorBrush(Color.FromRgb(0x40, 0x24, 0x1F)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = "!",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x5B, 0x4B)),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        corpo.Children.Add(icone);

        corpo.Children.Add(new TextBlock
        {
            Text = mensagem,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 6)
        });

        corpo.Children.Add(new TextBlock
        {
            Text = subMensagem,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB2)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });

        // Rodapé com botões
        var botoes = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(20, 6, 20, 18)
        };

        ControlTemplate CriarTemplateArredondado(System.Windows.Media.Brush fundo, System.Windows.Media.Brush fundoHover, double raio)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "BorderRoot");
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(raio));
            borderFactory.SetValue(Border.BackgroundProperty, fundo);
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(6));

            var presenterFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            presenterFactory.SetValue(HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            presenterFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(presenterFactory);
            template.VisualTree = borderFactory;

            var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, fundoHover, "BorderRoot"));
            template.Triggers.Add(trigger);
            return template;
        }

        var btnConfirmar = new Button
        {
            Content = textoConfirmar,
            Width = 110,
            Height = 36,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Template = CriarTemplateArredondado(
                new LinearGradientBrush(
                    Color.FromRgb(0xE0, 0x5B, 0x4B),
                    Color.FromRgb(0xB8, 0x3B, 0x2F),
                    90),
                new SolidColorBrush(Color.FromRgb(0xF2, 0x6F, 0x5E)),
                17),
            Margin = new Thickness(0, 0, 10, 0)
        };
        btnConfirmar.Click += (s, e) => janela.DialogResult = true;

        var btnCancelar = new Button
        {
            Content = "Cancelar",
            Width = 110,
            Height = 36,
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            BorderThickness = new Thickness(0),
            FontSize = 12,
            Cursor = Cursors.Hand,
            Template = CriarTemplateArredondado(
                new SolidColorBrush(Colors.Transparent),
                new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3F)),
                17)
        };
        btnCancelar.Click += (s, e) => janela.DialogResult = false;

        botoes.Children.Add(btnConfirmar);
        botoes.Children.Add(btnCancelar);

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(corpo);
        stack.Children.Add(botoes);

        moldura.Child = stack;
        janela.Content = moldura;

        janela.MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                e.OriginalSource is not Button &&
                e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
                janela.DragMove();
        };

        return janela.ShowDialog() == true;
    }

    // Tipo do ícone/cor usado pelo diálogo simples de mensagem.
    private enum MessageKind { Info, Warning, Error, Success }

    // Diálogo simples de mensagem (estilo do app): ícone + texto + botão único.
    private void MostrarMensagemSimples(string titulo, string mensagem, string textoBotao, MessageKind kind)
    {
        var janela = new Window
        {
            Title = titulo,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Colors.Transparent),
            ShowInTaskbar = false,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var moldura = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x21, 0x29)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3F)),
            BorderThickness = new Thickness(1)
        };

        var stack = new StackPanel();

        // Cabeçalho
        var header = new Border
        {
            Padding = new Thickness(18, 10, 10, 10),
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x27, 0x33)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3F)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(8, 8, 0, 0)
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        headerGrid.Children.Add(new TextBlock
        {
            Text = titulo,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btnFechar = new Button
        {
            Content = "✕",
            Width = 26,
            Height = 26,
            ToolTip = "Fechar",
            Style = (Style)System.Windows.Application.Current.Resources["GhostIconButton"]
        };
        btnFechar.Click += (s, e) => janela.DialogResult = true;
        Grid.SetColumn(btnFechar, 1);
        headerGrid.Children.Add(btnFechar);
        header.Child = headerGrid;
        stack.Children.Add(header);

        // Ícone (círculo desenhado) + mensagem
        var (fundoIcone, corIcone, geometria) = kind switch
        {
            MessageKind.Success => (Color.FromRgb(0x16, 0x38, 0x2A), Color.FromRgb(0x3B, 0xAF, 0x6B), "M 5.5 12.5 L 10 17 L 18.5 7.5"),
            MessageKind.Warning => (Color.FromRgb(0x3E, 0x2C, 0x17), Color.FromRgb(0xE0, 0xA1, 0x3B), "M 12 6.5 L 12 13.2 M 12 16.0 L 12 17.2"),
            MessageKind.Error   => (Color.FromRgb(0x40, 0x24, 0x1F), Color.FromRgb(0xE0, 0x5B, 0x4B), "M 7 7 L 17 17 M 17 7 L 7 17"),
            _                   => (Color.FromRgb(0x1F, 0x2A, 0x40), Color.FromRgb(0x3B, 0x9B, 0xFF), "M 12 10.8 L 12 17 M 12 6.4 L 12 7.6"),
        };

        var corpo = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(20, 18, 20, 16)
        };

        corpo.Children.Add(new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(23),
            Background = new SolidColorBrush(fundoIcone),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(geometria),
                Stroke = new SolidColorBrush(corIcone),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform
            }
        });

        corpo.Children.Add(new TextBlock
        {
            Text = mensagem,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDA, 0xE6)),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 330,
            Margin = new Thickness(14, 0, 0, 0)
        });

        stack.Children.Add(corpo);

        // Rodapé com botão único (verde; vermelho no caso de erro)
        var rodape = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3F)),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        ControlTemplate CriarTemplateArredondado(System.Windows.Media.Brush fundo, System.Windows.Media.Brush fundoHover, double raio)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "BorderRoot");
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(raio));
            borderFactory.SetValue(Border.BackgroundProperty, fundo);
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(6));

            var presenterFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            presenterFactory.SetValue(HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            presenterFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(presenterFactory);
            template.VisualTree = borderFactory;

            var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, fundoHover, "BorderRoot"));
            template.Triggers.Add(trigger);
            return template;
        }

        bool erro = kind == MessageKind.Error;
        var btnAcao = new Button
        {
            Content = textoBotao,
            Width = 110,
            Height = 36,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Foreground = new SolidColorBrush(erro ? Colors.White : Color.FromRgb(0x17, 0x1A, 0x21)),
            BorderThickness = new Thickness(0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Template = CriarTemplateArredondado(
                new SolidColorBrush(erro ? Color.FromRgb(0xE0, 0x5B, 0x4B) : Color.FromRgb(0xD9, 0x7B, 0x36)),
                new SolidColorBrush(erro ? Color.FromRgb(0xF2, 0x6F, 0x5E) : Color.FromRgb(0xE8, 0x90, 0x3F)),
                9)
        };
        btnAcao.Click += (s, e) => janela.DialogResult = true;
        rodape.Child = btnAcao;
        stack.Children.Add(rodape);

        moldura.Child = stack;
        janela.Content = moldura;

        janela.MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                e.OriginalSource is not Button &&
                e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
                janela.DragMove();
        };

        janela.ShowDialog();
    }

    // Recarrega apenas a página da conta informada (como F5 na webview dela).
    // As demais contas continuam rodando normalmente.
    private void RecarregarConta(string conta)
    {
        try
        {
            if (_accountViews.TryGetValue(conta, out var view) && view.CoreWebView2 != null)
            {
                view.CoreWebView2.Reload();
                LogFloatingWindowEvent($"RecarregarConta: reload acionado para '{conta}'");
            }
        }
        catch (Exception ex)
        {
            LogFloatingWindowEvent($"RecarregarConta: falhou ('{conta}'): {ex.Message}");
        }
    }

    // Abre o diálogo de edição de conta (nome + avatar).
    private void EditarConta(string conta)
    {
        var janela = new AccountEditorWindow(_settings, conta, SaveWindowSettings, this);
        _ = janela.ShowDialog();
        SaveWindowSettings();
        _ = AtualizarContaConectadaUIAsync();
    }

    // Remove uma conta extra (a padrão não pode ser removida).
    // O userDataFolder da conta NÃO é apagado — o login fica preservado.
    private void RemoverConta(string conta)
    {
        if (string.Equals(conta, DefaultAccountName, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "A conta principal não pode ser removida.",
                "Remover conta",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string display = GetAccountDisplayName(conta);
        bool confirmou = MostrarConfirmacaoRemover(
            "Remover conta",
            $"Remover conta '{display}'?",
            "O login salvo NÃO será apagado — você pode adicionar a conta novamente depois.",
            "Remover");
        if (!confirmou)
            return;

        _settings.ContasAdicionadas.RemoveAll(
            c => c.Equals(conta, StringComparison.OrdinalIgnoreCase));
        _settings.ContasNomes?.Remove(conta);

        if (_accountViews.TryGetValue(conta, out var view))
        {
            _accountViews.Remove(conta);
            AccountViewHost.Children.Remove(view);
            try { view.Dispose(); } catch { }
        }
        _accountEnvs.Remove(conta);

        // Limpa SEMPRE os dados por conta no settings (independente de ser a ativa).
        _settings.NotifyAccountsDisabled?.RemoveAll(
            c => c.Equals(conta, StringComparison.OrdinalIgnoreCase));
        _settings.ContasAvatarColor?.Remove(conta);
        _settings.ContasAvatarIcon?.Remove(conta);
        _settings.ContasAvatarImagePath?.Remove(conta);
        _settings.ContasAvatarImageOffsets?.Remove(conta);

        // Limpa o estado de notificações em memória dessa conta (não exibir conta removida).
        _contasNaoLidas.Remove(conta);
        _ultimasNotificacoes.RemoveAll(n => string.Equals(n.Conta, conta, StringComparison.OrdinalIgnoreCase));

        bool eraAtiva = string.Equals(_settings.ContaAtiva, conta, StringComparison.OrdinalIgnoreCase);
        if (eraAtiva)
        {
            _settings.ContaAtiva = DefaultAccountName;

            // Fecha a flutuante e a janela Hunt (mesmo padrão: nunca manter
            // referência à conta removida).
            if (_floatingWindow != null)
            {
                try { _floatingWindow.Close(); } catch { }
            }
            if (_huntWindow != null)
            {
                try { _huntWindow.Close(); } catch { }
                _huntWindow = null;
            }

            RestartPreparedFloating();
            _lastMissionsJson = null;

            ApplyAccountVisibility();
            // Contas em segundo plano voltam a ficar mudas (evita referência a áudio da conta removida).
            _ = ApplyAudioSettingsAsync();
        }

        _settings.ContasNomesPersonalizados?.RemoveAll(
            c => c.Equals(conta, StringComparison.OrdinalIgnoreCase));
        SaveWindowSettings();
        _ = AtualizarContaConectadaUIAsync();
        if (eraAtiva && _accountViews.ContainsKey(DefaultAccountName))
            _ = RefreshAccountLabelAsync(DefaultAccountName);
    }

    // Esconde/mostra a lista de contas (mantém o título "Contas" visível).
    private bool _contasAnimando = false;

    private void ContasHide_Click(object sender, RoutedEventArgs e)
    {
        if (ContasListPanel == null || _contasAnimando) return;
        _contasAnimando = true;

        bool recolher = ContasListPanel.Visibility == Visibility.Visible;
        var ease = new CubicEase { EasingMode = recolher ? EasingMode.EaseIn : EasingMode.EaseOut };

        if (recolher)
        {
            // Colapsa a lista (MaxHeight -> 0 + Opacity -> 0 + deslize para a direita); a faixa de avatares faz fade-in.
            double from = ContasListPanel.ActualHeight > 0 ? ContasListPanel.ActualHeight : ContasListPanel.DesiredSize.Height;
            ContasListPanel.MaxHeight = double.IsNaN(from) || from <= 0 ? 400 : from;
            ContasListPanel.RenderTransform = new TranslateTransform { X = 0 };

            var animH = new DoubleAnimation
            {
                From = ContasListPanel.MaxHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            animH.Completed += (_, _) =>
            {
                ContasListPanel.Visibility = Visibility.Collapsed;
                ContasListPanel.MaxHeight = double.PositiveInfinity;
                ContasListPanel.Opacity = 1;
                ContasListPanel.RenderTransform = null;
                if (ContasStripPanel != null)
                {
                    ContasStripPanel.Visibility = Visibility.Visible;
                    ContasStripPanel.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200) });
                }
                if (ContasHideButton != null)
                    ContasHideButton.Content = "Contas ▾";
                _contasAnimando = false;
            };
            ContasListPanel.BeginAnimation(FrameworkElement.MaxHeightProperty, animH);
            ContasListPanel.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(300), FillBehavior = FillBehavior.Stop });
            ((TranslateTransform)ContasListPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation { From = 0, To = 140, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        }
        else
        {
            // Expande: a faixa some na hora e a lista anima de MaxHeight 0 -> altura natural.
            if (ContasStripPanel != null)
                ContasStripPanel.Visibility = Visibility.Collapsed;
            ContasListPanel.Visibility = Visibility.Visible;
            ContasListPanel.MaxHeight = double.PositiveInfinity;
            ContasListPanel.Measure(new System.Windows.Size(
                ContasListPanel.ActualWidth > 0 ? ContasListPanel.ActualWidth : 300,
                double.PositiveInfinity));
            double para = ContasListPanel.DesiredSize.Height > 0 ? ContasListPanel.DesiredSize.Height : 400;
            ContasListPanel.MaxHeight = 0;
            ContasListPanel.Opacity = 0;
            ContasListPanel.RenderTransform = new TranslateTransform { X = 140 };

            var animH = new DoubleAnimation
            {
                From = 0,
                To = para,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            animH.Completed += (_, _) =>
            {
                ContasListPanel.MaxHeight = double.PositiveInfinity;
                ContasListPanel.Opacity = 1;
                ContasListPanel.RenderTransform = null;
                if (ContasHideButton != null)
                    ContasHideButton.Content = "Contas ▴";
                _contasAnimando = false;
            };
            ContasListPanel.BeginAnimation(FrameworkElement.MaxHeightProperty, animH);
            ContasListPanel.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            ((TranslateTransform)ContasListPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation { From = 140, To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        }
    }

    // Fecha com base na preferência CloseBehavior:
    // 0 = perguntar, 1 = continuar em segundo plano, 2 = sair e desligar as contas.
    private void RequestClose()
    {
        if (_settings.CloseBehavior == 1)
        {
            HideToTray();
            return;
        }
        if (_settings.CloseBehavior == 2)
        {
            ExitApplication();
            return;
        }

        // Perguntar ao usuário
        var dialog = new CloseConfirmWindow(this);
        if (dialog.ShowDialog() != true)
            return; // cancelou ou fechou pelo X

        var result = dialog.Result;
        if (result.Remember)
            _settings.CloseBehavior = result.Selected;
        SaveWindowSettings();

        if (result.Selected == 1)
            HideToTray();
        else
            ExitApplication();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        RequestClose();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowSettings();

        if (!_allowExit)
        {
            e.Cancel = true;
            RequestClose();
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
