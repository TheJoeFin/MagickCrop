using ImageMagick;
using MagickCrop.Models;
using System.Windows;
using System.Windows.Controls;

namespace MagickCrop.Controls;

public partial class SaveOptionsDialog : UserControl
{
    private static readonly List<FormatItem> _formats =
    [
        new FormatItem { Name = "PNG Image", Format = MagickFormat.Png, Extension = ".png", SupportsQuality = false },
        new FormatItem { Name = "JPEG Image", Format = MagickFormat.Jpg, Extension = ".jpg", SupportsQuality = true },
        new FormatItem { Name = "BMP Image", Format = MagickFormat.Bmp, Extension = ".bmp", SupportsQuality = false },
        new FormatItem { Name = "TIFF Image", Format = MagickFormat.Tiff, Extension = ".tiff", SupportsQuality = false },
        new FormatItem { Name = "WebP Image", Format = MagickFormat.WebP, Extension = ".webp", SupportsQuality = true },
        // new FormatItem { Name = "HEIC Image", Format = MagickFormat.Heic, Extension = ".heic", SupportsQuality = true }
    ];

    private double originalWidth;
    private double originalHeight;
    private double aspectRatio;
    private bool updatingDimensions = false;

    public SaveOptions Options { get; private set; }

    public SaveOptionsDialog(double imageWidth, double imageHeight)
    {
        InitializeComponent();

        // Store original dimensions and calculate aspect ratio
        originalWidth = imageWidth;
        originalHeight = imageHeight;
        aspectRatio = originalHeight / originalWidth;

        // Initialize format dropdown
        FormatComboBox.ItemsSource = _formats;
        FormatComboBox.SelectedIndex = 0;

        // Set initial dimensions
        WidthBox.Value = originalWidth;
        HeightBox.Value = originalHeight;

        // Initialize options object
        Options = new SaveOptions
        {
            Format = MagickFormat.Png,
            Extension = ".png",
            Resize = false,
            Width = (int)originalWidth,
            Height = (int)originalHeight,
            MaintainAspectRatio = true
        };
    }

    private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (FormatComboBox.SelectedItem is FormatItem selectedFormat)
        {
            Options.Format = selectedFormat.Format;
            Options.Extension = selectedFormat.Extension;

            // Show/hide quality slider based on format
            QualityGrid.Visibility = selectedFormat.SupportsQuality ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;

        int quality = (int)QualitySlider.Value;
        QualityValueText.Text = $"{quality}%";
        Options.Quality = quality;
    }

    private void ResizeCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        bool isChecked = ResizeCheckBox.IsChecked == true;

        WidthBox.IsEnabled = isChecked;
        HeightBox.IsEnabled = isChecked;
        MaintainAspectRatioCheckBox.IsEnabled = isChecked;

        Options.Resize = isChecked;
    }

    private void WidthBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (updatingDimensions || WidthBox.Value == null)
            return;

        Options.Width = (int)WidthBox.Value.Value;

        if (MaintainAspectRatioCheckBox.IsChecked == true)
        {
            updatingDimensions = true;
            HeightBox.Value = (int)(WidthBox.Value.Value * aspectRatio);
            Options.Height = (int)HeightBox.Value.Value;
            updatingDimensions = false;
        }
    }

    private void HeightBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (updatingDimensions || HeightBox.Value == null)
            return;

        Options.Height = (int)HeightBox.Value.Value;

        if (MaintainAspectRatioCheckBox.IsChecked == true)
        {
            updatingDimensions = true;
            WidthBox.Value = (int)(HeightBox.Value.Value / aspectRatio);
            Options.Width = (int)WidthBox.Value.Value;
            updatingDimensions = false;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Update final options
        Options.MaintainAspectRatio = MaintainAspectRatioCheckBox.IsChecked == true;

        if (WidthBox.Value is 0 || HeightBox.Value is 0)
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Invalid Dimensions",
                Content = "Width and Height must be greater than 0.",
            };

            await uiMessageBox.ShowDialogAsync();
            return;
        }

        Window.GetWindow(this).DialogResult = true;
        Window.GetWindow(this).Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this).DialogResult = false;
        Window.GetWindow(this).Close();
    }

    public static string GetFileFilter(FormatItem format)
    {
        return $"{format.Name}|*{format.Extension}";
    }

    public static string GetAllFileFilters()
    {
        string allFilters = "All supported formats|";
        string individualFilters = "";

        foreach (FormatItem format in _formats)
        {
            allFilters += $"*{format.Extension};";
            individualFilters += $"|{format.Name}|*{format.Extension}";
        }

        // Remove trailing semicolon
        allFilters = allFilters.TrimEnd(';');

        return allFilters + individualFilters;
    }
}
