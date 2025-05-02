namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Data transfer object for stroke information
/// </summary>
public class StrokeInfoDto
{
    /// <summary>
    /// Length in pixels
    /// </summary>
    public double PixelLength { get; set; }
    
    /// <summary>
    /// Length after applying scale factor
    /// </summary>
    public double ScaledLength { get; set; }
    
    /// <summary>
    /// Measurement units
    /// </summary>
    public string Units { get; set; } = "pixels";
    
    /// <summary>
    /// Position of the stroke length display (X coordinate)
    /// </summary>
    public double DisplayPositionX { get; set; }
    
    /// <summary>
    /// Position of the stroke length display (Y coordinate)
    /// </summary>
    public double DisplayPositionY { get; set; }

    /// <summary>
    /// Create a DTO from a StrokeInfo object
    /// </summary>
    /// <param name="info">The source StrokeInfo</param>
    /// <param name="displayX">X coordinate of display</param>
    /// <param name="displayY">Y coordinate of display</param>
    /// <returns>A new StrokeInfoDto instance</returns>
    public static StrokeInfoDto FromStrokeInfo(StrokeInfo info, double displayX, double displayY)
    {
        return new StrokeInfoDto
        {
            PixelLength = info.PixelLength,
            ScaledLength = info.ScaledLength,
            Units = info.Units,
            DisplayPositionX = displayX,
            DisplayPositionY = displayY
        };
    }

    /// <summary>
    /// Convert to a StrokeInfo object
    /// </summary>
    /// <returns>A new StrokeInfo instance</returns>
    public StrokeInfo ToStrokeInfo()
    {
        return new StrokeInfo
        {
            PixelLength = PixelLength,
            ScaledLength = ScaledLength,
            Units = Units
        };
    }
}
