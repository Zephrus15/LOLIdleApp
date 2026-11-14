using System.Runtime.InteropServices;
using System.Windows;

namespace LoLIdleDesktop;

public partial class App : System.Windows.Application
{
    internal const string AppAumid = "Zephrus15.LoLIdle.Desktop";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    protected override void OnStartup(StartupEventArgs e)
    {
        try { SetCurrentProcessExplicitAppUserModelID(AppAumid); } catch { }
        base.OnStartup(e);
    }
}
