# LoL Idle Desktop

Aplicativo desktop não oficial para Windows que permite jogar [LoL Idle](https://lolidle.com/jogar) em uma janela própria, sem precisar manter o navegador aberto.

O projeto utiliza **Microsoft Edge WebView2** para oferecer uma experiência integrada ao Windows, com recursos de janela flutuante, controle de áudio e gerenciamento pela bandeja do sistema.

## 📥 Download

**[Baixar LoL Idle Desktop v1.3.8](https://github.com/Zephrus15/LOLIdleApp/releases/tag/v1.3.8)**

### Instalador (quando disponível na Release)

Se a Release incluir o arquivo `LoLIdleDesktop-Setup-<versão>-x64.exe`:

1. Baixe o instalador.
2. Execute e siga o assistente.
3. O instalador cria os atalhos, registra a desinstalação e verifica o WebView2 automaticamente.

As preferências em `%LOCALAPPDATA%\LoLIdleDesktop` são preservadas na desinstalação.

### Instalação pelo ZIP (portátil)

1. Baixe o arquivo `LoLIdleDesktop-win-x64.zip` na página da Release.
2. Extraia o conteúdo para uma pasta de sua preferência.
3. Execute `LoL Idle.exe`.
4. Aguarde o carregamento do jogo e entre normalmente na sua conta.

Não é necessário instalar o .NET separadamente para utilizar o pacote publicado.

## 💛 Apoie o projeto

Se o **LoL Idle Desktop** te ajuda, você pode apoiar o desenvolvimento — qualquer valor já ajuda a manter e melhorar o app 🙂

[![Ko-fi](https://img.shields.io/badge/Apoiar%20no%20Ko--fi-13c3e6?style=for-the-badge)](https://ko-fi.com/zephrus)

- **Ko-fi (Pix, cartão ou PayPal):** https://ko-fi.com/zephrus

O projeto é gratuito e feito por um desenvolvedor independente — contribuições são totalmente voluntárias.

## ✨ Recursos

### Janela principal
- Abre o LoL Idle em uma janela própria.
- Interface sem abas ou barra de endereço do navegador.
- Sessão e cookies persistidos entre execuções.
- Links externos (fora de `lolidle.com`) abrem no navegador padrão.
- Na janela principal: o link não abre a flutuante.
- Na janela flutuante: o link abre no navegador real. Se vier via `window.open` (nova janela), a flutuante fica aberta; se for navegação direta da própria flutuante, ela fecha e o link abre no navegador.
- Hosts tratados: `discord.com`, `discordapp.com`, `discord.gg`, `accounts.google.com`, `topidle.com/voto`, YouTube, TikTok, Instagram.

### Janela flutuante
- A flutuante não é aberta por botão da interface principal. Ela abre quando o jogo solicita nova janela (`window.open` / `NewWindowRequested`, `HandleNewWindowRequested`).
- Ao abrir, a janela principal é oculta sem ir para a bandeja. Ao fechar a flutuante pelo ×, a principal é restaurada.
- Opção de manter a janela sempre no topo (preferência salva e restaurada ao reabrir).
- Possibilidade de movimentar a janela arrastando pela área do jogo (exceto controles).
- Controles personalizados para áudio, fixar e fechar.
- Controle de silenciamento geral do áudio.
- Possibilidade de fechar a janela flutuante e retornar à janela principal.

### Janela de Missões
- Janela separada e destacável, visível só com a janela flutuante aberta.
- Mostra dados das missões (somente leitura) vindos da janela flutuante.
- Arrastável e redimensionável (canto inferior direito, largura e altura).
- Salva tamanho, local e preferência "sempre no topo" (independente da flutuante).
- Lembra o último estado ativo/inativo ao reabrir a flutuante.

### Bandeja do Windows
- Fechar a janela principal (pelo × ou Alt+F4/fechar pelo sistema) abre o diálogo "Fechar o LoL Idle?" (ou aplica a preferência `CloseBehavior`, se definida) e mostra o ícone na bandeja quando a opção "continuar em segundo plano" é usada.
- Clique esquerdo no ícone para restaurar a janela.
- Clique direito para acessar as opções de abrir ou sair (`Restaurar` / `Som > Som do jogo` / `Sair`).
- A sessão permanece ativa enquanto o aplicativo estiver oculto.
- O mudo automático na bandeja só vale se a principal estiver oculta E a flutuante não estiver visível. Com a flutuante aberta, o som continua mesmo com a principal oculta.

### Preferências e sessão
- Salvamento automático do tamanho, da posição e das preferências de áudio.
- Restauração das dimensões e da posição da janela principal, da janela flutuante e da janela de Missões.
- Preferência "sempre no topo" salva separadamente para a janela flutuante e para a janela de Missões.
- Gerenciamento da sessão do WebView2.
- Botão ⚙ "Opções de fechamento" na sidebar para trocar a preferência do botão Fechar (perguntar / continuar na bandeja / sair e desligar as contas).
- Arquivos em `%LOCALAPPDATA%\LoLIdleDesktop`: `window-settings.json` (preferências/geometria), `WebView2\` (sessão/cookies) e `floating-window.log` (log técnico).

### Limitações desta versão
- Painéis disponíveis: **Missões** (somente leitura) incluído. **Barra lateral interativa** e **Personagem** não incluídos (o jogo bloqueia a segunda sessão).
- O controle de áudio disponível silencia todo o áudio do jogo. Não há uma tela acessível pela interface para ajustar música e efeitos separadamente (a tela de configurações de áudio `ShowFloatingSettings` existe no código, mas não é chamada pela interface atual).
- A janela separada de voto (`ShowVoteWindow` / `PrepareVoteWebViewAsync`) existe no código mas não é exibida; voto e login OAuth usam o navegador real.
- O botão fechar (×) e o Alt+F4/fechar pelo sistema abrem o diálogo "Fechar o LoL Idle?" (a menos que a preferência `CloseBehavior` esteja definida — ajuste pelo botão ⚙ "Opções de fechamento" na sidebar). Para encerrar direto, use **Sair** no menu da bandeja ou a opção "Sair e desligar as contas".
- `ReopenFloatingWindow_Click` existe no código sem ligação no `MainWindow.xaml` atual — a flutuante só abre via `window.open` do jogo.

## 🖥️ Requisitos

- Windows 10 ou Windows 11 de 64 bits.
- Microsoft Edge WebView2 Runtime.
- Conexão com a internet.

Se o WebView2 não estiver instalado, obtenha-o pelo [site oficial da Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/).

## 🛠️ Compilar a partir do código-fonte

Para compilar o projeto, é necessário instalar o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

1. Baixe ou clone este repositório.
2. Abra a pasta do projeto no Explorador de Arquivos.
3. Clique na barra de endereço, digite `cmd` e pressione Enter.
4. Execute o arquivo:

```bat
COMPILAR.bat
```

O executável será gerado em:

```text
bin\Release\net8.0-windows\win-x64\publish\LoL Idle.exe
```

As pastas `bin` e `obj` são geradas automaticamente durante a compilação e não precisam ser incluídas no código-fonte.

### Gerar o instalador

O instalador é criado com o [Inno Setup 6](https://jrsoftware.org/isinfo.php). Se ainda não estiver instalado:

```bat
winget install JRSoftware.InnoSetup -e
```

Depois execute:

```bat
GERAR-INSTALADOR.bat
```

O instalador será gerado em:

```text
artifacts\installer\LoLIdleDesktop-Setup-<versão>-x64.exe
```

O build baixa automaticamente o bootstrapper do WebView2 para `installer\redist\`. O instalador verifica se o runtime já está presente e o instala quando necessário. As preferências em `%LOCALAPPDATA%\LoLIdleDesktop` são preservadas na desinstalação.

## 📋 Histórico de versões

### v1.3.8
- Sidebar de contas: a seção **"Últimas notificações"** agora fica encostada no **rodapé da sidebar**, visível nos **dois modos** (lista expandida e lista recolhida), com as linhas de borda a borda. As **4 notificações mais recentes** aparecem de cima para baixo (a antiga em cima, a nova entra por baixo), com nome da conta, o que aconteceu e tempo relativo; **clicar** numa notificação troca para a conta dela e ela sai da lista com animação; o botão **"Limpar"** esvazia a lista (sem tocar nas bolinhas de não lido).
- **Bolinha vermelha de não lida** nos avatares de contas que receberam notificação e ainda não foram vistas — aparece na lista expandida, na faixa recolhida ("Contas ▴") e na barra minimizada; some ao trocar para a conta.
- Lista recolhida ("Contas ▴"): faixa vertical com os avatares das contas para trocar rápido; os botões **🔔 Notificações** e **⚙ Opções de fechamento** continuam visíveis abaixo dos avatares (sempre visíveis nos dois modos).
- Sidebar minimizada: faixinha na borda direita com os avatares das contas conectadas (menores, no topo); o botão de expandir/recolher fica **centralizado**; clique no avatar troca de conta.
- **Animação de recolhimento/expansão** da seção de contas (~210 ms, suavizada).
- **Clicar no balão da bandeja** volta a janela e troca para a conta da notificação (além de restaurar da bandeja).
- **Notificação de poções**: novo aviso quando as poções acabam — balão + som próprio (`notify_potion.wav`) com mensagem por família: "Poções de HP acabaram! Compre mais com o Lojista." (HP) / "Poções de MP acabaram! Compre mais com o Lojista." (MP). Detecção no HUD (`#hud-pocoes`, apenas na transição >0 → 0 — sem falso positivo). Um **único toggle** no 🔔 Notificações — "Poções de HP ou MP acabaram" — silencia as duas famílias.
- **Ao clicar** no balão da bandeja ou na linha da notificação de poções, o personagem também é mandado de volta para a Fonte — **somente no modo automático (Summoner's Rift)**; nas demais áreas/modos o jogo bloqueia o teleporte (mesma regra da tecla B; o aviso do jogo aparece normalmente).
- A seção **"Ajuste qual pedaço da imagem aparece no avatar"** só aparece para fotos escolhidas do PC (não aparece para avatares de campeão — recorte automático do rosto).
- A release **não inclui** botão de teste da notificação de poções (o botão "Testar poções" foi removido após a validação).

### v1.3.7
- Notificações na sidebar: **bolinha vermelha de não lida** nos avatares de contas que receberam notificação e ainda não foram vistas (na lista expandida e nas faixas recolhidas/minimizadas). Some ao trocar para a conta.
- Novo bloco **"Últimas notificações"** na parte de baixo da sidebar (modo expandido): as 4 notificações mais recentes, com nome da conta, o que aconteceu e tempo relativo ("agora", "há X min"). A mais antiga fica em cima e a nova entra por baixo.
- **Clicar numa notificação** troca para a conta dela e ela sai da lista com animação (fade + deslize); a linha tem um pulso de fundo ao ser clicada.
- Botão **"Limpar"** na seção: esvazia a lista com animação escalonada (não afeta as bolinhas de não lido).
- **Clicar no balão da bandeja** agora também troca para a conta da notificação (além de restaurar a janela).
- Com a lista de contas recolhida ("Contas ▴"), a faixa vertical mostra os avatares das contas (clique troca de conta).
- Com a sidebar inteira minimizada, uma faixinha na borda direita mostra os avatares das contas conectadas (menores, no topo), com o botão de expandir/recolher no meio; clique no avatar troca de conta.
- Correções de estabilidade: inicialização das contas WebView2 totalmente protegida por tratamento de exceções (falha ao criar o ambiente de uma conta desregistra só aquela conta, sem afetar as outras; sem corrida de dupla criação em clique duplo rápido); ao desmutar, o app só retoma AudioContext que ele próprio suspendeu; ao remover a conta ativa, o app troca para a principal (flutuante, missões e áudio acompanham) e limpa as preferências por conta da conta removida (o login salvo NÃO é apagado).

### v1.3.6
- Avatares das contas: nova janela **"Editar conta"** (botão ✏️ na sidebar ou clique direto no avatar da conta, estilo Facebook) — renomear + escolher avatar.
- O avatar pode ser **cor + iniciais**, **emoji** (12 opções), **imagem do PC** ou **campeão do LoL Idle**.
- Nova aba **"Campeões"** no editor de avatar, ao lado de "Ícones", com os 12 campeões do LoL Idle: Darius, Ezreal, Lux, Yasuo, Katarina, Taric, Zac, Ryze, Rengar, Shaco, Sona e Aphelios. Os ícones são os sprites pixelados extraídos do próprio jogo (in-game 32×32; Rengar 38×38), embutidos no app — sem depender de rede. Clicar no campeão aplica o avatar na hora, sem janela de recorte; o avatar foca no rosto (recorte 16×16 da cabeça, igual ao HUD do jogo), renderizado com pixels nítidos.
- Ao escolher uma imagem do PC, abre a janela **"Alterar avatar"** (estilo Facebook): preview grande quadrado com bordas arredondadas, **arrastar com o mouse** para reposicionar o recorte e **slider de zoom** (1.0x–4.0x). Mostra uma pré-visualização do chip (como a conta vai ficar na sidebar, 40×40), atualizando ao vivo a cada ajuste. Só aplica ao clicar **"Aplicar"** ("Cancelar" descarta); o posicionamento fica salvo por conta e persiste entre sessões.
- Escolher uma **cor agora apenas pinta o fundo** do avatar (não apaga mais o campeão/imagem escolhida — ideal para sprites transparentes). Nova **cor preta** na grade, com borda sutil para ser visível no painel escuro.
- Clique no avatar abre o editor (antes só havia o botão ✏️); **bordas arredondadas em qualquer avatar**; ícones de emoji agora em branco; bolinha de status do chip não bloqueia mais cliques no avatar; clique num emoji/cor/campeão funciona clicando em qualquer parte da célula.
- Diálogos de fechamento: cards de opção com hover destacado e melhor espaçamento interno.

### v1.3.5
- Diálogo fechar restaurado: clicar no botão fechar abre novamente o diálogo "Fechar o LoL Idle?" (Continuar em segundo plano / Sair e desligar as contas) com opção "Lembrar minha escolha".
- Novo botão ⚙ "Opções de fechamento" na sidebar — permite trocar a preferência de fechamento depois (atalho para abrir e editar as 3 opções).
- Diálogo "Fechar o LoL Idle?" redesenhado: ícones de play/power centralizados com Viewbox, botão Cancelar com estilo escuro discreto, Checkboxes do estilo das Notificações.
- Diálogo "Remover conta": sem a faixa vermelha do topo.

### v1.3.4
- Correção: a janela de Missões lia os dados da conta principal (fallback para `GameView`) em vez da conta na frente. Agora lê sempre a conta ativa (com fallback para a conta principal quando a ativa ainda está sem o tracker/painel indisponível).

### v1.3.3
- Notificações por conta e por tipo (Ledger/Épico/Missão/Inventário/Nível) em checkboxes com tema escuro + toggle geral.
- Quando mais de uma conta está marcada para notificar, o balão mostra o nome da conta que gerou o aviso (ex: Yasuo — Missão concluída!).
- Botão "Concluído" no painel de Notificações; painel no mesmo estilo do app (checkmarks escuros com check verde).

### v1.3.2
- Várias contas reais ao mesmo tempo (cada conta com WebView2/sessão isolada). Troca clicando na lista de contas.
- Lista de contas redesenhada (estilo IdleDeck): avatar com iniciais, bolinha de status (verde = na frente, azul = em segundo plano), item inteiro clicável.
- Renomear conta (✏️) com diálogo customizado no estilo do app; nome personalizado tem prioridade sobre o lido do DOM.
- Diálogo "Fechar o LoL Idle?" (reabrir na bandeja / sair) com opção de lembrar a escolha.
- Diálogo "Remover conta" customizado no estilo do app (confirmação com botão "Remover" vermelho).
- Áudio só da conta na frente; contas em segundo plano mudas.
- Missões mostrem os dados da conta na frente (abre o painel Missões no jogo para carregar).
- Botões da barra de título redesenhados (cantos arredondados, ícones vetoriais, hover vermelho no fechar) e toggle da sidebar corrigido (▶ visível / ◀ escondida).
- Botão "+ Adicionar conta" sem borda cinza; botão "Contas ▴" com hover discreto.

### v1.3.1
- Barra lateral de contas à direita com toggle esconder/mostrar (ícone bate com o estado).
- Conta conectada exibida na barra (lida do DOM do jogo via `ExecuteScriptAsync`).
- Botão "+ Adicionar conta" com limite máximo de 4 contas, estilo visual semelhante ao botão de toggle.

### v1.3.0
- Notificação da bandeja: clicar no balão restaura/abre a janela principal (`BalloonTipClicked` → `RestoreFromTray`).

### v1.2.9
- Missões: fallback inicial liberado quando a flutuante ainda está sem tracker (não fica mais em "Carregando...").

### v1.2.8
- Missões com prioridade por visibilidade (flutuante visível → flutuante primeiro; escondida → principal primeiro) e origem correta no fallback.
- Alt+F4 / fechar pelo sistema agora oculta na bandeja (não minimiza).

### v1.2.7
- Sempre no topo agora é persistente (janela flutuante e Missões, separadas) e sincronizado ao reabrir.
- Flutuante lembra se Missões estava ativa ao fechar.
- Fechar Missões pelo × apaga o caderninho na flutuante.
- Missões redimensiona em largura e altura (com limites mínimos).
- Missões com fallback flutuante → principal (não trava mais em "Carregando...").
- Notificação de missão restrita ao log real (sem tocar ao abrir a engrenagem).

### v1.2.6
- Missões lembra último estado (ativa/desativada, tamanho e local) ao reabrir a flutuante.

### v1.2.5
- Janela de Missões estável (só na flutuante, com barras de progresso e Caçada).
- Votar/Discord/Kick/Twitch abrem no navegador; a flutuante fecha junto ao abrir o link externo.
- Ícones redondos opacos no estilo do jogo e indicador de Missões ativa.
- Maximizar sem cobrir a barra de tarefas e restauração de tamanho/posição.

### v1.2.4
- Som próprio para item épico obtido.

### v1.2.3
- Som de mochila cheia só toca quando enche de verdade (30/30), não a cada item.

### v1.2.2
- Votar abre no navegador direto (TopIdle/OAuth fora da WebView).
- Links externos (Discord, Kick, Twitch) abrem no navegador e fecham a flutuante junto.
- Indicador da aba de Missões aberta (ícone de caderninho dourado na flutuante quando ativa).

### v1.2.1
- Janela de Missões (abre só junto com a flutuante): Passe/Tarefa com barras de progresso, Caçada, negrito nos rótulos, arrastável e redimensionável.
- Flutuante: ícones redondos opacos no estilo do jogo; ao abrir, a principal fecha (só vai para a bandeja ao fechar a principal).
- Bandeja: Restaurar / Som > Som do jogo / Sair.
- Sons de notificação 8-bit no padrão do jogo (lendário, missão, nível, mochila).
- Removida transparência das Missões e janela do personagem (exibição via polling ativo).

### v1.2.0
- Modo bandeja inteligente: jogo mutado na bandeja, com som+balão só em item lendário, missão concluída, mochila cheia e up de nível (sons 8-bit no padrão do jogo).
- Menu da bandeja (botão direito): Restaurar / Som > Som do jogo / Sair.
- Janela flutuante: ícones redondos opacos no estilo do jogo; ao abrir, a principal fecha (só vai para a bandeja ao fechar a principal).
- Correções de áudio: autoplay liberado e sincronização de música/efeitos via botões do jogo.

### v1.1.0
- Introdução da janela flutuante independente.
- Controles personalizados para a janela flutuante.
- Opção de manter a janela sempre no topo.
- Opção para silenciar o áudio.
- Movimentação da janela pelo jogo.
- Integração com a bandeja do Windows.
- Restauração da janela principal.
- Salvamento automático de preferências e dimensões.
- Melhorias no gerenciamento da sessão WebView2.
- Melhorias no comportamento de minimizar, maximizar, restaurar e fechar.
- Interface principal reorganizada.
- Melhorias na organização do projeto e no pacote de distribuição.
- Remoção dos painéis independentes de Missões, Barra lateral e Personagem desta versão.

## ⚠️ Avisos

- Este é um projeto independente e não oficial.
- O aplicativo funciona como um contêiner desktop para o site original do LoL Idle.
- O jogo, a conta, o login e os dados do jogo continuam sob responsabilidade do site oficial.
- É necessária conexão com a internet para jogar.
- O projeto não possui afiliação, parceria ou endosso da Riot Games ou dos responsáveis pelo LoL Idle.

---

**Desenvolvido para tornar a experiência de jogar LoL Idle no Windows mais prática e confortável.** ❤️
