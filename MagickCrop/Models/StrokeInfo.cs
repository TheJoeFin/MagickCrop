using System;

namespace MagickCrop.Models
{
    public class StrokeInfo
    {
        // Length in pixels
        public double PixelLength { get; set; }
        
        // Length after applying scale factor
        public double ScaledLength { get; set; }
        
        // Measurement units
        public string Units { get; set; } = "pixels";
    }
}
