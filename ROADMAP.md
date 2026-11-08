# Roadmap — LoL Idle Desktop

Versão base: **v1.1.0**

## Próximas funcionalidades

### 1. Painéis independentes
- **Missões** — painel separado e destacável
- **Barra lateral** — painel separado e destacável
- **Personagem** — painel separado e destacável
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
- Painéis independentes de Missões, Barra lateral e Personagem **não incluídos** → ver item 1
- Áudio só tem mute geral, sem volume separado música/efeitos
- Botão "sempre no topo" só vale para a sessão atual, não persiste
