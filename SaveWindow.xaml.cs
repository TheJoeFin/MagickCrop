using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace MagickCrop;

public partial class SaveWindow : FluentWindow
{
    BitmapImage? SavedSource { get; set; }

    public SaveWindow(string imagePath)
    {
        InitializeComponent();

        uiTitlebar.Title = $"Corrected Image {imagePath}";

        Uri uriSource = new(imagePath);
        SavedSource = new BitmapImage();
        SavedSource.BeginInit();
        SavedSource.CacheOption = BitmapCacheOption.OnLoad;
        SavedSource.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        SavedSource.UriSource = uriSource;
        SavedSource.EndInit();
        MainImage.Source = SavedSource;
    }

    private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SavedSource?.StreamSource?.Close();
        SavedSource?.StreamSource?.Dispose();
        SavedSource = null;
        MainImage.Source = null;
        GC.Collect();
    }
}
