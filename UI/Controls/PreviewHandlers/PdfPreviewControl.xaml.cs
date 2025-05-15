using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace ExplorerPro.UI.Controls.PreviewHandlers
{
    /// <summary>
    /// Interaction logic for PdfPreviewControl.xaml
    /// Provides a PDF viewer with navigation, zooming, panning and more
    /// </summary>
    public partial class PdfPreviewControl : UserControl
    {
        #region Fields

        // PDF Document
        private PdfiumViewer.PdfDocument _pdfDocument;
        private int _currentPage = 0;
        private double _zoomLevel = 1.0;
        private Window _parentWindow;

        // LRU Cache for rendered pages
        private Dictionary<CacheKey, BitmapSource> _pageCache;
        private const int MAX_CACHED_PAGES = 5;
        private List<CacheKey> _cacheKeys;

        // Mouse interaction tracking for dragging/panning
        private bool _isDragging = false;
        private Point _lastMousePosition;

        // Original window state before going fullscreen
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private ResizeMode _previousResizeMode;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor for PdfPreviewControl
        /// </summary>
        public PdfPreviewControl()
        {
            InitializeComponent();

            // Initialize cache
            _pageCache = new Dictionary<CacheKey, BitmapSource>(MAX_CACHED_PAGES);
            _cacheKeys = new List<CacheKey>(MAX_CACHED_PAGES);

            // Set up keyboard shortcuts
            this.KeyDown += PdfPreviewControl_KeyDown;
            this.Focusable = true;
            
            // Load empty image to avoid null reference exceptions
            pdfImageDisplay.Source = new BitmapImage();
        }

        /// <summary>
        /// Loads a PDF file from the specified path
        /// </summary>
        public async Task LoadPdfAsync(string filePath)
        {
            try
            {
                // Clear any existing document
                if (_pdfDocument != null)
                {
                    _pdfDocument.Dispose();
                    _pdfDocument = null;
                }

                // Clear the cache
                ClearCache();

                // Load the document
                await Task.Run(() =>
                {
                    _pdfDocument = PdfiumViewer.PdfDocument.Load(filePath);
                });

                // Reset view state
                _currentPage = 0;
                _zoomLevel = 1.0;

                // Update UI
                UpdatePage();
                UpdatePageInfo();

                // Find parent window for fullscreen functionality
                _parentWindow = Window.GetWindow(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Page Rendering

        /// <summary>
        /// Renders the current page with current zoom level
        /// </summary>
        private void UpdatePage()
        {
            if (_pdfDocument == null)
                return;

            try
            {
                // Check if we have this page and zoom level in the cache
                var cacheKey = new CacheKey(_currentPage, _zoomLevel);
                
                if (_pageCache.TryGetValue(cacheKey, out BitmapSource cachedImage))
                {
                    // Use cached image
                    pdfImageDisplay.Source = cachedImage;
                    UpdateCacheOrder(cacheKey);
                }
                else
                {
                    // Render fresh
                    var size = _pdfDocument.PageSizes[_currentPage];
                    float scale = (float)_zoomLevel;
                    int width = (int)(size.Width * scale);
                    int height = (int)(size.Height * scale);

                    // Ensure we have valid dimensions
                    width = Math.Max(1, width);
                    height = Math.Max(1, height);

                    // Render the page to a bitmap
                    using (var bitmap = new System.Drawing.Bitmap(width, height))
                    {
                        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                        {
                            // Clear with white background
                            graphics.Clear(System.Drawing.Color.White);

                            // Set high quality rendering
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                            // Render the page
                            _pdfDocument.Render(_currentPage, graphics, graphics.DpiX, graphics.DpiY, 
                                new System.Drawing.Rectangle(0, 0, width, height), 
                                PdfiumViewer.PdfRenderFlags.Annotations);
                        }

                        // Convert to WPF bitmap
                        var bitmapSource = ConvertBitmap(bitmap);
                        
                        // Add to cache
                        AddToCache(cacheKey, bitmapSource);
                        
                        // Display the image
                        pdfImageDisplay.Source = bitmapSource;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rendering PDF page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Updates page information display
        /// </summary>
        private void UpdatePageInfo()
        {
            if (_pdfDocument != null)
            {
                pageInfoLabel.Text = $"Page {_currentPage + 1} of {_pdfDocument.PageCount}";
                
                // Enable/disable navigation buttons based on current page
                prevPageButton.IsEnabled = (_currentPage > 0);
                nextPageButton.IsEnabled = (_currentPage < _pdfDocument.PageCount - 1);
            }
            else
            {
                pageInfoLabel.Text = "No PDF loaded";
                prevPageButton.IsEnabled = false;
                nextPageButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Converts a System.Drawing.Bitmap to a WPF BitmapSource
        /// </summary>
        private BitmapSource ConvertBitmap(System.Drawing.Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Key for caching rendered pages
        /// </summary>
        private class CacheKey : IEquatable<CacheKey>
        {
            public int PageNumber { get; }
            public double ZoomLevel { get; }

            public CacheKey(int pageNumber, double zoomLevel)
            {
                PageNumber = pageNumber;
                ZoomLevel = zoomLevel;
            }

            public bool Equals(CacheKey other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return PageNumber == other.PageNumber && Math.Abs(ZoomLevel - other.ZoomLevel) < 0.001;
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is CacheKey key && Equals(key);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PageNumber, ZoomLevel);
            }
        }

        /// <summary>
        /// Adds a rendered page to the cache, removing oldest if needed
        /// </summary>
        private void AddToCache(CacheKey key, BitmapSource bitmap)
        {
            // Check if already in cache
            if (_pageCache.ContainsKey(key))
            {
                _pageCache[key] = bitmap;
                UpdateCacheOrder(key);
                return;
            }

            // Ensure the cache doesn't exceed the maximum size
            if (_cacheKeys.Count >= MAX_CACHED_PAGES)
            {
                // Remove oldest
                var oldestKey = _cacheKeys[0];
                _pageCache.Remove(oldestKey);
                _cacheKeys.RemoveAt(0);
            }

            // Add new item
            _pageCache.Add(key, bitmap);
            _cacheKeys.Add(key);
        }

        /// <summary>
        /// Update cache order to make specified key most recently used
        /// </summary>
        private void UpdateCacheOrder(CacheKey key)
        {
            if (_cacheKeys.Contains(key))
            {
                _cacheKeys.Remove(key);
                _cacheKeys.Add(key);
            }
        }

        /// <summary>
        /// Clears the page cache
        /// </summary>
        private void ClearCache()
        {
            _pageCache.Clear();
            _cacheKeys.Clear();
        }

        #endregion

        #region Navigation and Zoom

        /// <summary>
        /// Moves to the previous page if available
        /// </summary>
        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfDocument != null && _currentPage > 0)
            {
                _currentPage--;
                UpdatePage();
                UpdatePageInfo();
                
                // Reset scroll position
                pdfScrollViewer.ScrollToTop();
            }
        }

        /// <summary>
        /// Moves to the next page if available
        /// </summary>
        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfDocument != null && _currentPage < _pdfDocument.PageCount - 1)
            {
                _currentPage++;
                UpdatePage();
                UpdatePageInfo();
                
                // Reset scroll position
                pdfScrollViewer.ScrollToTop();
            }
        }

        /// <summary>
        /// Increases zoom level by 20%
        /// </summary>
        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel *= 1.2;
            UpdatePage();
        }

        /// <summary>
        /// Decreases zoom level by 20%
        /// </summary>
        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel /= 1.2;
            if (_zoomLevel < 0.1)
                _zoomLevel = 0.1;
            UpdatePage();
        }

        /// <summary>
        /// Resets zoom level to 100%
        /// </summary>
        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = 1.0;
            UpdatePage();
        }

        /// <summary>
        /// Adjusts zoom level to fit the PDF page in the window
        /// </summary>
        private void FitToWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfDocument == null || pdfImageDisplay.Source == null)
                return;

            var source = pdfImageDisplay.Source as BitmapSource;
            if (source != null && source.PixelWidth > 0 && source.PixelHeight > 0)
            {
                // Calculate available space
                double availableWidth = pdfScrollViewer.ViewportWidth;
                double availableHeight = pdfScrollViewer.ViewportHeight;

                // Calculate scale factor to fit
                double scaleX = availableWidth / source.PixelWidth;
                double scaleY = availableHeight / source.PixelHeight;
                double scaleFactor = Math.Min(scaleX, scaleY);

                // Adjust zoom level
                _zoomLevel *= scaleFactor;
                
                // Update page with new zoom
                UpdatePage();
            }
        }

        /// <summary>
        /// Toggles fullscreen mode for the parent window
        /// </summary>
        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow == null)
                return;

            if (_parentWindow.WindowStyle != WindowStyle.None)
            {
                // Save current window state
                _previousWindowState = _parentWindow.WindowState;
                _previousWindowStyle = _parentWindow.WindowStyle;
                _previousResizeMode = _parentWindow.ResizeMode;

                // Go fullscreen
                _parentWindow.WindowState = WindowState.Maximized;
                _parentWindow.WindowStyle = WindowStyle.None;
                _parentWindow.ResizeMode = ResizeMode.NoResize;
                
                // Update button text
                fullscreenButton.Content = "❌ Exit Fullscreen";
            }
            else
            {
                // Restore previous state
                _parentWindow.WindowState = _previousWindowState;
                _parentWindow.WindowStyle = _previousWindowStyle;
                _parentWindow.ResizeMode = _previousResizeMode;
                
                // Update button text
                fullscreenButton.Content = "⛶ Fullscreen";
            }
        }

        #endregion

        #region Mouse and Keyboard Handling

        /// <summary>
        /// Handles key events for keyboard navigation
        /// </summary>
        private void PdfPreviewControl_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.PageUp:
                    PrevPageButton_Click(null, null);
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.PageDown:
                    NextPageButton_Click(null, null);
                    e.Handled = true;
                    break;

                case Key.Add:
                case Key.OemPlus:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        ZoomInButton_Click(null, null);
                        e.Handled = true;
                    }
                    break;

                case Key.Subtract:
                case Key.OemMinus:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        ZoomOutButton_Click(null, null);
                        e.Handled = true;
                    }
                    break;

                case Key.D0:
                case Key.NumPad0:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        ResetZoomButton_Click(null, null);
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    if (_parentWindow != null && _parentWindow.WindowStyle == WindowStyle.None)
                    {
                        FullscreenButton_Click(null, null);
                        e.Handled = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles mouse wheel for zooming and page navigation
        /// </summary>
        private void PdfScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ctrl + Mouse wheel for zooming
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    ZoomInButton_Click(null, null);
                else
                    ZoomOutButton_Click(null, null);
                
                e.Handled = true;
            }
            // Alt + Mouse wheel for page navigation
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (e.Delta > 0)
                    PrevPageButton_Click(null, null);
                else
                    NextPageButton_Click(null, null);
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// Initiates panning on mouse button press with Ctrl key
        /// </summary>
        private void PdfScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(pdfScrollViewer);
                Mouse.OverrideCursor = Cursors.Hand;
                pdfScrollViewer.CaptureMouse();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles panning during mouse movement
        /// </summary>
        private void PdfScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetPosition(pdfScrollViewer);
                var delta = currentPosition - _lastMousePosition;

                if (Math.Abs(delta.X) > 1 || Math.Abs(delta.Y) > 1)
                {
                    // Pan the viewer
                    pdfScrollViewer.ScrollToHorizontalOffset(pdfScrollViewer.HorizontalOffset - delta.X);
                    pdfScrollViewer.ScrollToVerticalOffset(pdfScrollViewer.VerticalOffset - delta.Y);
                    
                    // Update last position
                    _lastMousePosition = currentPosition;
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Ends panning on mouse button release
        /// </summary>
        private void PdfScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                Mouse.OverrideCursor = null;
                pdfScrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clean up resources when control is unloaded
        /// </summary>
        private void PdfPreviewControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_pdfDocument != null)
            {
                _pdfDocument.Dispose();
                _pdfDocument = null;
            }

            ClearCache();
        }

        #endregion
    }
}