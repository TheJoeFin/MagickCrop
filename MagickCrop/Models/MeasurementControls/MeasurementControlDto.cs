using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Base class for all measurement control data transfer objects
/// </summary>
public abstract class MeasurementControlDto
{
    /// <summary>
    /// Type identifier for the measurement control
    /// </summary>
    public string Type { get; set; } = string.Empty;
}
