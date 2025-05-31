// UI/FileTree/SelectionRectangleAdorner.cs
using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Adorner that displays a selection rectangle for lasso selection
    /// </summary>
    public class SelectionRectangleAdorner : Adorner
    {
        private Point _startPoint;
        private Point _endPoint;
        private Pen _pen;
        private Brush _fillBrush;
        
        public SelectionRectangleAdorner(UIElement adornedElement, Point startPoint) : base(adornedElement)
        {
            _startPoint = startPoint;
            _endPoint = startPoint;
            
            // Create visual elements
            var color = SystemColors.HighlightColor;
            _pen = new Pen(new SolidColorBrush(color), 1.5);
            _pen.DashStyle = DashStyles.Solid;
            
            // Semi-transparent fill
            _fillBrush = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B));
            
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }
        
        /// <summary>
        /// Updates the end point of the selection rectangle
        /// </summary>
        public void UpdateEndPoint(Point endPoint)
        {
            _endPoint = endPoint;
            InvalidateVisual();
        }
        
        /// <summary>
        /// Gets the bounds of the selection rectangle
        /// </summary>
        public Rect GetSelectionBounds()
        {
            double x = Math.Min(_startPoint.X, _endPoint.X);
            double y = Math.Min(_startPoint.Y, _endPoint.Y);
            double width = Math.Abs(_endPoint.X - _startPoint.X);
            double height = Math.Abs(_endPoint.Y - _startPoint.Y);
            
            return new Rect(x, y, width, height);
        }
        
        protected override void OnRender(DrawingContext drawingContext)
        {
            var rect = GetSelectionBounds();
            
            // Only draw if the rectangle has some size
            if (rect.Width > 1 && rect.Height > 1)
            {
                // Draw filled rectangle
                drawingContext.DrawRectangle(_fillBrush, _pen, rect);
            }
        }
    }
}