using Velopack;
using Application = System.Windows.Application; // UseWindowsForms also defines Application

namespace RemoteHub;

/// <summary>
/// Explicit entry point. Exists only so Velopack can run before WPF: the auto-generated
/// <c>Main</c> is disabled via <c>&lt;StartupObject&gt;</c> + App.xaml compiled as a Page.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        // MUST be the first statement: handles the install/update/uninstall hooks and may terminate
        // the process outright. Anything before it would run during those hooks too.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
