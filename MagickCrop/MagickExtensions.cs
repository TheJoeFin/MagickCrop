using ImageMagick;

namespace MagickCrop;
public static class MagickExtensions
{
    internal static void ScaleAll(this MagickGeometry geometry, double factor)
    {
        geometry.X = (int)(geometry.X * factor);
        geometry.Y = (int)(geometry.Y * factor);
        geometry.Width = (uint)(geometry.Width * factor);
        geometry.Height = (uint)(geometry.Height * factor);
    }
}
