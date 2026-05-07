using System.Windows;
using System.Windows.Controls;
using ChessInsight.Core.Enums;

namespace ChessInsight.UI.Views
{
    public partial class PromotionDialog : Window
    {
        public PieceType SelectedPiece { get; private set; } = PieceType.Queen;

        public PromotionDialog(PieceColor color)
        {
            InitializeComponent();

            bool isWhite = color == PieceColor.White;
            BtnQueen.Content  = isWhite ? "♕" : "♛";
            BtnRook.Content   = isWhite ? "♖" : "♜";
            BtnBishop.Content = isWhite ? "♗" : "♝";
            BtnKnight.Content = isWhite ? "♘" : "♞";
        }

        private void Piece_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                SelectedPiece = btn.Tag?.ToString() switch
                {
                    "Queen"  => PieceType.Queen,
                    "Rook"   => PieceType.Rook,
                    "Bishop" => PieceType.Bishop,
                    "Knight" => PieceType.Knight,
                    _        => PieceType.Queen
                };
                DialogResult = true;
                Close();
            }
        }
    }
}
