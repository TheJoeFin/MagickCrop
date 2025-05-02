using System.IO;
using System.Text.Json;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Collection of measurement control data for serialization/deserialization
/// </summary>
public class MeasurementCollection
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Collection of distance measurement control data
    /// </summary>
    public List<DistanceMeasurementControlDto> DistanceMeasurements { get; set; } = [];

    /// <summary>
    /// Collection of angle measurement control data
    /// </summary>
    public List<AngleMeasurementControlDto> AngleMeasurements { get; set; } = [];

    /// <summary>
    /// Collection of vertical line control data
    /// </summary>
    public List<VerticalLineControlDto> VerticalLines { get; set; } = [];

    /// <summary>
    /// Collection of horizontal line control data
    /// </summary>
    public List<HorizontalLineControlDto> HorizontalLines { get; set; } = [];

    /// <summary>
    /// Collection of serialized ink strokes
    /// </summary>
    public List<StrokeDto> InkStrokes { get; set; } = [];

    /// <summary>
    /// Collection of stroke information associated with each ink stroke
    /// </summary>
    public List<StrokeInfoDto> StrokeInfos { get; set; } = [];

    /// <summary>
    /// Global scale factor applied to all distance measurements
    /// </summary>
    public double GlobalScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Global measurement units applied to all distance measurements
    /// </summary>
    public string GlobalUnits { get; set; } = "pixels";

    /// <summary>
    /// Save the measurement collection to a file
    /// </summary>
    /// <param name="filePath">Path to save the measurements</param>
    public void SaveToFile(string filePath)
    {
        JsonSerializerOptions options = serializerOptions;

        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Load measurement collection from a file
    /// </summary>
    /// <param name="filePath">Path to load the measurements from</param>
    /// <returns>A MeasurementCollection object or null if the file does not exist or is invalid</returns>
    public static MeasurementCollection? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<MeasurementCollection>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
