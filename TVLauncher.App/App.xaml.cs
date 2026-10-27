using System.Windows;
using Velopack;

namespace TVLauncher.App;

public partial class App : Application
{
    // Velopack precisa interceptar os eventos de instalação antes de criar qualquer janela WPF.
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
