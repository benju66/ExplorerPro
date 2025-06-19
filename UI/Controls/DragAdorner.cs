using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;

namespace ExplorerPro.UI.Controls
{
    public class DragAdorner : Adorner
    {
        private readonly ContentPresenter _contentPresenter;
        private double _leftOffset;
        private double _topOffset;

        public DragAdorner(UIElement adornedElement, TabItem draggedTab) : base(adornedElement)
        {
            _contentPresenter = new ContentPresenter
            {
                Content = draggedTab.Header,
                Opacity = 0.7
            };
            
            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point position)
        {
            _leftOffset = position.X - 20;
            _topOffset = position.Y - 10;
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _contentPresenter.Measure(constraint);
            return _contentPresenter.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _contentPresenter.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override Visual GetVisualChild(int index)
        {
            return _contentPresenter;
        }

        protected override int VisualChildrenCount => 1;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.PushTransform(new TranslateTransform(_leftOffset, _topOffset));
            _contentPresenter.Arrange(new Rect(DesiredSize));
            drawingContext.Pop();
        }

        public void Remove()
        {
            var layer = AdornerLayer.GetAdornerLayer(AdornedElement);
            layer?.Remove(this);
        }
    }
} 