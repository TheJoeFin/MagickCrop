using System.Windows.Media;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Base data transfer object for line measurement controls
/// </summary>
public abstract class LineControlDto : MeasurementControlDto
{
    /// <summary>
    /// Position of the line (X coordinate for vertical, Y coordinate for horizontal)
    /// </summary>
    public double Position { get; set; }

    /// <summary>
    /// Color of the line
    /// </summary>
    public string StrokeColor { get; set; } = "#800080"; // Purple

    /// <summary>
    /// Thickness of the line
    /// </summary>
    public double StrokeThickness { get; set; } = 1.0;
}
