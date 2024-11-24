using ImageMagick;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MagickCrop;
/// <summary>
/// Interaction logic for ResizableRectangle.xaml
/// </summary>
public partial class ResizableRectangle : UserControl
{
    private MovingKind KindOfMove = MovingKind.None;
    private Point? MouseDownPoint = null;
    private Point? MouseDownCanvasPoint = null;
    private Size? OldSize = null;
    private FrameworkElement? currentCapter;

    public ResizableRectangle()
    {
        InitializeComponent();

        Canvas.SetLeft(this, 100);
        Canvas.SetTop(this, 100);
    }

    public MagickGeometry CropShape
    {
        get => new(
        (int)(Canvas.GetLeft(this) + Padding.Left),
        (int)(Canvas.GetTop(this) + Padding.Top),
        (uint)rectangle.ActualWidth,
        (uint)rectangle.ActualHeight);
    }

    private void UserControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (Mouse.LeftButton == MouseButtonState.Released
            || KindOfMove == MovingKind.None
            || OldSize is null
            || MouseDownPoint is null
            || MouseDownCanvasPoint is null)
        {
            KindOfMove = MovingKind.None;
            MouseDownPoint = null;
            MouseDownCanvasPoint = null;
            OldSize = null;
            currentCapter?.ReleaseMouseCapture();
            return;
        }

        Point position = e.GetPosition(Parent as FrameworkElement);

        // get delta of mouse move
        double canvasDeltaX = position.X - MouseDownCanvasPoint.Value.X;
        double canvasDeltaY = position.Y - MouseDownCanvasPoint.Value.Y;

        try
        {
            switch (KindOfMove)
            {
                case MovingKind.Pan:
                    Canvas.SetLeft(this, position.X - MouseDownPoint.Value.X);
                    Canvas.SetTop(this, position.Y - MouseDownPoint.Value.Y);
                    break;
                case MovingKind.TopLeft:
                    Canvas.SetLeft(this, position.X - MouseDownPoint.Value.X);
                    Canvas.SetTop(this, position.Y - MouseDownPoint.Value.Y);
                    Width = OldSize.Value.Width - canvasDeltaX;
                    Height = OldSize.Value.Height - canvasDeltaY;
                    break;
                case MovingKind.TopRight:
                    Canvas.SetTop(this, position.Y - MouseDownPoint.Value.Y);
                    Width = OldSize.Value.Width + canvasDeltaX;
                    Height = OldSize.Value.Height - canvasDeltaY;
                    break;
                case MovingKind.BottomLeft:
                    Canvas.SetLeft(this, position.X - MouseDownPoint.Value.X);
                    Width = OldSize.Value.Width - canvasDeltaX;
                    Height = OldSize.Value.Height + canvasDeltaY;
                    break;
                case MovingKind.BottomRight:
                    Width = OldSize.Value.Width + canvasDeltaX;
                    Height = OldSize.Value.Height + canvasDeltaY;
                    break;
                case MovingKind.Top:
                    Canvas.SetTop(this, position.Y - MouseDownPoint.Value.Y);
                    Height = OldSize.Value.Height - canvasDeltaY;
                    break;
                case MovingKind.Bottom:
                    Height = OldSize.Value.Height + canvasDeltaY;
                    break;
                case MovingKind.Left:
                    Canvas.SetLeft(this, position.X - MouseDownPoint.Value.X);
                    Width = OldSize.Value.Width - canvasDeltaX;
                    break;
                case MovingKind.Right:
                    Width = OldSize.Value.Width + canvasDeltaX;
                    break;
            }
        }
        catch (ArgumentException)
        {
            // tried to set a width or height to a negative number
            // swallow the exception
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

        currentCapter = element;
        currentCapter.CaptureMouse();
        OldSize = new(Width, Height);
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
