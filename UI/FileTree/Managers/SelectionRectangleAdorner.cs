using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Animation;

namespace ExplorerPro.UI.FileTree.Managers
{
    public class SelectionRectangleAdorner : Adorner
    {
        private Point _startPoint;
        private Point _endPoint;
        private readonly Pen _pen;
        private readonly Brush _brush;
        private readonly DoubleAnimation _fadeAnimation;
        private double _opacity = 1.0;

        public SelectionRectangleAdorner(UIElement adornedElement, Point startPoint) 
            : base(adornedElement)
        {
            _startPoint = startPoint;
            _endPoint = startPoint;
            
            // Use hardware acceleration for better performance
            CacheMode = new BitmapCache();
            
            // Optimize pen and brush creation
            _pen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)), 1.0)
            {
                DashCap = PenLineCap.Flat,
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat
            };
            
            _brush = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215))
            {
                Opacity = _opacity
            };

            // Setup fade animation for smooth appearance/disappearance
            _fadeAnimation = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }

        public void UpdateSelection(Point startPoint, Point endPoint)
        {
            _startPoint = startPoint;
            _endPoint = endPoint;
            
            // Use hardware acceleration for updates
            if (CacheMode == null)
            {
                CacheMode = new BitmapCache();
            }
            
            InvalidateVisual();
        }

        /// <summary>
        /// Updates the end point of the selection rectangle
        /// </summary>
        public void UpdateEndPoint(Point endPoint)
        {
            _endPoint = endPoint;
            
            // Use hardware acceleration for updates
            if (CacheMode == null)
            {
                CacheMode = new BitmapCache();
            }
            
            InvalidateVisual();
        }

        public Rect GetSelectionBounds()
        {
            return new Rect(
                Math.Min(_startPoint.X, _endPoint.X),
                Math.Min(_startPoint.Y, _endPoint.Y),
                Math.Abs(_endPoint.X - _startPoint.X),
                Math.Abs(_endPoint.Y - _startPoint.Y)
            );
        }

        public void FadeOut()
        {
            _fadeAnimation.From = _opacity;
            _fadeAnimation.To = 0;
            _brush.BeginAnimation(Brush.OpacityProperty, _fadeAnimation);
        }

        public void FadeIn()
        {
            _fadeAnimation.From = _opacity;
            _fadeAnimation.To = 1;
            _brush.BeginAnimation(Brush.OpacityProperty, _fadeAnimation);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var bounds = GetSelectionBounds();
            
            // Use optimized drawing
            drawingContext.PushOpacity(_opacity);
            drawingContext.DrawRectangle(_brush, _pen, bounds);
            drawingContext.Pop();
        }
    }
} 