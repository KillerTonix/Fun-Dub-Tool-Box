using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for PositioningLogoWindow.xaml
    /// </summary>
    public partial class PositioningLogoWindow : Window, INotifyPropertyChanged
    {
        private double initialImageWidth;
        private double initialImageHeight;
        private const double ScrollMargin = 10; // Margin from edge to start scrolling
        private const double ScrollSpeed = 1;  // Speed of scrolling
        private double _scaleFactorX = 1.0;
        private double _scaleFactorY = 1.0;
        public double ScaleFactorX
        {
            get { return _scaleFactorX; }
            set
            {
                _scaleFactorX = value;
                RaisePropertyChanged(nameof(ScaleFactorX));
            }
        }

        public double ScaleFactorY
        {
            get { return _scaleFactorY; }
            set
            {
                _scaleFactorY = value;
                RaisePropertyChanged(nameof(ScaleFactorY));
            }
        }


        public PositioningLogoWindow()
        {
            InitializeComponent();
            this.DataContext = this;


        }


        private Point BasePoint = new(0.0, 0.0);
        private double DeltaX = 0.0;
        private double DeltaY = 0.0;
        private bool moving = false;
        private Point PositionInLabel;

        public double XPosition
        {
            get { return BasePoint.X + DeltaX; }
        }

        public double YPosition
        {
            get { return BasePoint.Y + DeltaY; }
        }


        private void Feast_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is Image l)
            {
                l.CaptureMouse();
                moving = true;
                Point p = e.GetPosition(myCanvas);

                // Apply the scaling factors to the initial mouse position
                double scaledX = p.X / ScaleFactorX;
                double scaledY = p.Y / ScaleFactorY;

                PositionInLabel = new Point(scaledX - Canvas.GetLeft(l), scaledY - Canvas.GetTop(l));
            }
        }

        private void Feast_MouseMove(object sender, MouseEventArgs e)
        {
            double videoWidth = 1280;
            double videoHeight = 720;
            if (moving)
            {

                Point p = e.GetPosition(myCanvas);

                // Apply the scaling factors to the mouse position
                double scaledX = p.X / ScaleFactorX;
                double scaledY = p.Y / ScaleFactorY;

                DeltaX = scaledX - BasePoint.X - PositionInLabel.X;
                DeltaY = scaledY - BasePoint.Y - PositionInLabel.Y;

                RaisePropertyChanged(nameof(XPosition));
                RaisePropertyChanged(nameof(YPosition));

                Canvas.SetLeft(LogoImage, XPosition);
                Canvas.SetTop(LogoImage, YPosition);

                if (YPosition < -100)
                {
                    Canvas.SetTop(LogoImage, -100);
                }
                else if (YPosition > videoHeight - LogoImage.Height)
                {
                    Canvas.SetTop(LogoImage, videoHeight - LogoImage.Height);
                }

                if (XPosition < -100)
                {
                    Canvas.SetLeft(LogoImage, -100);
                }
                else if (XPosition > videoWidth - LogoImage.Width)
                {
                    Canvas.SetLeft(LogoImage, videoWidth - LogoImage.Width);
                }

                if (YPosition > this.ActualHeight - 130 - LogoImage.Height)
                {
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Top;
                }

                if (YPosition < 130)
                {
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;
                }
            }
        }

        private void Feast_MouseUp(object sender, MouseButtonEventArgs e)
        {
            double videoWidth = 1280;
            double videoHeight = 720;
            if (e.Source is Image l)
            {
                l.ReleaseMouseCapture();
                BasePoint.X += DeltaX;
                BasePoint.Y += DeltaY;
                DeltaX = 0.0;
                DeltaY = 0.0;
                moving = false;

                Canvas.SetLeft(LogoImage, XPosition);
                Canvas.SetTop(LogoImage, YPosition);

                if (YPosition < -100)
                {
                    Canvas.SetTop(LogoImage, -100);
                }
                else if (YPosition > videoHeight - LogoImage.Height)
                {
                    Canvas.SetTop(LogoImage, videoHeight - LogoImage.Height);
                }

                if (XPosition < -100)
                {
                    Canvas.SetLeft(LogoImage, -100);
                }
                else if (XPosition > videoWidth - LogoImage.Width)
                {
                    Canvas.SetLeft(LogoImage, videoWidth - LogoImage.Width);
                }
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
            if (LogoImage != null)
            {
                if (currentValue < 1) currentValue = 1;
                LogoImage.Width = (currentValue * 2 * initialImageWidth) / 100;
                LogoImage.Height = (currentValue * 2 * initialImageHeight) / 100;
            }
        }

        private void LogoImage_Initialized(object sender, EventArgs e)
        {
            string path = @"F:\My C# Projects\Fun Dub Tool Box\Materials\Images\Logo3.png";
            var (width, height) = GetImageDimensions(path);
            LogoImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
            initialImageWidth = width;
            initialImageHeight = height;
        }
        public static (int width, int height) GetImageDimensions(string imagePath)
        {
            // Create a BitmapImage and set the UriSource to the image file
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load the image immediately
            bitmap.EndInit();

            // Get the width and height
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            return (width, height);
        }

        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateImageTransparency(e.NewValue);

        }

        private void UpdateImageTransparency(double currentValue)
        {
            if (LogoImage != null)
            {
                LogoImage.Opacity = currentValue / 10;
            }
        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }


        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Control key is pressed, change Slider2 value
                TransparencySlider.Value += e.Delta > 1 ? 1 : -1;
            }
            else
            {
                // Control key is not pressed, change Slider1 value
                SizeSlider.Value += e.Delta > 1 ? 1 : -1;
            }
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            var mousePos = e.GetPosition(scrollViewer);
            var scrollViewerHeight = scrollViewer.ViewportHeight;
            var scrollViewerWidth = scrollViewer.ViewportWidth;

            // Auto-scroll vertically
            if (mousePos.Y >= scrollViewerHeight - ScrollMargin && scrollViewer.VerticalOffset < scrollViewer.ExtentHeight - scrollViewerHeight)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + ScrollSpeed);
            }
            else if (mousePos.Y <= ScrollMargin && scrollViewer.VerticalOffset > 0)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - ScrollSpeed);
            }

            // Auto-scroll horizontally
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

            double videoWidth = 1280;
            double videoHeight = 720;

            switch (e.Key)
            {
                case Key.Escape:
                    this.Close();
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
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        Canvas.SetTop(LogoImage, Canvas.GetTop(LogoImage) - 10);
                    else
                        Canvas.SetTop(LogoImage, Canvas.GetTop(LogoImage) - 1);
                    break;
                case Key.Down:
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        Canvas.SetTop(LogoImage, Canvas.GetTop(LogoImage) + 10);
                    else
                        Canvas.SetTop(LogoImage, Canvas.GetTop(LogoImage) + 1);
                    break;

                case Key.Left:
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        Canvas.SetLeft(LogoImage, Canvas.GetLeft(LogoImage) - 10);
                    else
                        Canvas.SetLeft(LogoImage, Canvas.GetLeft(LogoImage) - 1);
                    break;
                case Key.Right:
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        Canvas.SetLeft(LogoImage, Canvas.GetLeft(LogoImage) + 10);
                    else
                        Canvas.SetLeft(LogoImage, Canvas.GetLeft(LogoImage) + 1);
                    break;

                case Key.D1:
                case Key.NumPad1:
                    // Bottom-left
                    Canvas.SetLeft(LogoImage, 0);
                    Canvas.SetTop(LogoImage, videoHeight - LogoImage.Height);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Top;

                    break;
                case Key.D2:
                case Key.NumPad2:
                    // Bottom-center
                    Canvas.SetLeft(LogoImage, (videoWidth - LogoImage.Width) / 2);
                    Canvas.SetTop(LogoImage, videoHeight - LogoImage.Height);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Top;

                    break;
                case Key.D3:
                case Key.NumPad3:
                    // Bottom-right
                    Canvas.SetLeft(LogoImage, videoWidth - LogoImage.Width);
                    Canvas.SetTop(LogoImage, videoHeight - LogoImage.Height);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Top;

                    break;
                case Key.D4:
                case Key.NumPad4:
                    // Middle-left
                    Canvas.SetLeft(LogoImage, 0);
                    Canvas.SetTop(LogoImage, (videoHeight - LogoImage.Height) / 2);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;

                    break;
                case Key.D5:
                case Key.NumPad5:
                    // Center
                    Canvas.SetLeft(LogoImage, (videoWidth - LogoImage.Width) / 2);
                    Canvas.SetTop(LogoImage, (videoHeight - LogoImage.Height) / 2);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;

                    break;
                case Key.D6:
                case Key.NumPad6:
                    // Middle-right
                    Canvas.SetLeft(LogoImage, videoWidth - LogoImage.Width);
                    Canvas.SetTop(LogoImage, (videoHeight - LogoImage.Height) / 2);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;

                    break;
                case Key.D7:
                case Key.NumPad7:
                    // Top-left
                    Canvas.SetLeft(LogoImage, 0);
                    Canvas.SetTop(LogoImage, 0);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;

                    break;
                case Key.D8:
                case Key.NumPad8:
                    // Top-center
                    Canvas.SetLeft(LogoImage, (videoWidth - LogoImage.Width) / 2);
                    Canvas.SetTop(LogoImage, 0);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
                case Key.D9:
                case Key.NumPad9:
                    // Top-right
                    Canvas.SetLeft(LogoImage, videoWidth - LogoImage.Width);
                    Canvas.SetTop(LogoImage, 0);
                    LogoSettingsGrid.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
            }
        }


        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Canvas.SetLeft(LogoImage, 0);
            Canvas.SetTop(LogoImage, 0);
            SizeSlider.Value = 100;
            TransparencySlider.Value = 10;
        }
    }
}
