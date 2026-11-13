# 🗺️ Roadmap — LoL Idle Desktop

> Base: **v1.4.4** · Última atualização: 2026-11-12
> Versão web estilizada: [`ROADMAP.html`](./ROADMAP.html) — abra no navegador para visualizar como site.

<p align="center">
  <img src="https://img.shields.io/badge/Status-10%20feitos-2ea043?style=for-the-badge" alt="feitos">
  <img src="https://img.shields.io/badge/Status-1%20imposs%C3%ADvel-ff4d4f?style=for-the-badge" alt="impossíveis">
  <img src="https://img.shields.io/badge/Status-1%20futuro-58a6ff?style=for-the-badge" alt="futuro">
  <img src="https://img.shields.io/badge/Status-2%20pendentes%2Fpolir-f0b429?style=for-the-badge" alt="pendentes">
</p>

---

## 📊 Resumo rápido

| Painel / Recurso | Status | Versão |
|---|---|---|
| 📖 **Missões** | ✅ **Confirmado** | v1.2.5 / v1.2.6 / v1.4.4 (foco não rouba mais; toggle pelo botão de livro do jogo) |
| 🏹 **Hunt for LoL Idle (extensão sob demanda)** | ✅ **Feito** | v1.4.4 |
| 🔔 **Bandeja inteligente (sons + clique abre app e troca de conta)** | ✅ **Confirmado** | v1.2.0 → v1.3.0 / v1.3.7 / v1.3.8 |
| 🔔 **Toasts modernos + Central de Ações clicável** | ✅ **Feito** | v1.4.0 / v1.4.1 / v1.4.2 (conta ativa na frente não notifica) / v1.4.3 (tema tático + uso de recursos) / v1.4.4 (toast silencioso, som assíncrono, toast antes do som) |
| 🎛️ **Barra lateral de contas** | ✅ **Feito** | v1.3.2 / v1.3.3 / v1.3.5 / v1.3.6 / v1.3.7 / v1.3.8 / v1.3.9 / v1.4.3 / v1.4.4 |
| 🖼️ **Avatares das contas (3 fontes)** | ✅ **Feito** | v1.3.6 |
| 🔴 **Sinalizações de não lida / últimas notificações na sidebar** | ✅ **Feito** | v1.3.7 / v1.3.8 / v1.3.9 / v1.4.1 / v1.4.2 / v1.4.3 / v1.4.4 (bolinha na hora + Limpar preserva recém-chegadas) |
| 🔔 **Notificações por conta e por tipo** | ✅ **Feito** | v1.3.3 / v1.4.0 (Toasts modernos) |
| 🧪 **Poções: notificação + retorno à Fonte (modo Automático)** | ✅ **Feito** | v1.3.8 / v1.4.0 (Central de Ações clicável) |
| 🧙 **Personagem (interativo)** | ❌ **Impossível** | — (jogo bloqueia 2ª sessão) |
| 📌 **Sempre no topo persistente** | ✅ **Confirmado** | v1.2.7 |
| 📱 **Android** | 🔵 **Futuro** | — |
| 🔊 **Áudio separado (música/efeitos)** | 🟡 **Pendente — precisa de decisão** | lógica existe (script de sincronização + botões `bt-musica`/`bt-efeitos` do jogo); a antiga tela de configurações foi removida do código |
| 🔄 **Aviso de versão nova** | 🟡 **Falta polir** | — |

---

## ✅ 1. Painéis independentes

### 📖 Missões — `FEITO` ✅

Painel separado e destacável que **já está pronto** (v1.2.5 / v1.2.6):

