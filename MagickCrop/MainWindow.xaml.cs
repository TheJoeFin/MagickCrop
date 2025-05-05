using ImageMagick;
using MagickCrop.Controls;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Windows.ApplicationModel;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MagickCrop;

public partial class MainWindow : FluentWindow
{
    private Point clickedPoint = new();
    private Size oldGridSize = new();
    private Size originalImageSize = new();
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Polygon? lines;
    private string? imagePath;
    private string? savedPath;

    private double scaleFactor = 1;
    private DraggingMode draggingMode { get; set; } = DraggingMode.None;

    private string openedFileName = string.Empty;
    private readonly List<UIElement> _polygonElements;

    private readonly UndoRedo undoRedo = new();
    private AspectRatioItem? selectedAspectRatio;
    private readonly ObservableCollection<DistanceMeasurementControl> measurementTools = [];
    private DistanceMeasurementControl? activeMeasureControl;
    private readonly ObservableCollection<AngleMeasurementControl> angleMeasurementTools = [];
    private AngleMeasurementControl? activeAngleMeasureControl;

    private RecentProjectsManager? recentProjectsManager;
    private string? currentProjectId;
    private System.Timers.Timer? autoSaveTimer;
    private readonly int AutoSaveIntervalMs = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;

    private Rect? detectedRectangle; // Store the detected rectangle

    // Store detected OpenCV points for snapping
    private List<Point> _opencvDetectedPoints = [];

    public MainWindow()
    {
        ThemeService themeService = new();
        themeService.SetTheme(ApplicationTheme.Dark);

        Color teal = (Color)ColorConverter.ConvertFromString("#0066FF");
        ApplicationAccentColorManager.Apply(teal);

        InitializeComponent();
        DrawPolyLine();
        _polygonElements = [lines, TopLeft, TopRight, BottomRight, BottomLeft];

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        try
        {
            PackageVersion version = Package.Current.Id.Version;
            wpfuiTitleBar.Title += $" v{version.Major}.{version.Minor}.{version.Build}";
        }
        catch (Exception)
        {
            // do nothing this is just running unpackaged.
        }

        AspectRatioComboBox.ItemsSource = AspectRatioItem.GetStandardAspectRatios();
        AspectRatioComboBox.SelectedIndex = 0;
        selectedAspectRatio = AspectRatioComboBox.SelectedItem as AspectRatioItem;
        AspectRatioTransformPreview.RatioItem = selectedAspectRatio;

        InitializeProjectManager();
    }

    private void DrawPolyLine()
    {
        Color color = (Color)ColorConverter.ConvertFromString("#0066FF");

        if (lines is not null)
            ShapeCanvas.Children.Remove(lines);

        lines = new()
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            IsHitTestVisible = false,
            Opacity = 0.8,
        };

        List<Ellipse> ellipseList = [.. ShapeCanvas.Children.OfType<Ellipse>()];

        foreach (Ellipse ellipse in ellipseList)
        {
            lines.Points.Add(
                new Point(Canvas.GetLeft(ellipse) + (ellipse.Width / 2),
                                Canvas.GetTop(ellipse) + (ellipse.Height / 2)));
        }

