using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InfiniteWhiteboard
{
    public partial class MainWindow : Window
    {
        private static readonly SolidColorBrush BRUSH_COLOR_SELECTED = new SolidColorBrush(Color.FromRgb(0x67, 0x3A, 0xB7));
        private static readonly SolidColorBrush BRUSH_COLOR_DEFAULT = new SolidColorBrush(Color.FromRgb(0xAE, 0xEA, 0x00));

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
                BrushSizeText.Text = $"Brush size: {value.ToString("0.0")}";
            }
        }

        private InkCanvasEditingMode EditMode
        {
            get => InkCanvas.EditingMode;
            set
            {
                InkCanvas.EditingMode = value;
                BrushBtn.Background = BrushBtn.BorderBrush = BRUSH_COLOR_DEFAULT;
                EraseBtn.Background = EraseBtn.BorderBrush = BRUSH_COLOR_DEFAULT;
                if (value == InkCanvasEditingMode.Ink)
                    SetButtonColors(BrushBtn, BRUSH_COLOR_SELECTED);
                else if (value == InkCanvasEditingMode.EraseByPoint || value == InkCanvasEditingMode.EraseByStroke)
                    SetButtonColors(EraseBtn, BRUSH_COLOR_SELECTED);
            }
        }

        private double _zoom = 1;

        public MainWindow()
        {
            InitializeComponent();

            ColorPicker.Color = BrushColor = Colors.Black;
            BrushSizeSlider.Value = BrushSize = 5;
            EditMode = InkCanvasEditingMode.Ink;

            PreviewMouseUp += OnMouseEvent;
            PreviewMouseDown += OnMouseEvent;
            PreviewMouseMove += OnMouseMoveEvent;
            MouseWheel += OnMouseWheelEvent;

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
    }
}
