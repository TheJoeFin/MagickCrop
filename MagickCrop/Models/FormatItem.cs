using ImageMagick;

namespace MagickCrop.Models;

public class FormatItem
{
    public string Name { get; set; } = string.Empty;
    public MagickFormat Format { get; set; }
    public string Extension { get; set; } = string.Empty;
    public bool SupportsQuality { get; set; }

    public override string ToString() => Name;
}
