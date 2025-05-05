using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace MagickCrop.Controls;

public partial class VerticalLineControl : UserControl
{
    private bool isDragging = false;
    private Point initialMousePosition;

    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public VerticalLineControl()
    {
        InitializeComponent();
    }

    public void Initialize(double canvasWidth, double canvasHeight, double xPosition = 40)
    {
        // Set line points
        VerticalLine.X1 = xPosition;
        VerticalLine.Y1 = 0;
        VerticalLine.X2 = xPosition;
        VerticalLine.Y2 = canvasHeight;
    }

    public void Resize(double canvasHeight)
    {
        VerticalLine.Y2 = canvasHeight;
    }

    private void VerticalLine_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            isDragging = true;
            initialMousePosition = e.GetPosition(LineCanvas);
            VerticalLine.CaptureMouse();
            e.Handled = true;
        }
        else if (e.RightButton == MouseButtonState.Pressed && this.ContextMenu is ContextMenu contextMenu)
        {
            contextMenu.IsOpen = true;
        }
    }

    private void VerticalLine_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging)
        {
            Point currentMousePosition = e.GetPosition(LineCanvas);
            double deltaX = currentMousePosition.X - initialMousePosition.X;

            VerticalLine.X1 += deltaX;
            VerticalLine.X2 += deltaX;

            initialMousePosition = currentMousePosition;
            e.Handled = true;
        }
    }

    private void VerticalLine_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isDragging = false;
        VerticalLine.ReleaseMouseCapture();
        e.Handled = true;
    }

    private async void ChangeColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create a simple color picker dialog
        ContentDialog colorDialog = new()
        {
            Title = "Select Line Color",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Apply"
        };

        // ColorPicker colorPicker = new();
        // if (VerticalLine.Stroke is SolidColorBrush brush)
        // {
        //     colorPicker.Color = brush.Color;
        // }
        // 
        // colorDialog.Content = colorPicker;

        // Show dialog
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        colorDialog.DialogHost = mainWindow.Presenter;
        ContentDialogResult result = await colorDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // VerticalLine.Stroke = new SolidColorBrush(colorPicker.Color);
        }
    }

    private async void ChangeThicknessMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create a thickness selector dialog
        ContentDialog thicknessDialog = new()
        {
            Title = "Select Line Thickness",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Apply"
        };

        NumberBox thicknessSlider = new()
        {
            Value = VerticalLine.StrokeThickness,
            Minimum = 0.5,
            Maximum = 5.0,
            SmallChange = 0.5,
            PlaceholderText = "Line Thickness"
        };

        thicknessDialog.Content = thicknessSlider;

        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;
        thicknessDialog.DialogHost = mainWindow.Presenter;

        // Show dialog
        ContentDialogResult result = await thicknessDialog.ShowAsync();
        if (result == ContentDialogResult.Primary && thicknessSlider.Value.HasValue)
        {
            VerticalLine.StrokeThickness = thicknessSlider.Value.Value;
        }
    }

    private void RemoveLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    public VerticalLineControlDto ToDto()
    {
        return new VerticalLineControlDto
        {
            Position = VerticalLine.X1,
            StrokeColor = (VerticalLine.Stroke as SolidColorBrush)?.Color.ToString() ?? "#800080",
            StrokeThickness = VerticalLine.StrokeThickness
        };
    }

    public void FromDto(VerticalLineControlDto dto)
    {
        VerticalLine.X1 = dto.Position;
        VerticalLine.X2 = dto.Position;

        // Parse color from stored string
        if (dto.StrokeColor != null)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(dto.StrokeColor);
                VerticalLine.Stroke = new SolidColorBrush(color);
            }
            catch
            {
                // Fallback to default color if parsing fails
                VerticalLine.Stroke = Brushes.Purple;
            }
        }

        VerticalLine.StrokeThickness = dto.StrokeThickness;
    }
}