- Só aparece com a **flutuante aberta**
- Arrastável / redimensionável (resize grip ◢ no canto inferior direito), moldura no tema (a "borda amarela" das versões antigas não existe mais — o tema atual usa `BorderSubtle`/`BgPanel`)
- Progresso: Passe, Tarefa, Caçada
- Lembra último estado: ativa/desativada, tamanho e local
- Ícone sincronizado com o estado real
- **v1.4.4:** a janela ganhou cabeçalho destacado com título "📖 Missões" (Rajdhani), moldura arredondada com `CornerRadius=10` (conteúdo recortado pelos cantos), resize grip ◢ e `WindowChrome` (resize pela borda, sem frame visível do Windows). **Bug de foco resolvido:** a Missões não rouba mais o foco espontaneamente (`_missionsWindow.Activate()` removido de `ShowMissions`; o handler `window.Activated` da flutuante usa `SetWindowPos(HWND_TOP, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)` em vez de `BringWindowToTop`). O botão de livro do jogo abre/fecha a Missões com toggle funcional, e o jogo continua recebendo cliques normalmente pelo WebView2.
- **v1.4.4:** minimizar/restaurar pela barra de tarefas **esconde/exibe a flutuante e a Missões juntas** (só a visibilidade muda; a preferência `ShowMissions` é mantida)

> ℹ️ É um painel **só-texto** (igual ao que a Barra lateral poderia ser), sem segunda sessão do jogo.

### 🏹 Hunt for LoL Idle — `FEITO` ✅ (v1.4.4)

Extensão do Chrome ("Hunt for LoL Idle", leitor de tela do jogo) instala **sob demanda** no perfil WebView2 do app:

- **Instalação sob pedido** — nada é distribuído junto com o app; ao clicar no botão 🏹 **Hunt** (mira) na barra de título (ao lado de minimizar) pela primeira vez, o app pergunta e só então baixa da Chrome Web Store (~270 KB), extrai para `%LOCALAPPDATA%\LoLIdleDesktop\Extensions\HuntForLoLIdle` e registra como *unpacked* via `AddBrowserExtensionAsync`
- **Sanitização obrigatória** — o `manifest.json` local tem a permissão `notifications` removida na instalação (e em toda checagem de `IsExtensionExtracted`), então `chrome.notifications` não existe no service worker: o leitor de tela continua funcionando, mas **a extensão não envia notificações do Windows** (o painel também mostra o checkbox "Avisos" inerte)
- **ID registrado nos perfis:** `jnokkeaaaionjjooccdkiilhebohfkaa` (persistido em `window-settings.json` → `HuntExtensionId`). Instalações antigas obsoletas (ex.: id legado `hkaaipkllejfjgncdpkgbjgedkpkcnho`, v0.8.3) são detectadas e removidas via `RemoveAsync()` antes de (re)registrar
- **Instalação em todos os perfis** — principal + contas criadas (cada WebView2 tem perfil próprio); perfis que já têm a extensão atualizada não são mexidos
- **Painel em janela própria** — tema do app, cabeçalho escuro com título "🏹 Hunt for LoL Idle" (Rajdhani), arrastável, `WindowStyle=None` com cantos arredondados; usa o **mesmo environment da conta ativa** (descartada e recriada ao trocar de conta)
- **Atualização sob demanda** — a janela consulta em background a versão publicada na loja (update-check público); se houver versão maior, mostra o botão **"↻ Atualizar"** no cabeçalho; o clique refaz o ciclo completo (download → sanitize → re-registro nos perfis) e recarrega o painel
- **Botão de mira na barra de título** — abre/fecha o painel (toggle); durante a instalação inicial mostra aviso de espera

> ℹ️ **Nota de honestidade:** este documento descreve o que está no código; não atesta teste ao vivo da extensão no jogo. A v1.4.4 está em preparação (publicação pendente).

### 🎛️ Barra lateral de contas — `FEITO` ✅ (v1.3.2 / v1.3.3 / v1.3.5 / v1.3.6 / v1.3.7 / v1.3.8 / v1.3.9 / v1.4.3 / v1.4.4)

A sidebar **de contas** (lado direito, com toggle esconder/mostrar) foi implementada e evoluiu ao longo da v1.3.x:

