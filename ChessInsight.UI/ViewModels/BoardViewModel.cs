using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Media;
using ChessInsight.Core;
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

        // ── Oznake redova i kolona (dinamične pri flipu) ─────────
        [ObservableProperty] private List<string> _rowLabels = new();
        [ObservableProperty] private List<string> _colLabels = new();

        // ── Kolekcija polja ─────────────────────────────────────
        public ObservableCollection<SquareViewModel> Squares { get; } = new();

        // ── Istorija poteza ─────────────────────────────────────
        public ObservableCollection<MoveHistoryEntry> MoveHistory { get; } = new();

        // ── Events ─────────────────────────────────────────────
        public event Func<PieceColor, PieceType>? PromotionRequired;

        private const int AnalysisDepth = 4;

        private bool IsGameOver =>
            _gameState.Status is GameStatus.Checkmate or GameStatus.Stalemate or GameStatus.Draw;

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
            UpdateLabels();
            RefreshBoard();
        }

        // ── Učitaj FEN poziciju ─────────────────────────────────

        public void LoadFen(string fen)
        {
            _gameState = FenParser.Parse(fen);
            _selectedIndex = null;
            _selectedPieceMoves.Clear();

            MoveHistory.Clear();

            ScoreText = "—"; ScoreLabelText = "—";
            Move1Text = "—"; Score1Text = "";
            Move2Text = "—"; Score2Text = "";
            Move3Text = "—"; Score3Text = "";
            DepthText = "—"; NodesText = "—"; TimeText = "—";

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

            GameStatusText = _gameState.Status switch
            {
                GameStatus.Checkmate => _gameState.CurrentPlayer == PieceColor.White
                    ? "Šah-mat! Crni pobjeđuje."
                    : "Šah-mat! Bijeli pobjeđuje.",
                GameStatus.Stalemate => "Pat — Remi",
                GameStatus.Draw      => "Remi (pravilo 50 poteza)",
                GameStatus.Check     => _gameState.CurrentPlayer == PieceColor.White
                    ? "Bijeli je u šahu!"
                    : "Crni je u šahu!",
                _                    => _gameState.CurrentPlayer == PieceColor.White
                    ? "Bijeli na potezu"
                    : "Crni na potezu"
            };
        }

        // ── Klik na polje ───────────────────────────────────────

        public void OnSquareClicked(int visualIndex)
        {
            if (IsGameOver || IsAnalyzing) return;

            var (clickRow, clickCol) = ToBoardCoords(visualIndex);
            var piece = _gameState.Board.GetPiece(new Square(clickRow, clickCol));

            if (_selectedIndex.HasValue)
            {
                var legalMove = _selectedPieceMoves.FirstOrDefault(m =>
                    m.To.Row == clickRow && m.To.Column == clickCol);

                if (legalMove != null)
                {
                    legalMove = ResolvePromotion(legalMove, clickRow, clickCol);
                    CommitMove(legalMove);
                    return;
                }

                ClearHighlights();
                _selectedIndex = null;
                _selectedPieceMoves.Clear();

                if (piece == null || piece.Color != _gameState.CurrentPlayer) return;
            }

            if (piece != null && piece.Color == _gameState.CurrentPlayer)
                SelectPiece(visualIndex, clickRow, clickCol);
        }

        // ── Drag — API za BoardView ──────────────────────────────

        /// <summary>Poziva se kad drag počne — selektuje figuru i highlightuje legalne poteze.</summary>
        public void OnDragStart(int fromVisualIndex)
        {
            if (IsGameOver || IsAnalyzing) return;

            var (row, col) = ToBoardCoords(fromVisualIndex);
            var piece = _gameState.Board.GetPiece(new Square(row, col));
            if (piece == null || piece.Color != _gameState.CurrentPlayer) return;

            ClearHighlights();
            SelectPiece(fromVisualIndex, row, col);

            // Sakrij figuru na izvornom polju — vidjet će se samo kao adorner
            Squares[fromVisualIndex].PieceSymbol = "";
        }

        /// <summary>Poziva se kad drag završi na validnom cilju.</summary>
        public void TryApplyDragMove(int fromVisualIndex, int toVisualIndex)
        {
            if (IsGameOver || IsAnalyzing) return;

            var (toRow, toCol) = ToBoardCoords(toVisualIndex);

            var legalMove = _selectedPieceMoves.FirstOrDefault(m =>
                m.To.Row == toRow && m.To.Column == toCol);

            if (legalMove != null)
            {
                legalMove = ResolvePromotion(legalMove, toRow, toCol);
                CommitMove(legalMove);
            }
            else
            {
                CancelDrag();
            }
        }

        /// <summary>Poziva se kad drag završi na nevalidnom polju — vraća stanje.</summary>
        public void CancelDrag()
        {
            _selectedIndex = null;
            _selectedPieceMoves.Clear();
            RefreshBoard();  // Vraća originalnu figuru i briše highlighte
        }

        // ── Zajednički interna logika ────────────────────────────

        private void SelectPiece(int visualIndex, int row, int col)
        {
            _selectedIndex = visualIndex;
            Squares[visualIndex].Background = BrSelected;

            var allMoves = _generator.GetLegalMoves(_gameState);
            _selectedPieceMoves = allMoves
                .Where(m => m.From.Row == row && m.From.Column == col)
                .ToList();

            foreach (var m in _selectedPieceMoves)
                Squares[ToVisualIndex(m.To.Row, m.To.Column)].Background = BrLegal;
        }

        private Move ResolvePromotion(Move move, int toRow, int toCol)
        {
            if (move.Type != MoveType.PawnPromotion) return move;

            var chosen = PromotionRequired?.Invoke(_gameState.CurrentPlayer) ?? PieceType.Queen;
            return _selectedPieceMoves.First(m =>
                m.To.Row == toRow && m.To.Column == toCol &&
                m.Type == MoveType.PawnPromotion &&
                m.PromotionPiece == chosen);
        }

        private void CommitMove(Move move)
        {
            var stateBefore = _gameState;
            _gameState = _gameState.ApplyMove(move);

            var nextMoves = _generator.GetLegalMoves(_gameState);
            _gameState.UpdateStatus(nextMoves);

            string notation = FormatMoveNotation(stateBefore, move);
            RecordMove(notation, stateBefore.CurrentPlayer, stateBefore.FullMoveNumber);

            _selectedIndex = null;
            _selectedPieceMoves.Clear();
            RefreshBoard();
        }

        // ── Historija poteza — snimanje ─────────────────────────

        private void RecordMove(string notation, PieceColor player, int moveNumber)
        {
            if (player == PieceColor.White)
            {
                MoveHistory.Add(new MoveHistoryEntry
                {
                    Number    = moveNumber,
                    WhiteMove = notation
                });
            }
            else
            {
                if (MoveHistory.Count > 0)
                    MoveHistory[^1].BlackMove = notation;
                else
                    MoveHistory.Add(new MoveHistoryEntry
                    {
                        Number    = moveNumber,
                        BlackMove = notation
                    });
            }
        }

        // ── SAN notacija ────────────────────────────────────────

        private string FormatMoveNotation(GameState before, Move move)
        {
            if (move.Type == MoveType.CastleKingside)
                return CheckSuffix("O-O");
            if (move.Type == MoveType.CastleQueenside)
                return CheckSuffix("O-O-O");

            var piece = before.Board.GetPiece(move.From)!;
            bool isCapture = move.Type is MoveType.Capture or MoveType.EnPassant;
            var sb = new StringBuilder();

            if (piece.Type == PieceType.Pawn)
            {
                if (isCapture)
                    sb.Append((char)('a' + move.From.Column)).Append('x');
                sb.Append(move.To.ToAlgebraic());
                if (move.PromotionPiece.HasValue)
                    sb.Append('=').Append(PieceLetter(move.PromotionPiece.Value));
            }
            else
            {
                sb.Append(PieceLetter(piece.Type));

                // Disambiguacija — provjeri ima li drugih figura iste vrste koje mogu na isto polje
                var ambiguous = before.Board.GetPieces(piece.Color)
                    .Where(p => p.Type == piece.Type && !p.Position.Equals(move.From))
                    .Where(p => _generator.GetLegalMovesForPiece(p, before)
                                          .Any(m => m.To.Equals(move.To)))
                    .ToList();

                if (ambiguous.Count > 0)
                {
                    bool sameFile = ambiguous.Any(p => p.Position.Column == move.From.Column);
                    bool sameRank = ambiguous.Any(p => p.Position.Row == move.From.Row);

                    if (!sameFile)        sb.Append((char)('a' + move.From.Column));
                    else if (!sameRank)   sb.Append(move.From.Row + 1);
                    else                  sb.Append(move.From.ToAlgebraic());
                }

                if (isCapture) sb.Append('x');
                sb.Append(move.To.ToAlgebraic());
            }

            return CheckSuffix(sb.ToString());
        }

        // Status je već ažuriran na _gameState prije poziva
        private string CheckSuffix(string notation)
        {
            if (_gameState.Status == GameStatus.Checkmate) return notation + "#";
            if (_gameState.Status == GameStatus.Check)     return notation + "+";
            return notation;
        }

        private static string PieceLetter(PieceType type) => type switch
        {
            PieceType.Knight => "S",  // Skakač
            PieceType.Bishop => "L",  // Lovac
            PieceType.Rook   => "T",  // Top
            PieceType.Queen  => "D",  // Dama
            PieceType.King   => "K",  // Kralj
            _                => ""
        };

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

            if (topMoves.Count > 0)
            {
                ScoreText      = FormatScore(topMoves[0].Score);
                ScoreLabelText = GetScoreLabel(topMoves[0].Score);

                var best = topMoves[0].BestMove;
                if (best != null)
                {
                    Squares[ToVisualIndex(best.From.Row, best.From.Column)].Background = BrBest;
                    Squares[ToVisualIndex(best.To.Row,   best.To.Column)  ].Background = BrBest;
                }
            }

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

            MoveHistory.Clear();

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
            UpdateLabels();
            RefreshBoard();
        }

        // ── Dinamične oznake redova/kolona ──────────────────────

        private void UpdateLabels()
        {
            RowLabels = IsFlipped
                ? new List<string> { "1", "2", "3", "4", "5", "6", "7", "8" }
                : new List<string> { "8", "7", "6", "5", "4", "3", "2", "1" };

            ColLabels = IsFlipped
                ? new List<string> { "h", "g", "f", "e", "d", "c", "b", "a" }
                : new List<string> { "a", "b", "c", "d", "e", "f", "g", "h" };
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

        // ── Formatiranje (za analitički panel) ─────────────────

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
