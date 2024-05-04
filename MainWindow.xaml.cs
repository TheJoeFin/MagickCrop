using ImageMagick;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MagickCrop;

public partial class MainWindow : FluentWindow
{
    private bool isDragging = false;
    private Point clickedPoint = new();
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Polygon? lines;
    private string? imagePath;
    private string? savedPath;
    private bool isCropping = false;

    private double scaleFactor = 1;
    private bool isPanning = false;

    private string openedFileName = string.Empty;

    private List<UIElement> _polygonElements;

    public MainWindow()
    {
        ThemeService themeService = new();
        themeService.SetTheme(ApplicationTheme.Dark);

        Color teal = (Color)ColorConverter.ConvertFromString("#0066FF");
        ApplicationAccentColorManager.Apply(teal);

        InitializeComponent();
        DrawPolyLine();
        _polygonElements = [lines, TopLeft, TopRight, BottomRight, BottomLeft];
    }

    private void DrawPolyLine()
    {
        Color color = (Color)ColorConverter.ConvertFromString("#0066FF");

        if (lines is not null)
            ShapeCanvas.Children.Remove(lines);

        lines = new()
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            IsHitTestVisible = false,
            Opacity = 0.8,
        };

        List<Ellipse> ellipseList = ShapeCanvas.Children.OfType<Ellipse>().ToList();

        foreach (Ellipse ellipse in ellipseList)
        {
            lines.Points.Add(
                new Point(Canvas.GetLeft(ellipse) + ellipse.Width / 2,
                                Canvas.GetTop(ellipse) + ellipse.Height / 2));
        }

