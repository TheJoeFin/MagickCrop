using ImageMagick;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    public MainWindow()
    {
        InitializeComponent();
        DrawPolyLine();
    }

    private void DrawPolyLine()
    {
        lines = new()
        {
            Stroke = Brushes.LightSteelBlue,
            StrokeThickness = 2
        };

        List<Ellipse> ellipseList = ShapeCanvas.Children.OfType<Ellipse>().ToList();

        foreach (Ellipse ellipse in ellipseList)
        {
            lines.Points.Add(
                new Point(Canvas.GetLeft(ellipse) + ellipse.Width / 2,
                                Canvas.GetTop(ellipse) + ellipse.Height / 2));
        }

        ShapeCanvas.Children.Add(lines);
        Canvas.SetZIndex(lines, -1);
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

        string imageFileName = $"{folder}\\LetterPaperTest.jpg";
        string correctedImageFileName = $"{folder}\\LetterPaperTest-corrected.jpg";

        MagickImage image = new(imageFileName);

        double[] arguments =
        [
            lines.Points[0].X, lines.Points[0].Y,
            0,0,
            lines.Points[3].X, lines.Points[3].Y,
            0, 425,
            lines.Points[2].X, lines.Points[2].Y,
            550, 425,
            lines.Points[1].X, lines.Points[1].Y,
            550, 0,
        ];
        DistortSettings distortSettings = new()
        {
            Bestfit = true
        };
        image.Distort(DistortMethod.Perspective, distortSettings, arguments);
        image.Write(correctedImageFileName);
    }
}