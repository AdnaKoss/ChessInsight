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

        // Margin se razlikuje po tipu figure — pješaci su manji
        private Thickness _pieceMargin = new(6);
        public Thickness PieceMargin
        {
            get => _pieceMargin;
            set { _pieceMargin = value; OnPropertyChanged(nameof(PieceMargin)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
