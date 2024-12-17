using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace MagickCrop.Controls;

public partial class WelcomeMessage : UserControl
{
    public RoutedEventHandler PrimaryButtonEvent
    {
        get { return (RoutedEventHandler)GetValue(PrimaryButtonEventProperty); }
        set { SetValue(PrimaryButtonEventProperty, value); }
    }

    public static readonly DependencyProperty PrimaryButtonEventProperty =
        DependencyProperty.Register("PrimaryButtonEvent", typeof(RoutedEventHandler), typeof(WelcomeMessage), new PropertyMetadata(null));

    public WelcomeMessage()
    {
        InitializeComponent();
    }


    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.JoeFinApps.com",
            UseShellExecute = true
        });
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        WelcomeBorder.Visibility = Visibility.Collapsed;
        PrimaryButtonEvent?.Invoke(sender, e);
    }

    private void SourceLink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/TheJoeFin/MagickCrop",
            UseShellExecute = true
        });
    }
}
