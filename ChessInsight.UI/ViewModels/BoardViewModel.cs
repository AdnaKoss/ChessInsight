using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using ChessInsight.Engine;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChessInsight.UI.ViewModels
{
    public partial class BoardViewModel : ObservableObject
    {
        // ── Engine ──────────────────────────────────────────────
        private readonly AlphaBeta _engine = new();
        private readonly MoveGenerator _generator = new();

        // ── Stanje ─────────────────────────────────────────────
        private GameState _gameState = new();
        private int? _selectedIndex = null;
        private List<Move> _selectedPieceMoves = new();

        [ObservableProperty] private bool _isFlipped = false;
        [ObservableProperty] private bool _isAnalyzing = false;

        // ── Panel — evaluacija ───────────────────────────────────
        [ObservableProperty] private string _scoreText = "0.00";
        [ObservableProperty] private string _scoreLabelText = "Ravnopravno";
        [ObservableProperty] private string _gameStatusText = "Bijeli na potezu";

        // ── Panel — top 3 poteza ────────────────────────────────
        [ObservableProperty] private string _move1Text = "—";
        [ObservableProperty] private string _move2Text = "—";
        [ObservableProperty] private string _move3Text = "—";
        [ObservableProperty] private string _score1Text = "";
        [ObservableProperty] private string _score2Text = "";
        [ObservableProperty] private string _score3Text = "";

        // ── Panel — statistike ──────────────────────────────────
        [ObservableProperty] private string _depthText = "—";
        [ObservableProperty] private string _nodesText = "—";
        [ObservableProperty] private string _timeText = "—";

        // ── Kolekcija polja ─────────────────────────────────────
        public ObservableCollection<SquareViewModel> Squares { get; } = new();

        private const int AnalysisDepth = 4;

        // ── Statički brushevi ───────────────────────────────────
        private static readonly Brush BrLight    = new SolidColorBrush(Color.FromRgb(0xF0, 0xD9, 0xB5));
        private static readonly Brush BrDark     = new SolidColorBrush(Color.FromRgb(0xB5, 0x88, 0x63));
        private static readonly Brush BrLegal    = new SolidColorBrush(Color.FromArgb(210, 0xC8, 0xA8, 0x4B));
        private static readonly Brush BrBest     = new SolidColorBrush(Color.FromArgb(210, 0x4C, 0xAF, 0x50));
        private static readonly Brush BrSelected = new SolidColorBrush(Color.FromArgb(255, 0x60, 0x90, 0xC0));
        private static readonly Brush BrWhite    = Brushes.WhiteSmoke;
        private static readonly Brush BrBlack    = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));

        // ── Konstruktor ─────────────────────────────────────────

        public BoardViewModel()
        {
            for (int i = 0; i < 64; i++)
                Squares.Add(new SquareViewModel { Index = i });
            RefreshBoard();
        }

        // ── Osvježi prikaz table ────────────────────────────────

        public void RefreshBoard()
        {
            for (int i = 0; i < 64; i++)
            {
                var (r, c) = ToBoardCoords(i);
                var piece = _gameState.Board.GetPiece(new Square(r, c));
                bool isLight = (r + c) % 2 == 1;

                Squares[i].Background  = isLight ? BrLight : BrDark;
                Squares[i].PieceSymbol = GetSymbol(piece);
                Squares[i].PieceColor  = piece?.Color == PieceColor.White ? BrWhite : BrBlack;
                Squares[i].Row         = r;
                Squares[i].Column      = c;
            }

            GameStatusText = _gameState.CurrentPlayer == PieceColor.White
                ? "Bijeli na potezu" : "Crni na potezu";
        }

        // ── Klik na polje ───────────────────────────────────────

        public void OnSquareClicked(int visualIndex)
        {
            var (clickRow, clickCol) = ToBoardCoords(visualIndex);
            var piece = _gameState.Board.GetPiece(new Square(clickRow, clickCol));

            // Ako je selektovano polje — provjeri klik na legalni potez
            if (_selectedIndex.HasValue)
            {
                var legalMove = _selectedPieceMoves.FirstOrDefault(m =>
                    m.To.Row == clickRow && m.To.Column == clickCol);

                if (legalMove != null)
                {
                    // Primijeni potez — u analitičkom modu korisnik može navigirati pozicijom
                    _gameState = _gameState.ApplyMove(legalMove);
                    _selectedIndex = null;
                    _selectedPieceMoves.Clear();
                    RefreshBoard();
                    return;
                }

                // Klik na polje koje nije legalni potez — deselektuj
                ClearHighlights();
                _selectedIndex = null;
                _selectedPieceMoves.Clear();

                // Ako je klik na vlastitu figuru — odmah je selektuj
                if (piece == null || piece.Color != _gameState.CurrentPlayer) return;
            }

            // Selektuj figuru ako je na potezu
            if (piece != null && piece.Color == _gameState.CurrentPlayer)
            {
                _selectedIndex = visualIndex;
                Squares[visualIndex].Background = BrSelected;

                var allMoves = _generator.GetLegalMoves(_gameState);
                _selectedPieceMoves = allMoves
                    .Where(m => m.From.Row == clickRow && m.From.Column == clickCol)
                    .ToList();

                foreach (var m in _selectedPieceMoves)
                    Squares[ToVisualIndex(m.To.Row, m.To.Column)].Background = BrLegal;
            }
        }

        // ── Analiza ─────────────────────────────────────────────

        public async Task AnalyzeAsync()
        {
            if (IsAnalyzing) return;
            IsAnalyzing = true;
            ClearHighlights();
            _selectedIndex = null;

            var sw = Stopwatch.StartNew();
            var topMoves = await Task.Run(() =>
                _engine.FindTopMoves(_gameState, AnalysisDepth, 3));
            sw.Stop();

            DepthText = AnalysisDepth.ToString();
            NodesText = topMoves.Count > 0 ? topMoves[0].NodesSearched.ToString("N0") : "0";
            TimeText  = $"{sw.Elapsed.TotalSeconds:F2}s";

            // Skor i label
            if (topMoves.Count > 0)
            {
                ScoreText     = FormatScore(topMoves[0].Score);
                ScoreLabelText = GetScoreLabel(topMoves[0].Score);

                // Highlight najboljeg poteza zelenom
                var best = topMoves[0].BestMove;
                if (best != null)
                {
                    Squares[ToVisualIndex(best.From.Row, best.From.Column)].Background = BrBest;
                    Squares[ToVisualIndex(best.To.Row,   best.To.Column)  ].Background = BrBest;
                }
            }

            // Top 3 prikaz
            Move1Text = "—"; Score1Text = "";
            Move2Text = "—"; Score2Text = "";
            Move3Text = "—"; Score3Text = "";

            for (int i = 0; i < topMoves.Count && i < 3; i++)
            {
                var r = topMoves[i];
                if (r.BestMove == null) continue;
                string mv = FormatMove(r.BestMove);
                string sc = FormatScore(r.Score);
                switch (i)
                {
                    case 0: Move1Text = mv; Score1Text = sc; break;
                    case 1: Move2Text = mv; Score2Text = sc; break;
                    case 2: Move3Text = mv; Score3Text = sc; break;
                }
            }

            IsAnalyzing = false;
        }

        // ── Reset ───────────────────────────────────────────────

        public void Reset()
        {
            _gameState = new GameState();
            _selectedIndex = null;
            _selectedPieceMoves.Clear();

            ScoreText = "0.00"; ScoreLabelText = "Ravnopravno";
            Move1Text = "—"; Score1Text = "";
            Move2Text = "—"; Score2Text = "";
            Move3Text = "—"; Score3Text = "";
            DepthText = "—"; NodesText = "—"; TimeText = "—";

            RefreshBoard();
        }

        // ── Okreni ploču ────────────────────────────────────────

        public void FlipBoard()
        {
            IsFlipped = !IsFlipped;
            ClearHighlights();
            _selectedIndex = null;
            RefreshBoard();
        }

        // ── Highlight helpers ───────────────────────────────────

        private void ClearHighlights()
        {
            for (int i = 0; i < 64; i++)
            {
                var (r, c) = ToBoardCoords(i);
                bool isLight = (r + c) % 2 == 1;
                Squares[i].Background = isLight ? BrLight : BrDark;
            }
        }

        // ── Koordinatni sistem ──────────────────────────────────

        private (int row, int col) ToBoardCoords(int visualIndex)
        {
            int vRow = visualIndex / 8;
            int vCol = visualIndex % 8;
            return IsFlipped ? (vRow, 7 - vCol) : (7 - vRow, vCol);
        }

        private int ToVisualIndex(int boardRow, int boardCol) =>
            IsFlipped
                ? boardRow * 8 + (7 - boardCol)
                : (7 - boardRow) * 8 + boardCol;

        // ── Formatiranje ────────────────────────────────────────

        private static string FormatMove(Move move) =>
            $"{SquareName(move.From)}-{SquareName(move.To)}";

        private static string SquareName(Square sq) =>
            $"{(char)('a' + sq.Column)}{sq.Row + 1}";

        private static string FormatScore(int cp)
        {
            if (cp > 9000)  return "+M";
            if (cp < -9000) return "-M";
            double p = cp / 100.0;
            return p >= 0 ? $"+{p:F2}" : $"{p:F2}";
        }

        private static string GetScoreLabel(int cp)
        {
            if (cp > 9000)  return "Bijeli daje mat";
            if (cp < -9000) return "Crni daje mat";
            int abs = Math.Abs(cp);
            string side = cp > 0 ? "Bijeli" : cp < 0 ? "Crni" : "";
            return abs switch
            {
                < 30  => "Ravnopravno",
                < 150 => $"{side} malo bolje",
                < 300 => $"{side} bolje",
                < 600 => $"{side} znatno bolje",
                _     => $"{side} pobjeđuje"
            };
        }

        // ── Unicode figure ──────────────────────────────────────

        private static string GetSymbol(Piece? p) => p switch
        {
            null                               => "",
            King   { Color: PieceColor.White } => "♔",
            King                               => "♚",
            Queen  { Color: PieceColor.White } => "♕",
            Queen                              => "♛",
            Rook   { Color: PieceColor.White } => "♖",
            Rook                               => "♜",
            Bishop { Color: PieceColor.White } => "♗",
            Bishop                             => "♝",
            Knight { Color: PieceColor.White } => "♘",
            Knight                             => "♞",
            Pawn   { Color: PieceColor.White } => "♙",
            Pawn                               => "♟",
            _                                  => ""
        };
    }
}
