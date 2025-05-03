using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagickCrop.Models.MeasurementControls;

namespace MagickCrop.Controls;

public partial class DistanceMeasurementControl : UserControl
{
    private Point startPosition = new(100, 100);
    private Point endPosition = new(300, 300);
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Point clickedPoint;

    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public event MouseEventHandler? MeasurementPointMouseMove;
    // New event for real world length setting
    public delegate void SetRealWorldLengthRequestedEventHandler(object sender, double pixelDistance);
    public event SetRealWorldLengthRequestedEventHandler? SetRealWorldLengthRequested;
    // New event for removal request
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public double ScaleFactor
    {
        get => scaleFactor;
        set
        {
            scaleFactor = value;
            UpdatePositions();
        }
    }
    private double scaleFactor = 1.0;

    private string units = "pixels";

    public string Units
    {
        get { return units; }
        set
        {
            units = value;
            UpdatePositions();
        }
    }

    public DistanceMeasurementControl()
    {
        InitializeComponent();
        UpdatePositions();
    }

    public void InitializePositions(double canvasWidth, double canvasHeight)
    {
        // Place points at reasonable starting positions
        startPosition = new Point(canvasWidth * 0.3, canvasHeight * 0.4);
        endPosition = new Point(canvasWidth * 0.7, canvasHeight * 0.6);
        UpdatePositions();
    }

    private void UpdatePositions()
    {
        // Update the visual elements
        Canvas.SetLeft(StartPoint, startPosition.X - (StartPoint.Width / 2));
        Canvas.SetTop(StartPoint, startPosition.Y - (StartPoint.Height / 2));

        Canvas.SetLeft(EndPoint, endPosition.X - (EndPoint.Width / 2));
        Canvas.SetTop(EndPoint, endPosition.Y - (EndPoint.Height / 2));

        // Update line
        MeasurementLine.X1 = startPosition.X;
        MeasurementLine.Y1 = startPosition.Y;
        MeasurementLine.X2 = endPosition.X;
        MeasurementLine.Y2 = endPosition.Y;

        // Calculate distance and update text
        double distance = CalculateDistance();
        double scaledDistance = distance * ScaleFactor;
        DistanceTextBlock.Text = $"{scaledDistance:F2} {Units}";

        // Position the measurement text in the middle of the line
        Point midPoint = new(
            (startPosition.X + endPosition.X) / 2,
            (startPosition.Y + endPosition.Y) / 2);

        Canvas.SetLeft(MeasurementText, midPoint.X - (MeasurementText.ActualWidth / 2));
        Canvas.SetTop(MeasurementText, midPoint.Y - MeasurementText.ActualHeight - 5);
    }

    private double CalculateDistance()
    {
        double deltaX = endPosition.X - startPosition.X;
        double deltaY = endPosition.Y - startPosition.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Ellipse ellipse || ellipse.Tag is not string intAsString)
            return;

        pointDraggingIndex = int.Parse(intAsString);
        clickedElement = ellipse;
        clickedPoint = e.GetPosition(MeasurementCanvas);

        MeasurementPointMouseDown?.Invoke(sender, e);
    }

    public void StartDraggingPoint(int pointIndex)
    {
        pointDraggingIndex = pointIndex;
        clickedElement = pointIndex == 0 ? StartPoint : EndPoint;
        MeasurementPointMouseDown?.Invoke(clickedElement, null);
    }

    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (pointIndex == 0)
            startPosition = newPosition;
        else if (pointIndex == 1)
            endPosition = newPosition;

        UpdatePositions();
    }

    public int GetActivePointIndex()
    {
        return pointDraggingIndex;
    }

    public void ResetActivePoint()
    {
        pointDraggingIndex = -1;
        clickedElement = null;
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string distance = DistanceTextBlock.Text;
        Clipboard.SetText(distance);
    }

    private void SetRealWorldLengthMenuItem_Click(object sender, RoutedEventArgs e)
    {
        double pixelDistance = CalculateDistance();
        SetRealWorldLengthRequested?.Invoke(this, pixelDistance);
    }

    private void MeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        // Show context menu when button is clicked
        ContextMenu? contextMenu = MeasurementText.ContextMenu;
        if (contextMenu != null)
        {
            contextMenu.PlacementTarget = MeasurementText;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void RemoveMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Trigger the removal event so the parent canvas can remove this control
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Convert this control to a data transfer object
    /// </summary>
    public DistanceMeasurementControlDto ToDto()
    {
        return new DistanceMeasurementControlDto
        {
            StartPosition = startPosition,
            EndPosition = endPosition,
            ScaleFactor = ScaleFactor,
            Units = Units
        };
    }

    /// <summary>
    /// Apply data from a DTO to this control
    /// </summary>
    public void FromDto(DistanceMeasurementControlDto dto)
    {
        startPosition = dto.StartPosition;
        endPosition = dto.EndPosition;
        ScaleFactor = dto.ScaleFactor;
        Units = dto.Units;
        UpdatePositions();
    }
}
