namespace InfiniteWhiteboard;

using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// TODO: Custom title bar
/// TODO: Save button to save project
/// TODO: Load button to load project
/// TODO: Shapes button
/// TODO: Export button to export to png and svg
/// EXTREAME: Online mode
///     make a server, host it somewhere, sync user actions....

public partial class MainWindow : Window
{
    private static SolidColorBrush BUTTON_COLOR_SELECTED;
    private static SolidColorBrush BUTTON_COLOR_DEFAULT;
    private static Color COLORPICKER_COLOR_DEFAULT;
    private static SolidColorBrush INKCANVAS_BACKGROUND_DEFAULT;

    static MainWindow()
    {
        var val = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0);
        if(val is not null && val is int theme && theme == 0)
        {
            BUTTON_COLOR_DEFAULT = new SolidColorBrush(Color.FromRgb(0x67, 0x3A, 0xB7));
            BUTTON_COLOR_SELECTED = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            COLORPICKER_COLOR_DEFAULT = Colors.White;
            INKCANVAS_BACKGROUND_DEFAULT = Brushes.Black;
        }
        else
        {
            BUTTON_COLOR_SELECTED = new SolidColorBrush(Color.FromRgb(0x67, 0x3A, 0xB7));
            BUTTON_COLOR_DEFAULT = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            COLORPICKER_COLOR_DEFAULT = Colors.Black;
            INKCANVAS_BACKGROUND_DEFAULT = Brushes.White;
        }
    }

    private bool _isMiddleMousePressed = false;
    private Point _cursorPrevPos;

    private Color BrushColor
    {
        set
        {
            InkCanvas.DefaultDrawingAttributes.Color = value;
            SetButtonColors(ColorBtn, new SolidColorBrush(value));
        }
    }

    private double BrushSize
    {
        set
        {
            InkCanvas.DefaultDrawingAttributes.Width = value / 10d;
            InkCanvas.DefaultDrawingAttributes.Height = value / 10d;

            InkCanvas.EraserShape = new RectangleStylusShape(Math.Max(2, value / 10d), Math.Max(2, value / 10d));
            var editMode = InkCanvas.EditingMode;
            InkCanvas.EditingMode = InkCanvasEditingMode.None;
            InkCanvas.EditingMode = editMode;

            BrushSizeText.Text = $"Brush size: {value.ToString("0.0")}";
        }
    }

    private InkCanvasEditingMode EditMode
    {
        get => InkCanvas.EditingMode;
        set
        {
            InkCanvas.EditingMode = value;

            SetButtonColors(BrushBtn, BUTTON_COLOR_DEFAULT);
            BrushBtn.Margin = new Thickness(4);
            BrushBtn.HorizontalAlignment = HorizontalAlignment.Right;

            SetButtonColors(EraseBtn, BUTTON_COLOR_DEFAULT);
            EraseBtn.Margin = new Thickness(4);
            EraseBtn.HorizontalAlignment = HorizontalAlignment.Right;

            if (value == InkCanvasEditingMode.Ink)
            {
                SetButtonColors(BrushBtn, BUTTON_COLOR_SELECTED);
                BrushBtn.Margin = new Thickness(4, 4, 16, 4);
                BrushBtn.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else if (value == InkCanvasEditingMode.EraseByPoint || value == InkCanvasEditingMode.EraseByStroke)
            {
                SetButtonColors(EraseBtn, BUTTON_COLOR_SELECTED);
                EraseBtn.Margin = new Thickness(4, 4, 16, 4);
                EraseBtn.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
    }

    private double _zoom = 1;
    private bool _isInitializing = true;
    private bool _isUpdatingZoom = false;

    public MainWindow()
    {
        InitializeComponent();

        SetButtonColors(ColorBtn, BUTTON_COLOR_DEFAULT);
        SetButtonColors(BrushSizeBtn, BUTTON_COLOR_DEFAULT);
        SetButtonColors(BrushBtn, BUTTON_COLOR_DEFAULT);
        SetButtonColors(EraseBtn, BUTTON_COLOR_DEFAULT);

        ColorPicker.Color = BrushColor = COLORPICKER_COLOR_DEFAULT;
        BrushSizeSlider.Value = BrushSize = 5;
        EditMode = InkCanvasEditingMode.Ink;

        PreviewMouseUp += OnMouseEvent;
        PreviewMouseDown += OnMouseEvent;
        PreviewMouseMove += OnMouseMoveEvent;
        MouseWheel += OnMouseWheelEvent;

        InkCanvas.Background = INKCANVAS_BACKGROUND_DEFAULT;
        InkCanvas.DefaultDrawingAttributes.FitToCurve = true;
        InkCanvas.PreviewMouseDown += InkCanvas_MouseDown;
        InkCanvas.Loaded += InkCanvas_Loaded;

        ColorPicker.PreviewMouseUp += OnColorChange;
        ColorPicker.PreviewMouseMove += OnColorChange;

        BrushSizeSlider.ValueChanged += BrushSizeSlider_ValueChanged;

        ColorBtn.Click += ColorBtn_Click;
        BrushSizeBtn.Click += BrushSizeBtn_Click;
        BrushBtn.Click += BrushBtn_Click;
        EraseBtn.Click += EraseBtn_Click;

        KeyUp += MainWindow_KeyUp;

        TitleBarGrid.Background = new SolidColorBrush(Color.FromRgb(46,46,46));

        ZoomSlider.Value = (int)(_zoom * 10);
        _isInitializing = false;
    }

    private void OnMouseEvent(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed && !_isMiddleMousePressed)
        {
            _isMiddleMousePressed = true;
            _cursorPrevPos = e.GetPosition(this);
        }
        else if (e.MiddleButton == MouseButtonState.Released)
            _isMiddleMousePressed = false;
    }

    private void OnMouseWheelEvent(object sender, MouseWheelEventArgs e)
    {
        var diff = e.Delta > 0 ? 0.1 : -0.1;
        _zoom = Math.Clamp(_zoom + diff, 0.1, 10);

        var pos = e.GetPosition(InkCanvas);
        UpdateZoom(pos);
    }

    private void OnMouseMoveEvent(object sender, MouseEventArgs e)
    {
        if (_isMiddleMousePressed)
        {
            var cursorPoint = e.GetPosition(this);
            var vector = cursorPoint - _cursorPrevPos;
            _cursorPrevPos = cursorPoint;

            //var topLeft = InkCanvas.TranslatePoint(new Point(0, 0), this);
            //var bottomRight = InkCanvas.TranslatePoint(new Point(InkCanvas.ActualWidth, InkCanvas.ActualHeight), this);

            //if(topLeft.X + vector.X < 0 &&
            //    topLeft.Y + vector.Y < 0 &&
            //    bottomRight.X + vector.X > MainGrid.ActualWidth &&
            //    bottomRight.Y + vector.Y > MainGrid.ActualHeight && false)
            //{
            //    var matrix = InkCanvas.RenderTransform.Value;
            //    matrix.Translate(vector.X, vector.Y);
            //    InkCanvas.RenderTransform = new MatrixTransform(matrix);
            //}
            //else
            //{
            InkCanvas.Strokes.Transform(new Matrix(1, 0, 0, 1, vector.X * (0.1 / _zoom), vector.Y * (0.1 / _zoom)), false);
            //}
        }
    }

    private void InkCanvas_Loaded(object sender, RoutedEventArgs e)
    {
        var matrix = InkCanvas.RenderTransform.Value;
        matrix.ScaleAtPrepend(10, 10, MainGrid.ActualWidth / 2, MainGrid.ActualHeight / 2);
        InkCanvas.RenderTransform = new MatrixTransform(matrix);
    }

    private void InkCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        HideAll();
    }

    // for some reason event ColorChanged on ColorPicker do not work
    private void OnColorChange(object sender, MouseEventArgs e)
    {
        BrushColor = ColorPicker.Color;
    }

    private void SetButtonColors(Button btn, SolidColorBrush background)
    {
        btn.Background = btn.BorderBrush = background;

        // thank to https://stackoverflow.com/questions/11867545/change-text-color-based-on-brightness-of-the-covered-background-area
        var brightness = Math.Round((background.Color.R * 299f + background.Color.G * 587f + background.Color.B * 114f) / 1000f);
        btn.Foreground = brightness > 125 ? Brushes.Black : Brushes.White;
    }

    private void EraseBtn_Click(object sender, RoutedEventArgs e)
    {
        if(EditMode == InkCanvasEditingMode.EraseByPoint)
            EditMode = InkCanvasEditingMode.EraseByStroke;
        else EditMode = InkCanvasEditingMode.EraseByPoint;
    }

    private void BrushBtn_Click(object sender, RoutedEventArgs e)
    {
        EditMode = InkCanvasEditingMode.Ink;
    }

    private void BrushSizeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (BrushSizeCard.Visibility == Visibility.Visible)
            BrushSizeCard.Visibility = Visibility.Collapsed;
        else
        {
            HideAll();
            BrushSizeCard.Visibility = Visibility.Visible;
        }
    }

    private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        BrushSize = e.NewValue;
    }

    private void ColorBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ColorPickerCard.Visibility == Visibility.Visible)
            ColorPickerCard.Visibility = Visibility.Collapsed;
        else
        {
            HideAll();
            ColorPickerCard.Visibility = Visibility.Visible;
        }
    }

    private void MainWindow_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && InkCanvas.Strokes.Count > 0)
            InkCanvas.Strokes.RemoveAt(InkCanvas.Strokes.Count - 1);
    }

    private void HideAll()
    {
        BrushSizeCard.Visibility = Visibility.Collapsed;
        ColorPickerCard.Visibility = Visibility.Collapsed;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        if(_isUpdatingZoom)
        {
            _isUpdatingZoom = false;
            return;
        }

        _zoom = Math.Clamp(ZoomSlider.Value / 10d, 0.1, 10);
        var pos = new Point(MainGrid.ActualWidth / 2, MainGrid.ActualHeight / 2);
        UpdateZoom(pos);
    }

    private void UpdateZoom(Point pos)
    {
        _isUpdatingZoom = true;

        var matrix = InkCanvas.RenderTransform.Value;

        var targetWidth = InkCanvas.ActualWidth * _zoom * 10d;
        var targetHeight = InkCanvas.ActualHeight * _zoom * 10d;

        var topLeft = InkCanvas.TranslatePoint(new Point(0, 0), this);
        var bottomRight = InkCanvas.TranslatePoint(new Point(InkCanvas.ActualWidth, InkCanvas.ActualHeight), this);

        var renderWidth = bottomRight.X - topLeft.X;
        var renderHeight = bottomRight.Y - topLeft.Y;

        var scaleX = targetWidth / renderWidth;
        var scaleY = targetHeight / renderHeight;

        matrix.ScaleAtPrepend(scaleX, scaleY, pos.X, pos.Y);
        if (matrix.OffsetX > 0)
            matrix.Translate(-matrix.OffsetX, 0);
        if (matrix.OffsetY > 0)
            matrix.Translate(0, -matrix.OffsetY);
        if (matrix.OffsetX + targetWidth < MainGrid.ActualWidth)
            matrix.Translate(MainGrid.ActualWidth - (matrix.OffsetX + targetWidth), 0);
        if (matrix.OffsetY + targetHeight < MainGrid.ActualHeight)
            matrix.Translate(0, MainGrid.ActualHeight - (matrix.OffsetY + targetHeight));

        InkCanvas.RenderTransform = new MatrixTransform(matrix);

        ZoomSlider.Value = (int)(_zoom * 10);
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _isMiddleMousePressed = false;
    }

    private void TitleBarGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowStyle = WindowStyle.SingleBorderWindow;
        Close();
    }

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
    {
        var temp = WindowStyle;
        WindowStyle = WindowStyle.SingleBorderWindow;

        if (WindowState != WindowState.Maximized)
            WindowState = WindowState.Maximized;
        else WindowState = WindowState.Normal;

        WindowStyle = temp;
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        var temp = WindowStyle;
        WindowStyle = WindowStyle.SingleBorderWindow;

        if (WindowState != WindowState.Minimized)
            WindowState = WindowState.Minimized;
        else WindowState = WindowState.Normal;

        WindowStyle = temp;
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog();
        dialog.DefaultExt = "iwb";
        dialog.Filter = "Infinite Whiteboard (*.iwb)|*.iwb";
        if (!(dialog.ShowDialog() ?? false)) return;
        var stream = File.Create(dialog.FileName);
        var writter = new BinaryWriter(stream);

        writter.Write(InkCanvas.Strokes.Count);
        foreach(var stroke in InkCanvas.Strokes)
        {
            writter.Write(stroke.DrawingAttributes.Color.A);
            writter.Write(stroke.DrawingAttributes.Color.R);
            writter.Write(stroke.DrawingAttributes.Color.B);
            writter.Write(stroke.DrawingAttributes.Color.G);

            writter.Write(stroke.DrawingAttributes.Width);
            writter.Write(stroke.DrawingAttributes.Height);
            writter.Write(stroke.DrawingAttributes.FitToCurve);

            writter.Write(stroke.StylusPoints.Count);
            foreach(var point in stroke.StylusPoints)
            {
                writter.Write(point.X);
                writter.Write(point.Y);
            }
        }

        stream.Close();
    }

    private void LoadBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog();
        dialog.DefaultExt = "iwb";
        dialog.Filter = "Infinite Whiteboard (*.iwb)|*.iwb";
        if (!(dialog.ShowDialog() ?? false)) return;
        if (!File.Exists(dialog.FileName)) return;

        try
        {
            var stream = File.OpenRead(dialog.FileName);
            var reader = new BinaryReader(stream);

            InkCanvas.Strokes.Clear();

            int strokesCount = reader.ReadInt32();
            for (int i = 0; i < strokesCount; i++)
            {
                byte colorA = reader.ReadByte();
                byte colorR = reader.ReadByte();
                byte colorG = reader.ReadByte();
                byte colorB = reader.ReadByte();

                double width = reader.ReadDouble();
                double height = reader.ReadDouble();
                bool isFitToCurve = reader.ReadBoolean();

                var points = new StylusPointCollection();
                int pointsCount = reader.ReadInt32();
                for (int j = 0; j < pointsCount; j++)
                {
                    double pointX = reader.ReadDouble();
                    double pointY = reader.ReadDouble();

                    var point = new StylusPoint(pointX, pointY);
                    points.Add(point);
                }

                var stroke = new Stroke(points);
                stroke.DrawingAttributes.Color = Color.FromArgb(colorA, colorR, colorG, colorB);
                stroke.DrawingAttributes.Width = width;
                stroke.DrawingAttributes.Height = height;
                stroke.DrawingAttributes.FitToCurve = isFitToCurve;
                InkCanvas.Strokes.Add(stroke);
            }
        }
        catch { }
    }
}