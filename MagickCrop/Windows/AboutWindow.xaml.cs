using System.Diagnostics;
using System.Windows.Navigation;
using Windows.ApplicationModel;
using Wpf.Ui.Controls;

namespace MagickCrop.Windows;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"Version {GetAppVersion()}";
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private static string GetAppVersion()
    {
        PackageVersion version = Package.Current.Id.Version;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
