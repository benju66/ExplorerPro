using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Visual indicator showing where a tab will be inserted during drag operations
    /// </summary>
    public class TabDropInsertionIndicator : Adorner, IDisposable
    {
        #region Fields

        private readonly ILogger<TabDropInsertionIndicator>? _logger;
        private readonly Rectangle _insertionLine;
        private readonly Border _insertionMarker;
        private readonly Canvas _container;
        
        private double _insertionX = 0;
        private bool _isDisposed = false;
        private bool _isVisible = false;
        private DropIndicatorState _currentState = DropIndicatorState.Hidden;
        
        // Animation resources
        private readonly Storyboard _fadeInStoryboard;
        private readonly Storyboard _fadeOutStoryboard;
        private readonly DoubleAnimation _scaleAnimation;

        #endregion

        #region Enums

        public enum DropIndicatorState
        {
            Hidden,
            ValidDrop,
            InvalidDrop,
            DetachZone
        }

        #endregion

        #region Properties

        public DropIndicatorState CurrentState
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

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    if (value)
                        ShowIndicator();
                    else
                        HideIndicator();
                }
            }
        }

        #endregion

        #region Constructor

        public TabDropInsertionIndicator(UIElement adornedElement, ILogger<TabDropInsertionIndicator>? logger = null)
            : base(adornedElement)
        {
            _logger = logger;
            
            try
            {
                // Create container canvas
                _container = new Canvas
                {
                    IsHitTestVisible = false,
                    Opacity = 0
                };

                // Create insertion line (vertical line showing drop position)
                _insertionLine = new Rectangle
                {
                    Width = 3,
                    Height = 30,
                    Fill = new SolidColorBrush(Color.FromRgb(0, 120, 215)), // Windows accent blue
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 4,
                        ShadowDepth = 2,
                        Opacity = 0.3
                    }
                };

                // Create insertion marker (small circle at top/bottom)
                _insertionMarker = new Border
                {
                    Width = 8,
                    Height = 8,
                    Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    CornerRadius = new CornerRadius(4),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 2,
                        ShadowDepth = 1,
                        Opacity = 0.3
                    }
                };

                // Add elements to container
                _container.Children.Add(_insertionLine);
                _container.Children.Add(_insertionMarker);

                // Create animations
                _fadeInStoryboard = CreateFadeAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                _fadeOutStoryboard = CreateFadeAnimation(1, 0, TimeSpan.FromMilliseconds(100));
                
                _scaleAnimation = new DoubleAnimation
                {
                    From = 0.8,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                AddVisualChild(_container);
                
                _logger?.LogDebug("Tab drop insertion indicator created successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating tab drop insertion indicator");
                throw;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the insertion position
        /// </summary>
        public void UpdatePosition(double x, double tabHeight)
        {
            if (_isDisposed) return;

            try
            {
                _insertionX = x;
                
                // Position the insertion line
                Canvas.SetLeft(_insertionLine, x - (_insertionLine.Width / 2));
                Canvas.SetTop(_insertionLine, (tabHeight - _insertionLine.Height) / 2);
                
                // Position the marker at the top
                Canvas.SetLeft(_insertionMarker, x - (_insertionMarker.Width / 2));
                Canvas.SetTop(_insertionMarker, -2);
                
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating insertion indicator position");
            }
        }

        /// <summary>
        /// Shows the insertion indicator with animation
        /// </summary>
        public void ShowIndicator()
        {
            if (_isDisposed || _isVisible) return;

            try
            {
                _isVisible = true;
                
                // Start fade in animation
                _fadeInStoryboard.Begin(_container);
                
                // Start scale animation
                var scaleTransform = new ScaleTransform(0.8, 0.8, _insertionX, 15);
                _container.RenderTransform = scaleTransform;
                
                _scaleAnimation.Completed += (s, e) => 
                {
                    _container.RenderTransform = new ScaleTransform(1, 1, _insertionX, 15);
                };
                
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, _scaleAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, _scaleAnimation);
                
                _logger?.LogDebug("Insertion indicator shown");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing insertion indicator");
            }
        }

        /// <summary>
        /// Hides the insertion indicator with animation
        /// </summary>
        public void HideIndicator()
        {
            if (_isDisposed || !_isVisible) return;

            try
            {
                _isVisible = false;
                
                // Start fade out animation
                _fadeOutStoryboard.Completed += (s, e) => 
                {
                    _container.Opacity = 0;
                };
                
                _fadeOutStoryboard.Begin(_container);
                
                _logger?.LogDebug("Insertion indicator hidden");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error hiding insertion indicator");
            }
        }

        /// <summary>
        /// Removes the indicator with proper cleanup
        /// </summary>
        public void Remove()
        {
            if (_isDisposed) return;

            try
            {
                var layer = AdornerLayer.GetAdornerLayer(AdornedElement);
                layer?.Remove(this);
                
                _logger?.LogDebug("Tab drop insertion indicator removed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error removing insertion indicator");
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

            _container.Measure(constraint);
            return _container.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_isDisposed) return Size.Empty;

            _container.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override Visual GetVisualChild(int index)
        {
            return _container;
        }

        protected override int VisualChildrenCount => _isDisposed ? 0 : 1;

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates visual state based on current drop state
        /// </summary>
        private void UpdateVisualState()
        {
            if (_isDisposed) return;

            try
            {
                SolidColorBrush brush;
                
                switch (_currentState)
                {
                    case DropIndicatorState.ValidDrop:
                        brush = new SolidColorBrush(Color.FromRgb(46, 204, 113)); // Success green
                        break;
                        
                    case DropIndicatorState.InvalidDrop:
                        brush = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Error red
                        break;
                        
                    case DropIndicatorState.DetachZone:
                        brush = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Info blue
                        break;
                        
                    default:
                        brush = new SolidColorBrush(Color.FromRgb(0, 120, 215)); // Default blue
                        break;
                }

                _insertionLine.Fill = brush;
                _insertionMarker.Background = brush;
                
                // Add glow effect for better visibility
                var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = brush.Color,
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    Opacity = 0.6
                };
                
                _insertionLine.Effect = glowEffect;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating insertion indicator visual state");
            }
        }

        /// <summary>
        /// Creates fade animation storyboard
        /// </summary>
        private Storyboard CreateFadeAnimation(double from, double to, TimeSpan duration)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            
            Storyboard.SetTarget(animation, _container);
            Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
            
            return storyboard;
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
                    // Stop animations
                    _fadeInStoryboard?.Stop(_container);
                    _fadeOutStoryboard?.Stop(_container);
                    
                    // Clear container
                    _container?.Children.Clear();
                    
                    _isDisposed = true;
                    _logger?.LogDebug("Tab drop insertion indicator disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing insertion indicator");
                }
            }
        }

        #endregion
    }
}
