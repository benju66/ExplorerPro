using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExplorerPro.UI.Controls.PreviewHandlers
{
    /// <summary>
    /// Interaction logic for ImagePreviewControl.xaml
    /// </summary>
    public partial class ImagePreviewControl : UserControl
    {
        #region Fields

        // Original image source
        private BitmapSource _originalImage;
        
        // Cached scaled images at different zoom levels
        private Dictionary<(double ZoomLevel, int Rotation), BitmapSource> _scaledImageCache = new Dictionary<(double ZoomLevel, int Rotation), BitmapSource>();
        
        // Maximum number of cached zoom levels
        private const int MaxCachedZooms = 5;
        
        // Current zoom and rotation 
        private double _zoomLevel = 1.0;
        private int _currentRotation = 0; // 0, 90, 180, 270 degrees
        
        // Dragging support
        private bool _isDragging;
        private Point _lastMousePosition;
        
        // The parent Window for fullscreen mode
        private Window _parentWindow;
        private WindowState _previousWindowState;
        private bool _isFullscreen;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public ImagePreviewControl()
        {
            InitializeComponent();
            
            // Register event handlers for dragging functionality
            ScrollViewer.MouseLeftButtonDown += ScrollViewer_MouseLeftButtonDown;
            ScrollViewer.MouseLeftButtonUp += ScrollViewer_MouseLeftButtonUp;
            ScrollViewer.MouseMove += ScrollViewer_MouseMove;
            
            // Register mousewheel for zooming
            PreviewMouseWheel += ImagePreviewControl_PreviewMouseWheel;
            
            // Initialize cache
            _scaledImageCache = new Dictionary<(double ZoomLevel, int Rotation), BitmapSource>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads an image from the specified file path
        /// </summary>
        /// <param name="filePath">The path to the image file</param>
        /// <returns>True if the image was loaded successfully, false otherwise</returns>
        public bool LoadImage(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Clear the cache
                _scaledImageCache.Clear();
                
                // Reset zoom and rotation
                _zoomLevel = 1.0;
                _currentRotation = 0;
                
                // Load the image
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Load into memory immediately
                bitmapImage.UriSource = new Uri(filePath);
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Make it thread-safe
                
                _originalImage = bitmapImage;
                
                // Update the display
                UpdateImage();
                
                // Auto-fit the image on initial load
                FitToWindow();
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Resets all image transformations and cache
        /// </summary>
        public void Reset()
        {
            _scaledImageCache.Clear();
            _zoomLevel = 1.0;
            _currentRotation = 0;
            UpdateImage();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates the displayed image based on current zoom and rotation
        /// </summary>
        private void UpdateImage()
        {
            if (_originalImage == null)
                return;

            // Create a cache key based on zoom level and rotation
            var cacheKey = (_zoomLevel, _currentRotation);
            
            // Check if we already have this zoom/rotation cached
            if (_scaledImageCache.TryGetValue(cacheKey, out BitmapSource cachedImage))
            {
                PreviewImage.Source = cachedImage;
                return;
            }
            
            // Need to create a new transformed image
            TransformedBitmap transformedBitmap = new TransformedBitmap();
            transformedBitmap.BeginInit();
            transformedBitmap.Source = _originalImage;
            
            // Apply rotation if needed
            if (_currentRotation != 0)
            {
                transformedBitmap.Transform = new RotateTransform(_currentRotation);
            }
            transformedBitmap.EndInit();
            transformedBitmap.Freeze();
            
            // Create a scaled version if zoom isn't 1.0
            BitmapSource finalImage;
            if (Math.Abs(_zoomLevel - 1.0) > 0.01)
            {
                int newWidth = (int)(transformedBitmap.PixelWidth * _zoomLevel);
                int newHeight = (int)(transformedBitmap.PixelHeight * _zoomLevel);
                
                // Ensure minimum dimensions
                newWidth = Math.Max(1, newWidth);
                newHeight = Math.Max(1, newHeight);
                
                // Create the scaled bitmap
                finalImage = new TransformedBitmap(
                    transformedBitmap,
                    new ScaleTransform(_zoomLevel, _zoomLevel)
                );
                finalImage.Freeze();
            }
            else
            {
                finalImage = transformedBitmap;
            }
            
            // Store in cache
            if (_scaledImageCache.Count >= MaxCachedZooms)
            {
                // Remove a random entry if we're at capacity (simple approach)
                // In a more sophisticated implementation, you might use LRU logic
                var enumerator = _scaledImageCache.GetEnumerator();
                enumerator.MoveNext();
                _scaledImageCache.Remove(enumerator.Current.Key);
            }
            
            _scaledImageCache[cacheKey] = finalImage;
            
            // Update the image control
            PreviewImage.Source = finalImage;
        }

        /// <summary>
        /// Fits the image to the available viewport size
        /// </summary>
        private void FitToWindow()
        {
            if (_originalImage == null || ScrollViewer.ActualWidth <= 0 || ScrollViewer.ActualHeight <= 0)
                return;
            
            // Calculate the scaling factors for width and height
            double scaleX = ScrollViewer.ViewportWidth / _originalImage.PixelWidth;
            double scaleY = ScrollViewer.ViewportHeight / _originalImage.PixelHeight;
            
            // Use the smaller scale to ensure the image fits completely
            _zoomLevel = Math.Min(scaleX, scaleY);
            
            // Apply a small margin
            _zoomLevel *= 0.95;
            
            // Update the image
            UpdateImage();
        }

        /// <summary>
        /// Toggles fullscreen mode
        /// </summary>
        private void ToggleFullscreen()
        {
            if (_parentWindow == null)
            {
                _parentWindow = Window.GetWindow(this);
                if (_parentWindow == null)
                    return;
            }
            
            if (!_isFullscreen)
            {
                // Store current state and go fullscreen
                _previousWindowState = _parentWindow.WindowState;
                _parentWindow.WindowState = WindowState.Maximized;
                _parentWindow.WindowStyle = WindowStyle.None;
                _parentWindow.ResizeMode = ResizeMode.NoResize;
                FullscreenButton.Content = "❌";
                FullscreenButton.ToolTip = "Exit Fullscreen";
                _isFullscreen = true;
            }
            else
            {
                // Restore previous state
                _parentWindow.WindowState = _previousWindowState;
                _parentWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                _parentWindow.ResizeMode = ResizeMode.CanResize;
                FullscreenButton.Content = "⛶";
                FullscreenButton.ToolTip = "Toggle Fullscreen";
                _isFullscreen = false;
            }
        }

        #endregion

        #region Event Handlers

        private void RotateLeftButton_Click(object sender, RoutedEventArgs e)
        {
            _currentRotation = (_currentRotation - 90) % 360;
            if (_currentRotation < 0)
                _currentRotation += 360;
            UpdateImage();
        }

        private void RotateRightButton_Click(object sender, RoutedEventArgs e)
        {
            _currentRotation = (_currentRotation + 90) % 360;
            UpdateImage();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel *= 1.2;
            UpdateImage();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel /= 1.2;
            
            // Prevent zoom level from getting too small
            if (_zoomLevel < 0.05)
                _zoomLevel = 0.05;
                
            UpdateImage();
        }

        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = 1.0;
            UpdateImage();
        }

        private void FitToWindowButton_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ImagePreviewControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Check if Ctrl key is pressed for zooming
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                
                if (e.Delta > 0)
                {
                    // Zoom in
                    _zoomLevel *= 1.1;
                }
                else
                {
                    // Zoom out
                    _zoomLevel /= 1.1;
                    
                    // Prevent zoom level from getting too small
                    if (_zoomLevel < 0.05)
                        _zoomLevel = 0.05;
                }
                
                UpdateImage();
            }
        }

        private void ScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if Ctrl is pressed for dragging
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(ScrollViewer);
                ScrollViewer.Cursor = Cursors.Hand;
                e.Handled = true;
                ScrollViewer.CaptureMouse();
            }
        }

        private void ScrollViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ScrollViewer.Cursor = Cursors.Arrow;
                ScrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPosition = e.GetPosition(ScrollViewer);
                Vector offset = _lastMousePosition - currentPosition;
                
                // Apply the scrolling offset
                ScrollViewer.ScrollToHorizontalOffset(ScrollViewer.HorizontalOffset + offset.X);
                ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset + offset.Y);
                
                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }

        #endregion
    }
}