using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Models;

public class StrokeDto
{
    public List<Point> Points { get; set; } = [];
    public Color Color { get; set; }
    public double Thickness { get; set; }

    public static StrokeDto ConvertStrokeToDto(Stroke stroke)
    {
        List<Point> points = [.. stroke.StylusPoints.Select(sp => new Point(sp.X, sp.Y))];
        Color color = stroke.DrawingAttributes.Color;
        double thickness = stroke.DrawingAttributes.Width;

        return new StrokeDto
        {
            Points = points,
            Color = color,
            Thickness = thickness
        };
    }

    public static Stroke ConvertDtoToStroke(StrokeDto strokeDto)
    {
        // Create a StylusPointCollection from the list of points
        StylusPointCollection stylusPoints = [.. strokeDto.Points.Select(p => new StylusPoint(p.X, p.Y))];

        // Create DrawingAttributes and set the color and thickness
        DrawingAttributes drawingAttributes = new()
        {
            Color = strokeDto.Color,
            Width = strokeDto.Thickness,
            Height = strokeDto.Thickness // Assuming height is the same as width for simplicity
        };

        // Create and return the Stroke
        return new Stroke(stylusPoints, drawingAttributes);
    }
}
