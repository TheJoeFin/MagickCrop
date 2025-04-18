using OpenCvSharp;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MagickCrop.Services;

public class OpenCvService
{
    /// <summary>
    /// Detects contours in an image and returns a new image with green contours drawn on it
    /// </summary>
    /// <param name="imagePath">Path to the source image</param>
    /// <returns>A BitmapSource with green contours drawn on it</returns>
    public static BitmapSource DetectAndDrawContours(string imagePath)
    {
        using Mat src = Cv2.ImRead(imagePath);
        if (src.Empty())
            throw new ArgumentException("Could not load image", nameof(imagePath));

        // Convert to grayscale
        using Mat gray = new();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // Apply blur to reduce noise
        using Mat blurred = new();
        Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);

        // Apply Canny edge detection
        using Mat edges = new();
        Cv2.Canny(blurred, edges, 50, 150);

        // Find contours
        Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        // Filter contours to avoid noise - only keep contours with a reasonable area
        Point[][] filteredContours = Array.FindAll(contours, c => Cv2.ContourArea(c) > 500);

        // Create a copy of the original image to draw on
        using Mat contourImage = src.Clone();

        // Draw all contours in green with a line thickness of 2
        Cv2.DrawContours(
            contourImage,
            filteredContours,
            -1, // Draw all contours
            new Scalar(0, 255, 0), // Green color in BGR format
            2); // Thickness

