using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Adorner for showing drag preview
    /// </summary>
    public class DragPreviewAdorner : Adorner
    {
        private readonly Visual _visual;
        private Point _offset;

        public DragPreviewAdorner(UIElement adornedElement, Visual visual, Point offset) 
            : base(adornedElement)
        {
            _visual = visual;
            _offset = offset;
            IsHitTestVisible = false;
        }

        public Point Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                InvalidateVisual();
            }
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _visual;

        protected override Size MeasureOverride(Size constraint)
        {
            return AdornedElement.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var transform = new TranslateTransform(_offset.X, _offset.Y);
            drawingContext.PushTransform(transform);
            
            var rect = new Rect(0, 0, 200, 40);
            var brush = new VisualBrush(_visual);
            drawingContext.DrawRectangle(brush, null, rect);
            
            drawingContext.Pop();
        }
    }
} 