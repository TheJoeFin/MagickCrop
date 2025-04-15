using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Data transfer object for distance measurement controls
/// </summary>
public class DistanceMeasurementControlDto : MeasurementControlDto
{
    public DistanceMeasurementControlDto()
    {
        Type = "Distance";
    }

    /// <summary>
    /// Start position of the measurement line
    /// </summary>
    public Point StartPosition { get; set; }

    /// <summary>
    /// End position of the measurement line
    /// </summary>
    public Point EndPosition { get; set; }

    /// <summary>
    /// Scale factor for converting pixel distances to real-world units
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Units of measurement (e.g., "pixels", "mm", "in")
    /// </summary>
    public string Units { get; set; } = "pixels";
}