        // Convert the OpenCV Mat to a WPF BitmapSource
        return ConvertMatToBitmapSource(contourImage);
    }

    /// <summary>
    /// Converts an OpenCV Mat to a WPF BitmapSource
    /// </summary>
    private static BitmapSource ConvertMatToBitmapSource(Mat mat)
    {
        // Convert from BGR to RGB format
        using Mat rgbMat = new();
        Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);

        try
        {
            // Create the bitmap
            int width = rgbMat.Width;
            int height = rgbMat.Height;
            int stride = width * rgbMat.ElemSize();
            IntPtr ptr = rgbMat.Data;

            BitmapSource bitmap = BitmapSource.Create(
                width,
                height,
                96, 96, // DPI
                PixelFormats.Rgb24,
                null,
                ptr,
                stride * height,
                stride);

            bitmap.Freeze(); // Makes it immutable and improves performance
            return bitmap;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert OpenCV Mat to BitmapSource: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves the original image with green contours to a temporary file and returns its path
    /// </summary>
    public static string ProcessImageWithContours(string imagePath)
    {
        try
        {
            using Mat src = Cv2.ImRead(imagePath);
            if (src.Empty())
                throw new ArgumentException("Could not load image", nameof(imagePath));

            // Convert to grayscale
            using Mat gray = new();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // Apply blur to reduce noise
            using Mat blurred = new();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);

            // Apply Canny edge detection
            using Mat edges = new();
            Cv2.Canny(blurred, edges, 50, 150);

            // Find contours
            Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            // Detect and draw rectangles
            using Mat rectangleImage = src.Clone();
            List<Point[]> rectangles = DetectRectangles(contours);

            // Draw all contours in green
            Cv2.DrawContours(
                rectangleImage,
                contours,
                -1,
                new Scalar(0, 155, 0),
                1);

            // Draw detected rectangles in bright green with thicker lines
            foreach (Point[] rect in rectangles)
            {
                Cv2.Polylines(rectangleImage, [rect], true, new Scalar(0, 255, 0), 2);

                // Draw the corners as circles
                foreach (Point point in rect)
                {
                    Cv2.Circle(rectangleImage, point, 4, new Scalar(0, 0, 255), -1);
                }
            }

            // Save to a temporary file
            string tempFile = Path.GetTempFileName();
            tempFile = Path.ChangeExtension(tempFile, ".jpg");
            Cv2.ImWrite(tempFile, rectangleImage);

            return tempFile;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error processing image with contours: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Detects rectangular shapes in the provided contours
    /// </summary>
    /// <param name="contours">List of contours to analyze</param>
    /// <returns>List of contours that represent rectangles</returns>
    public static List<Point[]> DetectRectangles(Point[][] contours)
    {
        List<Point[]> rectangles = [];

        foreach (Point[] contour in contours)
        {
            // Skip small contours
            double area = Cv2.ContourArea(contour);
            if (area < 1000) continue;

            // Approximate the contour to simplify the shape
            double epsilon = 0.02 * Cv2.ArcLength(contour, true);
            Point[] approx = Cv2.ApproxPolyDP(contour, epsilon, true);

            // Check if the approximated shape has 4 points (quadrilateral)
            if (approx.Length == 4)
            {
                // Verify it's a convex shape
                if (Cv2.IsContourConvex(approx))
                {
                    rectangles.Add(approx);
                }
            }
        }

        return rectangles;
    }

    /// <summary>
    /// Finds rectangular shapes in an image and returns their corner points
    /// </summary>
    /// <param name="imagePath">Path to the source image</param>
    /// <returns>List of rectangular contours, each containing 4 corner points</returns>
    public static List<System.Windows.Point[]> FindRectangularContours(string imagePath)
    {
        using Mat src = Cv2.ImRead(imagePath);
        if (src.Empty())
            throw new ArgumentException("Could not load image", nameof(imagePath));

        // Convert to grayscale
        using Mat gray = new();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // Apply blur to reduce noise
        using Mat blurred = new();
        Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);

        // Apply Canny edge detection
        using Mat edges = new();
        Cv2.Canny(blurred, edges, 50, 150);

        // Find contours
        Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        // Detect rectangles
        List<Point[]> rectangles = DetectRectangles(contours);

        // Convert OpenCV points to WPF points
        List<System.Windows.Point[]> result = [];
        foreach (Point[] rect in rectangles)
        {
            System.Windows.Point[] wpfPoints = [.. rect.Select(p => new System.Windows.Point(p.X, p.Y))];

            // Sort points to be in top-left, top-right, bottom-right, bottom-left order
            wpfPoints = OrderRectanglePoints(wpfPoints);

            result.Add(wpfPoints);
        }

        return result;
    }

    /// <summary>
    /// Sorts rectangle points to be in top-left, top-right, bottom-right, bottom-left order
    /// </summary>
    private static System.Windows.Point[] OrderRectanglePoints(System.Windows.Point[] points)
    {
        if (points.Length != 4)
            return points;

        // Calculate centroid
        double centerX = points.Average(p => p.X);
        double centerY = points.Average(p => p.Y);

        // Sort by angle from center (atan2)
        System.Windows.Point[] sortedPoints = [.. points
            .Select(p => new
            {
                Point = p,
                Angle = Math.Atan2(p.Y - centerY, p.X - centerX)
            })
            .OrderBy(item => item.Angle)
            .Select(item => item.Point)];

        // Rotate so the top-left point comes first (based on min Y, and if tied, min X)
        int indexOfTopLeft = 0;
        double minSum = double.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            double sum = sortedPoints[i].X + sortedPoints[i].Y;
            if (sum < minSum)
            {
                minSum = sum;
                indexOfTopLeft = i;
            }
        }

        // Rearrange so top-left is first
        System.Windows.Point[] result = new System.Windows.Point[4];
        for (int i = 0; i < 4; i++)
        {
            result[i] = sortedPoints[(indexOfTopLeft + i) % 4];
        }

        return result;
    }

    internal static System.Windows.Point? SnapToNearestRectangleCorner(string imagePath, System.Windows.Point pointInImage)
    {
        // Retrieve rectangles from the image
        List<System.Windows.Point[]> rectangles = FindRectangularContours(imagePath);
        if (rectangles.Count == 0)
            return null;

        System.Windows.Point? nearestCorner = null;
        double minDistance = double.MaxValue;

        // Iterate through each rectangle and its corners
        foreach (System.Windows.Point[] rectangle in rectangles)
        {
            foreach (System.Windows.Point corner in rectangle)
            {
                // Calculate the distance between the given point and the corner
                double distance = Math.Sqrt(
                    Math.Pow(corner.X - pointInImage.X, 2) +
                    Math.Pow(corner.Y - pointInImage.Y, 2));

                // Update the nearest corner if this one is closer
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCorner = corner;
                }
            }
        }

        // Return the nearest corner if it is within a reasonable snapping distance
        const double maxSnappingDistance = 20.0; // Adjust as needed
        return minDistance <= maxSnappingDistance ? nearestCorner : null;
    }

    /// <summary>
    /// Detects the largest rectangle in the image and returns it as a Rect object.
    /// </summary>
    /// <param name="imagePath">Path to the source image</param>
    /// <returns>The largest rectangle as a Rect object, or null if no rectangle is found</returns>
    public static Rect? DetectMainRectangle(string imagePath)
    {
        using Mat src = Cv2.ImRead(imagePath);
        if (src.Empty())
            throw new ArgumentException("Could not load image", nameof(imagePath));

        // Convert to grayscale
        using Mat gray = new();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // Apply blur to reduce noise
        using Mat blurred = new();
        Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);

        // Apply Canny edge detection
        using Mat edges = new();
        Cv2.Canny(blurred, edges, 50, 150);

        // Find contours
        Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        // Detect rectangles
        List<Point[]> rectangles = DetectRectangles(contours);

        // Find the largest rectangle by area
        Point[]? largestRectangle = null;
        double maxArea = 0;

        foreach (Point[] rect in rectangles)
        {
            double area = Cv2.ContourArea(rect);
            if (area > maxArea)
            {
                maxArea = area;
                largestRectangle = rect;
            }
        }

        if (largestRectangle == null)
            return null;

        // Convert the largest rectangle to a Rect object
        OpenCvSharp.Rect boundingRect = Cv2.BoundingRect(largestRectangle);
        return boundingRect;
    }
}