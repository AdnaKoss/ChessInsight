using SharpVectors.Converters;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ChessInsight.UI.Views
{
    internal class PieceDragAdorner : Adorner
    {
        private readonly FrameworkElement _child;
        private Point _position;
        private readonly double _size;

        public PieceDragAdorner(UIElement adornedElement, Uri svgUri, Point startPos, double size = 46)
            : base(adornedElement)
        {
            _position = startPos;
            _size     = size;
            IsHitTestVisible = false;

            _child = new SvgViewbox
            {
                Source           = svgUri,
                Width            = size,
                Height           = size,
                Stretch          = Stretch.Uniform,
                IsHitTestVisible = false,
                Opacity          = 0.92
            };

            AddVisualChild(_child);
            AddLogicalChild(_child);
        }

        public void UpdatePosition(Point pos)
        {
            _position = pos;
            InvalidateArrange();
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _child;

        protected override Size MeasureOverride(Size constraint)
        {
            _child.Measure(new Size(_size, _size));
            return constraint;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _child.Arrange(new Rect(
                _position.X - _size / 2,
                _position.Y - _size / 2,
                _size,
                _size));
            return finalSize;
        }
    }
}
