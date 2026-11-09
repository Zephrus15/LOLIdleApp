# Roadmap — LoL Idle Desktop

Versão base: **v1.2.6**

## Próximas funcionalidades

### 1. Painéis independentes
- **Missões** — ✅ **Confirmado (feito em v1.2.5/v1.2.6)** — painel separado e destacável, só aparece com a flutuante aberta, arrastável/redimensionável, borda amarela, barra de progresso Passe/Tarefa, Caçada, lembra último estado (ativa/desativada, tamanho e local)
- **Barra lateral** — ❌ **Impossível (interativo)** — jogo bloqueia segunda sessão; clone vivo/interativo não funciona (só seria viável versão só-texto como Missões, que não é o desejado)
- **Personagem** — ❌ **Não confirmado** — não tem como fazer do jeito desejado (segunda sessão/WebView é bloqueada pelo jogo)
- Comportamento esperado: cada painel pode ficar em janela própria, sempre no topo se necessário, e sem travar a janela principal
- Salvar posição/tamanho de cada painel (como já fazemos com a principal e a flutuante)

### 2. Modo bandeja inteligente (áudio por eventos) ✅ v1.2.0
- Ao minimizar para a bandeja, o app fica **mutado por padrão** (configurável)
- Som só toca em **notificações de item importante**, sendo:
  - **Item lendário** obtido (raridade lendária)
  - **Missão concluída** (qualquer missão finalizada)
  - **Inventário cheio**
- Detecção via `MutationObserver` no DOM + `postMessage` para o app tocar `notify.wav` e balão na bandeja
- Manter a sessão ativa enquanto oculto (comportamento atual)

### 3. App para Android
- Versão Android do LoL Idle Desktop (WebView nativa)
- Requisitos de paridade: login/sessão persistente, janela aceitável em mobile, controle de áudio
- Definir estratégia: MAUI / .NET Android / WebView wrapper nativo

## Limitações atuais (base para o roadmap)
- Painel de **Missões**: ✅ **já incluído** (v1.2.5/v1.2.6) → ver item 1
- Painéis independentes de Barra lateral e Personagem **não incluídos** → ver item 1 (Barra lateral interativa e Personagem inviáveis do jeito desejado: jogo bloqueia segunda sessão)
- Áudio só tem mute geral, sem volume separado música/efeitos
- Botão "sempre no topo" só vale para a sessão atual, não persiste
