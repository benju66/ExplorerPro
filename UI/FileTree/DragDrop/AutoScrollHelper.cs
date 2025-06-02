// UI/FileTree/DragDrop/AutoScrollHelper.cs - Fixed version with proper cleanup
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using ExplorerPro.UI.FileTree.Utilities;

namespace ExplorerPro.UI.FileTree.DragDrop
{
    /// <summary>
    /// Provides automatic scrolling when dragging near edges of a scrollable control
    /// Fixed version with proper timer management and disposal
    /// </summary>
    public class AutoScrollHelper : IDisposable
    {
        #region Constants
        
        private const double EDGE_THRESHOLD = 30.0; // Pixels from edge to trigger scroll
        private const double SCROLL_SPEED_MIN = 1.0; // Minimum scroll speed
        private const double SCROLL_SPEED_MAX = 20.0; // Maximum scroll speed
        private const int TIMER_INTERVAL = 20; // Milliseconds between scroll updates
        
        #endregion
        
        #region Fields
        
        private readonly ScrollViewer _scrollViewer;
        private DispatcherTimer _scrollTimer;
        private Point _currentPosition;
        private bool _isActive;
        private double _verticalScrollSpeed;
        private double _horizontalScrollSpeed;
        
        // Store event handler for cleanup
        private EventHandler _scrollTimerTickHandler;
        
        #endregion
        
        #region Constructor
        
        public AutoScrollHelper(ScrollViewer scrollViewer)
        {
            _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
            
            // Create event handler
            _scrollTimerTickHandler = OnScrollTimerTick;
            
            // Initialize timer
            _scrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL)
            };
            _scrollTimer.Tick += _scrollTimerTickHandler;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Starts auto-scrolling if needed based on position
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AutoScrollHelper));
                
            if (!_isActive && _scrollTimer != null)
            {
                _isActive = true;
                _scrollTimer.Start();
            }
        }
        
        /// <summary>
        /// Stops auto-scrolling
        /// </summary>
        public void Stop()
        {
            if (_disposed) return;
            
            if (_isActive)
            {
                _isActive = false;
                _scrollTimer?.Stop();
                _verticalScrollSpeed = 0;
                _horizontalScrollSpeed = 0;
            }
        }
        
        /// <summary>
        /// Updates the current drag position
        /// </summary>
        public void UpdatePosition(Point position)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AutoScrollHelper));
                
            _currentPosition = position;
            CalculateScrollSpeeds();
        }
        
        /// <summary>
        /// Checks if position is in a scroll zone
        /// </summary>
        public bool IsInScrollZone(Point position)
        {
            if (_disposed) return false;
            
            if (_scrollViewer == null) return false;
            
            var bounds = new Rect(0, 0, _scrollViewer.ActualWidth, _scrollViewer.ActualHeight);
            
            if (!bounds.Contains(position))
                return false;
            
            return position.Y < EDGE_THRESHOLD ||
                   position.Y > bounds.Height - EDGE_THRESHOLD ||
                   position.X < EDGE_THRESHOLD ||
                   position.X > bounds.Width - EDGE_THRESHOLD;
        }
        
        #endregion
        
        #region Private Methods
        
        private void CalculateScrollSpeeds()
        {
            if (_disposed || _scrollViewer == null) return;
            
            var bounds = new Rect(0, 0, _scrollViewer.ActualWidth, _scrollViewer.ActualHeight);
            
            // Reset speeds
            _verticalScrollSpeed = 0;
            _horizontalScrollSpeed = 0;
            
            if (!bounds.Contains(_currentPosition))
                return;
            
            // Vertical scrolling
            if (_currentPosition.Y < EDGE_THRESHOLD)
            {
                // Scroll up
                var factor = 1.0 - (_currentPosition.Y / EDGE_THRESHOLD);
                _verticalScrollSpeed = -Lerp(SCROLL_SPEED_MIN, SCROLL_SPEED_MAX, factor);
            }
            else if (_currentPosition.Y > bounds.Height - EDGE_THRESHOLD)
            {
                // Scroll down
                var factor = 1.0 - ((bounds.Height - _currentPosition.Y) / EDGE_THRESHOLD);
                _verticalScrollSpeed = Lerp(SCROLL_SPEED_MIN, SCROLL_SPEED_MAX, factor);
            }
            
            // Horizontal scrolling
            if (_currentPosition.X < EDGE_THRESHOLD)
            {
                // Scroll left
                var factor = 1.0 - (_currentPosition.X / EDGE_THRESHOLD);
                _horizontalScrollSpeed = -Lerp(SCROLL_SPEED_MIN, SCROLL_SPEED_MAX, factor);
            }
            else if (_currentPosition.X > bounds.Width - EDGE_THRESHOLD)
            {
                // Scroll right
                var factor = 1.0 - ((bounds.Width - _currentPosition.X) / EDGE_THRESHOLD);
                _horizontalScrollSpeed = Lerp(SCROLL_SPEED_MIN, SCROLL_SPEED_MAX, factor);
            }
        }
        
        private void OnScrollTimerTick(object sender, EventArgs e)
        {
            if (_disposed || !_isActive || _scrollViewer == null) return;
            
            bool scrolled = false;
            
            // Apply vertical scrolling
            if (Math.Abs(_verticalScrollSpeed) > 0.01)
            {
                var newOffset = _scrollViewer.VerticalOffset + _verticalScrollSpeed;
                newOffset = Math.Max(0, Math.Min(newOffset, _scrollViewer.ScrollableHeight));
                _scrollViewer.ScrollToVerticalOffset(newOffset);
                scrolled = true;
            }
            
            // Apply horizontal scrolling
            if (Math.Abs(_horizontalScrollSpeed) > 0.01)
            {
                var newOffset = _scrollViewer.HorizontalOffset + _horizontalScrollSpeed;
                newOffset = Math.Max(0, Math.Min(newOffset, _scrollViewer.ScrollableWidth));
                _scrollViewer.ScrollToHorizontalOffset(newOffset);
                scrolled = true;
            }
            
            // If we scrolled, we might need to update hover effects
            if (scrolled)
            {
                OnAutoScrolled();
            }
        }
        
        /// <summary>
        /// Called when auto-scroll occurs - can be used to update hover effects
        /// </summary>
        protected virtual void OnAutoScrolled()
        {
            // Derived classes can override to update UI during scroll
        }
        
        /// <summary>
        /// Linear interpolation between two values
        /// </summary>
        private static double Lerp(double a, double b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return a + (b - a) * t;
        }
        
        #endregion
        
        #region Static Helper Methods
        
        /// <summary>
        /// Finds the ScrollViewer in a control's visual tree
        /// </summary>
        public static ScrollViewer FindScrollViewer(DependencyObject element)
        {
            return VisualTreeHelperEx.FindScrollViewer(element);
        }
        
        #endregion
        
        #region IDisposable
        
        private bool _disposed;
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Stop timer first
                    Stop();
                    
                    // Cleanup timer
                    if (_scrollTimer != null)
                    {
                        // Unsubscribe event handler
                        if (_scrollTimerTickHandler != null)
                        {
                            _scrollTimer.Tick -= _scrollTimerTickHandler;
                        }
                        
                        // Clear timer reference
                        _scrollTimer = null;
                    }
                    
                    // Clear event handler reference
                    _scrollTimerTickHandler = null;
                }
                
                _disposed = true;
            }
        }
        
        ~AutoScrollHelper()
        {
            Dispose(false);
        }
        
        #endregion
    }
}