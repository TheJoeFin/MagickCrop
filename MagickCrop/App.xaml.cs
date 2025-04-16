using System.IO;
using System.Windows;

namespace MagickCrop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && File.Exists(e.Args[0]) && Path.GetExtension(e.Args[0]).Equals(".mcm", StringComparison.OrdinalIgnoreCase))
        {
            MainWindow mainWindow = new();
            mainWindow.LoadMeasurementsPackageFromFile(e.Args[0]);
            mainWindow.Show();
        }
        else
        {
            MainWindow mainWindow = new();
            mainWindow.Show();
        }
    }
}

