# 🗺️ Roadmap — LoL Idle Desktop

> Base: **v1.2.9** · Última atualização: 2026-11-09
> Versão web estilizada: [`ROADMAP.html`](./ROADMAP.html) — abra no navegador para visualizar como site.

<p align="center">
  <img src="https://img.shields.io/badge/Status-3%20feitos-2ea043?style=for-the-badge" alt="feitos">
  <img src="https://img.shields.io/badge/Status-2%20imposs%C3%ADveis-ff4d4f?style=for-the-badge" alt="impossíveis">
  <img src="https://img.shields.io/badge/Status-1%20futuro-58a6ff?style=for-the-badge" alt="futuro">
  <img src="https://img.shields.io/badge/Status-2%20pendentes%2Fpolir-f0b429?style=for-the-badge" alt="pendentes">
</p>

---

## 📊 Resumo rápido

| Painel / Recurso | Status | Versão |
|---|---|---|
| 📖 **Missões** | ✅ **Confirmado** | v1.2.5 / v1.2.6 |
| 🎛️ **Barra lateral (interativa)** | ❌ **Impossível** | — (jogo bloqueia 2ª sessão) |
| 🧙 **Personagem (interativo)** | ❌ **Impossível** | — (jogo bloqueia 2ª sessão) |
| 🔔 **Bandeja inteligente (sons)** | ✅ **Confirmado** | v1.2.0 → v1.2.7 |
| 📌 **Sempre no topo persistente** | ✅ **Confirmado** | v1.2.7 |
| 📱 **Android** | 🔵 **Futuro** | — |
| 🔊 **Áudio separado (música/efeitos)** | 🟡 **Pendente — precisa de decisão** | lógica existe; UI inacessível (`ShowFloatingSettings` sem chamada) |
| 🔄 **Aviso de versão nova** | 🟡 **Falta polir** | — |

---

## ✅ 1. Painéis independentes

### 📖 Missões — `FEITO` ✅

Painel separado e destacável que **já está pronto** (v1.2.5 / v1.2.6):

- Só aparece com a **flutuante aberta**
- Arrastável / redimensionável, **borda amarela**
- Progresso: Passe, Tarefa, Caçada
- Lembra último estado: ativa/desativada, tamanho e local
- Ícone sincronizado com o estado real

> ℹ️ É um painel **só-texto** (igual ao que a Barra lateral poderia ser), sem segunda sessão do jogo.

### 🎛️ Barra lateral — `IMPOSSÍVEL (interativo)` ❌

- **Por que não dá:** o LoL Idle **bloqueia a segunda sessão/WebView**.
- Um clone vivo/interativo da barra **não funciona**.
- Só seria viável a versão **só-texto** (como Missões) — **não é o desejado** → marcado como impossível.

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
- Detecção via `MutationObserver` no DOM + `postMessage` → toca som e balão na bandeja
- Sessão permanece ativa enquanto oculto

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
| Áudio | 🟡 Lógica de música/efeitos existe no código (`AudioBootstrapScript` + `bt-musica`/`bt-efeitos`), mas a tela `ShowFloatingSettings()` **não é chamada pela interface** — precisa de decisão para expor |
| Sempre no topo | ✅ **Resolvido na v1.2.7** — persistente por janela (`FloatingTopmost` / `MissionsTopmost`) |

---

## 🎯 Próximo foco

- [ ] Sons de notificação (volume/timbre por evento)
- [ ] Detalhes da flutuante + Missões
- [ ] Áudio separado (música/efeitos) — **decisão necessária:** expor a tela `ShowFloatingSettings()` (hoje órfã) ou manter apenas os botões `bt-musica`/`bt-efeitos` do jogo
- [x] Sempre no topo persistente — **feito na v1.2.7** (`FloatingTopmost` / `MissionsTopmost`, com sincronia visual)
- [ ] Aviso de nova versão (GitHub)

---

<sub>Documento gerado a partir do estado atual do app \(v1.2.9\). Veja o <a href="./ROADMAP.html">ROADMAP.html</a> para a versão visual like-site.</sub>