        ShapeCanvas.Children.Add(lines);
    }

    private void TopLeft_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not string intAsString)
            return;

        pointDraggingIndex = int.Parse(intAsString);
        clickedElement = ellipse;
        isDragging = true;
        clickedPoint = e.GetPosition(ShapeCanvas);
        CaptureMouse();
    }

    private void TopLeft_MouseMove(object sender, MouseEventArgs e)
    {
        if (isPanning)
        {
            PanCanvas(e);
            return;
        }

        if (Mouse.LeftButton != MouseButtonState.Pressed && Mouse.RightButton != MouseButtonState.Pressed)
        {
            isDragging = false;
            clickedElement = null;
            ReleaseMouseCapture();
        }

        if (!isDragging || clickedElement is null)
            return;

        Point movingPoint = e.GetPosition(ShapeCanvas);
        Canvas.SetTop(clickedElement, movingPoint.Y - (clickedElement.Height / 2));
        Canvas.SetLeft(clickedElement, movingPoint.X - (clickedElement.Width / 2));

        MovePolyline(movingPoint);
    }

    private void PanCanvas(MouseEventArgs e)
    {
        if (Mouse.MiddleButton == MouseButtonState.Released && Mouse.LeftButton == MouseButtonState.Released)
        {
            isPanning = false;
            return;
        }


        Point currentPoint = e.GetPosition(ShapeCanvas);
        double deltaX = currentPoint.X - clickedPoint.X;
        double deltaY = currentPoint.Y - clickedPoint.Y;

        if (ShapeCanvas.RenderTransform is not MatrixTransform matTrans)
            return;

        Matrix mat = matTrans.Matrix;
        mat.Translate(deltaX, deltaY);
        matTrans.Matrix = mat;
        e.Handled = true;
        Debug.WriteLine($"scaleFactor: {scaleFactor}");
    }

    private void MovePolyline(Point newPoint)
    {
        if (pointDraggingIndex < 0 || lines is null)
            return;

        lines.Points[pointDraggingIndex] = newPoint;
    }

    private async Task<MagickImage> CorrectDistortion(string pathOfImage)
    {
        if (lines is null)
            return new();

        AspectRatio aspectRatioEnum = AspectRatio.LetterLandscape;
        if (AspectRatioComboBox.SelectedItem is ComboBoxItem boxItem && boxItem.Tag is string tag)
            _ = Enum.TryParse(tag, out aspectRatioEnum);

        MagickImage image = new(pathOfImage);
        double scaleFactor = image.Width / MainImage.ActualWidth;

        //  #   X     Y
        //  1   798   304
        //  2   2410  236
        //  3   2753  1405
        //  4   704   1556
        //  3264 x 1836

        // Ratio defined by Height / Width
        double aspectRatio = 1;

        switch (aspectRatioEnum)
        {
            case AspectRatio.Square:
                // already 1
                break;
            case AspectRatio.LetterPortrait:
                aspectRatio = 11 / 8.5;
                break;
            case AspectRatio.LetterLandscape:
                aspectRatio = 8.5 / 11;
                break;
            case AspectRatio.A4Portrait:
                aspectRatio = 297 / 210;
                break;
            case AspectRatio.A4Landscape:
                aspectRatio = 210 / 297;
                break;
            case AspectRatio.UsDollarBillLandscape:
                aspectRatio = 2.61 / 6.14;
                break;
            case AspectRatio.UsDollarBillPortrait:
                aspectRatio = 6.14 / 2.61;
                break;
            case AspectRatio.Custom:
                if (CustomHeight.Value is double height
                    && CustomWidth.Value is double width
                    && height != 0
                    && width != 0)
                    aspectRatio = height / width;
                break;
            default:
                break;
        }

        Rect? visualContentBounds = GetPrivatePropertyValue(lines, "VisualContentBounds") as Rect?;
        Rect finalSize = new(0, 0, MainImage.ActualWidth, MainImage.ActualHeight);

        if (visualContentBounds is not null)
        {
            int width = (int)(visualContentBounds.Value.Width * scaleFactor);
            int height = (int)(width * aspectRatio);
            finalSize = new(0, 0, width, height);
        }

        double[] arguments =
        [
            // top left
            lines.Points[0].X * scaleFactor, lines.Points[0].Y * scaleFactor,
            0,0,

            // bottom left
            lines.Points[3].X * scaleFactor, lines.Points[3].Y * scaleFactor,
            0, finalSize.Height,

            // bottom right
            lines.Points[2].X * scaleFactor, lines.Points[2].Y * scaleFactor,
            finalSize.Width, finalSize.Height,

            // top right
            lines.Points[1].X * scaleFactor, lines.Points[1].Y * scaleFactor,
            finalSize.Width, 0,
        ];
        DistortSettings distortSettings = new()
        {
            Bestfit = true,
        };

        try
        {
            await Task.Run(() => image.Distort(DistortMethod.Perspective, distortSettings, arguments));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return image;
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage image = await CorrectDistortion(imagePath);

        string tempFileName = System.IO.Path.GetTempFileName();
        await image.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = image.ToBitmapSource();

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        SetUiForCompletedTask();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SetUiForLongTask();

        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Image Files|*.jpg;",
            RestoreDirectory = true,
            FileName = $"{openedFileName}_corrected.jpg",
        };

        if (saveFileDialog.ShowDialog() is not true || lines is null)
        {
            BottomPane.IsEnabled = true;
            BottomPane.Cursor = null;
            return;
        }

        string correctedImageFileName = saveFileDialog.FileName;

        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(correctedImageFileName))
        {
            SetUiForCompletedTask();
            return;
        }

        MagickImage image = await CorrectDistortion(imagePath);

        try
        {
            await image.WriteAsync(correctedImageFileName);

            OpenFolderButton.IsEnabled = true;
            SaveWindow saveWindow = new(correctedImageFileName);
            saveWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            savedPath = correctedImageFileName;

            SetUiForCompletedTask();
            image.Dispose();
        }
    }

    private void SetUiForLongTask()
    {
        BottomPane.IsEnabled = false;
        BottomPane.Cursor = Cursors.Wait;
        IsWorkingBar.Visibility = Visibility.Visible;
    }

    private void SetUiForCompletedTask()
    {
        IsWorkingBar.Visibility = Visibility.Collapsed;
        BottomPane.Cursor = null;
        BottomPane.IsEnabled = true;
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.heic;*.bmp|All files (*.*)|*.*",
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        wpfuiTitleBar.Title = $"Magick Crop: {openFileDialog.FileName}";
        await OpenImagePath(openFileDialog.FileName);
    }

    private async Task OpenImagePath(string imageFilePath)
    {
        Save.IsEnabled = true;
        ApplyButton.IsEnabled = true;
        MagickImage bitmap = new(imageFilePath);
        bitmap.AutoOrient();

        string tempFileName = System.IO.Path.GetTempFileName();
        await bitmap.WriteAsync(tempFileName);

        MagickImage bitmapImage = new(tempFileName);

        imagePath = tempFileName;
        openedFileName = System.IO.Path.GetFileNameWithoutExtension(imageFilePath);
        MainImage.Source = bitmapImage.ToBitmapSource();
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? folderPath = System.IO.Path.GetDirectoryName(savedPath);

        if (folderPath is null || lines is null)
            return;

        Process.Start("explorer.exe", folderPath);
    }

    private static object? GetPrivatePropertyValue(object obj, string propName)
    {
        ArgumentNullException.ThrowIfNull(obj);

        Type t = obj.GetType();
        PropertyInfo? pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new ArgumentOutOfRangeException(nameof(propName), string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
        return pi.GetValue(obj, null);
    }

    private void ShapeCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ShapeCanvas.RenderTransform is not MatrixTransform matTrans)
            return;

        Point pos1 = e.GetPosition(ShapeCanvas);

        scaleFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;

        Debug.WriteLine($"scaleFactor: {scaleFactor}");
        Matrix mat = matTrans.Matrix;
        mat.ScaleAt(scaleFactor, scaleFactor, pos1.X, pos1.Y);
        matTrans.Matrix = mat;
        e.Handled = true;
    }

    private void ShapeCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.MiddleButton == MouseButtonState.Pressed || Mouse.LeftButton == MouseButtonState.Pressed)
        {
            clickedPoint = e.GetPosition(ShapeCanvas);
            isPanning = true;
        }
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.JoeFinApps.com",
            UseShellExecute = true
        });
    }

    private void AspectRatioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem item || !IsLoaded)
            return;

        if (item.Tag is "Custom")
        {
            CustomButtonGrid.Visibility = Visibility.Visible;
            return;
        }

        CustomButtonGrid.Visibility = Visibility.Hidden;
    }

    private void CustomWidth_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        double aspectRatio = double.NaN;

        if (CustomHeight.Value is double height && CustomWidth.Value is double width && height != 0 && width != 0)
            aspectRatio = height / width;

        double trimmedValue = Math.Round(aspectRatio, 2);
        CustomComboBoxItem.Content = $"Custom aspect ratio: {trimmedValue}";
    }

    private void FluentWindow_PreviewDragOver(object sender, DragEventArgs e)
    {
        bool isText = e.Data.GetDataPresent("Text");
        e.Handled = true;

        if (isText)
        {
            string textData = (string)e.Data.GetData("Text");
            if (!File.Exists(textData))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
        }

        // After here we will now allow the dropping of "non-text" content
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void FluentWindow_PreviewDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent("Text"))
        {
            if (e.Data.GetData("Text") is string filePath && File.Exists(filePath))
                await OpenImagePath(filePath);
            return;
        }


        if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
        {
            if (e.Data.GetData(DataFormats.FileDrop, true) is not string[] fileNames || fileNames.Length == 0)
                return;

            if (File.Exists(fileNames[0]))
                await OpenImagePath(fileNames[0]);
        }
    }

    private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ShapeCanvas.RenderTransform is not MatrixTransform matTrans)
            return;

        matTrans.Matrix = new Matrix();
    }

    private async void AutoContrastMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.SigmoidalContrast(5));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void WhiteBalanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.WhiteBalance());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void BlackPointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.BlackThreshold(new Percentage(10)));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void WhitePointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.WhiteThreshold(new Percentage(90)));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void GrayscaleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Grayscale());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void InvertMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Negate());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void AutoLevelsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.AutoLevel());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void AutoGammaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.AutoGamma());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void Rotate90CwMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Rotate(90));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void Rotate90CcwMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Rotate(-90));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private void SquareThePolygon()
    {
        if (lines is null)
            return;

        Point topLeftPoint = lines.Points[0];
        Point bottomRightPoint = lines.Points[2];

        Point newTopRight = new(bottomRightPoint.X, topLeftPoint.Y);
        Point newBottomLeft = new(topLeftPoint.X, bottomRightPoint.Y);

        pointDraggingIndex = 1;
        MovePolyline(newTopRight);
        pointDraggingIndex = 3;
        MovePolyline(newBottomLeft);
        pointDraggingIndex = -1;

        Canvas.SetLeft(TopRight, newTopRight.X - (TopRight.Width / 2));
        Canvas.SetTop(TopRight, newTopRight.Y - (TopRight.Height / 2));

        Canvas.SetLeft(BottomLeft, newBottomLeft.X - (BottomLeft.Width / 2));
        Canvas.SetTop(BottomLeft, newBottomLeft.Y - (BottomLeft.Height / 2));
    }

    private void CropImage_Click(object sender, RoutedEventArgs e)
    {
        isCropping = true;
        ApplyButton.Visibility = Visibility.Collapsed;
        CropButtonPanel.Visibility = Visibility.Visible;

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        CroppingRectangle.Visibility = Visibility.Visible;
    }

    private async void ApplyCropButton_Click(object sender, RoutedEventArgs e)
    {
        CropButtonPanel.Visibility = Visibility.Collapsed;
        ApplyButton.Visibility = Visibility.Visible;

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Visible;

        if (string.IsNullOrEmpty(imagePath))
            return;

        MagickGeometry cropGeometry = CroppingRectangle.CropShape;
        MagickImage magickImage = new(imagePath);
        MagickGeometry actualSize = new(magickImage.Width, magickImage.Height);

        double factor = actualSize.Height / MainImage.ActualHeight;
        cropGeometry.ScaleAll(factor);

        SetUiForLongTask();

        magickImage.Crop(cropGeometry);

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        CroppingRectangle.Visibility = Visibility.Collapsed;
        SetUiForCompletedTask();
    }

    private void CancelCrop_Click(object sender, RoutedEventArgs e)
    {
        CropButtonPanel.Visibility = Visibility.Collapsed;
        ApplyButton.Visibility = Visibility.Visible;

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Visible;

        CroppingRectangle.Visibility = Visibility.Collapsed;
    }
}

public enum AspectRatio
{
    Square = 0,
    LetterPortrait = 1,
    LetterLandscape = 2,
    A4Portrait = 3,
    A4Landscape = 4,
    UsDollarBillPortrait = 5,
    UsDollarBillLandscape = 6,
    Custom = 7,
}