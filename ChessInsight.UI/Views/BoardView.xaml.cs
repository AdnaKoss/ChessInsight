using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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

        public BoardView()
        {
            InitializeComponent();
        }

        // ── Klik (postojeća logika) ──────────────────────────────

        private void Square_Click(object sender, RoutedEventArgs e)
        {
            // Ne obrađuj klik ako je akcija bila drag
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
                Mouse.Capture(this);        // preuzmi miša da evente primamo i van grida
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

            e.Handled    = true;   // spriječi Button.Click na odredišnom polju
            _isDragging  = false;
            _dragSourceIndex = -1;
        }

        // ── Adorner helpers ─────────────────────────────────────

        private void ShowAdorner(int sourceIndex, Point pos, BoardViewModel vm)
        {
            var sq = vm.Squares[sourceIndex];
            if (string.IsNullOrEmpty(sq.PieceSymbol)) return;

            var layer = AdornerLayer.GetAdornerLayer(this);
            if (layer == null) return;

            _adorner = new PieceDragAdorner(this, sq.PieceSymbol, sq.PieceColor, pos);
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
