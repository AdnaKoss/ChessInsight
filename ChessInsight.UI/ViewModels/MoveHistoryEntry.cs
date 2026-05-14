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
            set { _blackMove = value; Notify(nameof(BlackMove)); }
        }

        private bool _isWhiteActive;
        public bool IsWhiteActive
        {
            get => _isWhiteActive;
            set { _isWhiteActive = value; Notify(nameof(IsWhiteActive)); }
        }

        private bool _isBlackActive;
        public bool IsBlackActive
        {
            get => _isBlackActive;
            set { _isBlackActive = value; Notify(nameof(IsBlackActive)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