- **Várias contas reais ao mesmo tempo** — uma WebView2 por conta, cada uma com sessão/partição independente (`%LOCALAPPDATA%\LoLIdleDesktop\WebView2\Contas\<slug>`)
- **Troca entre contas clicando na lista**, sem precisar deslogar/logar (fica salva para sempre naquela conta)
- **Lista vertical estilo IdleDeck** — avatar quadrado arredondado (roxo com iniciais por padrão; personalizável desde a v1.3.6), bolinha de status no avatar (verde = na frente, azul = em segundo plano), item inteiro clicável
- **Renomear** via ✏️ / janela "Editar conta" (nome personalizado tem prioridade sobre o lido do DOM)
- **Remover** conta com o × (o login salvo NÃO é apagado)
- **Até 4 contas no total** (principal + 3 extras)
- **Botão "🔔 Notificações"** (v1.3.3) — ativar/desativar, checkmarks escuros por conta, checkboxes por tipo (Lendário, Épico, Missão, Inventário, Nível) e prefixo automático com o nome da conta (vários habilitados: "Yasuo — Missão concluída!")
- Botão "Contas ▴" esconde/mostra a lista (sem borda cinza residual)
- **⚙ Opções de fechamento** (v1.3.5; **v1.4.4**: aba dentro da janela **Ajustes**, junto com 🔔 Notificações) — troca da preferência do botão Fechar depois. As janelas `NotifySettingsWindow` e `CloseBehaviorWindow` foram unificadas numa única janela **Ajustes** (`AjustesWindow`) com duas abas: 🔔 **Notificações** (conteúdo de antes) e ⚙ **Opções de fechamento** (perguntar / continuar na bandeja / sair), mantendo a moldura no tema
- **Diálogo Fechar restaurado** (v1.3.5) — clicar em × abre "Fechar o LoL Idle?" (Continuar em segundo plano / Sair e desligar as contas) com "Lembrar minha escolha"
- **Editor de conta com avatares** (v1.3.6) — janela **"Editar conta"** (✏️ na sidebar ou clique direto no avatar da conta, estilo Facebook): renomear + escolher avatar
- **Avatar com três fontes** (v1.3.6) — cor + iniciais, **emoji** (12 opções, ícones em branco) ou **imagem do PC**; bordas arredondadas em qualquer avatar
- **Aba "Campeões"** (v1.3.6) — ao lado de "Ícones", com os 12 campeões do LoL Idle (Darius, Ezreal, Lux, Yasuo, Katarina, Taric, Zac, Ryze, Rengar, Shaco, Sona, Aphelios); sprites pixelados extraídos do próprio jogo (in-game 32×32; Rengar 38×38), embutidos no app; clique aplica na hora, sem recorte, com foco no rosto (recorte 16×16 da cabeça, como o HUD do jogo)
- **Janela "Alterar avatar"** (v1.3.6) — estilo Facebook: preview quadrado arredondado, **arrastar com o mouse** para reposicionar, **slider de zoom** (1.0x–4.0x) e **pré-visualização do chip ao vivo** (40×40, nome e status); só aplica em "Aplicar" ("Cancelar" descarta); posição/zoom salvos por conta e persistidos
- **Cor como fundo** (v1.3.6) — escolher cor pinta só o fundo (não apaga campeão/imagem; ideal para sprites transparentes); nova cor **preta** na grade, com borda sutil para divisar no painel escuro
- **Seção "Ajuste qual pedaço da imagem aparece no avatar" só para fotos do PC** (v1.3.8) — não aparece para avatares de campeão (recorte automático do rosto)
- **Correções de interação** (v1.3.6) — clique no avatar abre o editor; bolinha de status não bloqueia mais cliques; clique em emoji/cor/campeão funciona em qualquer parte da célula (antes só na borda)
- **Sinalizações de não lida + "Últimas notificações"** (v1.3.7) — ver seção abaixo
- **"Últimas notificações" no rodapé, nos dois modos** (v1.3.8) — a seção fica encostada no rodapé da sidebar e visível tanto com a lista expandida quanto recolhida, com linhas de borda a borda
- **Animação de recolhimento/expansão da seção de contas** (v1.3.8; **refinada na v1.3.9**) — deslize lateral + fade (a lista desliza para a direita e desvanece ao recolher, e volta deslizando ao expandir), junto com o encolhimento/crescimento; faixa de avatares com fade. Duração **~300 ms** (v1.3.9; antes ~210 ms), suavizada (`CubicEase`)
- **Botões 🔔 Notificações e ⚙ Opções de fechamento sempre visíveis** (v1.3.8) — continuam abaixo dos avatares na lista recolhida, nos dois modos
- **Sidebar inteira com fade + deslize lateral** (v1.3.9) — esconder/mostrar a barra pelo chevron da borda anima fade + deslize para a direita (~160 ms); na primeira chamada (startup) o estado salvo é aplicado sem flash
- **Botão de expandir/recolher centralizado na sidebar minimizada** (v1.3.8) — faixa na borda direita com os avatares (menores, no topo) e o botão no meio; clique no avatar troca de conta
- **Correções de estabilidade** (v1.3.7) — inicialização das contas WebView2 protegida por exceções (falha em uma conta desregistra só ela, sem afetar as outras); desmute só retoma AudioContext suspenso pelo próprio app; remover a conta ativa troca para a principal e limpa as preferências por conta da conta removida (login salvo preservado)
- **Seção "Consumo total"** (v1.4.3; meta de RAM ajustada na **v1.4.4**) — bloco acima das contas individuais: **CPU total** do app à esquerda (verde quando ok) e **RAM total** à direita (processo principal + WebViews, incluindo páginas compartilhadas); **barra de RAM com meta dinâmica** (`MetaRamBytes()`; **v1.4.4**: **200 MB fixos para o app + 1,0 GB por conta aberta** — antes: 1 GB por conta cadastrada, mesmo fechada); pill **Normal**/**Acima da meta** (vermelho acima da meta); abaixo, lista de **uso por conta**
- **Botão "⚡ Limpar cache"** (v1.4.3) — remove só `Cache`/`Code Cache`/`GPUCache` do WebView2 (`%LOCALAPPDATA%\LoLIdleDesktop\WebView2\EBWebView\Default\`, principal + extras via `Contas\<slug>`), com feedback de quanto foi liberado (MB/GB) e tela de resultado (botão "Ok"); cookies, sessão e login ficam intactos
- **Botão 🔄 no chip** (v1.4.3) — recarrega o jogo **apenas daquela conta** (como F5), sem afetar as outras contas
- **Rodapé (status bar) com pills de CPU e RAM** (v1.4.3) — pill de **CPU** (dot cobre, barra com `Accent`) e pill de **RAM** (dot `AccentIce`, absoluto em pt-BR), ambos mostrando o **total do app**, alinhados com a mesma contagem da sidebar
- **Tema tático copper + ardósia (slate)** (v1.4.3) — paleta nova, bordas mais sutis, tipografia **Rajdhani** (títulos) e **Barlow** (corpo) embutidas (`Assets\Fonts\`), compatível com todos os diálogos internos
- Conta ativa lembrada ao reabrir (`ContaAtiva`)

> ℹ️ **O que continua inviável** é um **painel do jogo interativo dentro da barra** (2ª sessão viva acoplada ao painel) — o LoL Idle bloqueia uma segunda WebView com o jogo absoluto no mesmo momento. A barra feita aqui é **seletor + gerenciador de contas**, não um painel vivo.

### 🔔 Sinalizações de não lida / últimas notificações na sidebar — `FEITO` ✅ (v1.3.7 / v1.3.8 / v1.3.9 / v1.4.1 / v1.4.2 / v1.4.3 / v1.4.4)

A sidebar de contas passou a sinalizar notificações pendentes sem precisar abrir a janela do jogo:

- **Bolinha vermelha de não lida** nos avatares de contas que receberam uma notificação e ainda não foram vistas — aparece tanto na **lista expandida** quanto nas **faixas recolhidas/minimizadas**. Some ao trocar para a conta.
- **Seção "Últimas notificações"** na parte de baixo da sidebar (v1.3.7: modo expandido; **v1.3.8: visível nos dois modos** — expandida e recolhida —, encostada no rodapé, com linhas de borda a borda): as **4 notificações mais recentes**, com nome da conta, o que aconteceu (Missão concluída!, Subiu de nível!, Inventário cheio!, Item épico/lendário obtido!, Poções de HP/MP acabaram!) e tempo relativo ("agora", "há X min"). A mais antiga fica em cima e a nova entra por baixo.
- **Clicar numa notificação** troca para a conta dela e ela sai da lista com animação (fade + deslize para a direita; v1.3.9, antes deslize para cima); a linha tem um **pulso de fundo** ao ser clicada.
- Botão **"Limpar"** na seção: esvazia a lista com animação escalonada (fade/deslize para a direita, 240 ms, escalonamento de 45 ms) — desde a v1.4.1 também afeta (zera) as bolinhas das contas esvaziadas; o comportamento exato está detalhado abaixo (v1.4.4)
- **Clicar no balão da bandeja** restaura/abre a janela e troca para a conta da notificação (v1.3.7; **v1.3.8**: se o aviso for de poções, o personagem também volta para a Fonte — ver card abaixo).
- Com a lista de contas recolhida ("Contas ▴"), a faixa vertical mostra os **avatares das contas** (clique troca de conta) no lugar do espaço vazio.
- Com a sidebar inteira minimizada, uma **faixinha na borda direita** mostra os avatares das contas conectadas (menores, no topo), com o botão de expandir/recolher no meio; clique no avatar troca de conta.
- Animações: linha de notificação pulsa ao ser clicada; limpar faz as linhas deslizar para a direita e desaparecer escalonado (v1.3.9).
- **"Limpar" também zera as bolinhas de não lida** (v1.4.1) — o botão esvazia a lista **e** limpa `_contasNaoLidas` (a sidebar é recriada sem as bolinhas); só retorna sem fazer nada se a lista **e** o conjunto de não lidas estiverem vazios. *(Refinado na v1.4.4 — ver abaixo.)*
- **Clique no toast/balão remove a linha correspondente** de "Últimas notificações" (v1.4.2) — o clique consome/zera `_lastBalloonAccount`/`_lastBalloonKind`, troca para a conta (se ela ainda existe) e remove a linha via `RemoverUltimaNotificacao`; se a conta foi removida, a linha órfã é descartada sem restaurar/trocar
- **Trocar de conta remove a linha daquela conta** do histórico, com animação (fade + deslize à direita) (v1.4.3)
- **Conta ativa na frente não notifica** (v1.4.2) — com a principal (não minimizada) ou a flutuante visíveis, `HandleNotification` retorna cedo: sem som, sem toast/balão e sem registro no histórico; em segundo plano e minimizada/na bandeja, volta a avisar
- **Bolinha de não lida aparece na hora** (v1.4.4) — `_contasNaoLidas.Add()` agora ocorre **antes** do rebuild disparado por `RegistrarNotificacao`, então chips/faixas/linhas do histórico já nascem com o estado final
- **"Limpar" preserva notificações recém-chegadas** (v1.4.4) — as linhas animadas são capturadas antes da animação; notificações que chegarem durante ela sobrevivem (inclusive no fallback de erro), e a bolinha só é zerada das contas que ficaram sem nenhuma notificação restante (antes, o conjunto inteiro era zerado)

> ℹ️ O rastreamento das notificações (`_ultimasNotificacoes`, `_contasNaoLidas`) é **somente em memória** — nada é persistido no `window-settings.json`.

### 🧪 Poções de HP/MP — notificação + retorno à Fonte — `FEITO` ✅ (v1.3.8)

Quando as poções acabam, o app avisa e ajuda o personagem a voltar para a Fonte:

- **Balão + som próprio** (`notify_potion.wav`) com mensagem por família: "Poções de HP acabaram! Compre mais com o Lojista." (HP) / "Poções de MP acabaram! Compre mais com o Lojista." (MP)
- **Detecção no HUD** (`#hud-pocoes`), na transição >0 → 0 (baseline na primeira leitura) — sem falso positivo; throttle de 3s por tipo
- **Um único toggle** no 🔔 Notificações — "Poções de HP ou MP acabaram" — silencia as duas famílias (`potion_hp`/`potion_mana` caem no `potion`)
- **Clicar no balão da bandeja ou na linha da notificação** também manda o personagem de volta para a Fonte (simula a tecla B no WebView da conta — `VoltarParaFonteAsync`) — **somente no modo automático (Summoner's Rift)**; nas demais áreas/modos o jogo bloqueia o teleporte (mesma regra da tecla B; o aviso do jogo aparece normalmente)
- A **release não inclui** botão de teste ("Testar poções" existiu durante o desenvolvimento e foi removido após a validação)

### 🧙 Personagem — `IMPOSSÍVEL (interativo)` ❌

- **Por que não dá:** mesma limitação da barra — **segunda sessão é bloqueada**.
- Não tem como fazer do jeito desejado (clone vivo/interativo).
- Só seria viável a versão **só-texto** (como Missões), que não é o desejado.

**Comportamento esperado (para painéis futuros):**

- Cada painel em janela própria, sempre no topo se necessário
- Sem travar a janela principal
- Salvar posição/tamanho de cada painel (como já fazemos com principal e flutuante)

---

## ✅ 2. Modo bandeja inteligente (áudio por eventos) — `FEITO` ✅ v1.2.0

Ao minimizar para a bandeja, o app fica **mutado por padrão** (configurável):

- 🔔 Som só toca em notificações importantes:
  - 🟠 **Item lendário** obtido
  - 🟣 **Item épico** obtido
  - 📜 **Missão concluída**
  - 🎒 **Inventário cheio** (só no 30/30)
  - ⭐ **Level up**
  - 🧪 **Poções de HP ou MP acabaram** (v1.3.8)
- Detecção via `MutationObserver` no DOM + `postMessage` → toca som e balão na bandeja
- Sessão permanece ativa enquanto oculto
- Clicar no balão restaura/abre a janela principal e troca para a conta da notificação (v1.3.0 → v1.3.8); se o aviso for de poções, também manda o personagem de volta para a Fonte (somente no modo automático)
- **v1.4.4** — toast **silencioso** (`AddAudio { Silent = true }`, sem o "ding" padrão do Windows); o **toast aparece antes do som**; o som é carregado em memória (`SoundPlayer(MemoryStream)`, sem arquivo temporário e sem `PlaySync()` bloqueante, tocando em thread separada)

> **Polimento atual:** ajustar volumes/timbre de cada som (lendário, épico, missão, nível, inventário).

---

## 🔵 3. App para Android — `FUTURO`

- Versão Android do LoL Idle Desktop (WebView nativa)
- Paridade: login/sessão persistente, janela aceitável em mobile, controle de áudio
- Estratégia: MAUI / .NET Android / WebView wrapper nativo

---

## ⚠️ Limitações atuais

| Limitação | Situação |
|---|---|
| Missões | ✅ **Resolvido** (v1.2.5/v1.2.6) |
| Barra lateral / Personagem | ❌ Impossíveis interativos (bloqueio de 2ª sessão) |
| Áudio | 🟡 Lógica de música/efeitos existe no código (`AudioBootstrapScript` + `bt-musica`/`bt-efeitos`), mas não há tela acessível pela interface (a antiga `ShowFloatingSettings()` foi removida) — precisa de decisão para recriar uma tela ou manter só os botões do jogo |
| Sempre no topo | ✅ **Resolvido na v1.2.7** — persistente por janela (`FloatingTopmost` / `MissionsTopmost`) |

---

## 🎯 Próximo foco

- [ ] Sons de notificação (volume/timbre por evento)
- [ ] Detalhes da flutuante + Missões
- [ ] Teste ao vivo da notificação de poções — seletores confirmados apenas por análise estática; verificação no jogo ainda pendente
- [ ] Áudio separado (música/efeitos) — **decisão necessária:** recriar uma tela de configurações (a antiga foi removida do código) ou manter apenas os botões `bt-musica`/`bt-efeitos` do jogo
- [x] Sempre no topo persistente — **feito na v1.2.7** (`FloatingTopmost` / `MissionsTopmost`, com sincronia visual)
- [ ] Aviso de nova versão (GitHub)
- [x] Avatares das contas (3 fontes) + aba Campeões + janela "Alterar avatar" com zoom — **feito na v1.3.6**
- [x] Sinalizações de não lida / últimas notificações na sidebar — **feito na v1.3.7**
- [x] Poções: notificação + retorno à Fonte (modo Automático) — **feito na v1.3.8**
- [x] Animações refinadas (sidebar e notificações: deslize lateral, fade, startup sem flash) — **feito na v1.3.9**
- [x] Toasts modernos e Central de Ações clicável (AppUserModelId `Zephrus15.LoLIdle.Desktop`) — **feito na v1.4.0**
- [x] Botão "Limpar" em "Últimas notificações" também remove as bolinhas de não lida — **feito na v1.4.1**
- [x] Conta ativa na frente não notifica; limpeza preserva recém-chegadas; toast/balão remove item correspondente e linha órfã de conta removida; HUD de poções analisado estaticamente — **feito na v1.4.2**
- [x] Parte superior do sidebar com consumo total (CPU à esquerda, RAM à direita, barra de RAM com meta dinâmica 1 GB por conta; pill Normal/Acima da meta); rodapé com pills de CPU e RAM; tema tático copper + ardósia (slate); botão recarregar por conta — **feito na v1.4.3**
- [x] Hunt for LoL Idle — instalação sob demanda da extensão (download da Chrome Web Store sob pedido, unpacked, manifest sanitizado sem `notifications`), botão 🏹 na barra de título, painel em janela própria no tema, instalação em todos os perfis WebView2, atualização sob demanda ("↻ Atualizar") — **feito na v1.4.4**
- [x] Ajustes em abas — `NotifySettingsWindow` + `CloseBehaviorWindow` unificadas em `AjustesWindow` (abas 🔔 Notificações e ⚙ Opções de fechamento) — **feito na v1.4.4**
- [x] Bug de foco com Missões — Missões não rouba mais o foco (`SetWindowPos` com `SWP_NOACTIVATE` na z-order); toggle do botão de livro do jogo funcional — **feito na v1.4.4**
- [x] Notificações refinadas — bolinha de não lida na hora (`_contasNaoLidas.Add` antes do rebuild); "Limpar" preserva notificações que chegam durante a animação (só zera bolinha das contas esvaziadas); toast silencioso, toast antes do som, som assíncrono em memória (`SoundPlayer(MemoryStream)`) — **feito na v1.4.4**
- [x] Flutuante — abre em 360×287 na primeira vez (era 900×650), `WindowChrome` (resize pelas bordas), moldura externa no tema com `CornerRadius=10`, toasts/tooltips no tema; tooltips arredondados globais (Barlow, BgPanel2); barra de tarefas sincroniza flutuante/Missões — **feito na v1.4.4**
- [x] Meta de RAM — **200 MB fixos para o app + 1 GB por conta aberta** (antes: 1 GB por conta cadastrada, mesmo fechada) — **feito na v1.4.4**

---

<sub>Documento atualizado a partir do estado atual do app (estado v1.4.4). Veja o <a href="./ROADMAP.html">ROADMAP.html</a> para a versão visual like-site (o HTML ainda descreve a base v1.4.3 e precisa ser regenerado).</sub>
