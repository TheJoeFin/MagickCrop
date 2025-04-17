using System.IO;
using System.Windows;

namespace MagickCrop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && File.Exists(e.Args[0])
            && Path.GetExtension(e.Args[0]).Equals(".mcm", StringComparison.OrdinalIgnoreCase))
        {
            MainWindow mainWindow = new();
            mainWindow.LoadMeasurementsPackageFromFile(e.Args[0]);
            mainWindow.Show();
            return;
        }

        MainWindow normalMainWindow = new();
        normalMainWindow.Show();
    }
}

