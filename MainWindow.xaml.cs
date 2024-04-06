using ImageMagick;
using Microsoft.Win32;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MagickCrop;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool isDragging = false;
    private Point clickedPoint = new();
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Polygon? lines;
    private string? imagePath;

    private bool isPanning = false;

    public MainWindow()
    {
        InitializeComponent();
        DrawPolyLine();
    }

    private void DrawPolyLine()
    {
        lines = new()
        {
            Stroke = Brushes.Blue,
            StrokeThickness = 2,
            IsHitTestVisible = false,
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
        if (Mouse.MiddleButton == MouseButtonState.Released)
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
        string? folder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        if (folder is null || lines is null)
            return;

        string correctedImageFileName = $"{folder}\\LetterPaperTest-corrected.jpg";
        if (string.IsNullOrEmpty(imagePath))
            imagePath = $"{folder}\\LetterPaperTest.jpg";

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
        image.Distort(DistortMethod.Perspective, distortSettings, arguments);
        await image.WriteAsync(correctedImageFileName);
        OpenFolderButton.IsEnabled = true;
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new()
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.heic;*.bmp|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.UriSource = new(openFileDialog.FileName);
        imagePath = openFileDialog.FileName;
        bitmap.EndInit();
        MainImage.Source = bitmap;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? folder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        if (folder is null || lines is null)
            return;

        System.Diagnostics.Process.Start("explorer.exe", folder);
    }

    private static object? GetPrivatePropertyValue(object obj, string propName)
    {
        ArgumentNullException.ThrowIfNull(obj);

        Type t = obj.GetType();
        PropertyInfo? pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (pi == null)
            throw new ArgumentOutOfRangeException("propName", string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));

        return pi.GetValue(obj, null);
    }

    private void ShapeCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScaleTransform scaleTransform = new()
        {
            CenterX = ShapeCanvas.ActualWidth / 2,
            CenterY = ShapeCanvas.ActualHeight / 2,
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
        if (Mouse.MiddleButton == MouseButtonState.Pressed)
        {
            clickedPoint = e.GetPosition(ShapeCanvas);
            isPanning = true;
        }
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
}