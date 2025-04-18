using OpenCvSharp;
using System.Windows;

namespace MagickCrop.Extensions;

public static class RectExtensions
{
    /// <summary>
    /// Converts an OpenCvSharp.Rect? to a System.Windows.Rect?.
    /// </summary>
    /// <param name="rect">The OpenCvSharp.Rect? to convert.</param>
    /// <returns>A System.Windows.Rect? representing the same rectangle, or null if the input is null.</returns>
    public static System.Windows.Rect? ToWindowsRect(this OpenCvSharp.Rect? rect)
    {
        if (rect == null)
            return null;

        return new System.Windows.Rect(rect.Value.X, rect.Value.Y, rect.Value.Width, rect.Value.Height);
    }
}
