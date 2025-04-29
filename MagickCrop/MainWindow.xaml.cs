using ImageMagick;
using MagickCrop.Controls;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    private DraggingMode draggingMode = DraggingMode.None;

    private string openedFileName = string.Empty;
    private MagickCropMeasurementPackage? openedPackage;
    private readonly List<UIElement> _polygonElements;

    private readonly UndoRedo undoRedo = new();
    private AspectRatioItem? selectedAspectRatio;
    private readonly ObservableCollection<DistanceMeasurementControl> measurementTools = [];
    private DistanceMeasurementControl? activeMeasureControl;
    private readonly ObservableCollection<AngleMeasurementControl> angleMeasurementTools = [];
    private AngleMeasurementControl? activeAngleMeasureControl;

    private Services.RecentProjectsManager? recentProjectsManager;
    private string? currentProjectId;
    private System.Timers.Timer? autoSaveTimer;
    private readonly int AutoSaveIntervalMs = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

    private static readonly List<FormatItem> _formats =
    [
        new FormatItem { Name = "JPEG Image", Format = MagickFormat.Jpg, Extension = ".jpg", SupportsQuality = true },
        new FormatItem { Name = "PNG Image", Format = MagickFormat.Png, Extension = ".png", SupportsQuality = false },
        new FormatItem { Name = "BMP Image", Format = MagickFormat.Bmp, Extension = ".bmp", SupportsQuality = false },
        new FormatItem { Name = "TIFF Image", Format = MagickFormat.Tiff, Extension = ".tiff", SupportsQuality = false },
        new FormatItem { Name = "WebP Image", Format = MagickFormat.WebP, Extension = ".webp", SupportsQuality = true },
        // new FormatItem { Name = "HEIC Image", Format = MagickFormat.Heic, Extension = ".heic", SupportsQuality = true }
    ];

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
        UpdateOpenedFileNameText();
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
        CaptureMouse();
    }

    private void TopLeft_MouseMove(object sender, MouseEventArgs e)
    {
        if (Mouse.MiddleButton == MouseButtonState.Released && Mouse.LeftButton == MouseButtonState.Released)
        {
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
            ReleaseMouseCapture();
            draggingMode = DraggingMode.None;

            return;
        }

        if (draggingMode == DraggingMode.Panning)
        {
            PanCanvas(e);
            return;
        }

        if (draggingMode == DraggingMode.Resizing)
        {
            ResizeImage(e);
            return;
        }

        Point movingPoint = e.GetPosition(ShapeCanvas);
        if (draggingMode == DraggingMode.MeasureDistance && activeMeasureControl is not null)
        {
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
            int pointIndex = activeAngleMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activeAngleMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode != DraggingMode.MoveElement || clickedElement is null)
            return;

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

    private void PanCanvas(MouseEventArgs e)
    {
        Mouse.SetCursor(Cursors.SizeAll);
        Point currentPoint = e.GetPosition(ShapeCanvas);
        double deltaX = currentPoint.X - clickedPoint.X;
        double deltaY = currentPoint.Y - clickedPoint.Y;

        if (ShapeCanvas.RenderTransform is not MatrixTransform matTrans)
            return;

        Matrix mat = matTrans.Matrix;
        mat.Translate(deltaX / scaleFactor, deltaY / scaleFactor);
        matTrans.Matrix = mat;
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
        if (string.IsNullOrEmpty(imagePath))
            return;

        SetUiForLongTask();

        try
        {
            // Get current image dimensions
            MagickImage magickImage = new(imagePath);
            double width = magickImage.Width;
            double height = magickImage.Height;
            magickImage.Dispose();

            // Create and show save options dialog in a window
            SaveOptionsDialog saveOptionsDialog = new(width, height);
            Window dialogWindow = new()
            {
                Title = "Save Options",
                Content = saveOptionsDialog,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize
            };

            // Show the dialog window
            bool? dialogResult = dialogWindow.ShowDialog();

            // If dialog was cancelled or closed
            if (dialogResult != true)
            {
                SetUiForCompletedTask();
                return;
            }

            SaveOptions options = saveOptionsDialog.Options;

            // Configure save file dialog based on selected format
            SaveFileDialog saveFileDialog = new()
            {
                Filter = SaveOptionsDialog.GetFileFilter(
                            _formats.FirstOrDefault(f => f.Format == options.Format)
                            ?? _formats[0]),
                DefaultExt = options.Extension,
                RestoreDirectory = true,
                FileName = $"{openedFileName}_edited{options.Extension}",
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                SetUiForCompletedTask();
                return;
            }

            string correctedImageFileName = saveFileDialog.FileName;

            // Load image and apply options
            using MagickImage image = new(imagePath);

            // Resize if requested
            if (options.Resize)
            {
                MagickGeometry resizeGeometry = new((uint)options.Width, (uint)options.Height)
                {
                    IgnoreAspectRatio = !options.MaintainAspectRatio
                };
                image.Resize(resizeGeometry);
            }

            // Set quality for formats that support it
            image.Quality = (uint)options.Quality;

            // Save with the selected format
            await image.WriteAsync(correctedImageFileName, options.Format);

            // Show preview and enable open folder button
            OpenFolderButton.IsEnabled = true;
            SaveWindow saveWindow = new(correctedImageFileName);
            saveWindow.Show();

            // Store the saved path for the open folder button
            savedPath = correctedImageFileName;
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
            SetUiForCompletedTask();
        }
    }

    private void SetUiForLongTask()
    {
        BottomPane.IsEnabled = false;
        BottomPane.Cursor = Cursors.Wait;
        IsWorkingBar.Visibility = Visibility.Visible;
        autoSaveTimer?.Stop();
    }

    private void SetUiForCompletedTask()
    {
        IsWorkingBar.Visibility = Visibility.Collapsed;
        BottomPane.Cursor = null;
        BottomPane.IsEnabled = true;

        autoSaveTimer?.Stop();
        autoSaveTimer?.Start();
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
        wpfuiTitleBar.Title = $"Magick Crop & Measure: {System.IO.Path.GetFileName(openFileDialog.FileName)}";
        await OpenImagePath(openFileDialog.FileName);
    }

    private async Task OpenImagePath(string imageFilePath)
    {
        Save.IsEnabled = true;
        ImageGrid.Width = 700;
        MainImage.Stretch = Stretch.Uniform;

        WelcomeMessageModal.Visibility = Visibility.Collapsed;
        string tempFileName = System.IO.Path.GetTempFileName();
        tempFileName = System.IO.Path.ChangeExtension(tempFileName, ".jpg");
        await Task.Run(async () =>
        {
            MagickImage bitmap = new(imageFilePath);
            bitmap.AutoOrient();

            await bitmap.WriteAsync(tempFileName, MagickFormat.Jpeg);
        });

        MagickImage bitmapImage = new(tempFileName);

        imagePath = tempFileName;
        openedFileName = System.IO.Path.GetFileNameWithoutExtension(imageFilePath);
        MainImage.Source = bitmapImage.ToBitmapSource();

        BottomBorder.Visibility = Visibility.Visible;
        SetUiForCompletedTask();

        // Create a new project ID for this image
        currentProjectId = Guid.NewGuid().ToString();

        // Update the ReOpenFileButton to show the current file name
        UpdateOpenedFileNameText();
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

    private void ShapeCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ShapeCanvas.RenderTransform is not MatrixTransform matTrans)
            return;

        Point pos1 = e.GetPosition(ShapeCanvas);

        scaleFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;

        Matrix mat = matTrans.Matrix;
        mat.ScaleAt(scaleFactor, scaleFactor, pos1.X, pos1.Y);
        matTrans.Matrix = mat;
        e.Handled = true;
    }

    private void ShapeCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.MiddleButton == MouseButtonState.Pressed || Mouse.LeftButton == MouseButtonState.Pressed)
        {
            clickedPoint = e.GetPosition(ShapeCanvas);
            draggingMode = DraggingMode.Panning;
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

        matTrans.Matrix = new Matrix();
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
        await Task.Run(() => magickImage.WhiteBalance());

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

        draggingMode = DraggingMode.None;
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

    private async void SaveMeasurementsPackageToFile()
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Error",
                Content = "No image loaded. Please open an image first.",
            };
            await uiMessageBox.ShowDialogAsync();
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
            bool success = package.SaveToFileAsync(saveFileDialog.FileName);

            if (!success)
            {
                Wpf.Ui.Controls.MessageBox uiMessageBox = new()
                {
                    Title = "Error",
                    Content = "Failed to save the measurement package.",
                };
                await uiMessageBox.ShowDialogAsync();
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
            package = MagickCropMeasurementPackage.LoadFromFileAsync(fileName);
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
                WelcomeMessageModal.Visibility = Visibility.Visible;
                return;
            }
            openedPackage = package;

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
        
        UpdateOpenedFileNameText();
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
        recentProjectsManager = new Services.RecentProjectsManager();

        // Setup autosave timer
        autoSaveTimer = new System.Timers.Timer(AutoSaveIntervalMs);
        autoSaveTimer.Elapsed += AutoSaveTimer_Elapsed;
        autoSaveTimer.AutoReset = true;

        // Create a new project ID
        currentProjectId = Guid.NewGuid().ToString();
    }

    private void AutoSaveTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (IsWorkingBar.Visibility == Visibility.Visible)
            return; // Don't autosave if the UI is busy

        // Run on UI thread
        Dispatcher.Invoke(() =>
        {
            // Only autosave if we have an image and measurements that need saving
            if (MainImage.Source == null || string.IsNullOrEmpty(imagePath))
                return;

            AutosaveCurrentState();
        });
    }

    private void AutosaveCurrentState()
    {
        if (recentProjectsManager == null || MainImage.Source == null || string.IsNullOrEmpty(imagePath))
            return;

        try
        {
            PackageMetadata packageMetadata = new()
            {
                OriginalFilename = openedFileName,
                ProjectId = currentProjectId,
                LastModified = DateTime.Now
            };

            if (openedPackage is not null)
            {
                packageMetadata = openedPackage.Metadata;
                packageMetadata.LastModified = DateTime.Now;
            }

            // Create a package with the current state
            MagickCropMeasurementPackage package = new()
            {
                ImagePath = imagePath,
                Metadata = packageMetadata,
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

            recentProjectsManager.AutosaveProject(package, MainImage.Source as BitmapSource);
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
        AutosaveCurrentState();

        base.OnClosing(e);
    }

    private void UpdateOpenedFileNameText()
    {
        if (string.IsNullOrEmpty(openedFileName))
        {
            ReOpenFileText.Text = "Image/Project Name";
            CloseFileIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            ReOpenFileText.Text = openedFileName;

            if (openedPackage is not null)
                ReOpenFileText.Text = $" {openedPackage.Metadata.OriginalFilename}";
            CloseFileIcon.Visibility = Visibility.Visible;
        }
    }

    private void CloseFileIcon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // Prevent the click from bubbling to the button
        ResetApplicationState();
    }

    private void ResetApplicationState()
    {
        // Stop the autosave timer
        autoSaveTimer?.Stop();
        AutosaveCurrentState();

        // Clear the image
        MainImage.Source = null;
        imagePath = null;
        openedFileName = string.Empty;
        openedPackage = null;
        savedPath = null;
        
        // Reset the title
        wpfuiTitleBar.Title = "Magick Crop & Measure by TheJoeFin";
        
        // Reset UI elements
        RemoveMeasurementControls();
        HideTransformControls();
        HideCroppingControls();
        HideResizeControls();
        BottomBorder.Visibility = Visibility.Collapsed;
        WelcomeMessageModal.Visibility = Visibility.Visible;
        OpenFolderButton.IsEnabled = false;
        Save.IsEnabled = false;
        
        // Reset the canvas transform
        if (ShapeCanvas.RenderTransform is MatrixTransform matTrans)
        {
            matTrans.Matrix = new Matrix();
        }
        
        // Reset undo/redo
        undoRedo.Clear();
        
        // Create a new project ID
        currentProjectId = Guid.NewGuid().ToString();
        
        // Update the button state
        UpdateOpenedFileNameText();
    }
}
