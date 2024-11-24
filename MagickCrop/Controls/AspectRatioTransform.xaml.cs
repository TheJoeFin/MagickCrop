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

    private PointCollection _transformPoints = [new(-5, -5), new(- 7, 6), new(8, 6), new(5, -5)];
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
}
