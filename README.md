LoL Idle - Aplicativo desktop para Windows

O que é
-------
Um aplicativo Windows com janela própria que abre https://lolidle.com/jogar
usando Microsoft Edge WebView2. Não altera o jogo nem automatiza ações.

Recursos
--------
- Janela personalizada sem abas ou barra de endereço do navegador.
- Botões voltar, avançar, atualizar e início.
- Cookies e sessão persistidos em %LOCALAPPDATA%\LoLIdleDesktop\WebView2.
- Links externos abrem no navegador padrão.
- Pode ser minimizado; para continuar jogando, mantenha o aplicativo aberto.

Requisitos
----------
- Windows 10 ou Windows 11 de 64 bits.
- .NET 8 SDK para compilar.
- Microsoft Edge WebView2 Runtime (normalmente já vem instalado com o Edge).

Como compilar
-------------
1. Instale o .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
2. Abra a pasta deste projeto no Explorador.
3. Clique na barra de endereço, digite cmd e pressione Enter.
4. Execute:

   dotnet restore
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

5. O executável será criado em:
   bin\Release\net8.0-windows\win-x64\publish\LoL Idle.exe

Observações importantes
----------------------
- Este projeto é um contêiner desktop para o site oficial; o jogo e o login
  continuam sendo fornecidos pelo próprio lolidle.com.
- A primeira abertura precisa de internet.
- Fechar o aplicativo encerra a sessão ativa do jogo. Minimizá-lo não.
- O projeto não é oficial nem afiliado à Riot Games ou ao LoL Idle.
