using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ChessInsight.UI.ViewModels;

namespace ChessInsight.UI.Views
{
    public partial class BoardView : UserControl
    {
        // ── Drag stanje ─────────────────────────────────────────
        private bool  _isDragging      = false;
        private int   _dragSourceIndex = -1;
        private Point _dragStartPoint;
        private PieceDragAdorner? _adorner;

        private const double DragThreshold = 6.0;
        private const double BoardSize     = 500.0;
        private const double SquareSize    = BoardSize / 8;

        public BoardView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        // ── DataContext → subscribe to ViewModel events ──────────

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is BoardViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
                oldVm.MoveAnimated    -= AnimateMoveSquare;
            }
            if (e.NewValue is BoardViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                newVm.MoveAnimated    += AnimateMoveSquare;
                RefreshArrows();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BoardViewModel.MoveArrows))
                RefreshArrows();
        }

        // ── Animacija poteza ─────────────────────────────────────

        private async void AnimateMoveSquare(int fromIdx, int toIdx)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var container = BoardGrid.ItemContainerGenerator.ContainerFromIndex(toIdx) as ContentPresenter;
            if (container == null) return;

            double sqW = BoardGrid.ActualWidth  / 8;
            double sqH = BoardGrid.ActualHeight / 8;

            double offsetX = (fromIdx % 8 - toIdx % 8) * sqW;
            double offsetY = (fromIdx / 8 - toIdx / 8) * sqH;

            if (Math.Abs(offsetX) < 1 && Math.Abs(offsetY) < 1) return;

            var tt = new TranslateTransform(offsetX, offsetY);
            container.RenderTransform = tt;

            var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
            var dur  = new Duration(TimeSpan.FromMilliseconds(170));

            tt.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(offsetX, 0, dur) { EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(offsetY, 0, dur) { EasingFunction = ease });
        }

        // ── Arrow drawing ────────────────────────────────────────

        private void RefreshArrows()
        {
            ArrowCanvas.Children.Clear();
            if (DataContext is not BoardViewModel vm) return;

            foreach (var arrow in vm.MoveArrows)
            {
                int fc = arrow.FromIdx % 8, fr = arrow.FromIdx / 8;
                int tc = arrow.ToIdx   % 8, tr = arrow.ToIdx   / 8;

                var from = new Point(fc * SquareSize + SquareSize / 2,
                                     fr * SquareSize + SquareSize / 2);
                var to   = new Point(tc * SquareSize + SquareSize / 2,
                                     tr * SquareSize + SquareSize / 2);

                var (color, strokeW) = arrow.Priority switch
                {
                    0 => (Color.FromArgb(185, 0x3E, 0xA8, 0x12),  17.0),  // #3EA812 — najbolji potez
                    1 => (Color.FromArgb(170, 133, 183, 235),       13.0),  // plava — drugi
                    _ => (Color.FromArgb(150, 136, 135, 128),        9.0)   // siva — treći
                };

                DrawArrow(ArrowCanvas, from, to, new SolidColorBrush(color), strokeW);
            }
        }

        private static void DrawArrow(Canvas canvas, Point from, Point to, Brush brush, double shaftW)
        {
            var dir = to - from;
            double len = dir.Length;
            if (len < 2) return;
            dir.Normalize();
            var perp = new Vector(-dir.Y, dir.X);

            double headLen = shaftW * 1.8;
            double headWid = shaftW * 1.9;
            double shaftHW = shaftW / 2.0;

            Point shaftStart = from + dir * (SquareSize * 0.20);
            Point arrowBase  = to   - dir * headLen;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(shaftStart + perp * shaftHW, isFilled: true, isClosed: true);
                ctx.LineTo(arrowBase  + perp * shaftHW,       isStroked: false, isSmoothJoin: true);
                ctx.LineTo(arrowBase  + perp * (headWid / 2), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(to,                                 isStroked: false, isSmoothJoin: false);
                ctx.LineTo(arrowBase  - perp * (headWid / 2), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(arrowBase  - perp * shaftHW,       isStroked: false, isSmoothJoin: true);
                ctx.LineTo(shaftStart - perp * shaftHW,       isStroked: false, isSmoothJoin: true);
            }
            geo.Freeze();

            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data             = geo,
                Fill             = brush,
                IsHitTestVisible = false,
            });
        }

        // ── Klik (postojeća logika) ──────────────────────────────

        private void Square_Click(object sender, RoutedEventArgs e)
        {
            if (_isDragging) return;
            if (sender is Button btn && btn.Tag is int index)
                (DataContext as BoardViewModel)?.OnSquareClicked(index);
        }

        // ── Drag — početak ──────────────────────────────────────

        private void Board_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint  = e.GetPosition(this);
            _dragSourceIndex = GetSquareIndex(e.GetPosition(BoardGrid));
            _isDragging      = false;
        }

        // ── Drag — kretanje ─────────────────────────────────────

        private void Board_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSourceIndex < 0) return;

            var pos   = e.GetPosition(this);
            var delta = pos - _dragStartPoint;

            if (!_isDragging &&
                (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold))
            {
                var vm = DataContext as BoardViewModel;
                if (vm == null) return;

                _isDragging = true;
                Mouse.Capture(this);
                vm.OnDragStart(_dragSourceIndex);
                ShowAdorner(_dragSourceIndex, pos, vm);
            }

            if (_isDragging)
            {
                _adorner?.UpdatePosition(pos);
                e.Handled = true;
            }
        }

        // ── Drag — završetak ────────────────────────────────────

        private void Board_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                _dragSourceIndex = -1;
                return;
            }

            Mouse.Capture(null);
            RemoveAdorner();

            int toIndex = GetSquareIndex(Mouse.GetPosition(BoardGrid));
            var vm = DataContext as BoardViewModel;

            if (toIndex >= 0 && toIndex != _dragSourceIndex)
                vm?.TryApplyDragMove(_dragSourceIndex, toIndex);
            else
                vm?.CancelDrag();

            e.Handled    = true;
            _isDragging  = false;
            _dragSourceIndex = -1;
        }

        // ── Adorner helpers ─────────────────────────────────────

        private void ShowAdorner(int sourceIndex, Point pos, BoardViewModel vm)
        {
            var sq  = vm.Squares[sourceIndex];
            var uri = sq.PieceSvgUri;
            if (uri == null) return;

            var layer = AdornerLayer.GetAdornerLayer(this);
            if (layer == null) return;

            _adorner = new PieceDragAdorner(this, uri, pos);
            layer.Add(_adorner);
        }

        private void RemoveAdorner()
        {
            if (_adorner == null) return;
            AdornerLayer.GetAdornerLayer(this)?.Remove(_adorner);
            _adorner = null;
        }

        // ── Mapiranje pozicije miša → indeks polja ──────────────

        private int GetSquareIndex(Point posRelativeToGrid)
        {
            double w = BoardGrid.ActualWidth;
            double h = BoardGrid.ActualHeight;
            if (w <= 0 || h <= 0) return -1;

            int col = (int)(posRelativeToGrid.X / (w / 8));
            int row = (int)(posRelativeToGrid.Y / (h / 8));

            if (col < 0 || col > 7 || row < 0 || row > 7) return -1;
            return row * 8 + col;
        }
    }
}
