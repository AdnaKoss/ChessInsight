using System.ComponentModel;

namespace ChessInsight.UI.ViewModels
{
    public class MoveHistoryEntry : INotifyPropertyChanged
    {
        public int Number { get; set; }
        public string WhiteMove { get; set; } = "";

        private string _blackMove = "";
        public string BlackMove
        {
            get => _blackMove;
            set { _blackMove = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BlackMove))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