        ShapeCanvas.Children.Add(lines);
    }

    private void TopLeft_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not string intAsString)
            return;

        pointDraggingIndex = int.Parse(intAsString);
        clickedElement = ellipse;
        draggingMode = DraggingMode.MoveElement;
        clickedPoint = e.GetPosition(ShapeCanvas);

        // If contour detection is enabled, try to snap to a rectangle corner
        if (ShowContours.IsChecked == true && !string.IsNullOrEmpty(imagePath))
        {
            try
            {
                // Get the position in the image coordinates
                Point pointInImage = e.GetPosition(MainImage);

                // Try to snap to a corner
                Point? snappedPoint = OpenCvService.SnapToNearestRectangleCorner(imagePath, pointInImage);

                if (snappedPoint.HasValue)
                {
                    // Convert the snapped point to canvas coordinates
                    Point canvasPoint = MainImage.TranslatePoint(snappedPoint.Value, ShapeCanvas);

                    // Update the ellipse position
                    Canvas.SetTop(ellipse, canvasPoint.Y - (ellipse.Height / 2));
                    Canvas.SetLeft(ellipse, canvasPoint.X - (ellipse.Width / 2));

                    // Update the polyline
                    if (lines is not null)
                    {
                        lines.Points[pointDraggingIndex] = canvasPoint;
                        AspectRatioTransformPreview.SetAndScalePoints(lines.Points);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception but don't disrupt the user experience
                Debug.WriteLine($"Error snapping to rectangle: {ex.Message}");
            }
        }

        CaptureMouse();
    }


    private void TopLeft_MouseMove(object sender, MouseEventArgs e)
    {
        if (Mouse.MiddleButton == MouseButtonState.Released && Mouse.LeftButton == MouseButtonState.Released)
        {
            Debug.WriteLine($"MouseMove: resetting, {draggingMode}");
            if (draggingMode == DraggingMode.Panning)
                Mouse.SetCursor(null);

            if (draggingMode == DraggingMode.MeasureDistance && activeMeasureControl is not null)
            {
                activeMeasureControl.ResetActivePoint();
                activeMeasureControl = null;
            }

            if (draggingMode == DraggingMode.MeasureAngle && activeAngleMeasureControl is not null)
            {
                activeAngleMeasureControl.ResetActivePoint();
                activeAngleMeasureControl = null;
            }

            clickedElement = null;
            ShapeCanvas.ReleaseMouseCapture();
            draggingMode = DraggingMode.None;

            return;
        }

        if (draggingMode == DraggingMode.Panning)
        {
            Debug.WriteLine("MouseMove: Panning");
            PanCanvas(e);
            return;
        }

        if (draggingMode == DraggingMode.Resizing)
        {
            Debug.WriteLine("MouseMove: Resizing");
            ResizeImage(e);
            return;
        }

        Point movingPoint = e.GetPosition(ShapeCanvas);

        // --- SNAP TO OPENCV POINTS IF ENABLED ---
        if (draggingMode == DraggingMode.MoveElement && clickedElement is not null)
        {
            Debug.WriteLine($"MouseMove: MoveElement {clickedElement.Tag}");
            // Only snap if ShowContours is checked and we have detected points
            if (ShowContours.IsChecked == true && _opencvDetectedPoints.Count > 0)
            {
                // Find the nearest detected point within a threshold (e.g., 20 pixels)
                const double snapThreshold = 20.0;
                var nearest = _opencvDetectedPoints
                    .Select(p => new { Point = p, Dist = (p - movingPoint).Length })
                    .OrderBy(x => x.Dist)
                    .FirstOrDefault();

                if (nearest != null && nearest.Dist < snapThreshold)
                {
                    movingPoint = nearest.Point;
                }
            }

            Canvas.SetTop(clickedElement, movingPoint.Y - (clickedElement.Height / 2));
            Canvas.SetLeft(clickedElement, movingPoint.X - (clickedElement.Width / 2));

            MovePolyline(movingPoint);
            return;
        }

        if (draggingMode == DraggingMode.MeasureDistance && activeMeasureControl is not null)
        {
            Debug.WriteLine("MouseMove: MeasureDistance");
            int pointIndex = activeMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activeMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MeasureAngle && activeAngleMeasureControl is not null)
        {
            Debug.WriteLine("MouseMove: MeasureAngle");

            int pointIndex = activeAngleMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activeAngleMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode != DraggingMode.MoveElement || clickedElement is null)
        {
            Debug.WriteLine($"MouseMove: not moving? {draggingMode}");
            return;
        }

        Debug.WriteLine($"MouseMove: MoveElement {clickedElement.Tag}");
        Canvas.SetTop(clickedElement, movingPoint.Y - (clickedElement.Height / 2));
        Canvas.SetLeft(clickedElement, movingPoint.X - (clickedElement.Width / 2));

        MovePolyline(movingPoint);
    }

    private void ResizeImage(MouseEventArgs e)
    {
        MainImage.Stretch = Stretch.Fill;
        Mouse.SetCursor(Cursors.SizeAll);
        Point currentPoint = e.GetPosition(ShapeCanvas);
        double deltaX = currentPoint.X - clickedPoint.X;
        double deltaY = currentPoint.Y - clickedPoint.Y;

        ImageGrid.Width = oldGridSize.Width + deltaX;
        ImageGrid.Height = oldGridSize.Height + deltaY;

        e.Handled = true;
    }

    private void MovePolyline(Point newPoint)
    {
        if (pointDraggingIndex < 0 || lines is null)
            return;

        lines.Points[pointDraggingIndex] = newPoint;
        AspectRatioTransformPreview.SetAndScalePoints(lines.Points);
    }

    private async Task<MagickImage?> CorrectDistortion(string pathOfImage)
    {
        if (lines is null || selectedAspectRatio is null)
            return null;

        MagickImage image = new(pathOfImage);
        double scaleFactor = image.Width / MainImage.ActualWidth;

        //  #   X     Y
        //  1   798   304
        //  2   2410  236
        //  3   2753  1405
        //  4   704   1556
        //  3264 x 1836

        // Ratio defined by Height / Width
        double aspectRatio = selectedAspectRatio.RatioValue;

        if (selectedAspectRatio.AspectRatioEnum == AspectRatio.Custom)
        {
            if (CustomHeight.Value is double height
                && CustomWidth.Value is double width
                && height != 0
                && width != 0)
                aspectRatio = height / width;
            else
                return null;
        }

        Rect? visualContentBounds = GetPrivatePropertyValue(lines, "VisualContentBounds") as Rect?;
        Rect finalSize = new(0, 0, MainImage.ActualWidth, MainImage.ActualHeight);

        if (visualContentBounds is not null)
        {
            int width = (int)(visualContentBounds.Value.Width * scaleFactor);
            int height = (int)(width * aspectRatio);
            finalSize = new(0, 0, width, height);
        }

        double[] arguments =
        [
            // top left
            lines.Points[0].X * scaleFactor, lines.Points[0].Y * scaleFactor,
            0,0,

            // bottom left
            lines.Points[3].X * scaleFactor, lines.Points[3].Y * scaleFactor,
            0, finalSize.Height,

            // bottom right
            lines.Points[2].X * scaleFactor, lines.Points[2].Y * scaleFactor,
            finalSize.Width, finalSize.Height,

            // top right
            lines.Points[1].X * scaleFactor, lines.Points[1].Y * scaleFactor,
            finalSize.Width, 0,
        ];

        DistortSettings distortSettings = new(DistortMethod.Perspective)
        {
            Bestfit = true,
        };

        try
        {
            await Task.Run(() => image.Distort(distortSettings, arguments));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return image;
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage? image = await CorrectDistortion(imagePath);

        if (image is null)
        {
            SetUiForCompletedTask();
            return;
        }

        string tempFileName = System.IO.Path.GetTempFileName();
        await image.WriteAsync(tempFileName);
        imagePath = tempFileName;

        MainImage.Source = image.ToBitmapSource();

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        SetUiForCompletedTask();
        HideTransformControls();
    }


    private async void ApplySaveSplitButton_Click(object sender, RoutedEventArgs e)
    {
        SetUiForLongTask();

        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Image Files|*.jpg;",
            RestoreDirectory = true,
            FileName = $"{openedFileName}_corrected.jpg",
        };

        if (saveFileDialog.ShowDialog() is not true || lines is null)
        {
            BottomPane.IsEnabled = true;
            BottomPane.Cursor = null;
            SetUiForCompletedTask();
            return;
        }

        string correctedImageFileName = saveFileDialog.FileName;

        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(correctedImageFileName))
        {
            SetUiForCompletedTask();
            return;
        }

        MagickImage? image = await CorrectDistortion(imagePath);


        if (image is null)
        {
            SetUiForCompletedTask();
            return;
        }

        try
        {
            await image.WriteAsync(correctedImageFileName);

            OpenFolderButton.IsEnabled = true;
            SaveWindow saveWindow = new(correctedImageFileName);
            saveWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            savedPath = correctedImageFileName;

            SetUiForCompletedTask();
            image.Dispose();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Image Files|*.jpg;",
            RestoreDirectory = true,
            FileName = $"{openedFileName}_corrected.jpg",
        };

        if (saveFileDialog.ShowDialog() is not true || lines is null)
        {
            BottomPane.IsEnabled = true;
            BottomPane.Cursor = null;
            SetUiForCompletedTask();
            return;
        }

        string correctedImageFileName = saveFileDialog.FileName;

        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(correctedImageFileName))
        {
            SetUiForCompletedTask();
            return;
        }

        MagickImage image = new(imagePath);

        try
        {
            await image.WriteAsync(correctedImageFileName);

            OpenFolderButton.IsEnabled = true;
            SaveWindow saveWindow = new(correctedImageFileName);
            saveWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            savedPath = correctedImageFileName;

            SetUiForCompletedTask();
            image.Dispose();
        }
    }

    private void SetUiForLongTask()
    {
        BottomPane.IsEnabled = false;
        BottomPane.Cursor = Cursors.Wait;
        IsWorkingBar.Visibility = Visibility.Visible;
    }

    private void SetUiForCompletedTask()
    {
        IsWorkingBar.Visibility = Visibility.Collapsed;
        BottomPane.Cursor = null;
        BottomPane.IsEnabled = true;
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        SetUiForLongTask();

        OpenFileDialog openFileDialog = new()
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.heic;*.bmp|All files (*.*)|*.*",
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() != true)
        {
            SetUiForCompletedTask();
            return;
        }

        RemoveMeasurementControls();
        wpfuiTitleBar.Title = $"Magick Crop & Measure: {openFileDialog.FileName}";
        await OpenImagePath(openFileDialog.FileName);
    }

    private async Task OpenImagePath(string imageFilePath)
    {
        Save.IsEnabled = true;
        MainImage.Stretch = Stretch.Uniform;

        string tempFileName = System.IO.Path.GetTempFileName();
        tempFileName = System.IO.Path.ChangeExtension(tempFileName, ".jpg");
        await Task.Run(async () =>
        {
            MagickImage bitmap = new(imageFilePath);
            bitmap.AutoOrient();

            await bitmap.WriteAsync(tempFileName, MagickFormat.Jpeg);
        });

        // Detect the main rectangle in the image
        SetUiForLongTask();

        imagePath = tempFileName;

        MagickImage bitmapImage = new(imagePath);
        openedFileName = System.IO.Path.GetFileNameWithoutExtension(imageFilePath);
        MainImage.Source = bitmapImage.ToBitmapSource();

        BottomBorder.Visibility = Visibility.Visible;
        SetUiForCompletedTask();

        // Clear OpenCV detected points when loading a new image
        _opencvDetectedPoints.Clear();

        // Create a new project ID for this image
        currentProjectId = Guid.NewGuid().ToString();

        // Trigger an autosave after loading a new image
        _ = AutosaveCurrentState();
    }

    private async void ShowContours_Toggle(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath) 
            || sender is not ToggleSwitch toggleSwitch
            || toggleSwitch.IsChecked is not true)
        {
            _opencvDetectedPoints.Clear();
            return;
        }

        SetUiForLongTask();

        // Apply Harris corner detection to find significant corners  
        List<Point[]> cornerClusters = await Task.Run(() => OpenCvService.DetectCornersWithHarris(imagePath));

        // Flatten all clusters into a single list of points for snapping

        // average the clusters into a single point
        _opencvDetectedPoints = [.. cornerClusters.Select(cluster => new Point(cluster.Average(p => p.X), cluster.Average(p => p.Y)))];

        // scale the point positions to fit within the ImageGrid bounds
        MagickImage magickImage = new(imagePath);
        double scaleX = ImageGrid.ActualWidth / magickImage.Width;
        double scaleY = ImageGrid.ActualHeight / magickImage.Height;

        List<Point> temp = [.. _opencvDetectedPoints];
        _opencvDetectedPoints.Clear();

        foreach (Point point1 in temp)
        {
            Point newPoint = new()
            {
                X = point1.X * scaleX,
                Y = point1.Y * scaleY
            };
            _opencvDetectedPoints.Add(newPoint);
        }

        // for each corner cluster point put an ellipse on the ShapeCanvas  
        foreach (Point point in _opencvDetectedPoints)
        {
            Ellipse ellipse = new()
            {
                Width = 3,
                Height = 3,
                Fill = Brushes.Red,
                StrokeThickness = 1,
                Stroke = Brushes.Black,
                Opacity = 0.3,
            };
            Canvas.SetLeft(ellipse, point.X - (ellipse.Width / 2));
            Canvas.SetTop(ellipse, point.Y - (ellipse.Height / 2));
            ShapeCanvas.Children.Add(ellipse);
        }

        // If we have at least one cluster with enough points, use them for the polygon  
        if (cornerClusters.Count > 0 && cornerClusters[0].Length >= 4)
        {
            // Get the largest cluster (it's already sorted)  
            Point[] largestCluster = cornerClusters[0];

            // Take the most significant 4 points for the polygon  
            // If there are more than 4, we need a strategy to select the best 4 corners  
            Point[] selectedCorners;

            if (largestCluster.Length > 4)
            {
                // Find the 4 points that form the largest quadrilateral  
                selectedCorners = FindOutermostPoints(largestCluster);
            }
            else
            {
                // Use all points if there are exactly 4  
                selectedCorners = largestCluster;
            }

            // Make sure corners form a convex quadrilateral in the correct order  
            //selectedCorners = OrderRectanglePoints(selectedCorners);  

            // Update the polygon corners on the UI  
            // UpdatePolygonCorners(selectedCorners);
        }

        SetUiForCompletedTask();
    }

    /// <summary>
    /// Find the four points that form the largest quadrilateral (the outermost points)
    /// </summary>
    private Point[] FindOutermostPoints(Point[] points)
    {
        // Find the points with min/max X and Y coordinates
        double minX = points.Min(p => p.X);
        double maxX = points.Max(p => p.X);
        double minY = points.Min(p => p.Y);
        double maxY = points.Max(p => p.Y);

        // Find the points closest to each corner of the bounding box
        Point topLeft = points.OrderBy(p => Math.Sqrt(Math.Pow(p.X - minX, 2) + Math.Pow(p.Y - minY, 2))).First();
        Point topRight = points.OrderBy(p => Math.Sqrt(Math.Pow(p.X - maxX, 2) + Math.Pow(p.Y - minY, 2))).First();
        Point bottomLeft = points.OrderBy(p => Math.Sqrt(Math.Pow(p.X - minX, 2) + Math.Pow(p.Y - maxY, 2))).First();
        Point bottomRight = points.OrderBy(p => Math.Sqrt(Math.Pow(p.X - maxX, 2) + Math.Pow(p.Y - maxY, 2))).First();

        return [topLeft, topRight, bottomRight, bottomLeft];
    }

    /// <summary>
    /// Updates the polygon corners on the UI canvas
    /// </summary>
    private void UpdatePolygonCorners(Point[] corners)
    {
        if (corners.Length != 4)
            return;

        // Update the positions of the corner ellipses
        Canvas.SetLeft(TopLeft, corners[0].X - (TopLeft.Width / 2));
        Canvas.SetTop(TopLeft, corners[0].Y - (TopLeft.Height / 2));

        Canvas.SetLeft(TopRight, corners[1].X - (TopRight.Width / 2));
        Canvas.SetTop(TopRight, corners[1].Y - (TopRight.Height / 2));

        Canvas.SetLeft(BottomRight, corners[2].X - (BottomRight.Width / 2));
        Canvas.SetTop(BottomRight, corners[2].Y - (BottomRight.Height / 2));

        Canvas.SetLeft(BottomLeft, corners[3].X - (BottomLeft.Width / 2));
        Canvas.SetTop(BottomLeft, corners[3].Y - (BottomLeft.Height / 2));

        // Update the polyline
        if (lines != null)
        {
            lines.Points.Clear();
            foreach (Point corner in corners)
            {
                lines.Points.Add(corner);
            }
            // Close the polygon by adding the first point again
            lines.Points.Add(corners[0]);

            // Update the aspect ratio preview if needed
            AspectRatioTransformPreview.SetAndScalePoints(lines.Points);
        }

        // Make all polygon elements visible
        foreach (UIElement element in _polygonElements)
        {
            element.Visibility = Visibility.Visible;
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? folderPath = System.IO.Path.GetDirectoryName(savedPath);

        if (folderPath is null || lines is null)
            return;

        Process.Start("explorer.exe", folderPath);
    }

    private static object? GetPrivatePropertyValue(object obj, string propName)
    {
        ArgumentNullException.ThrowIfNull(obj);

        Type t = obj.GetType();
        PropertyInfo? pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new ArgumentOutOfRangeException(nameof(propName), string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
        return pi.GetValue(obj, null);
    }

    private const double ZoomFactor = 0.1;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;

    private void ShapeCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Get the current mouse position relative to the canvas
        Point mousePosition = e.GetPosition(ShapeCanvas);

        // Calculate new scale based on wheel delta
        double zoomChange = e.Delta > 0 ? ZoomFactor : -ZoomFactor;
        double newScaleX = canvasScale.ScaleX + (canvasScale.ScaleX * zoomChange);
        double newScaleY = canvasScale.ScaleY + (canvasScale.ScaleY * zoomChange);

        // Limit zoom to min/max values
        newScaleX = Math.Clamp(newScaleX, MinZoom, MaxZoom);
        newScaleY = Math.Clamp(newScaleY, MinZoom, MaxZoom);

        // Adjust the zoom center to the mouse position
        Point relativePt = mousePosition;

        // Calculate new transform origin
        double absoluteX = (relativePt.X * canvasScale.ScaleX) + canvasTranslate.X;
        double absoluteY = (relativePt.Y * canvasScale.ScaleY) + canvasTranslate.Y;

        // Calculate the new translate values to maintain mouse position
        canvasTranslate.X = absoluteX - (relativePt.X * newScaleX);
        canvasTranslate.Y = absoluteY - (relativePt.Y * newScaleY);

        // Apply new scale
        canvasScale.ScaleX = newScaleX;
        canvasScale.ScaleY = newScaleY;

        e.Handled = true;
    }

    private void PanCanvas(MouseEventArgs e)
    {
        Point currentPosition = e.GetPosition(this);
        Vector delta = currentPosition - clickedPoint;

        Debug.WriteLine($"Delta: {delta}");

        // Update the translation
        canvasTranslate.X += delta.X;
        canvasTranslate.Y += delta.Y;

        clickedPoint = currentPosition;
    }

    private void ShapeCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Debug.WriteLine($"MouseDown: Beginning {draggingMode}");

        if (e.ChangedButton == MouseButton.Left)
        {
            draggingMode = DraggingMode.Panning;
            clickedPoint = e.GetPosition(this);
            ShapeCanvas.CaptureMouse();
            e.Handled = true;
            Debug.WriteLine($"MouseDown: End {draggingMode}");
        }
    }

    private void AspectRatioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not AspectRatioItem item || !IsLoaded)
            return;

        selectedAspectRatio = item;

        if (item.AspectRatioEnum == AspectRatio.Custom)
        {
            CustomButtonGrid.Visibility = Visibility.Visible;
            return;
        }

        CustomButtonGrid.Visibility = Visibility.Collapsed;
        AspectRatioTransformPreview.RatioItem = item;
    }

    private void CustomWidth_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        double aspectRatio = double.NaN;

        if (CustomHeight.Value is double height && CustomWidth.Value is double width && height != 0 && width != 0)
            aspectRatio = height / width;

        double trimmedValue = Math.Round(aspectRatio, 2);
        AspectRatioTextBox.Text = $"Ratio: {trimmedValue}";
    }

    private void FluentWindow_PreviewDragOver(object sender, DragEventArgs e)
    {
        bool isText = e.Data.GetDataPresent("Text");
        e.Handled = true;

        if (isText)
        {
            string textData = (string)e.Data.GetData("Text");
            if (!File.Exists(textData))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
        }

        // After here we will now allow the dropping of "non-text" content
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void FluentWindow_PreviewDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent("Text"))
        {
            if (e.Data.GetData("Text") is string filePath && File.Exists(filePath))
                await OpenImagePath(filePath);
            return;
        }


        if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
        {
            if (e.Data.GetData(DataFormats.FileDrop, true) is not string[] fileNames || fileNames.Length == 0)
                return;

            if (File.Exists(fileNames[0]))
                await OpenImagePath(fileNames[0]);
        }
    }

    private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ShapeCanvas.RenderTransform is not MatrixTransform matTrans)
            return;

        canvasTranslate.X = 0.0;
        canvasTranslate.Y = 0.0;

        canvasScale.ScaleX = 1.0;
        canvasScale.ScaleY = 1.0;
    }

    private async void AutoContrastMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.SigmoidalContrast(10));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void WhiteBalanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Blur(20, 10));
        await Task.Run(() => magickImage.Edge(2.0));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void BlackPointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.BlackThreshold(new Percentage(10)));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void WhitePointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.WhiteThreshold(new Percentage(90)));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void GrayscaleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Grayscale());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void InvertMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Negate());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void AutoLevelsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.AutoLevel());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void AutoGammaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.AutoGamma());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void Rotate90CwMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Rotate(90));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void Rotate90CcwMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Rotate(-90));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void FlipVertMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Flip());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void FlipHozMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Flop());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private void StretchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        oldGridSize = new(ImageGrid.ActualWidth, ImageGrid.ActualHeight);
        originalImageSize = new(MainImage.ActualWidth, MainImage.ActualHeight);
        ShowResizeControls();
    }

    private void CropImage_Click(object sender, RoutedEventArgs e)
    {
        ShowCroppingControls();
    }

    private void ShowCroppingControls()
    {
        HideResizeControls();
        HideTransformControls();

        CropButtonPanel.Visibility = Visibility.Visible;
        CroppingRectangle.Visibility = Visibility.Visible;
    }

    private async void ApplyCropButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(imagePath))
            return;

        MagickGeometry cropGeometry = CroppingRectangle.CropShape;
        MagickImage magickImage = new(imagePath);
        MagickGeometry actualSize = new(magickImage.Width, magickImage.Height);

        double factor = actualSize.Height / MainImage.ActualHeight;
        cropGeometry.ScaleAll(factor);

        SetUiForLongTask();

        magickImage.Crop(cropGeometry);

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();

        HideCroppingControls();
    }

    private void CancelCrop_Click(object sender, RoutedEventArgs e)
    {
        HideCroppingControls();
    }

    private void HideCroppingControls()
    {
        CropButtonPanel.Visibility = Visibility.Collapsed;
        CroppingRectangle.Visibility = Visibility.Collapsed;
    }

    private void PerspectiveCorrectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowTransformControls();
    }

    private void CancelTransformButton_Click(object sender, RoutedEventArgs e)
    {
        HideTransformControls();
    }

    private void ShowTransformControls()
    {
        HideCroppingControls();
        HideResizeControls();

        TransformButtonPanel.Visibility = Visibility.Visible;

        if (detectedRectangle.HasValue)
        {
            // Position the transform corners at the detected rectangle's corners
            Point topLeft = new(detectedRectangle.Value.X, detectedRectangle.Value.Y);
            Point topRight = new(detectedRectangle.Value.X + detectedRectangle.Value.Width, detectedRectangle.Value.Y);
            Point bottomLeft = new(detectedRectangle.Value.X, detectedRectangle.Value.Y + detectedRectangle.Value.Height);
            Point bottomRight = new(detectedRectangle.Value.X + detectedRectangle.Value.Width, detectedRectangle.Value.Y + detectedRectangle.Value.Height);

            Canvas.SetLeft(TopLeft, topLeft.X);
            Canvas.SetTop(TopLeft, topLeft.Y);

            Canvas.SetLeft(TopRight, topRight.X);
            Canvas.SetTop(TopRight, topRight.Y);

            Canvas.SetLeft(BottomLeft, bottomLeft.X);
            Canvas.SetTop(BottomLeft, bottomLeft.Y);

            Canvas.SetLeft(BottomRight, bottomRight.X);
            Canvas.SetTop(BottomRight, bottomRight.Y);
        }

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Visible;
    }

    private void HideTransformControls()
    {
        TransformButtonPanel.Visibility = Visibility.Collapsed;

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;
    }

    private void ImageResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            clickedPoint = e.GetPosition(ShapeCanvas);
            draggingMode = DraggingMode.Resizing;
        }
    }

    private async void ApplyResizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(imagePath))
            return;

        MagickImage magickImage = new(imagePath);
        Percentage imageWidthChangePercentage = new(MainImage.ActualWidth / originalImageSize.Width * 100);
        Percentage imageHeightChangePercentage = new(MainImage.ActualHeight / originalImageSize.Height * 100);

        MagickGeometry resizeGeometry = new(imageWidthChangePercentage, imageHeightChangePercentage)
        {
            IgnoreAspectRatio = true
        };

        SetUiForLongTask();

        magickImage.Resize(resizeGeometry);

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        ResizeUndoRedoItem undoRedoItem = new(MainImage, ImageGrid, oldGridSize, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = null;
        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
        HideResizeControls();
    }

    private void CancelResizeButton_Click(object sender, RoutedEventArgs e)
    {
        ImageGrid.Width = oldGridSize.Width;
        ImageGrid.Height = oldGridSize.Height;
        ImageGrid.InvalidateMeasure();

        HideResizeControls();
    }

    private void HideResizeControls()
    {
        ResizeButtonsPanel.Visibility = Visibility.Collapsed;
        ImageResizeGrip.Visibility = Visibility.Hidden;
    }

    private void ShowResizeControls()
    {
        HideCroppingControls();
        HideTransformControls();

        ResizeButtonsPanel.Visibility = Visibility.Visible;
        ImageResizeGrip.Visibility = Visibility.Visible;
    }

    private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string path = undoRedo.Undo();
        if (!string.IsNullOrWhiteSpace(path))
            imagePath = path;
    }

    private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string path = undoRedo.Redo();
        if (!string.IsNullOrWhiteSpace(path))
            imagePath = path;
    }

    private void MeasureDistanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddNewMeasurementToolToCanvas();
    }

    private void MeasureAngleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddNewAngleMeasurementToolToCanvas();
    }

    private void AddNewMeasurementToolToCanvas()
    {
        double scale = ScaleInput.Value ?? 1.0;
        DistanceMeasurementControl measurementControl = new()
        {
            ScaleFactor = scale,
            Units = MeasurementUnits.Text
        };
        measurementControl.MeasurementPointMouseDown += MeasurementPoint_MouseDown;
        measurementControl.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
        measurementControl.RemoveControlRequested += DistanceMeasurementControl_RemoveControlRequested;
        measurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        // Initialize with reasonable positions based on the canvas size
        measurementControl.InitializePositions(ShapeCanvas.ActualWidth, ShapeCanvas.ActualHeight);
    }

    private void DistanceMeasurementControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is DistanceMeasurementControl control)
        {
            ShapeCanvas.Children.Remove(control);
            measurementTools.Remove(control);
        }
    }

    private void AddNewAngleMeasurementToolToCanvas()
    {
        AngleMeasurementControl measurementControl = new();
        measurementControl.MeasurementPointMouseDown += AngleMeasurementPoint_MouseDown;
        measurementControl.RemoveControlRequested += AngleMeasurementControl_RemoveControlRequested;
        angleMeasurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        // Initialize with reasonable positions based on the canvas size
        measurementControl.InitializePositions(ShapeCanvas.ActualWidth, ShapeCanvas.ActualHeight);
    }

    private void AngleMeasurementControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is AngleMeasurementControl control)
        {
            ShapeCanvas.Children.Remove(control);
            angleMeasurementTools.Remove(control);
        }
    }

    private async void MeasurementControl_SetRealWorldLengthRequested(object sender, double pixelDistance)
    {
        if (sender is not DistanceMeasurementControl measurementControl)
            return;

        // Create and configure the number input dialog
        Wpf.Ui.Controls.TextBox inputTextBox = new()
        {
            PlaceholderText = "ex: 8.5 in",
            ClearButtonEnabled = true,
            Width = 250,
        };

        ContentDialog dialog = new()
        {
            Title = "Set Real World Length",
            Content = inputTextBox,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel"
        };

        // Show the dialog and handle the result
        ContentDialogService dialogService = new();
        dialog.DialogHost = Presenter;
        dialog.Closing += (s, args) =>
        {
            // Check if the primary button was clicked and input is valid
            string[] strings = inputTextBox.Text.Split(' ');
            if (args.Result == ContentDialogResult.Primary &&
                strings.Length > 0 &&
                double.TryParse(strings[0], out double realWorldLength) &&
                realWorldLength > 0)
            {
                // Calculate new scale factor (real-world units per pixel)
                double newScaleFactor = realWorldLength / pixelDistance;
                ScaleInput.Value = newScaleFactor;

                if (strings.Length > 1)
                    MeasurementUnits.Text = strings[1];
            }
        };

        await dialog.ShowAsync();
    }

    private void RemoveMeasurementControls()
    {
        foreach (DistanceMeasurementControl measurementControl in measurementTools)
        {
            measurementControl.MeasurementPointMouseDown -= MeasurementPoint_MouseDown;
            measurementControl.SetRealWorldLengthRequested -= MeasurementControl_SetRealWorldLengthRequested;
            measurementControl.RemoveControlRequested -= DistanceMeasurementControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(measurementControl);
        }

        measurementTools.Clear();

        foreach (AngleMeasurementControl measurementControl in angleMeasurementTools)
        {
            measurementControl.MeasurementPointMouseDown -= AngleMeasurementPoint_MouseDown;
            measurementControl.RemoveControlRequested -= AngleMeasurementControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(measurementControl);
        }

        angleMeasurementTools.Clear();

        //draggingMode = DraggingMode.None;
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse senderEllipse
            && senderEllipse.Parent is Canvas measureCanvas
            && measureCanvas.Parent is DistanceMeasurementControl measureControl
            )
        {
            activeMeasureControl = measureControl;

            draggingMode = DraggingMode.MeasureDistance;
            clickedPoint = e.GetPosition(ShapeCanvas);
            CaptureMouse();
        }
    }

    private void AngleMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse senderEllipse
            && senderEllipse.Parent is Canvas measureCanvas
            && measureCanvas.Parent is AngleMeasurementControl measureControl
            )
        {
            activeAngleMeasureControl = measureControl;

            draggingMode = DraggingMode.MeasureAngle;
            clickedPoint = e.GetPosition(ShapeCanvas);
            CaptureMouse();
        }
    }

    private void ScaleInput_ValueChanged(object sender, RoutedEventArgs e)
    {
        double newScale = ScaleInput.Value ?? 1.0;
        foreach (DistanceMeasurementControl tool in measurementTools)
            tool.ScaleFactor = newScale;
    }

    private void MeasurementUnits_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || string.IsNullOrWhiteSpace(textBox.Text))
            return;

        foreach (DistanceMeasurementControl tool in measurementTools)
            tool.Units = textBox.Text;
    }

    private void CloseMeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveMeasurementControls();
    }

    private void SaveMeasurementsToFile()
    {
        // Create the measurement collection
        MeasurementCollection collection = new()
        {
            GlobalScaleFactor = ScaleInput.Value ?? 1.0,
            GlobalUnits = MeasurementUnits.Text
        };

        foreach (DistanceMeasurementControl control in measurementTools)
            collection.DistanceMeasurements.Add(control.ToDto());

        foreach (AngleMeasurementControl control in angleMeasurementTools)
            collection.AngleMeasurements.Add(control.ToDto());

        // Show save file dialog
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Measurement Files|*.measurements.json",
            RestoreDirectory = true,
            FileName = $"{openedFileName}_measurements.json"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        // Save to the selected file
        collection.SaveToFile(saveFileDialog.FileName);
    }

    private void LoadMeasurementsFromFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "Measurement Files|*.measurements.json|All Files|*.*",
            RestoreDirectory = true
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        // Clear existing measurements
        RemoveMeasurementControls();

        // Load from file
        MeasurementCollection? collection = MeasurementCollection.LoadFromFile(openFileDialog.FileName);
        if (collection == null)
        {
            System.Windows.MessageBox.Show(
                "Failed to load measurements file.",
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        ScaleInput.Value = collection.GlobalScaleFactor;
        MeasurementUnits.Text = collection.GlobalUnits;

        // Add distance measurements
        foreach (DistanceMeasurementControlDto dto in collection.DistanceMeasurements)
        {
            DistanceMeasurementControl control = new()
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            control.FromDto(dto);
            control.MeasurementPointMouseDown += MeasurementPoint_MouseDown;
            control.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
            control.RemoveControlRequested += DistanceMeasurementControl_RemoveControlRequested;
            measurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        // Add angle measurements
        foreach (AngleMeasurementControlDto dto in collection.AngleMeasurements)
        {
            AngleMeasurementControl control = new();
            control.FromDto(dto);
            control.MeasurementPointMouseDown += AngleMeasurementPoint_MouseDown;
            control.RemoveControlRequested += AngleMeasurementControl_RemoveControlRequested;
            angleMeasurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }
    }

    private async void SaveMeasurementsPackageToFile()
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            System.Windows.MessageBox.Show(
                "No image loaded. Please open an image first.",
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // Create the package
        MagickCropMeasurementPackage package = new()
        {
            ImagePath = imagePath,
            Metadata = new PackageMetadata
            {
                OriginalFilename = openedFileName
            },
            Measurements = new MeasurementCollection
            {
                GlobalScaleFactor = ScaleInput.Value ?? 1.0,
                GlobalUnits = MeasurementUnits.Text
            }
        };

        // Add all measurements to the package
        foreach (DistanceMeasurementControl control in measurementTools)
        {
            package.Measurements.DistanceMeasurements.Add(control.ToDto());
        }

        foreach (AngleMeasurementControl control in angleMeasurementTools)
        {
            package.Measurements.AngleMeasurements.Add(control.ToDto());
        }

        // Show save file dialog
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "MagickCrop Measurement Files|*.mcm",
            RestoreDirectory = true,
            FileName = $"{openedFileName}_measurements.mcm"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        SetUiForLongTask();

        try
        {
            // Save to the selected file
            bool success = await package.SaveToFileAsync(saveFileDialog.FileName);

            if (success)
            {
                System.Windows.MessageBox.Show(
                    "Measurements and image saved successfully.",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Failed to save the measurement package.",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            SetUiForCompletedTask();
        }
    }

    public async Task<bool> LoadMeasurementsPackageFromFile()
    {
        SetUiForLongTask();

        OpenFileDialog openFileDialog = new()
        {
            Filter = "Magick Crop Project Files|*.mcm|All Files|*.*",
            RestoreDirectory = true
        };

        if (openFileDialog.ShowDialog() is not true)
        {
            SetUiForCompletedTask();
            return false;
        }

        string fileName = openFileDialog.FileName;
        await LoadMeasurementPackageAsync(fileName);

        return true;
    }

    private async Task LoadMeasurementPackageAsync(string fileName)
    {
        MagickCropMeasurementPackage? package = null;
        try
        {
            package = await MagickCropMeasurementPackage.LoadFromFileAsync(fileName);
            if (package is null
                || string.IsNullOrEmpty(package.ImagePath)
                || !File.Exists(package.ImagePath))
            {
                Wpf.Ui.Controls.MessageBox uiMessageBox = new()
                {
                    Title = "Error",
                    Content = "Failed to load measurement package. The image file may be missing or corrupted.",
                };
                await uiMessageBox.ShowDialogAsync();
                SetUiForCompletedTask();
                return;
            }

            // Load the image
            await OpenImagePath(package.ImagePath);
        }
        finally
        {
            SetUiForCompletedTask();
        }

        // Clear existing measurements
        RemoveMeasurementControls();

        // Set global measurement properties
        ScaleInput.Value = package.Measurements.GlobalScaleFactor;
        MeasurementUnits.Text = package.Measurements.GlobalUnits;

        // Add distance measurements
        foreach (DistanceMeasurementControlDto dto in package.Measurements.DistanceMeasurements)
        {
            DistanceMeasurementControl control = new()
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            control.FromDto(dto);
            control.MeasurementPointMouseDown += MeasurementPoint_MouseDown;
            control.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
            control.RemoveControlRequested += DistanceMeasurementControl_RemoveControlRequested;
            measurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        // Add angle measurements
        foreach (AngleMeasurementControlDto dto in package.Measurements.AngleMeasurements)
        {
            AngleMeasurementControl control = new();
            control.FromDto(dto);
            control.MeasurementPointMouseDown += AngleMeasurementPoint_MouseDown;
            control.RemoveControlRequested += AngleMeasurementControl_RemoveControlRequested;
            angleMeasurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        if (package?.Metadata?.ProjectId is not null)
            currentProjectId = package.Metadata.ProjectId;
        else
            currentProjectId = Guid.NewGuid().ToString();
    }

    public async void LoadMeasurementsPackageFromFile(string filePath)
    {
        SetUiForLongTask();
        WelcomeMessageModal.Visibility = Visibility.Collapsed;

        await LoadMeasurementPackageAsync(filePath);
    }

    private void SavePackageButton_Click(object sender, RoutedEventArgs e)
    {
        SaveMeasurementsPackageToFile();
    }

    private void InitializeProjectManager()
    {
        recentProjectsManager = new RecentProjectsManager();

        // Setup autosave timer
        autoSaveTimer = new System.Timers.Timer(AutoSaveIntervalMs);
        autoSaveTimer.Elapsed += AutoSaveTimer_Elapsed;
        autoSaveTimer.AutoReset = true;
        //autoSaveTimer.Start();

        // Create a new project ID
        currentProjectId = Guid.NewGuid().ToString();
    }

    private void AutoSaveTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Run on UI thread
        Dispatcher.Invoke(async () =>
        {
            // Only autosave if we have an image and measurements that need saving
            if (MainImage.Source != null && !string.IsNullOrEmpty(imagePath))
            {
                await AutosaveCurrentState();
            }
        });
    }

    private async Task AutosaveCurrentState()
    {
        return;

        if (recentProjectsManager == null || MainImage.Source == null || string.IsNullOrEmpty(imagePath))
            return;

        try
        {
            // Create a package with the current state
            MagickCropMeasurementPackage package = new()
            {
                ImagePath = imagePath,
                Metadata = new PackageMetadata
                {
                    OriginalFilename = openedFileName,
                    ProjectId = currentProjectId,
                    LastModified = DateTime.Now
                },
                Measurements = new MeasurementCollection
                {
                    GlobalScaleFactor = ScaleInput.Value ?? 1.0,
                    GlobalUnits = MeasurementUnits.Text
                }
            };

            foreach (DistanceMeasurementControl control in measurementTools)
                package.Measurements.DistanceMeasurements.Add(control.ToDto());

            foreach (AngleMeasurementControl control in angleMeasurementTools)
                package.Measurements.AngleMeasurements.Add(control.ToDto());

            await recentProjectsManager.AutosaveProject(package, MainImage.Source as BitmapSource);
        }
        catch (Exception ex)
        {
            // Log error but don't show to user since this is automatic
            Debug.WriteLine($"Error autosaving project: {ex.Message}");
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Stop the autosave timer
        autoSaveTimer?.Stop();

        // Save the current project state one last time
        _ = AutosaveCurrentState();

        base.OnClosing(e);
    }

    private void FluentWindow_MouseUp(object sender, MouseButtonEventArgs e)
    {
        //if (e.ChangedButton == MouseButton.Left && draggingMode == DraggingMode.Panning)
        //{
        //    draggingMode = DraggingMode.None;
        //    ShapeCanvas.ReleaseMouseCapture();
        //    e.Handled = true;
        //}
    }
}
