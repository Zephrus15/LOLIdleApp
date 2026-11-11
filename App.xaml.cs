using System.Runtime.InteropServices;
using System.Windows;

namespace LoLIdleDesktop;

public partial class App : System.Windows.Application
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    protected override void OnStartup(StartupEventArgs e)
    {
        try { SetCurrentProcessExplicitAppUserModelID("Zephrus15.LoLIdle.Desktop"); } catch { }
        base.OnStartup(e);
    }
}
