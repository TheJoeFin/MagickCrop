using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop;
/// <summary>
/// Interaction logic for ResizableRectangle.xaml
/// </summary>
public partial class ResizableRectangle : UserControl
{
    private MovingKind KindOfMove = MovingKind.None;
    private Point? MouseDownPoint = null;
    private Point? MouseDownCanvasPoint = null;

    public ResizableRectangle()
    {
        InitializeComponent();

        Canvas.SetLeft(this, 100);
        Canvas.SetTop(this, 100);
    }

    private void UserControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (Mouse.LeftButton == MouseButtonState.Released
            || KindOfMove == MovingKind.None
            || MouseDownPoint is null
            || MouseDownCanvasPoint is null)
        {
            KindOfMove = MovingKind.None;
            MouseDownPoint = null;
            MouseDownCanvasPoint = null;
            ReleaseMouseCapture();
            return;
        }

        Point position = e.GetPosition(Parent as FrameworkElement);
        double prevLeft = Canvas.GetLeft(this);
        double prevTop = Canvas.GetTop(this);

        // get delta of mouse move
        double canvasDeltaX = position.X - MouseDownCanvasPoint.Value.X;
        double canvasDeltaY = position.Y - MouseDownCanvasPoint.Value.Y;

        switch (KindOfMove)
        {
            case MovingKind.Pan:
                Canvas.SetLeft(this, position.X - MouseDownPoint.Value.X);
                Canvas.SetTop(this, position.Y - MouseDownPoint.Value.Y);
                break;
            case MovingKind.TopLeft:
                Canvas.SetLeft(this, prevLeft + canvasDeltaX);
                Canvas.SetTop(this, prevTop + canvasDeltaY);
                Width -= canvasDeltaX;
                Height -= canvasDeltaY;
                break;
            case MovingKind.TopRight:
                Canvas.SetTop(this, prevTop + canvasDeltaY);
                Width += canvasDeltaX;
                Height -= canvasDeltaY;
                break;
            case MovingKind.BottomLeft:
                Canvas.SetLeft(this, prevLeft + canvasDeltaX);
                Width -= canvasDeltaX;
                Height += canvasDeltaY;
                break;
            case MovingKind.BottomRight:
                Width += canvasDeltaX;
                Height += canvasDeltaY;
                break;
            case MovingKind.Top:
                Canvas.SetTop(this, prevTop + canvasDeltaY);
                Height -= canvasDeltaY;
                break;
            case MovingKind.Bottom:
                Height += canvasDeltaY;
                break;
            case MovingKind.Left:
                Canvas.SetLeft(this, prevLeft + canvasDeltaX);
                Width -= canvasDeltaX;
                break;
            case MovingKind.Right:
                Width += canvasDeltaX;
                break;
        }
    }

    private void element_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        bool success = Enum.TryParse(element.Tag.ToString(), out KindOfMove);

        if (!success)
        {
            KindOfMove = MovingKind.None;
            return;
        }

        // CaptureMouse();
        MouseDownPoint = e.GetPosition(this);
        MouseDownCanvasPoint = e.GetPosition(Parent as FrameworkElement);
    }

}

public enum MovingKind
{
    None,
    Pan,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Top,
    Bottom,
    Left,
    Right
}
