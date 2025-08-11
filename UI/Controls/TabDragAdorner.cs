using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Tab drag adorner with enhanced visual feedback and performance optimization
    /// </summary>
    public class TabDragAdorner : Adorner, IDisposable
    {
        #region Fields

        private readonly ILogger<TabDragAdorner>? _logger;
        private readonly ContentPresenter _contentPresenter;
        private readonly Border _dragPreview;
        private readonly DropShadowEffect _shadowEffect;
        
        private double _leftOffset;
        private double _topOffset;
        private bool _isDisposed = false;
        private Point _lastPosition = new Point(-1, -1);
        
        // Performance optimization - throttle updates
        private DateTime _lastUpdate = DateTime.MinValue;
        private const int UPDATE_THROTTLE_MS = 16; // ~60 FPS
        
        // Visual feedback states
        private DragState _currentState = DragState.Dragging;
        private readonly SolidColorBrush _validDropBrush = new SolidColorBrush(Colors.LightGreen) { Opacity = 0.3 };
        private readonly SolidColorBrush _invalidDropBrush = new SolidColorBrush(Colors.LightCoral) { Opacity = 0.3 };
        private readonly SolidColorBrush _detachBrush = new SolidColorBrush(Colors.LightBlue) { Opacity = 0.3 };

        #endregion

        #region Enums

        public enum DragState
        {
            Dragging,
            ValidDrop,
            InvalidDrop,
            DetachZone
        }

        #endregion

        #region Properties

        public DragState CurrentState
        {
            get => _currentState;
            set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    UpdateVisualState();
                }
            }
        }

        public bool IsDetachable { get; set; } = true;

        #endregion

        #region Constructor

        public TabDragAdorner(UIElement adornedElement, TabItem draggedTab, ILogger<TabDragAdorner>? logger = null) 
            : base(adornedElement)
        {
            _logger = logger;
            
            try
            {
                // Create shadow effect
                _shadowEffect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 10,
                    ShadowDepth = 5,
                    Opacity = 0.5
                };

                // Create drag preview with enhanced styling
                _dragPreview = new Border
                {
                    Background = new SolidColorBrush(Colors.White) { Opacity = 0.9 },
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    Effect = _shadowEffect,
                    MinWidth = 100,
                    MaxWidth = 200
                };

                // Create content presenter with tab header
                _contentPresenter = new ContentPresenter
                {
                    Content = draggedTab.Header,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                _dragPreview.Child = _contentPresenter;
                
                IsHitTestVisible = false;
                
                _logger?.LogDebug("Enhanced drag adorner created successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating enhanced drag adorner");
                throw;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the adorner position with performance throttling
        /// </summary>
        public void UpdatePosition(Point position)
        {
            if (_isDisposed) return;

            try
            {
                // Throttle updates for performance
                var now = DateTime.UtcNow;
                if ((now - _lastUpdate).TotalMilliseconds < UPDATE_THROTTLE_MS &&
                    Math.Abs(position.X - _lastPosition.X) < 2 &&
                    Math.Abs(position.Y - _lastPosition.Y) < 2)
                {
                    return;
                }

                _lastUpdate = now;
                _lastPosition = position;

                _leftOffset = position.X - 20;
                _topOffset = position.Y - 10;

                // Update visual feedback based on position
                UpdateDragState(position);
                
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating adorner position");
            }
        }

        /// <summary>
        /// Sets the drag state for visual feedback
        /// </summary>
        public void SetDragState(DragState state)
        {
            CurrentState = state;
        }

        /// <summary>
        /// Creates a snapshot image of the dragged tab for better performance
        /// </summary>
        public void CreateTabSnapshot(TabItem tabItem)
        {
            if (_isDisposed) return;

            try
            {
                var renderTargetBitmap = new RenderTargetBitmap(
                    (int)tabItem.ActualWidth,
                    (int)tabItem.ActualHeight,
                    96, 96,
                    PixelFormats.Pbgra32);

                renderTargetBitmap.Render(tabItem);

                var image = new Image
                {
                    Source = renderTargetBitmap,
                    Width = tabItem.ActualWidth,
                    Height = tabItem.ActualHeight
                };

                _dragPreview.Child = image;
                
                _logger?.LogDebug("Tab snapshot created for enhanced drag feedback");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to create tab snapshot, using fallback");
                // Fallback to content presenter
            }
        }

        /// <summary>
        /// Removes the adorner with proper cleanup
        /// </summary>
        public void Remove()
        {
            if (_isDisposed) return;

            try
            {
                var layer = AdornerLayer.GetAdornerLayer(AdornedElement);
                layer?.Remove(this);
                
                _logger?.LogDebug("Enhanced drag adorner removed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error removing drag adorner");
            }
            finally
            {
                Dispose();
            }
        }

        #endregion

        #region Override Methods

        protected override Size MeasureOverride(Size constraint)
        {
            if (_isDisposed) return Size.Empty;

            _dragPreview.Measure(constraint);
            return _dragPreview.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_isDisposed) return Size.Empty;

            _dragPreview.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override Visual GetVisualChild(int index)
        {
            return _dragPreview;
        }

        protected override int VisualChildrenCount => _isDisposed ? 0 : 1;

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_isDisposed) return;

            try
            {
                drawingContext.PushTransform(new TranslateTransform(_leftOffset, _topOffset));
                _dragPreview.Arrange(new Rect(DesiredSize));
                drawingContext.Pop();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error rendering drag adorner");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates visual state based on current drag state
        /// </summary>
        private void UpdateVisualState()
        {
            if (_isDisposed) return;

            try
            {
                switch (_currentState)
                {
                    case DragState.ValidDrop:
                        _dragPreview.Background = _validDropBrush;
                        _dragPreview.BorderBrush = new SolidColorBrush(Colors.Green);
                        break;
                        
                    case DragState.InvalidDrop:
                        _dragPreview.Background = _invalidDropBrush;
                        _dragPreview.BorderBrush = new SolidColorBrush(Colors.Red);
                        break;
                        
                    case DragState.DetachZone:
                        _dragPreview.Background = _detachBrush;
                        _dragPreview.BorderBrush = new SolidColorBrush(Colors.Blue);
                        break;
                        
                    default:
                        _dragPreview.Background = new SolidColorBrush(Colors.White) { Opacity = 0.9 };
                        _dragPreview.BorderBrush = new SolidColorBrush(Colors.Gray);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating visual state");
            }
        }

        /// <summary>
        /// Updates drag state based on current position
        /// </summary>
        private void UpdateDragState(Point position)
        {
            if (_isDisposed) return;

            try
            {
                // Determine if we're in a detach zone (near screen edges)
                var screen = System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point((int)position.X, (int)position.Y));

                const int DETACH_ZONE_SIZE = 50;
                
                bool inDetachZone = IsDetachable && (
                    position.Y < screen.WorkingArea.Top + DETACH_ZONE_SIZE ||
                    position.Y > screen.WorkingArea.Bottom - DETACH_ZONE_SIZE ||
                    position.X < screen.WorkingArea.Left + DETACH_ZONE_SIZE ||
                    position.X > screen.WorkingArea.Right - DETACH_ZONE_SIZE);

                if (inDetachZone)
                {
                    CurrentState = DragState.DetachZone;
                }
                else
                {
                    // Default to dragging state, specific drop validation will update this
                    CurrentState = DragState.Dragging;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating drag state");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                try
                {
                    _shadowEffect?.Freeze();
                    _validDropBrush?.Freeze();
                    _invalidDropBrush?.Freeze();
                    _detachBrush?.Freeze();
                    
                    _logger?.LogDebug("Enhanced drag adorner disposed");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing enhanced drag adorner");
                }
                
                _isDisposed = true;
            }
        }

        ~TabDragAdorner()
        {
            Dispose(false);
        }

        #endregion
    }
}


