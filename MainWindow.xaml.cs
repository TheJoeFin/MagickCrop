using ImageMagick;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace MagickCrop;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private bool isDragging = false;
    private Point clickedPoint = new();
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Polygon? lines;
    private string? imagePath;
    private string? savedPath;

    private bool isPanning = false;

    public MainWindow()
    {
        InitializeComponent();
        DrawPolyLine();
    }

    private void DrawPolyLine()
    {
        Color color = (Color)ColorConverter.ConvertFromString("#0066FF");
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
        //Canvas.SetZIndex(lines, -1);
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

        TranslateTransform translateTransform = new(deltaX, deltaY);

        if (ShapeCanvas.RenderTransform is TranslateTransform transform)
        {
            translateTransform = transform;
            translateTransform.X += deltaX;
            translateTransform.Y += deltaY;
        }

        ShapeCanvas.RenderTransform = translateTransform;
    }

    private void MovePolyline(Point newPoint)
    {
        if (pointDraggingIndex < 0 || lines is null)
            return;

        lines.Points[pointDraggingIndex] = newPoint;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        BottomPane.IsEnabled = false;
        BottomPane.Cursor = Cursors.Wait;
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Image Files|*.jpg;",
            RestoreDirectory = true,
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
            BottomPane.IsEnabled = true;
            BottomPane.Cursor = null;
            return;
        }

        IsWorkingBar.Visibility = Visibility.Visible;

        AspectRatio aspectRatioEnum = AspectRatio.LetterLandscape;
        if (AspectRatioComboBox.SelectedItem is ComboBoxItem boxItem && boxItem.Tag is string tag)
            _ = Enum.TryParse(tag, out aspectRatioEnum);

        MagickImage image = new(imagePath);
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
                if (CustomHeight.Value is double height && CustomWidth.Value is double width && height != 0 && width != 0)
                    aspectRatio = height / width;
                break;
            default:
                break;
        }

        Rect? visualContentBounds = (Rect)GetPrivatePropertyValue(lines, "VisualContentBounds");
        Rect finalSize = new(0, 0, 1100, 850);

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
            await image.WriteAsync(correctedImageFileName);

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
            OpenFolderButton.IsEnabled = true;
            savedPath = correctedImageFileName;

            IsWorkingBar.Visibility = Visibility.Collapsed;
            BottomPane.Cursor = null;
            BottomPane.IsEnabled = true;
            image.Dispose();
        }
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.heic;*.bmp|All files (*.*)|*.*",
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        Save.IsEnabled = true;
        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.UriSource = new(openFileDialog.FileName);
        imagePath = openFileDialog.FileName;
        bitmap.EndInit();
        MainImage.Source = bitmap;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? folderPath = System.IO.Path.GetDirectoryName(savedPath);

        if (folderPath is null || lines is null)
            return;

        System.Diagnostics.Process.Start("explorer.exe", folderPath);
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
        ScaleTransform scaleTransform = new()
        {
            CenterX = MainImage.ActualWidth / 2,
            CenterY = MainImage.ActualHeight / 2,
        };

        double startingScaleAmount = 1;

        if (ShapeCanvas.LayoutTransform is ScaleTransform transform)
        {
            scaleTransform = transform;
            startingScaleAmount = transform.ScaleY;
        }

        if (e.Delta > 0)
            scaleTransform.ScaleX = scaleTransform.ScaleY = (startingScaleAmount += 0.1);
        else
            scaleTransform.ScaleX = scaleTransform.ScaleY = (startingScaleAmount -= 0.1);

        ShapeCanvas.LayoutTransform = scaleTransform;
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