using System.Windows;
using System.Windows.Input;
using ChessInsight.UI.ViewModels;

namespace ChessInsight.UI
{
    public partial class MainWindow : Window
    {
        private readonly BoardViewModel _vm = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            BtnAnalyze.IsEnabled = false;
            await _vm.AnalyzeAsync();
            BtnAnalyze.IsEnabled = true;
        }

        private void BtnLoadFen_Click(object sender, RoutedEventArgs e)
        {
            // FEN parser — dolazi u sljedećem koraku
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
