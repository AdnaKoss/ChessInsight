using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ChessInsight.UI.ViewModels
{
    public class SquareViewModel : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }

        private Brush _background = Brushes.Transparent;
        public Brush Background
        {
            get => _background;
            set { _background = value; OnPropertyChanged(nameof(Background)); }
        }

        private string _pieceSymbol = "";
        public string PieceSymbol
        {
            get => _pieceSymbol;
            set { _pieceSymbol = value; OnPropertyChanged(nameof(PieceSymbol)); }
        }

        private Brush _pieceColor = Brushes.White;
        public Brush PieceColor
        {
            get => _pieceColor;
            set { _pieceColor = value; OnPropertyChanged(nameof(PieceColor)); }
        }

        private Uri? _pieceSvgUri;
        public Uri? PieceSvgUri
        {
            get => _pieceSvgUri;
            set { _pieceSvgUri = value; OnPropertyChanged(nameof(PieceSvgUri)); }
        }

        private Thickness _pieceMargin = new(6);
        public Thickness PieceMargin
        {
            get => _pieceMargin;
            set { _pieceMargin = value; OnPropertyChanged(nameof(PieceMargin)); }
        }

        private bool _isLegalMove;
        public bool IsLegalMove
        {
            get => _isLegalMove;
            set { _isLegalMove = value; OnPropertyChanged(nameof(IsLegalMove)); }
        }

        private bool _isLegalCapture;
        public bool IsLegalCapture
        {
            get => _isLegalCapture;
            set { _isLegalCapture = value; OnPropertyChanged(nameof(IsLegalCapture)); }
        }

        private bool _isLastMove;
        public bool IsLastMove
        {
            get => _isLastMove;
            set { _isLastMove = value; OnPropertyChanged(nameof(IsLastMove)); }
        }

        private string _rankLabel = "";
        public string RankLabel
        {
            get => _rankLabel;
            set { _rankLabel = value; OnPropertyChanged(nameof(RankLabel)); }
        }

        private string _fileLabel = "";
        public string FileLabel
        {
            get => _fileLabel;
            set { _fileLabel = value; OnPropertyChanged(nameof(FileLabel)); }
        }

        private Brush _coordColor = Brushes.Transparent;
        public Brush CoordColor
        {
            get => _coordColor;
            set { _coordColor = value; OnPropertyChanged(nameof(CoordColor)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
