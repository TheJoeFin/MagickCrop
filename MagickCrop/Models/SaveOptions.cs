using ImageMagick;

namespace MagickCrop.Models;

public class SaveOptions
{
    public MagickFormat Format { get; set; }
    public string Extension { get; set; } = string.Empty;
    public int Quality { get; set; }
    public bool Resize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool MaintainAspectRatio { get; set; }
}
