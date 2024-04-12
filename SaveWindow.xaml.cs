using MagickCrop.Models;
using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace MagickCrop;

public partial class SaveWindow : FluentWindow
{
    private IntPtr hBitmap;
    private string tempPath;

    BitmapImage? SavedSource { get; set; }

    public SaveWindow(string imagePath)
    {
        InitializeComponent();

        uiTitlebar.Title = $"Corrected Image {imagePath}";

        tempPath = imagePath;
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

    private void MainImage_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SavedSource is null)
            return;

        try
        {
            // DoDragDrop with file thumbnail as drag image
            var dataObject = DragDataObject.FromFile(tempPath);
            dataObject.SetDragImage(hBitmap, (int)SavedSource.Width, (int)SavedSource.Height);
            DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
        }
        catch
        {
            // DoDragDrop without drag image
            IDataObject dataObject = new DataObject(DataFormats.FileDrop, new[] { tempPath });
            DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
        }
    }
}
