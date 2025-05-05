using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace MagickCrop.Controls;

public partial class HorizontalLineControl : UserControl
{
    private bool isDragging = false;
    private Point initialMousePosition;

    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public HorizontalLineControl()
    {
        InitializeComponent();
    }

    public void Initialize(double canvasWidth, double canvasHeight, double yPosition = 40)
    {
        // Set line points
        HorizontalLine.X1 = 0;
        HorizontalLine.Y1 = yPosition;
        HorizontalLine.X2 = canvasWidth;
        HorizontalLine.Y2 = yPosition;
    }

    public void Resize(double canvasWidth)
    {
        HorizontalLine.X2 = canvasWidth;
    }

    private void HorizontalLine_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            isDragging = true;
            initialMousePosition = e.GetPosition(LineCanvas);
            HorizontalLine.CaptureMouse();
            e.Handled = true;
        }
        else if (e.RightButton == MouseButtonState.Pressed && this.ContextMenu is ContextMenu contextMenu)
        {
            contextMenu.IsOpen = true;
        }
    }

    private void HorizontalLine_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging)
        {
            Point currentMousePosition = e.GetPosition(LineCanvas);
            double deltaY = currentMousePosition.Y - initialMousePosition.Y;

            HorizontalLine.Y1 += deltaY;
            HorizontalLine.Y2 += deltaY;

            initialMousePosition = currentMousePosition;
            e.Handled = true;
        }
    }

    private void HorizontalLine_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isDragging = false;
        HorizontalLine.ReleaseMouseCapture();
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
        // if (HorizontalLine.Stroke is SolidColorBrush brush)
        // {
        //     colorPicker.Color = brush.Color;
        // }
        // 
        // colorDialog.Content = colorPicker;
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        colorDialog.DialogHost = mainWindow.Presenter;

        // Show dialog
        ContentDialogResult result = await colorDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // HorizontalLine.Stroke = new SolidColorBrush(colorPicker.Color);
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
            Value = HorizontalLine.StrokeThickness,
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
            HorizontalLine.StrokeThickness = thicknessSlider.Value.Value;
        }
    }

    private void RemoveLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    public HorizontalLineControlDto ToDto()
    {
        return new HorizontalLineControlDto
        {
            Position = HorizontalLine.Y1,
            StrokeColor = (HorizontalLine.Stroke as SolidColorBrush)?.Color.ToString() ?? "#800080",
            StrokeThickness = HorizontalLine.StrokeThickness
        };
    }

    public void FromDto(HorizontalLineControlDto dto)
    {
        HorizontalLine.Y1 = dto.Position;
        HorizontalLine.Y2 = dto.Position;

        // Parse color from stored string
        if (dto.StrokeColor != null)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(dto.StrokeColor);
                HorizontalLine.Stroke = new SolidColorBrush(color);
            }
            catch
            {
                // Fallback to default color if parsing fails
                HorizontalLine.Stroke = Brushes.Purple;
            }
        }

        HorizontalLine.StrokeThickness = dto.StrokeThickness;
    }
}
