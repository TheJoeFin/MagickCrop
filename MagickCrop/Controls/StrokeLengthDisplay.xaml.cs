using MagickCrop.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace MagickCrop.Controls;

public partial class StrokeLengthDisplay : UserControl
{
    private readonly StrokeInfo _strokeInfo;
    private readonly Stroke _stroke;
    private readonly InkCanvas _inkCanvas;
    private readonly Canvas _parentCanvas;

    public delegate void SetRealWorldLengthRequestedEventHandler(object sender, double pixelDistance);
    public event SetRealWorldLengthRequestedEventHandler? SetRealWorldLengthRequested;

    public StrokeLengthDisplay(StrokeInfo info, Stroke stroke, InkCanvas inkCanvas, Canvas parentCanvas)
    {
        InitializeComponent();

        _strokeInfo = info;
        _stroke = stroke;
        _inkCanvas = inkCanvas;
        _parentCanvas = parentCanvas;

        DistanceTextBlock.Text = $"Stroke length {info.ScaledLength:F2} {info.Units}";
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string distance = DistanceTextBlock.Text;
        Clipboard.SetText(distance);
    }

    private void SetRealWorldLengthMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetRealWorldLengthRequested?.Invoke(this, _strokeInfo.PixelLength);
    }

    private void RemoveMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _parentCanvas.Children.Remove(this);
        _inkCanvas.Strokes.Remove(_stroke);
    }

    private void MeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu? contextMenu = MeasurementText.ContextMenu;
        if (contextMenu != null)
        {
            contextMenu.PlacementTarget = MeasurementText;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}
