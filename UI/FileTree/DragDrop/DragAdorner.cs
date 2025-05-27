// UI/FileTree/DragDrop/DragAdorner.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ExplorerPro.UI.FileTree.DragDrop
{
    /// <summary>
    /// Provides visual feedback during drag operations with file preview and count badge
    /// </summary>
    public class DragAdorner : Adorner
    {
        private readonly VisualBrush _visualBrush;
        private readonly Rectangle _rectangle;
        private readonly Point _offset;
        private Point _location;
        private readonly int _itemCount;
        private readonly DragDropEffects _effects;
        
        public DragAdorner(UIElement adornedElement, Visual draggedElement, Point offset, int itemCount = 1, DragDropEffects effects = DragDropEffects.None) 
            : base(adornedElement)
        {
            _offset = offset;
            _itemCount = itemCount;
            _effects = effects;
            
            // Create visual brush from dragged element
            _visualBrush = new VisualBrush(draggedElement)
            {
                Opacity = 0.7,
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };
            
            // Create rectangle to show the visual
            _rectangle = new Rectangle
            {
                Width = ((FrameworkElement)draggedElement).ActualWidth,
                Height = ((FrameworkElement)draggedElement).ActualHeight,
                Fill = _visualBrush,
                IsHitTestVisible = false
            };
            
            IsHitTestVisible = false;
            IsClipEnabled = true;
        }
        
        public void UpdatePosition(Point location)
        {
            _location = location;
            InvalidateVisual();
        }
        
        public void UpdateEffects(DragDropEffects effects)
        {
            // Could update cursor or visual based on effects
            InvalidateVisual();
        }
        
        protected override void OnRender(DrawingContext drawingContext)
        {
            // Calculate position
            var point = _location;
            point.Offset(-_offset.X, -_offset.Y);
            
            // Create a drawing group for complex rendering
            var drawingGroup = new DrawingGroup();
            using (var groupContext = drawingGroup.Open())
            {
                // Draw the main visual
                var rect = new Rect(point, new Size(_rectangle.Width, _rectangle.Height));
                groupContext.DrawRectangle(_visualBrush, null, rect);
                
                // Draw count badge if multiple items
                if (_itemCount > 1)
                {
                    DrawCountBadge(groupContext, point);
                }
                
                // Draw effect indicator
                DrawEffectIndicator(groupContext, point);
            }
            
            // Apply opacity to the entire group
            drawingGroup.Opacity = 0.8;
            drawingContext.DrawDrawing(drawingGroup);
        }
        
        private void DrawCountBadge(DrawingContext context, Point basePoint)
        {
            var badgeSize = 24.0;
            var badgeCenter = new Point(
                basePoint.X + _rectangle.Width - badgeSize / 2 - 4,
                basePoint.Y + badgeSize / 2 + 4
            );
            
            // Badge background
            var badgeBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            context.DrawEllipse(badgeBrush, null, badgeCenter, badgeSize / 2, badgeSize / 2);
            
            // Badge border
            var borderPen = new Pen(Brushes.White, 2);
            context.DrawEllipse(null, borderPen, badgeCenter, badgeSize / 2, badgeSize / 2);
            
            // Count text
            var countText = new FormattedText(
                _itemCount.ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Brushes.White,
                96);
            
            var textPoint = new Point(
                badgeCenter.X - countText.Width / 2,
                badgeCenter.Y - countText.Height / 2
            );
            context.DrawText(countText, textPoint);
        }
        
        private void DrawEffectIndicator(DrawingContext context, Point basePoint)
        {
            // Small icon in bottom-left to indicate operation type
            var iconSize = 16.0;
            var iconPoint = new Point(basePoint.X + 4, basePoint.Y + _rectangle.Height - iconSize - 4);
            
            Geometry icon = null;
            Brush iconBrush = Brushes.White;
            
            switch (_effects)
            {
                case DragDropEffects.Copy:
                    // Plus sign for copy
                    icon = CreatePlusIcon(iconPoint, iconSize);
                    iconBrush = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                    break;
                    
                case DragDropEffects.Move:
                    // Arrow for move
                    icon = CreateArrowIcon(iconPoint, iconSize);
                    iconBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                    break;
                    
                case DragDropEffects.Link:
                    // Link chain icon
                    icon = CreateLinkIcon(iconPoint, iconSize);
                    iconBrush = new SolidColorBrush(Color.FromRgb(150, 150, 0));
                    break;
            }
            
            if (icon != null)
            {
                // Background circle
                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    null,
                    new Point(iconPoint.X + iconSize / 2, iconPoint.Y + iconSize / 2),
                    iconSize / 2 + 2,
                    iconSize / 2 + 2);
                
                // Icon
                context.DrawGeometry(iconBrush, null, icon);
            }
        }
        
        private Geometry CreatePlusIcon(Point origin, double size)
        {
            var group = new GeometryGroup();
            
            // Horizontal line
            group.Children.Add(new RectangleGeometry(
                new Rect(origin.X + size * 0.2, origin.Y + size * 0.45, size * 0.6, size * 0.1)));
            
            // Vertical line
            group.Children.Add(new RectangleGeometry(
                new Rect(origin.X + size * 0.45, origin.Y + size * 0.2, size * 0.1, size * 0.6)));
            
            return group;
        }
        
        private Geometry CreateArrowIcon(Point origin, double size)
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(origin.X + size * 0.2, origin.Y + size * 0.5)
            };
            
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.6, origin.Y + size * 0.5), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.45, origin.Y + size * 0.35), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.6, origin.Y + size * 0.5), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.45, origin.Y + size * 0.65), true));
            
            return new PathGeometry(new[] { figure });
        }
        
        private Geometry CreateLinkIcon(Point origin, double size)
        {
            var group = new GeometryGroup();
            
            // Two interlocked circles representing a chain link
            var circle1 = new EllipseGeometry(
                new Point(origin.X + size * 0.35, origin.Y + size * 0.5), 
                size * 0.2, size * 0.3);
            
            var circle2 = new EllipseGeometry(
                new Point(origin.X + size * 0.65, origin.Y + size * 0.5), 
                size * 0.2, size * 0.3);
            
            group.Children.Add(circle1);
            group.Children.Add(circle2);
            
            return group;
        }
        
        protected override Size MeasureOverride(Size constraint)
        {
            return new Size(_rectangle.Width, _rectangle.Height);
        }
        
        protected override Size ArrangeOverride(Size finalSize)
        {
            _rectangle.Arrange(new Rect(finalSize));
            return finalSize;
        }
        
        protected override Visual GetVisualChild(int index)
        {
            return _rectangle;
        }
        
        protected override int VisualChildrenCount => 1;
    }
}