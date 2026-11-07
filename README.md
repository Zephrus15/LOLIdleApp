# LoL Idle Desktop

Aplicativo desktop não oficial para Windows que abre o [LoL Idle](https://lolidle.com/jogar) em uma janela própria, usando Microsoft Edge WebView2.

## Baixar

Acesse **[Releases](https://github.com/Zephrus15/LOLIdleApp/releases)** e baixe `LoLIdleDesktop-win-x64.zip`.

1. Extraia o ZIP para uma pasta de sua preferência.
2. Execute `LoL Idle.exe`.
3. Entre no jogo normalmente.

O pacote é publicado para Windows 10/11 de 64 bits e inclui o runtime .NET necessário. O **Microsoft Edge WebView2 Runtime** também é necessário; em muitos computadores ele já está instalado junto com o Edge. Se o aplicativo informar que o WebView2 está ausente, instale-o pelo [site oficial da Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/).

> Se ainda não houver um arquivo ZIP na página Releases, significa que a compilação automática ainda não terminou ou que uma versão ainda não foi publicada.

## Recursos atuais

- Janela própria, sem abas ou barra de endereço do navegador.
- Botão de modo compacto e controles personalizados de janela.
- Minimizar (－) envia a janela para a barra de tarefas do Windows.
- Fechar (×) oculta o aplicativo na bandeja do sistema, sem encerrar o jogo.
- Clique esquerdo no ícone da bandeja restaura a janela; clique direito abre as opções para abrir ou sair.
- Cookies e sessão persistidos em `%LOCALAPPDATA%\LoLIdleDesktop\WebView2`.
- Links externos abrem no navegador padrão.

## Requisitos

- Windows 10 ou Windows 11 de 64 bits.
- Microsoft Edge WebView2 Runtime.
- Para compilar a partir do código-fonte: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Compilar a partir do código-fonte

1. Instale o .NET 8 SDK.
2. Abra a pasta do projeto no Explorador de Arquivos.
3. Clique na barra de endereço, digite `cmd` e pressione Enter.
4. Execute:

   ```bat
   COMPILAR.bat
   ```

O executável será criado em:

```
bin\Release\net8.0-windows\win-x64\publish\LoL Idle.exe
```

As pastas `bin` e `obj` são geradas durante a compilação e não precisam estar no código-fonte.

## Publicação de novas versões

A versão atual em preparação é **1.0.1**. Para gerar o pacote de uma versão, publique uma nova Release no GitHub com a tag correspondente, por exemplo `v1.0.1`. A GitHub Actions compila o projeto para Windows x64 e anexa automaticamente `LoLIdleDesktop-win-x64.zip` à Release.

## Avisos

- Este aplicativo é apenas um contêiner desktop para o site oficial. O jogo, a conta e o login continuam sendo fornecidos por lolidle.com.
- É necessária conexão com a internet para acessar o jogo.
- Ocultar na bandeja mantém o aplicativo e a sessão ativos. A opção **Sair** no menu da bandeja encerra o aplicativo.
- Projeto independente, sem afiliação ou endosso da Riot Games ou dos responsáveis pelo LoL Idle.
