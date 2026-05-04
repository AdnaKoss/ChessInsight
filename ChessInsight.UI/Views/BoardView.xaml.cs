using System.Windows;
using System.Windows.Controls;
using ChessInsight.UI.ViewModels;

namespace ChessInsight.UI.Views
{
    public partial class BoardView : UserControl
    {
        public BoardView()
        {
            InitializeComponent();
        }

        private void Square_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int index)
                (DataContext as BoardViewModel)?.OnSquareClicked(index);
        }
    }
}
