using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ChessInsight.UI.Views
{
    internal class PieceDragAdorner : Adorner
    {
        private readonly string _symbol;
        private readonly Brush _foreground;
        private Point _position;
        private const double FontSize = 46;

        public PieceDragAdorner(UIElement adornedElement, string symbol, Brush foreground, Point startPos)
            : base(adornedElement)
        {
            _symbol    = symbol;
            _foreground = foreground;
            _position  = startPos;
            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point pos)
        {
            _position = pos;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var typeface = new Typeface(
                new FontFamily("Segoe UI Symbol"),
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var shadow = new FormattedText(
                _symbol, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, FontSize,
                new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), dpi);

            var text = new FormattedText(
                _symbol, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, FontSize, _foreground, dpi);

            double cx = _position.X - text.Width  / 2;
            double cy = _position.Y - text.Height / 2;

            dc.PushOpacity(0.88);
            dc.DrawText(shadow, new Point(cx + 2, cy + 2));
            dc.DrawText(text,   new Point(cx, cy));
            dc.Pop();
        }
    }
}
