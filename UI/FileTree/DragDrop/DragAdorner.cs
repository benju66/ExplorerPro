// UI/FileTree/DragDrop/DragAdorner.cs - Fixed with proper offset handling
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
        private readonly Visual _draggedVisual;
        private readonly Point _offset;
        private Point _location;
        private readonly int _itemCount;
        private DragDropEffects _effects;
        private readonly Size _visualSize;
        
        public DragAdorner(UIElement adornedElement, Visual draggedElement, Point offset, int itemCount = 1, DragDropEffects effects = DragDropEffects.None) 
            : base(adornedElement)
        {
            _offset = offset;
            _itemCount = itemCount;
            _effects = effects;
            _draggedVisual = draggedElement;
            
            // Store the size of the visual
            if (draggedElement is FrameworkElement fe)
            {
                _visualSize = new Size(fe.ActualWidth, fe.ActualHeight);
            }
            else
            {
                _visualSize = new Size(100, 20); // Default size
            }
            
            IsHitTestVisible = false;
            IsClipEnabled = true;
            
            // Ensure adorner layer updates
            InvalidateVisual();
        }
        
        public void UpdatePosition(Point location)
        {
            _location = location;
            InvalidateVisual();
        }
        
        public void UpdateEffects(DragDropEffects effects)
        {
            _effects = effects;
            InvalidateVisual();
        }
        
        protected override void OnRender(DrawingContext drawingContext)
        {
            // Calculate position adjusted by offset
            var point = new Point(_location.X - _offset.X, _location.Y - _offset.Y);
            
            // Create a drawing group for complex rendering
            var drawingGroup = new DrawingGroup();
            using (var groupContext = drawingGroup.Open())
            {
                // Draw the main visual
                var visualBrush = new VisualBrush(_draggedVisual)
                {
                    Opacity = 0.7,
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };
                
                var rect = new Rect(point, _visualSize);
                groupContext.DrawRectangle(visualBrush, null, rect);
                
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
                basePoint.X + _visualSize.Width - badgeSize / 2 - 4,
                basePoint.Y + badgeSize / 2 + 4
            );
            
            // Badge background with gradient
            var gradientBrush = new RadialGradientBrush(
                Color.FromRgb(0, 140, 232),
                Color.FromRgb(0, 100, 192));
            gradientBrush.Center = new Point(0.3, 0.3);
            gradientBrush.RadiusX = 0.7;
            gradientBrush.RadiusY = 0.7;
            
            context.DrawEllipse(gradientBrush, null, badgeCenter, badgeSize / 2, badgeSize / 2);
            
            // Badge border
            var borderPen = new Pen(Brushes.White, 2);
            context.DrawEllipse(null, borderPen, badgeCenter, badgeSize / 2, badgeSize / 2);
            
            // Count text
            var formattedText = new FormattedText(
                _itemCount.ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Brushes.White,
                96);
            
            var textPoint = new Point(
                badgeCenter.X - formattedText.Width / 2,
                badgeCenter.Y - formattedText.Height / 2
            );
            context.DrawText(formattedText, textPoint);
        }
        
        private void DrawEffectIndicator(DrawingContext context, Point basePoint)
        {
            // Small icon in bottom-left to indicate operation type
            var iconSize = 16.0;
            var iconPoint = new Point(basePoint.X + 4, basePoint.Y + _visualSize.Height - iconSize - 4);
            
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
                    
                case DragDropEffects.None:
                    // No drop icon
                    icon = CreateNoDropIcon(iconPoint, iconSize);
                    iconBrush = new SolidColorBrush(Color.FromRgb(200, 0, 0));
                    break;
            }
            
            if (icon != null)
            {
                // Background circle with shadow effect
                var shadowBrush = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));
                context.DrawEllipse(
                    shadowBrush,
                    null,
                    new Point(iconPoint.X + iconSize / 2 + 1, iconPoint.Y + iconSize / 2 + 1),
                    iconSize / 2 + 3,
                    iconSize / 2 + 3);
                
                // Background circle
                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                    new Pen(iconBrush, 1),
                    new Point(iconPoint.X + iconSize / 2, iconPoint.Y + iconSize / 2),
                    iconSize / 2 + 2,
                    iconSize / 2 + 2);
                
                // Icon
                context.DrawGeometry(iconBrush, new Pen(iconBrush, 1), icon);
            }
        }
        
        private Geometry CreatePlusIcon(Point origin, double size)
        {
            var group = new GeometryGroup();
            
            // Horizontal line
            group.Children.Add(new RectangleGeometry(
                new Rect(origin.X + size * 0.15, origin.Y + size * 0.425, size * 0.7, size * 0.15)));
            
            // Vertical line
            group.Children.Add(new RectangleGeometry(
                new Rect(origin.X + size * 0.425, origin.Y + size * 0.15, size * 0.15, size * 0.7)));
            
            return group;
        }
        
        private Geometry CreateArrowIcon(Point origin, double size)
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(origin.X + size * 0.2, origin.Y + size * 0.5),
                IsClosed = false,
                IsFilled = false
            };
            
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.7, origin.Y + size * 0.5), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.5, origin.Y + size * 0.3), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.7, origin.Y + size * 0.5), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.5, origin.Y + size * 0.7), true));
            
            var path = new PathGeometry(new[] { figure });
            path.Transform = new TranslateTransform(0, 0);
            return path;
        }
        
        private Geometry CreateLinkIcon(Point origin, double size)
        {
            var group = new GeometryGroup();
            
            // Create chain link shape
            var link1 = new PathFigure
            {
                StartPoint = new Point(origin.X + size * 0.35, origin.Y + size * 0.35)
            };
            link1.Segments.Add(new ArcSegment(
                new Point(origin.X + size * 0.35, origin.Y + size * 0.65),
                new Size(size * 0.15, size * 0.15), 0, true, SweepDirection.Clockwise, true));
            link1.Segments.Add(new LineSegment(new Point(origin.X + size * 0.5, origin.Y + size * 0.65), true));
            link1.Segments.Add(new ArcSegment(
                new Point(origin.X + size * 0.5, origin.Y + size * 0.35),
                new Size(size * 0.15, size * 0.15), 0, true, SweepDirection.Clockwise, true));
            link1.IsClosed = true;
            
            var link2 = new PathFigure
            {
                StartPoint = new Point(origin.X + size * 0.5, origin.Y + size * 0.35)
            };
            link2.Segments.Add(new ArcSegment(
                new Point(origin.X + size * 0.5, origin.Y + size * 0.65),
                new Size(size * 0.15, size * 0.15), 0, true, SweepDirection.Clockwise, true));
            link2.Segments.Add(new LineSegment(new Point(origin.X + size * 0.65, origin.Y + size * 0.65), true));
            link2.Segments.Add(new ArcSegment(
                new Point(origin.X + size * 0.65, origin.Y + size * 0.35),
                new Size(size * 0.15, size * 0.15), 0, true, SweepDirection.Clockwise, true));
            link2.IsClosed = true;
            
            var pathGeo = new PathGeometry();
            pathGeo.Figures.Add(link1);
            pathGeo.Figures.Add(link2);
            
            group.Children.Add(pathGeo);
            return group;
        }
        
        private Geometry CreateNoDropIcon(Point origin, double size)
        {
            var group = new GeometryGroup();
            
            // Circle
            group.Children.Add(new EllipseGeometry(
                new Point(origin.X + size / 2, origin.Y + size / 2),
                size * 0.4, size * 0.4));
            
            // Diagonal line
            var line = new LineGeometry(
                new Point(origin.X + size * 0.25, origin.Y + size * 0.25),
                new Point(origin.X + size * 0.75, origin.Y + size * 0.75));
            
            group.Children.Add(line);
            
            return group;
        }
        
        protected override Size MeasureOverride(Size constraint)
        {
            return _visualSize;
        }
        
        protected override Size ArrangeOverride(Size finalSize)
        {
            return _visualSize;
        }
        
        protected override Visual GetVisualChild(int index)
        {
            return null; // We're drawing directly
        }
        
        protected override int VisualChildrenCount => 0;
    }
}