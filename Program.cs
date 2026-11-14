using System;

namespace LoLIdleDesktop;

// Entry point explícito (StartupObject no csproj). O VelopackApp.Build().Run() precisa
// rodar ANTES de qualquer WPF: é ele quem intercepta os argumentos --veloapp-* que o
// Setup/Update do Velopack passa (install, update, uninstall, first-run) e sai antes
// de a janela principal existir. Sem isso, o app abriria normalmente durante um update.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Velopack.VelopackApp.Build().Run();

        // Guarda de WebView2: o Inno Setup baixava o bootstrapper; o Setup do Velopack
        // não. Preferimos avisar claramente e sair com código não-zero a abrir um app
        // que vai falhar mais tarde sem contexto. MessageBox funciona aqui (STA), antes
        // do App existir. Qualquer erro na detecção conta como "runtime ausente".
        try
        {
            _ = Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .GetAvailableBrowserVersionString();
        }
        catch
        {
            try
            {
                System.Windows.MessageBox.Show(
                    "O Microsoft Edge WebView2 Runtime é necessário para o LoL Idle.\n\n" +
                    "Baixe e instale o WebView2 Evergreen Runtime em:\n" +
                    "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                    "Depois de instalar, abra o LoL Idle novamente.",
                    "LoL Idle — WebView2 necessário",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            catch
            {
                // Nem o aviso conseguiu aparecer (sessão sem área de trabalho, etc.):
                // mesmo assim sair não-zero em vez de seguir para um WPF que vai falhar.
            }
            Environment.Exit(1);
        }

        App app = new();
        app.InitializeComponent();
        app.Run();
    }
}
