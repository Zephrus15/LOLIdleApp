# LoL Idle Desktop

Aplicativo desktop não oficial para Windows que permite jogar [LoL Idle](https://lolidle.com/jogar) em uma janela própria, sem precisar manter o navegador aberto.

O projeto utiliza **Microsoft Edge WebView2** para oferecer uma experiência integrada ao Windows, com recursos de janela flutuante, controle de áudio e gerenciamento pela bandeja do sistema.

## 📥 Download

**[Baixar LoL Idle Desktop v1.2.0](https://github.com/Zephrus15/LOLIdleApp/releases/tag/v1.2.0)**

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

## ✨ Recursos

### Janela principal
- Abre o LoL Idle em uma janela própria.
- Interface sem abas ou barra de endereço do navegador.
- Sessão e cookies persistidos entre execuções.
- Links externos direcionados ao navegador padrão.

### Janela flutuante
- Permite manter o jogo em uma janela independente.
- Opção de manter a janela sempre no topo durante a sessão atual.
- Possibilidade de movimentar a janela arrastando pela área do jogo.
- Controles personalizados para áudio, fixar e fechar.
- Controle de silenciamento geral do áudio.
- Possibilidade de fechar a janela flutuante e retornar à janela principal.

### Bandeja do Windows
- Fechar a janela principal oculta o aplicativo na bandeja do sistema.
- Clique esquerdo no ícone para restaurar a janela.
- Clique direito para acessar as opções de abrir ou sair.
- A sessão permanece ativa enquanto o aplicativo estiver oculto.

### Preferências e sessão
- Salvamento automático do tamanho, da posição e das preferências de áudio.
- Restauração das dimensões e da posição da janela principal e da janela flutuante.
- Gerenciamento da sessão do WebView2.

### Limitações desta versão
- Os painéis independentes de Missões, Barra lateral e Personagem não estão incluídos.
- O controle de áudio disponível silencia todo o áudio do jogo. Nesta versão não há uma tela acessível para ajustar música e efeitos separadamente.
- O botão de fixar (sempre no topo) vale para a sessão atual e não é restaurado na próxima execução.
- O botão fechar (×) oculta o aplicativo na bandeja. Para encerrar, use **Sair** no menu da bandeja.

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
artifacts\installer\LoLIdleDesktop-Setup-1.1.0-x64.exe
```

O build baixa automaticamente o bootstrapper do WebView2 para `installer\redist\`. O instalador verifica se o runtime já está presente e o instala quando necessário. As preferências em `%LOCALAPPDATA%\LoLIdleDesktop` são preservadas na desinstalação.

## 📋 Histórico de versões

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
