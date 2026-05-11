using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Collections.Generic;
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

        // ── Historija za navigaciju ─────────────────────────────
        // _stateHistory[i] = stanje nakon i poteza (index 0 = početno stanje)
        // _moveLog[i]      = potez koji vodi iz stanja i u stanje i+1
        private readonly List<GameState> _stateHistory = new();
        private readonly List<(Move move, string san, PieceColor player, int moveNum)> _moveLog = new();
        private int _viewIndex = 0;

        [ObservableProperty] private bool _canGoBack    = false;
        [ObservableProperty] private bool _canGoForward = false;

        [ObservableProperty] private bool _isFlipped = false;
        [ObservableProperty] private bool _isAnalyzing = false;
        [ObservableProperty] private bool _isAutoAnalyzing = false;
        [ObservableProperty] private string _analyzeBtnText = "▶  KRENI ANALIZATOR";
        [ObservableProperty] private string _analyzingText = "";

        private bool _pendingAnalysis = false;
        private CancellationTokenSource? _analysisCts;

        // ── Panel — evaluacija ───────────────────────────────────
        [ObservableProperty] private string _scoreText = "0.00";
        [ObservableProperty] private string _scoreLabelText = "Ravnopravno";
        [ObservableProperty] private string _gameStatusText = "Bijeli na potezu";
        [ObservableProperty] private Brush  _sideToMoveFill = Brushes.White;

        // ── Eval bar (bijeli % od dna, crni % od vrha) ──────────
        [ObservableProperty] private double _evalBarWhitePct = 50.0;
        [ObservableProperty] private double _evalBarBlackPct = 50.0;

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

        // ── Pristup trenutnom stanju (za editor pozicije) ───────
        public GameState CurrentGameState => _gameState;

        private const int AnalysisDepth = 6;

        private bool IsGameOver =>
            _gameState.Status is GameStatus.Checkmate or GameStatus.Stalemate or GameStatus.Draw;

        // ── Statički brushevi ───────────────────────────────────
        private static readonly Brush BrLight    = new SolidColorBrush(Color.FromRgb(0xBA, 0xBA, 0xBA));
        private static readonly Brush BrDark     = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
        private static readonly Brush BrLegal    = new SolidColorBrush(Color.FromArgb(210, 0xC8, 0xA8, 0x4B));
        private static readonly Brush BrBest     = new SolidColorBrush(Color.FromArgb(210, 0x1D, 0x9E, 0x75));
        private static readonly Brush BrSelected = new SolidColorBrush(Color.FromArgb(255, 0x85, 0xB7, 0xEB));
        private static readonly Brush BrWhite    = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
        private static readonly Brush BrBlack    = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16));

        private static readonly System.Windows.Thickness MarginPiece      = new(5);
        private static readonly System.Windows.Thickness MarginRookKnight = new(8);
        private static readonly System.Windows.Thickness MarginPawn       = new(9);

        // ── Konstruktor ─────────────────────────────────────────

        public BoardViewModel()
        {
            for (int i = 0; i < 64; i++)
                Squares.Add(new SquareViewModel { Index = i });
            _stateHistory.Add(_gameState);
            UpdateLabels();
            RefreshBoard();
        }

        // ── Učitaj FEN poziciju ─────────────────────────────────

        public void LoadFen(string fen)
        {
            StopAutoAnalysis();
            _gameState = FenParser.Parse(fen);
            _selectedIndex = null;
            _selectedPieceMoves.Clear();
            ResetHistory();

            ScoreText = "—"; ScoreLabelText = "—";
            Move1Text = "—"; Score1Text = "";
            Move2Text = "—"; Score2Text = "";
            Move3Text = "—"; Score3Text = "";
            DepthText = "—"; NodesText = "—"; TimeText = "—";

            RefreshBoard();
        }

        // ── Učitaj PGN partiju ──────────────────────────────────

        public void LoadPgn(string pgn)
        {
            var moves = ParsePgnMoves(pgn);
            if (moves.Count == 0)
                throw new ArgumentException("Nije pronađen nijedan potez u PGN tekstu.");

            StopAutoAnalysis();

            // PGN može imati [FEN "..."] tag za nestandardni početak
            var fenTag = Regex.Match(pgn, @"\[FEN\s+""([^""]+)""\]");
            GameState startState = fenTag.Success
                ? FenParser.Parse(fenTag.Groups[1].Value)
                : new GameState();

            _gameState = startState;
            _selectedIndex = null;
            _selectedPieceMoves.Clear();

            _stateHistory.Clear();
            _stateHistory.Add(_gameState);
            _moveLog.Clear();
            _viewIndex = 0;
            MoveHistory.Clear();

            foreach (var san in moves)
            {
                var move = FindMoveBySan(san, _gameState);
                if (move == null)
                    throw new ArgumentException($"Nepoznat potez: \"{san}\"");
                FastCommitMove(move);
            }

            // Postavi navigaciju na početak
            _viewIndex = 0;
            _gameState = _stateHistory[0];
            _selectedIndex = null;
            _selectedPieceMoves.Clear();

            ScoreText = "—"; ScoreLabelText = "—";
            Move1Text = "—"; Score1Text = "";
            Move2Text = "—"; Score2Text = "";
            Move3Text = "—"; Score3Text = "";
            DepthText = "—"; NodesText = "—"; TimeText = "—";

            RebuildMoveHistory();
            RefreshBoard();
            UpdateCanNavigate();
        }

        private void FastCommitMove(Move move)
        {
            var stateBefore = _gameState;
            _gameState = _gameState.ApplyMove(move);
            var nextMoves = _generator.GetLegalMoves(_gameState);
            _gameState.UpdateStatus(nextMoves);
            string notation = FormatMoveNotation(stateBefore, move);
            _stateHistory.Add(_gameState);
            _moveLog.Add((move, notation, stateBefore.CurrentPlayer, stateBefore.FullMoveNumber));
            _viewIndex++;
        }

        private Move? FindMoveBySan(string san, GameState state)
        {
            var legalMoves = _generator.GetLegalMoves(state);

            // Strip check/mate markers
            san = san.TrimEnd('+', '#');
            // Strip NAGs ($1, $2 …)
            san = Regex.Replace(san, @"\$\d+", "").Trim();

            if (san.Length == 0) return null;

            // Castling (accept both O and 0)
            if (san is "O-O-O" or "0-0-0")
                return legalMoves.FirstOrDefault(m => m.Type == MoveType.CastleQueenside);
            if (san is "O-O" or "0-0")
                return legalMoves.FirstOrDefault(m => m.Type == MoveType.CastleKingside);

            // Promotion: =Q or trailing Q without '='
            PieceType? promotionPiece = null;
            if (san.Length >= 2 && san[^1] is 'Q' or 'R' or 'B' or 'N')
            {
                if (san.Length >= 3 && san[^2] == '=')
                {
                    promotionPiece = PgnLetterToPiece(san[^1]);
                    san = san[..^2];
                }
                else if (san.Length >= 3 && char.IsDigit(san[^2]))
                {
                    // e8Q (no equals sign)
                    promotionPiece = PgnLetterToPiece(san[^1]);
                    san = san[..^1];
                }
            }

            // Destination square — always the last 2 chars
            if (san.Length < 2) return null;
            string destStr = san[^2..];
            int destCol = destStr[0] - 'a';
            int destRow = destStr[1] - '1';
            if (destCol < 0 || destCol > 7 || destRow < 0 || destRow > 7) return null;
            san = san[..^2];

            // Strip capture marker
            if (san.Length > 0 && san[^1] == 'x')
                san = san[..^1];

            // Piece type — uppercase K Q R B N; else pawn
            PieceType pieceType = PieceType.Pawn;
            if (san.Length > 0 && san[0] is 'K' or 'Q' or 'R' or 'B' or 'N')
            {
                pieceType = PgnLetterToPiece(san[0]);
                san = san[1..];
            }

            // Remaining chars are disambiguation (file, rank, or both)
            string disambig = san;

            return legalMoves.FirstOrDefault(m =>
            {
                if (m.To.Row != destRow || m.To.Column != destCol) return false;

                var piece = state.Board.GetPiece(m.From);
                if (piece == null || piece.Type != pieceType) return false;

                if (promotionPiece.HasValue)
                {
                    if (m.Type != MoveType.PawnPromotion || m.PromotionPiece != promotionPiece) return false;
                }

                if (disambig.Length > 0)
                {
                    int idx = 0;
                    if (idx < disambig.Length && char.IsLower(disambig[idx]))
                    {
                        if (m.From.Column != disambig[idx] - 'a') return false;
                        idx++;
                    }
                    if (idx < disambig.Length && char.IsDigit(disambig[idx]))
                    {
                        if (m.From.Row != disambig[idx] - '1') return false;
                    }
                }

                return true;
            });
        }

        private static PieceType PgnLetterToPiece(char c) => c switch
        {
            'K' => PieceType.King,
            'Q' => PieceType.Queen,
            'R' => PieceType.Rook,
            'B' => PieceType.Bishop,
            'N' => PieceType.Knight,
            _   => PieceType.Pawn
        };

        private static List<string> ParsePgnMoves(string pgn)
        {
            // Remove headers [Tag "value"]
            pgn = Regex.Replace(pgn, @"\[[^\]]*\]", "");
            // Remove comments { ... }
            pgn = Regex.Replace(pgn, @"\{[^}]*\}", "");
            // Remove variations — repeat to handle up to 5 levels of nesting
            for (int i = 0; i < 6; i++)
                pgn = Regex.Replace(pgn, @"\([^()]*\)", "");
            // Remove NAGs
            pgn = Regex.Replace(pgn, @"\$\d+", "");
            // Remove result markers
            pgn = Regex.Replace(pgn, @"\b(1-0|0-1|1/2-1/2|\*)\s*$", "");
            // Remove move numbers (1. 1... 12.)
            pgn = Regex.Replace(pgn, @"\d+\.+", "");

            return pgn
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 0 && char.IsLetter(t[0]))
                .ToList();
        }

        // ── Osvježi prikaz table ────────────────────────────────

        public void RefreshBoard()
        {
            for (int i = 0; i < 64; i++)
            {
                var (r, c) = ToBoardCoords(i);
                var piece = _gameState.Board.GetPiece(new Square(r, c));
                bool isLight = (r + c) % 2 == 1;

                Squares[i].Background     = isLight ? BrLight : BrDark;
                Squares[i].IsLegalMove    = false;
                Squares[i].IsLegalCapture = false;
                Squares[i].PieceSymbol    = GetSymbol(piece);
                Squares[i].PieceColor   = piece?.Color == PieceColor.White ? BrWhite : BrBlack;
                Squares[i].PieceSvgUri  = GetSvgUri(piece);
                Squares[i].PieceMargin  = piece?.Type switch
                {
                    PieceType.Pawn             => MarginPawn,
                    PieceType.Rook or
                    PieceType.Knight           => MarginRookKnight,
                    _                          => MarginPiece
                };
                Squares[i].Row          = r;
                Squares[i].Column       = c;
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

            SideToMoveFill = _gameState.CurrentPlayer == PieceColor.White
                ? new SolidColorBrush(Color.FromRgb(0xF0, 0xD9, 0xB5))
                : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
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

            // Figura ostaje vidljiva na izvoru — adorner prati kursor kao kopija
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
            {
                int vi = ToVisualIndex(m.To.Row, m.To.Column);
                if (m.Type is MoveType.Capture or MoveType.EnPassant)
                    Squares[vi].IsLegalCapture = true;
                else
                    Squares[vi].IsLegalMove = true;
            }
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

            // Obreži buduće stanje ako idemo u novu varijantu
            if (_viewIndex < _stateHistory.Count - 1)
            {
                _stateHistory.RemoveRange(_viewIndex + 1, _stateHistory.Count - _viewIndex - 1);
                _moveLog.RemoveRange(_viewIndex, _moveLog.Count - _viewIndex);
            }

            _stateHistory.Add(_gameState);
            _moveLog.Add((move, notation, stateBefore.CurrentPlayer, stateBefore.FullMoveNumber));
            _viewIndex++;

            _selectedIndex = null;
            _selectedPieceMoves.Clear();
            RebuildMoveHistory();
            RefreshBoard();
            UpdateCanNavigate();

            if (IsAutoAnalyzing && !IsGameOver)
            {
                if (IsAnalyzing) _pendingAnalysis = true;
                else _ = AnalyzeAsync();
            }
        }

        // ── Auto-analizator — start/stop toggle ─────────────────

        public void ToggleAutoAnalysis()
        {
            if (IsAutoAnalyzing)
                StopAutoAnalysis();
            else
            {
                IsAutoAnalyzing = true;
                AnalyzeBtnText  = "■  STOP ANALIZATOR";
                if (!IsAnalyzing && !IsGameOver)
                    _ = AnalyzeAsync();
            }
        }

        private void StopAutoAnalysis()
        {
            IsAutoAnalyzing = false;
            AnalyzeBtnText  = "▶  KRENI ANALIZATOR";
            _analysisCts?.Cancel();
        }

        // ── Navigacija kroz historiju ───────────────────────────

        public void GoBack()
        {
            if (_viewIndex <= 0 || IsAnalyzing) return;
            _viewIndex--;
            _gameState = _stateHistory[_viewIndex];
            _selectedIndex = null;
            _selectedPieceMoves.Clear();
            RebuildMoveHistory();
            RefreshBoard();
            UpdateCanNavigate();
            if (IsAutoAnalyzing && !IsGameOver) _ = AnalyzeAsync();
        }

        public void GoForward()
        {
            if (_viewIndex >= _stateHistory.Count - 1 || IsAnalyzing) return;
            _viewIndex++;
            _gameState = _stateHistory[_viewIndex];
            _selectedIndex = null;
            _selectedPieceMoves.Clear();
            RebuildMoveHistory();
            RefreshBoard();
            UpdateCanNavigate();
            if (IsAutoAnalyzing && !IsGameOver) _ = AnalyzeAsync();
        }

        private void UpdateCanNavigate()
        {
            CanGoBack    = _viewIndex > 0 && !IsAnalyzing;
            CanGoForward = _viewIndex < _stateHistory.Count - 1 && !IsAnalyzing;
        }

        partial void OnIsAnalyzingChanged(bool value) => UpdateCanNavigate();

        // ── Rebuild prikaza historije (najnoviji prvi) ──────────

        private void RebuildMoveHistory()
        {
            MoveHistory.Clear();

            var entries = new Dictionary<int, MoveHistoryEntry>();
            for (int i = 0; i < _viewIndex; i++)
            {
                var (_, san, player, moveNum) = _moveLog[i];
                if (!entries.TryGetValue(moveNum, out var entry))
                {
                    entry = new MoveHistoryEntry { Number = moveNum };
                    entries[moveNum] = entry;
                }
                if (player == PieceColor.White) entry.WhiteMove = san;
                else entry.BlackMove = san;
            }

            foreach (var e in entries.Values.OrderByDescending(x => x.Number))
                MoveHistory.Add(e);
        }

        // ── Reset historije ─────────────────────────────────────

        private void ResetHistory()
        {
            _stateHistory.Clear();
            _stateHistory.Add(_gameState);
            _moveLog.Clear();
            _viewIndex = 0;
            MoveHistory.Clear();
            UpdateCanNavigate();
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

            _analysisCts?.Dispose();
            _analysisCts = new CancellationTokenSource();
            var token = _analysisCts.Token;

            _pendingAnalysis = false;
            IsAnalyzing      = true;
            AnalyzingText    = $"Analiziram... (dub. 0/{AnalysisDepth})";
            ClearHighlights();
            _selectedIndex = null;

            var snapshot = _gameState;
            var sw = Stopwatch.StartNew();

            var progress = new Progress<(int depth, List<SearchResult> results)>(update =>
            {
                if (token.IsCancellationRequested) return;
                if (!ReferenceEquals(snapshot, _gameState)) return;
                var (d, res) = update;
                AnalyzingText = $"Analiziram... (dub. {d}/{AnalysisDepth})";
                ApplyAnalysisResults(snapshot, res, d, sw.Elapsed.TotalSeconds);
            });

            // Napravi historiju pozicija iz stvarne igre (za detekciju ponavljanja)
            var gameHistory = new Dictionary<ulong, int>();
            for (int i = 0; i <= _viewIndex; i++)
            {
                ulong h = Zobrist.Compute(_stateHistory[i]);
                gameHistory.TryGetValue(h, out int c);
                gameHistory[h] = c + 1;
            }

            List<SearchResult> topMoves;
            try
            {
                topMoves = await Task.Run(() =>
                    _engine.FindTopMovesIterative(snapshot, AnalysisDepth, 3, progress, token, gameHistory));
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                IsAnalyzing   = false;
                AnalyzingText = "";
                return;
            }

            sw.Stop();
            IsAnalyzing   = false;
            AnalyzingText = "";

            if (token.IsCancellationRequested || !ReferenceEquals(snapshot, _gameState))
            {
                if (_pendingAnalysis && IsAutoAnalyzing && !IsGameOver)
                    _ = AnalyzeAsync();
                return;
            }

            // Označi najbolji potez na tabli
            if (topMoves.Count > 0 && topMoves[0].BestMove != null)
            {
                var best = topMoves[0].BestMove!;
                Squares[ToVisualIndex(best.From.Row, best.From.Column)].Background = BrBest;
                Squares[ToVisualIndex(best.To.Row,   best.To.Column)  ].Background = BrBest;
            }

            if (_pendingAnalysis && IsAutoAnalyzing && !IsGameOver)
                _ = AnalyzeAsync();
        }

        private void ApplyAnalysisResults(GameState snapshot, List<SearchResult> results,
                                          int depth, double elapsedSec)
        {
            DepthText = $"{depth}/{AnalysisDepth}";
            TimeText  = $"{elapsedSec:F2}s";
            NodesText = results.Count > 0 ? results[0].NodesSearched.ToString("N0") : "0";

            if (results.Count > 0)
            {
                ScoreText      = FormatScore(results[0].Score);
                ScoreLabelText = GetScoreLabel(results[0].Score);
                UpdateEvalBar(results[0].Score);
            }

            Move1Text = "—"; Score1Text = "";
            Move2Text = "—"; Score2Text = "";
            Move3Text = "—"; Score3Text = "";

            for (int i = 0; i < results.Count && i < 3; i++)
            {
                var r = results[i];
                if (r.BestMove == null) continue;
                string mv = BuildSan(snapshot, r.BestMove);
                string sc = FormatScore(r.Score);
                switch (i)
                {
                    case 0: Move1Text = mv; Score1Text = sc; break;
                    case 1: Move2Text = mv; Score2Text = sc; break;
                    case 2: Move3Text = mv; Score3Text = sc; break;
                }
            }
        }

        // ── Reset ───────────────────────────────────────────────

        public void Reset()
        {
            StopAutoAnalysis();
            _gameState = new GameState();
            _selectedIndex = null;
            _selectedPieceMoves.Clear();
            ResetHistory();

            ScoreText = "0.00"; ScoreLabelText = "Ravnopravno";
            Move1Text = "—"; Score1Text = "";
            Move2Text = "—"; Score2Text = "";
            Move3Text = "—"; Score3Text = "";
            DepthText = "—"; NodesText = "—"; TimeText = "—";
            UpdateEvalBar(0);

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
                Squares[i].Background     = isLight ? BrLight : BrDark;
                Squares[i].IsLegalMove    = false;
                Squares[i].IsLegalCapture = false;
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

        private string BuildSan(GameState state, Move move)
        {
            if (move.Type == MoveType.CastleKingside)  return "O-O";
            if (move.Type == MoveType.CastleQueenside) return "O-O-O";

            var piece = state.Board.GetPiece(move.From)!;
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

                var ambiguous = state.Board.GetPieces(piece.Color)
                    .Where(p => p.Type == piece.Type && !p.Position.Equals(move.From))
                    .Where(p => _generator.GetLegalMovesForPiece(p, state)
                                          .Any(m => m.To.Equals(move.To)))
                    .ToList();

                if (ambiguous.Count > 0)
                {
                    bool sameFile = ambiguous.Any(p => p.Position.Column == move.From.Column);
                    bool sameRank = ambiguous.Any(p => p.Position.Row    == move.From.Row);

                    if (!sameFile)      sb.Append((char)('a' + move.From.Column));
                    else if (!sameRank) sb.Append(move.From.Row + 1);
                    else                sb.Append(move.From.ToAlgebraic());
                }

                if (isCapture) sb.Append('x');
                sb.Append(move.To.ToAlgebraic());
            }

            return sb.ToString();
        }

        private static string SquareName(Square sq) =>
            $"{(char)('a' + sq.Column)}{sq.Row + 1}";

        private void UpdateEvalBar(int cp)
        {
            double white;
            if      (cp >  9000) white = 100.0;
            else if (cp < -9000) white = 0.0;
            else                 white = 50.0 + 50.0 * Math.Tanh(cp / 600.0);
            EvalBarWhitePct = white;
            EvalBarBlackPct = 100.0 - white;
        }

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

        // ── SVG figure ──────────────────────────────────────────

        // Cache: jednom provjeri koje SVG datoteke postoje u Resources/Pieces/
        private static readonly Dictionary<(PieceColor, PieceType), Uri?> _svgCache = BuildSvgCache();

        private static Dictionary<(PieceColor, PieceType), Uri?> BuildSvgCache()
        {
            var cache = new Dictionary<(PieceColor, PieceType), Uri?>();
            var colors = new[] { PieceColor.White, PieceColor.Black };
            var types  = new[] { PieceType.King, PieceType.Queen, PieceType.Rook,
                                  PieceType.Bishop, PieceType.Knight, PieceType.Pawn };

            foreach (var color in colors)
            foreach (var type in types)
            {
                string c = color == PieceColor.White ? "w" : "b";
                string t = type switch
                {
                    PieceType.King   => "K",
                    PieceType.Queen  => "Q",
                    PieceType.Rook   => "R",
                    PieceType.Bishop => "B",
                    PieceType.Knight => "N",
                    PieceType.Pawn   => "P",
                    _                => ""
                };

                var packUri = new Uri(
                    $"pack://application:,,,/ChessInsight;component/Resources/Pieces/{c}{t}.svg",
                    UriKind.Absolute);

                try
                {
                    System.Windows.Application.GetResourceStream(packUri);
                    cache[(color, type)] = packUri;
                }
                catch
                {
                    cache[(color, type)] = null; // fajl ne postoji — koristit će se unicode
                }
            }

            return cache;
        }

        private static Uri? GetSvgUri(Piece? piece)
        {
            if (piece == null) return null;
            _svgCache.TryGetValue((piece.Color, piece.Type), out var uri);
            return uri;
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
