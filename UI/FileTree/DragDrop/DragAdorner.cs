// UI/FileTree/DragDrop/DragAdorner.cs - Updated for better visual feedback
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
        private double _opacity = 0.8;
        
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
                if (_visualSize.Width == 0) _visualSize.Width = 100;
                if (_visualSize.Height == 0) _visualSize.Height = 20;
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
        
        public void UpdateOpacity(double opacity)
        {
            _opacity = Math.Max(0.1, Math.Min(1.0, opacity));
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
                // Draw shadow first
                DrawShadow(groupContext, point);
                
                // Draw the main visual
                var visualBrush = new VisualBrush(_draggedVisual)
                {
                    Opacity = 1.0,
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
            drawingGroup.Opacity = _opacity;
            drawingContext.DrawDrawing(drawingGroup);
        }
        
        private void DrawShadow(DrawingContext context, Point basePoint)
        {
            // Draw a soft shadow behind the visual
            var shadowBrush = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
            var shadowRect = new Rect(
                basePoint.X + 3,
                basePoint.Y + 3,
                _visualSize.Width,
                _visualSize.Height);
            
            context.DrawRoundedRectangle(shadowBrush, null, shadowRect, 2, 2);
        }
        
        private void DrawCountBadge(DrawingContext context, Point basePoint)
        {
            var badgeSize = 22.0;
            var badgeCenter = new Point(
                basePoint.X + _visualSize.Width - badgeSize / 2 - 2,
                basePoint.Y + badgeSize / 2 + 2
            );
            
            // Badge background with gradient
            var gradientBrush = new RadialGradientBrush(
                Color.FromRgb(0, 120, 215),  // Windows 10/11 blue
                Color.FromRgb(0, 90, 158));
            gradientBrush.Center = new Point(0.3, 0.3);
            gradientBrush.RadiusX = 0.7;
            gradientBrush.RadiusY = 0.7;
            
            // Draw badge background
            context.DrawEllipse(gradientBrush, null, badgeCenter, badgeSize / 2, badgeSize / 2);
            
            // Badge border
            var borderPen = new Pen(new SolidColorBrush(Colors.White), 1.5);
            context.DrawEllipse(null, borderPen, badgeCenter, badgeSize / 2, badgeSize / 2);
            
            // Count text
            var formattedText = new FormattedText(
                _itemCount > 99 ? "99+" : _itemCount.ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                Brushes.White,
                96);
            
            formattedText.SetFontWeight(FontWeights.SemiBold);
            
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
            Brush backgroundBrush = Brushes.White;
            
            switch (_effects)
            {
                case DragDropEffects.Copy:
                    // Plus sign for copy
                    icon = CreatePlusIcon(iconPoint, iconSize);
                    iconBrush = Brushes.White;
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                    break;
                    
                case DragDropEffects.Move:
                    // Arrow for move
                    icon = CreateArrowIcon(iconPoint, iconSize);
                    iconBrush = Brushes.White;
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                    break;
                    
                case DragDropEffects.Link:
                    // Link chain icon
                    icon = CreateLinkIcon(iconPoint, iconSize);
                    iconBrush = Brushes.White;
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(128, 128, 0));
                    break;
                    
                case DragDropEffects.None:
                    // No drop icon
                    icon = CreateNoDropIcon(iconPoint, iconSize);
                    iconBrush = Brushes.White;
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(200, 0, 0));
                    break;
            }
            
            if (icon != null)
            {
                // Background circle
                var bgCenter = new Point(iconPoint.X + iconSize / 2, iconPoint.Y + iconSize / 2);
                context.DrawEllipse(
                    backgroundBrush,
                    null,
                    bgCenter,
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
                new Rect(origin.X + size * 0.2, origin.Y + size * 0.425, size * 0.6, size * 0.15)));
            
            // Vertical line
            group.Children.Add(new RectangleGeometry(
                new Rect(origin.X + size * 0.425, origin.Y + size * 0.2, size * 0.15, size * 0.6)));
            
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
            
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.65, origin.Y + size * 0.5), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.45, origin.Y + size * 0.3), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.65, origin.Y + size * 0.5), true));
            figure.Segments.Add(new LineSegment(new Point(origin.X + size * 0.45, origin.Y + size * 0.7), true));
            
            var path = new PathGeometry(new[] { figure });
            path.Transform = new TranslateTransform(0, 0);
            
            // Create a pen with thicker stroke
            var pen = new Pen(Brushes.White, 2);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            
            return path.GetWidenedPathGeometry(pen);
        }
        
        private Geometry CreateLinkIcon(Point origin, double size)
        {
            var group = new GeometryGroup();
            
            // Create chain link shape - simplified version
            var path = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(origin.X + size * 0.3, origin.Y + size * 0.5)
            };
            
            // Left link
            figure.Segments.Add(new ArcSegment(
                new Point(origin.X + size * 0.3, origin.Y + size * 0.5),
                new Size(size * 0.2, size * 0.3), 
                0, true, 
                SweepDirection.Clockwise, 
                true));
            
            // Connection
            figure.Segments.Add(new LineSegment(
                new Point(origin.X + size * 0.7, origin.Y + size * 0.5), 
                true));
            
            // Right link
            figure.Segments.Add(new ArcSegment(
                new Point(origin.X + size * 0.7, origin.Y + size * 0.5),
                new Size(size * 0.2, size * 0.3), 
                0, true, 
                SweepDirection.Clockwise, 
                true));
            
            path.Figures.Add(figure);
            
            var pen = new Pen(Brushes.White, 2);
            return path.GetWidenedPathGeometry(pen);
        }
        
        private Geometry CreateNoDropIcon(Point origin, double size)
        {
            var group = new GeometryGroup();
            
            // Circle
            var circle = new EllipseGeometry(
                new Point(origin.X + size / 2, origin.Y + size / 2),
                size * 0.4, size * 0.4);
            
            var pen = new Pen(Brushes.White, 2);
            group.Children.Add(circle.GetWidenedPathGeometry(pen));
            
            // Diagonal line
            var line = new LineGeometry(
                new Point(origin.X + size * 0.25, origin.Y + size * 0.25),
                new Point(origin.X + size * 0.75, origin.Y + size * 0.75));
            
            group.Children.Add(line.GetWidenedPathGeometry(pen));
            
            return group;
        }
        
        protected override Size MeasureOverride(Size constraint)
        {
            // Return a size that includes the visual plus any badges/indicators
            return new Size(_visualSize.Width + 30, _visualSize.Height + 30);
        }
        
        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }
        
        protected override Visual GetVisualChild(int index)
        {
            return null; // We're drawing directly
        }
        
        protected override int VisualChildrenCount => 0;
    }
}