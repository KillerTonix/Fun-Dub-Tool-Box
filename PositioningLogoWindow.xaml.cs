using Fun_Dub_Tool_Box.Utilities.Collections;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for PositioningLogoWindow.xaml
    /// </summary>
    public partial class PositioningLogoWindow : Window, INotifyPropertyChanged
    {
        private const double ScrollMargin = 10;
        private const double ScrollSpeed = 1;
        private const double VideoWidth = 1280;
        private const double VideoHeight = 720;

        private readonly LogoSettings _settings;
        private readonly string? _logoPath;

        private double _scaleFactorX = 1.0;
        private double _scaleFactorY = 1.0;
        private double _initialImageWidth;
        private double _initialImageHeight;
        private bool _initializing;

        private Point BasePoint = new(0.0, 0.0);
        private double DeltaX = 0.0;
        private double DeltaY = 0.0;
        private bool moving = false;
        private Point PositionInLabel;
        public double ScaleFactorX
        {
            get => _scaleFactorX;
            set
            {
                _scaleFactorX = value;
                RaisePropertyChanged(nameof(ScaleFactorX));
            }
        }

        public double ScaleFactorY
        {
            get => _scaleFactorY;
            set
            {
                _scaleFactorY = value;
                RaisePropertyChanged(nameof(ScaleFactorY));
            }
        }

        public double XPosition => BasePoint.X + DeltaX;
        public double YPosition => BasePoint.Y + DeltaY;



        public PositioningLogoWindow(LogoSettings settings, string? logoPath)
        {
            _settings = settings ?? new LogoSettings();
            _logoPath = logoPath;
            InitializeComponent();
            DataContext = this;
        }


        private void Feast_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is Image image)
            {
                image.CaptureMouse();
                moving = true;
                Point p = e.GetPosition(myCanvas);

                double scaledX = p.X / ScaleFactorX;
                double scaledY = p.Y / ScaleFactorY;

                PositionInLabel = new Point(scaledX - Canvas.GetLeft(image), scaledY - Canvas.GetTop(image));
            }
        }

        private void Feast_MouseMove(object sender, MouseEventArgs e)
        {
            if (!moving)
            {
                return;
            }
            Point p = e.GetPosition(myCanvas);
            double scaledX = p.X / ScaleFactorX;
            double scaledY = p.Y / ScaleFactorY;
            DeltaX = scaledX - BasePoint.X - PositionInLabel.X;
            DeltaY = scaledY - BasePoint.Y - PositionInLabel.Y;
            Canvas.SetLeft(LogoImage, XPosition);
            Canvas.SetTop(LogoImage, YPosition);
            ConstrainToViewport();
            UpdateManualFromCanvas();
            UpdatePanelAlignmentForCurrentPosition();
        }

        private void Feast_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is Image image)
            {
                image.ReleaseMouseCapture();
                BasePoint.X += DeltaX;
                BasePoint.Y += DeltaY;
                DeltaX = 0.0;
                DeltaY = 0.0;
                moving = false;
                ConstrainToViewport();
                UpdateManualFromCanvas();
                UpdatePanelAlignmentForCurrentPosition();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }


        private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateImageSize(e.NewValue);
        }
        private void UpdateImageSize(double currentValue)
        {
            if (LogoImage == null)
            {
                return;
            }

            if (_initialImageWidth <= 0 || _initialImageHeight <= 0)
            {
                return;
            }

            if (SizeSlider != null)
            {
                currentValue = Math.Clamp(currentValue, SizeSlider.Minimum, SizeSlider.Maximum);
            }

            LogoImage.Width = _initialImageWidth * currentValue / 100.0;
            LogoImage.Height = _initialImageHeight * currentValue / 100.0;
            _settings.ScalePercent = currentValue;

            if (!_initializing)
            {
                ConstrainToViewport();
                UpdateManualFromCanvas(_settings.UseManualPlacement);
            }
        }

        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateImageTransparency(e.NewValue);
        }

        private void UpdateImageTransparency(double currentValue)
        {
            if (LogoImage == null)
            {
                return;
            }

            if (TransparencySlider != null)
            {
                currentValue = Math.Clamp(currentValue, TransparencySlider.Minimum, TransparencySlider.Maximum);
            }

            var opacity = currentValue / 100.0;
            LogoImage.Opacity = opacity;
            _settings.Opacity = opacity;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _initializing = true;
            LoadLogoImage();

            var sizeValue = Math.Clamp(_settings.ScalePercent, SizeSlider.Minimum, SizeSlider.Maximum);
            SizeSlider.Value = sizeValue;
            UpdateImageSize(sizeValue);

            var transparencyValue = Math.Clamp(_settings.Opacity * 100, TransparencySlider.Minimum, TransparencySlider.Maximum);
            TransparencySlider.Value = transparencyValue;
            UpdateImageTransparency(transparencyValue);

            ApplyPlacement();
            _initializing = false;
        }

        private void LoadLogoImage()
        {
            if (!string.IsNullOrWhiteSpace(_logoPath) && File.Exists(_logoPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(_logoPath, UriKind.Absolute);
                    bitmap.EndInit();
                    LogoImage.Source = bitmap;
                    _initialImageWidth = bitmap.PixelWidth;
                    _initialImageHeight = bitmap.PixelHeight;
                }
                catch
                {
                    // Fallback to the embedded image if loading fails.
                }
            }

            if ((_initialImageWidth <= 0 || _initialImageHeight <= 0) && LogoImage.Source is BitmapSource source)
            {
                _initialImageWidth = source.PixelWidth;
                _initialImageHeight = source.PixelHeight;
            }

            if (_initialImageWidth <= 0)
            {
                _initialImageWidth = LogoImage.Width > 0 ? LogoImage.Width : 100;
            }

            if (_initialImageHeight <= 0)
            {
                _initialImageHeight = LogoImage.Height > 0 ? LogoImage.Height : 100;
            }

        }

        private void ApplyPlacement()
        {
            if (LogoImage == null)
            {
                return;
            }

            if (_settings.UseManualPlacement)
            {
                SetManualPosition(_settings.ManualX, _settings.ManualY);
            }
            else
            {
                ApplyAnchor(_settings.Anchor);
            }

            UpdatePanelAlignmentForCurrentPosition();
        }



        private void ApplyAnchor(LogoAnchor anchor)
        {
            double width = LogoImage.Width;
            double height = LogoImage.Height;

            double left = anchor switch
            {
                LogoAnchor.TopLeft => 0,
                LogoAnchor.TopRight => Math.Max(0, VideoWidth - width),
                LogoAnchor.BottomLeft => 0,
                LogoAnchor.BottomRight => Math.Max(0, VideoWidth - width),
                _ => 0
            };

            double top = anchor switch
            {
                LogoAnchor.TopLeft => 0,
                LogoAnchor.TopRight => 0,
                LogoAnchor.BottomLeft => Math.Max(0, VideoHeight - height),
                LogoAnchor.BottomRight => Math.Max(0, VideoHeight - height),
                _ => 0
            };

            Canvas.SetLeft(LogoImage, left);
            Canvas.SetTop(LogoImage, top);
            ConstrainToViewport();

            _settings.ApplyAnchor(anchor);
            UpdateManualFromCanvas(false);
            UpdatePanelAlignmentForCurrentPosition();
        }

        private void SetManualPosition(double x, double y)
        {
            Canvas.SetLeft(LogoImage, x);
            Canvas.SetTop(LogoImage, y);
            ConstrainToViewport();
            UpdateManualFromCanvas();
            UpdatePanelAlignmentForCurrentPosition();
        }

        private void ConstrainToViewport()
        {
            if (LogoImage == null)
            {
                return;
            }

            double left = Canvas.GetLeft(LogoImage);
            double top = Canvas.GetTop(LogoImage);

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            left = Math.Clamp(left, -100, VideoWidth - LogoImage.Width);
            top = Math.Clamp(top, -100, VideoHeight - LogoImage.Height);

            Canvas.SetLeft(LogoImage, left);
            Canvas.SetTop(LogoImage, top);
        }

        private void UpdateManualFromCanvas(bool manual = true)
        {
            double left = Canvas.GetLeft(LogoImage);
            double top = Canvas.GetTop(LogoImage);

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            if (manual)
            {
                _settings.ApplyManualPosition(left, top);
            }
            else
            {
                _settings.ManualX = left;
                _settings.ManualY = top;
                _settings.UseManualPlacement = false;
            }

            BasePoint = new Point(left, top);
        }

        private void UpdatePanelAlignmentForCurrentPosition()
        {
            if (LogoSettingsGrid == null || LogoImage == null)
            {
                return;
            }

            double top = Canvas.GetTop(LogoImage);
            if (double.IsNaN(top))
            {
                top = 0;
            }

            if (top > ActualHeight - 130 - LogoImage.Height)
            {
                LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Top;
            }
            else if (top < 130)
            {
                LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;
            }
        }



        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                TransparencySlider.Value += e.Delta > 1 ? 1 : -1;
            }
            else
            {
                SizeSlider.Value += e.Delta > 1 ? 1 : -1;
            }
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            var mousePos = e.GetPosition(scrollViewer);
            var scrollViewerHeight = scrollViewer.ViewportHeight;
            var scrollViewerWidth = scrollViewer.ViewportWidth;

            if (mousePos.Y >= scrollViewerHeight - ScrollMargin && scrollViewer.VerticalOffset < scrollViewer.ExtentHeight - scrollViewerHeight)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + ScrollSpeed);
            }
            else if (mousePos.Y <= ScrollMargin && scrollViewer.VerticalOffset > 0)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - ScrollSpeed);
            }

            if (mousePos.X >= scrollViewerWidth - ScrollMargin && scrollViewer.HorizontalOffset < scrollViewer.ExtentWidth - scrollViewerWidth)
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + ScrollSpeed);
            }
            else if (mousePos.X <= ScrollMargin && scrollViewer.HorizontalOffset > 0)
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - ScrollSpeed);
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {

            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    break;

                case Key.Add:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        TransparencySlider.Value += 1;
                    else
                        SizeSlider.Value += 1;
                    break;
                case Key.Subtract:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        TransparencySlider.Value -= 1;
                    else
                        SizeSlider.Value -= 1;
                    break;
                case Key.Up:
                    Canvas.SetTop(LogoImage, Canvas.GetTop(LogoImage) - (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 10 : 1));
                    ConstrainToViewport();
                    UpdateManualFromCanvas();
                    UpdatePanelAlignmentForCurrentPosition();
                    break;
                case Key.Down:
                    Canvas.SetTop(LogoImage, Canvas.GetTop(LogoImage) + (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 10 : 1));
                    ConstrainToViewport();
                    UpdateManualFromCanvas();
                    UpdatePanelAlignmentForCurrentPosition();
                    break;

                case Key.Left:
                    Canvas.SetLeft(LogoImage, Canvas.GetLeft(LogoImage) - (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 10 : 1));
                    ConstrainToViewport();
                    UpdateManualFromCanvas();
                    UpdatePanelAlignmentForCurrentPosition();
                    break;
                case Key.Right:
                    Canvas.SetLeft(LogoImage, Canvas.GetLeft(LogoImage) + (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 10 : 1));
                    ConstrainToViewport();
                    UpdateManualFromCanvas();
                    UpdatePanelAlignmentForCurrentPosition();
                    break;

                case Key.D1:
                case Key.NumPad1:
                    ApplyAnchor(LogoAnchor.BottomLeft);
                    break;
                case Key.D2:
                case Key.NumPad2:
                    SetManualPosition((VideoWidth - LogoImage.Width) / 2, Math.Max(0, VideoHeight - LogoImage.Height));
                    break;
                case Key.D3:
                case Key.NumPad3:
                    // Bottom-right
                    ApplyAnchor(LogoAnchor.BottomRight);
                    break;
                case Key.D4:
                case Key.NumPad4:
                    // Middle-left
                    SetManualPosition(0, (VideoHeight - LogoImage.Height) / 2);
                    break;
                case Key.D5:
                case Key.NumPad5:
                    SetManualPosition((VideoWidth - LogoImage.Width) / 2, (VideoHeight - LogoImage.Height) / 2);
                    break;
                case Key.D6:
                case Key.NumPad6:
                    SetManualPosition(Math.Max(0, VideoWidth - LogoImage.Width), (VideoHeight - LogoImage.Height) / 2);
                    break;
                case Key.D7:
                case Key.NumPad7:
                    ApplyAnchor(LogoAnchor.TopLeft);
                    break;
                case Key.D8:
                case Key.NumPad8:
                    // Top-center
                    SetManualPosition((VideoWidth - LogoImage.Width) / 2, 0);
                    break;
                case Key.D9:
                case Key.NumPad9:
                    // Top-right
                    ApplyAnchor(LogoAnchor.TopRight);
                    break;
            }
        }


        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyAnchor(LogoAnchor.TopLeft);
            SizeSlider.Value = 100;
            TransparencySlider.Value = 100;
        }
    }
}
