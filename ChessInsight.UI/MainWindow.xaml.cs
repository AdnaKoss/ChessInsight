using System.Windows;
using System.Windows.Input;
using ChessInsight.Core.Enums;
using ChessInsight.UI.ViewModels;
using ChessInsight.UI.Views;

namespace ChessInsight.UI
{
    public partial class MainWindow : Window
    {
        private readonly BoardViewModel _vm = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            // Kad pješak stigne na zadnji red, otvori dijalog za izbor figure
            _vm.PromotionRequired += color =>
            {
                var dialog = new PromotionDialog(color) { Owner = this };
                dialog.ShowDialog();
                return dialog.SelectedPiece;
            };

            // Skroluj na vrh kad se historija promijeni (najnoviji potez je uvijek gore)
            _vm.MoveHistory.CollectionChanged += (_, _) =>
                Dispatcher.BeginInvoke(() => MoveHistoryScroller.ScrollToTop());
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e) =>
            _vm.ToggleAutoAnalysis();

        private void BtnLoadFen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _vm.LoadFen(TxtFen.Text.Trim());
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(
                    $"Nevažeći FEN string:\n\n{ex.Message}",
                    "Greška pri učitavanju",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void TxtFen_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnLoadFen_Click(sender, e);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) =>
            _vm.GoBack();

        private void BtnForward_Click(object sender, RoutedEventArgs e) =>
            _vm.GoForward();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Ignoriši dok je FEN TextBox fokusiran
            if (TxtFen.IsFocused) return;

            if (e.Key == Key.Left)  { _vm.GoBack();    e.Handled = true; }
            if (e.Key == Key.Right) { _vm.GoForward(); e.Handled = true; }
        }

        private void BtnFlip_Click(object sender, RoutedEventArgs e) =>
            _vm.FlipBoard();

        private void BtnReset_Click(object sender, RoutedEventArgs e) =>
            _vm.Reset();

        private void BtnSetupPosition_Click(object sender, RoutedEventArgs e)
        {
            var gs     = _vm.CurrentGameState;
            var dialog = new PositionEditorDialog { Owner = this };
            dialog.LoadCurrentPosition(gs.Board, gs.CurrentPlayer);

            if (dialog.ShowDialog() == true && dialog.ResultFen != null)
            {
                TxtFen.Text = dialog.ResultFen;
                try
                {
                    _vm.LoadFen(dialog.ResultFen);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(
                        $"Greška pri učitavanju pozicije:\n\n{ex.Message}",
                        "Greška",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
    }
}
