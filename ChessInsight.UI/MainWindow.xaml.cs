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

            // Auto-scroll istorije poteza na najnoviji potez
            _vm.MoveHistory.CollectionChanged += (_, _) =>
                Dispatcher.BeginInvoke(() => MoveHistoryScroller.ScrollToEnd());
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

        private void BtnFlip_Click(object sender, RoutedEventArgs e) =>
            _vm.FlipBoard();

        private void BtnReset_Click(object sender, RoutedEventArgs e) =>
            _vm.Reset();

        private void BtnSetupPosition_Click(object sender, RoutedEventArgs e)
        {
            // Position editor — dolazi u kasnijem koraku
        }
    }
}
