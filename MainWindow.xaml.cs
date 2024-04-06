using ImageMagick;
using Microsoft.Win32;
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
        {
            imagePath = $"{folder}\\LetterPaperTest.jpg";
        }

        MagickImage image = new(imagePath);
        double scaleFactor = image.Width / MainImage.ActualWidth;

        //  #   X     Y
        //  1   798   304
        //  2   2410  236
        //  3   2753  1405
        //  4   704   1556
        //  3264 x 1836

        double[] arguments =
        [
            // top left
            lines.Points[0].X * scaleFactor, lines.Points[0].Y * scaleFactor,
            0,0,

            // bottom left
            lines.Points[3].X * scaleFactor, lines.Points[3].Y * scaleFactor,
            0, 850,

            // bottom right
            lines.Points[2].X * scaleFactor, lines.Points[2].Y * scaleFactor,
            1100, 850,

            // top right
            lines.Points[1].X * scaleFactor, lines.Points[1].Y * scaleFactor,
            1100, 0,
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
            Filter = "PNG Files(*.png)|*.png|JPEG Files(*.jpg;*.jpeg)|*.jpg;*.jpeg|All files (*.*)|*.*"
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
}