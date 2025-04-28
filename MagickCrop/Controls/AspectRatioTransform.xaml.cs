using MagickCrop.Models;
using System.Windows.Controls;
using System.Windows.Media;

namespace MagickCrop;

public partial class AspectRatioTransform : UserControl
{
    private AspectRatioItem? _ratioItem;
    public AspectRatioItem? RatioItem
    {
        get
        {
            return _ratioItem;
        }
        set
        {
            _ratioItem = value;

            if (_ratioItem is null)
                return;

            RectanglePoly.Points = GetPointsFromAspectRatio(_ratioItem.RatioValue);
        }
    }

    private PointCollection _transformPoints = [new(-5, -5), new(-7, 6), new(8, 6), new(5, -5)];
    public PointCollection TransformPoints
    {
        get
        {
            return _transformPoints;
        }
        set
        {
            _transformPoints = value;
            Polygon.Points = _transformPoints;
        }
    }

    public AspectRatioTransform()
    {
        InitializeComponent();

        Polygon.Points = _transformPoints;
    }

    public void SetAndScalePoints(PointCollection fullPoints)
    {
        if (fullPoints == null || fullPoints.Count == 0)
            return;

        TransformPoints = ScalePointsToSquare(fullPoints);
    }


    private static PointCollection GetPointsFromAspectRatio(double aspectRatio)
    {
        double height = 12;
        double width = height / aspectRatio;

        if (aspectRatio > 1)
        {
            width = 12;
            height = width * aspectRatio;
        }

        double halfHeight = height / 2;
        double halfWidth = width / 2;

        return
        [
            new(-halfWidth, -halfHeight),
            new(-halfWidth, halfHeight),
            new(halfWidth, halfHeight),
            new(halfWidth, -halfHeight)
        ];
    }

    public static PointCollection ScalePointsToSquare(PointCollection fullPoints)
    {
        if (fullPoints == null || fullPoints.Count == 0)
            return [];

        float squareSize = 20;
        (double x, double y) topLeft = (-10.0, -10.0);

        // Find the bounding box of the points
        double minX = fullPoints.Min(p => p.X);
        double minY = fullPoints.Min(p => p.Y);
        double maxX = fullPoints.Max(p => p.X);
        double maxY = fullPoints.Max(p => p.Y);

        // Calculate width and height of the bounding box
        double width = maxX - minX;
        double height = maxY - minY;

        // Determine the scaling factor to fit within the square
        double scale = squareSize / Math.Max(width, height);

        // Scale and translate the points
        PointCollection scaledPoints = [];

        foreach (System.Windows.Point p in fullPoints)
        {
            System.Windows.Point newPoint = new(p.X * scale + topLeft.x, p.Y * scale + topLeft.y);
            scaledPoints.Add(newPoint);
        }
        // .Select(p => (
        //     topLeft.X + (p.X - minX) * scale,
        //     topLeft.Y + (p.Y - minY) * scale))
        // .ToList();]

        return scaledPoints;
    }
}
