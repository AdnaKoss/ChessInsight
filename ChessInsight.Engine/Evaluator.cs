using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    public class Evaluator
    {
        // ── Materijalne vrijednosti ──────────────────────────────
        internal static readonly Dictionary<PieceType, int> PieceValues = new()
        {
            { PieceType.Pawn,   100   },
            { PieceType.Knight, 320   },
            { PieceType.Bishop, 330   },
            { PieceType.Rook,   500   },
            { PieceType.Queen,  900   },
            { PieceType.King,   20000 }
        };

        // ── PST tablice ──────────────────────────────────────────
        // Red 0 = rank 8 (protivnička strana), red 7 = rank 1 (vlastita strana).
        // Bijeli: tableRow = 7 - piece.Position.Row
        // Crni:   tableRow = piece.Position.Row

        private static readonly int[,] PawnTable = {
            {  0,  0,  0,  0,  0,  0,  0,  0 },
            { 50, 50, 50, 50, 50, 50, 50, 50 },
            { 10, 10, 20, 30, 30, 20, 10, 10 },
            {  5,  5, 10, 25, 25, 10,  5,  5 },
            {  0,  0,  0, 20, 20,  0,  0,  0 },
            {  5, -5,-10,  0,  0,-10, -5,  5 },
            {  5, 10, 10,-20,-20, 10, 10,  5 },
            {  0,  0,  0,  0,  0,  0,  0,  0 }
        };

        private static readonly int[,] KnightTable = {
            { -50,-40,-30,-30,-30,-30,-40,-50 },
            { -40,-20,  0,  0,  0,  0,-20,-40 },
            { -30,  0, 10, 15, 15, 10,  0,-30 },
            { -30,  5, 15, 20, 20, 15,  5,-30 },
            { -30,  0, 15, 20, 20, 15,  0,-30 },
            { -30,  5, 10, 15, 15, 10,  5,-30 },
            { -40,-20,  0,  5,  5,  0,-20,-40 },
            { -50,-40,-30,-30,-30,-30,-40,-50 }
        };

        private static readonly int[,] BishopTable = {
            { -20,-10,-10,-10,-10,-10,-10,-20 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -10,  0,  5, 10, 10,  5,  0,-10 },
            { -10,  5,  5, 10, 10,  5,  5,-10 },
            { -10,  0, 10, 10, 10, 10,  0,-10 },
            { -10, 10, 10, 10, 10, 10, 10,-10 },
            { -10,  5,  0,  0,  0,  0,  5,-10 },
            { -20,-10,-10,-10,-10,-10,-10,-20 }
        };

        private static readonly int[,] RookTable = {
            {  0,  0,  0,  0,  0,  0,  0,  0 },
            {  5, 10, 10, 10, 10, 10, 10,  5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            {  0,  0,  0,  5,  5,  0,  0,  0 }
        };

        private static readonly int[,] QueenTable = {
            { -20,-10,-10, -5, -5,-10,-10,-20 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -10,  0,  5,  5,  5,  5,  0,-10 },
            {  -5,  0,  5,  5,  5,  5,  0, -5 },
            {  -5,  0,  5,  5,  5,  5,  0, -5 },
            { -10,  0,  5,  5,  5,  5,  0,-10 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -20,-10,-10, -5, -5,-10,-10,-20 }
        };

        private static readonly int[,] KingMiddlegameTable = {
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -20,-30,-30,-40,-40,-30,-30,-20 },
            { -10,-20,-20,-20,-20,-20,-20,-10 },
            {  20, 20,  0,  0,  0,  0, 20, 20 },
            {  20, 30, 10,  0,  0, 10, 30, 20 }
        };

        private static readonly int[,] KingEndgameTable = {
            { -50,-40,-30,-20,-20,-30,-40,-50 },
            { -30,-20,-10,  0,  0,-10,-20,-30 },
            { -30,-10, 20, 30, 30, 20,-10,-30 },
            { -30,-10, 30, 40, 40, 30,-10,-30 },
            { -30,-10, 30, 40, 40, 30,-10,-30 },
            { -30,-10, 20, 30, 30, 20,-10,-30 },
            { -30,-30,  0,  0,  0,  0,-30,-30 },
            { -50,-30,-30,-30,-30,-30,-30,-50 }
        };

        private static readonly int[] PassedPawnBonus = { 0, 10, 20, 35, 55, 80, 120, 0 };

        // ── Pješački hash (Zobrist) za keširanje pješačke strukture ──
        private readonly Dictionary<ulong, int> _pawnCache = new(512);
        private const int PawnCacheMaxSize = 4096;
        private static readonly ulong[,] _pawnZobrist = BuildPawnZobrist();

        private static ulong[,] BuildPawnZobrist()
        {
            var rng = new Random(unchecked((int)0x8D5F_3A21)); // fiksni seed — reproducibilnost
            var t = new ulong[2, 64];
            for (int c = 0; c < 2; c++)
                for (int sq = 0; sq < 64; sq++)
                    t[c, sq] = ((ulong)(uint)rng.Next()) | ((ulong)(uint)rng.Next() << 32);
            return t;
        }

        private static ulong PawnHash(List<Piece> whitePawns, List<Piece> blackPawns)
        {
            ulong h = 0;
            foreach (var p in whitePawns)
                h ^= _pawnZobrist[0, p.Position.Row * 8 + p.Position.Column];
            foreach (var p in blackPawns)
                h ^= _pawnZobrist[1, p.Position.Row * 8 + p.Position.Column];
            return h;
        }

        private int GetCachedPawnStructure(List<Piece> whitePawns, List<Piece> blackPawns)
        {
            if (whitePawns.Count == 0 && blackPawns.Count == 0) return 0;
            ulong key = PawnHash(whitePawns, blackPawns);
            if (_pawnCache.TryGetValue(key, out int cached)) return cached;
            int score = EvaluatePawnStructure(whitePawns, blackPawns);
            if (_pawnCache.Count >= PawnCacheMaxSize) _pawnCache.Clear();
            _pawnCache[key] = score;
            return score;
        }

        // ── Faza igre ────────────────────────────────────────────
        private enum GamePhase { Opening, Middlegame, Endgame }

        // ── Glavna evaluacijska funkcija (jedan prolaz) ──────────
        public int Evaluate(GameState state)
        {
            int score = 0, nonKingMatDiff = 0, totalNonPawnNonKing = 0, nonPawnMaterial = 0;
            var whitePawns  = new List<Piece>(8);
            var blackPawns  = new List<Piece>(8);
            var whiteRooks  = new List<Piece>(2);
            var blackRooks  = new List<Piece>(2);
            var allPieces   = new List<Piece>(32);
            int whiteBishops = 0, blackBishops = 0;
            Piece? whiteKing = null, blackKing = null;

            // Jedan prolaz — sakupi sve što treba svim pod-evaluatorima
            foreach (var piece in state.Board.GetAllPieces())
            {
                allPieces.Add(piece);
                bool isWhite = piece.Color == PieceColor.White;
                int sign = isWhite ? 1 : -1;
                int mat  = PieceValues[piece.Type];
                score += sign * mat;

                if (piece.Type == PieceType.King)
                {
                    // PST kralja ovisi o fazi — dodaj nakon što je faza poznata
                    if (isWhite) whiteKing = piece; else blackKing = piece;
                    continue;
                }

                score += sign * GetPositionBonusForPiece(piece);
                nonKingMatDiff += sign * mat;

                if (piece.Type != PieceType.Pawn)
                {
                    totalNonPawnNonKing += mat;
                    nonPawnMaterial += mat;
                }

                switch (piece.Type)
                {
                    case PieceType.Pawn:
                        (isWhite ? whitePawns : blackPawns).Add(piece);
                        break;
                    case PieceType.Bishop:
                        if (isWhite) whiteBishops++; else blackBishops++;
                        break;
                    case PieceType.Rook:
                        (isWhite ? whiteRooks : blackRooks).Add(piece);
                        break;
                }
            }

            // Faza sada poznata — primijeni PST kralja
            var phase = totalNonPawnNonKing > 5000 ? GamePhase.Opening :
                        totalNonPawnNonKing > 2600 ? GamePhase.Middlegame : GamePhase.Endgame;
            if (whiteKing != null) score += GetPositionBonus(whiteKing, phase);
            if (blackKing != null) score -= GetPositionBonus(blackKing, phase);

            // Rani izlaz — velika materijalna razlika, pozicijski detalji nevažni
            if (Math.Abs(nonKingMatDiff) > 500) return score;

            // Pješačka struktura (keširana Zobristom)
            score += GetCachedPawnStructure(whitePawns, blackPawns);

            // Par lovaca
            if (whiteBishops >= 2) score += 30;
            if (blackBishops >= 2) score -= 30;

            // Topovi na otvorenim linijama (bitmask, bez dodatnog skeniranja)
            score += EvaluateRooksInline(whiteRooks, blackRooks, whitePawns, blackPawns);

            // Sigurnost kralja (samo u otvaranju i srednici)
            if (phase != GamePhase.Endgame && whiteKing != null && blackKing != null)
                score += EvaluateKingSafety(state, whiteKing, blackKing, whitePawns, blackPawns, phase);

            // Završnička evaluacija
            if (phase == GamePhase.Endgame)
            {
                if (nonPawnMaterial == 0)
                {
                    int? kpkOverride = KPKOutcomeOverride(state, whitePawns, blackPawns);
                    if (kpkOverride.HasValue) return kpkOverride.Value;
                }

                if (whiteKing != null && blackKing != null)
                    score += EvaluateKingOpposition(state, whiteKing, blackKing);

                score += EvaluatePawnSquare(state, whitePawns, blackPawns, whiteKing, blackKing);

                if (nonPawnMaterial == 0)
                    score += EvaluateKPvK(state, whitePawns, blackPawns);

                // Čista pješačka završnica s ≤2 pješaka — teorijski ishodi
                if (nonPawnMaterial == 0 && whitePawns.Count + blackPawns.Count <= 2)
                {
                    int? theory = EvaluatePawnEndgameTheory(state, whitePawns, blackPawns,
                                      whiteKing, blackKing);
                    if (theory.HasValue) return theory.Value;
                }
            }

            if (phase == GamePhase.Opening)
                score += EvaluateOpeningPrinciples(state, allPieces);

            return score;
        }

        // ── Principi otvaranja ────────────────────────────────────

        private static int EvaluateOpeningPrinciples(GameState state, List<Piece> allPieces)
        {
            int score = 0;

            foreach (var piece in allPieces)
            {
                int sign    = piece.Color == PieceColor.White ? 1 : -1;
                int homeRow = piece.Color == PieceColor.White ? 0 : 7;
                int row     = piece.Position.Row;
                int col     = piece.Position.Column;

                switch (piece.Type)
                {
                    case PieceType.Pawn:
                        // Centralni pješaci na e4/d4 (bijeli) ili e5/d5 (crni) → +30cp
                        if (piece.Color == PieceColor.White && row == 3 && (col == 3 || col == 4))
                            score += 30;
                        else if (piece.Color == PieceColor.Black && row == 4 && (col == 3 || col == 4))
                            score -= 30;

                        // Centralni pješaci nepomjereni → -10cp
                        {
                            bool unmoved = piece.Color == PieceColor.White
                                ? row == 1 && (col == 3 || col == 4)
                                : row == 6 && (col == 3 || col == 4);
                            if (unmoved) score -= sign * 10;
                        }
                        break;

                    case PieceType.Knight:
                    case PieceType.Bishop:
                        // Razvoj lakih figura: izašao +20cp, još na početnoj liniji -10cp
                        score += row != homeRow ? sign * 20 : -sign * 10;

                        // Skakač na rubu (a ili h kolona) → -25cp
                        if (piece.Type == PieceType.Knight && (col == 0 || col == 7))
                            score -= sign * 25;

                        // Blokiranje centralnih pješaka figurom → -15cp
                        {
                            bool blocking = piece.Color == PieceColor.White
                                ? row == 1 && (col == 3 || col == 4)
                                : row == 6 && (col == 3 || col == 4);
                            if (blocking) score -= sign * 15;
                        }
                        break;

                    case PieceType.Queen:
                        // Dama prerano (prvih 6 poteza) → -20cp
                        if (state.FullMoveNumber <= 6 && row != homeRow)
                            score -= sign * 20;
                        break;

                    case PieceType.King:
                        // Rokada: obavljena → +40cp; izgubljena oba prava bez rokade → -30cp
                        {
                            bool lostBoth = piece.Color == PieceColor.White
                                ? !state.WhiteCanCastleKingside && !state.WhiteCanCastleQueenside
                                : !state.BlackCanCastleKingside && !state.BlackCanCastleQueenside;

                            if (lostBoth)
                            {
                                bool castled = row == homeRow && (col == 6 || col == 2);
                                score += sign * (castled ? 40 : -30);
                            }
                        }
                        break;
                }
            }

            return score;
        }

        // ── Pozicijski bonus — bez kralja (faza nepoznata) ───────

        private static int GetPositionBonusForPiece(Piece piece)
        {
            int row = piece.Color == PieceColor.White
                ? 7 - piece.Position.Row
                : piece.Position.Row;
            int col = piece.Position.Column;

            return piece.Type switch
            {
                PieceType.Pawn   => PawnTable[row, col],
                PieceType.Knight => KnightTable[row, col],
                PieceType.Bishop => BishopTable[row, col],
                PieceType.Rook   => RookTable[row, col],
                PieceType.Queen  => QueenTable[row, col],
                _                => 0
            };
        }

        // ── Pozicijski bonus s fazom (samo kralj) ─────────────────

        private static int GetPositionBonus(Piece piece, GamePhase phase)
        {
            int row = piece.Color == PieceColor.White
                ? 7 - piece.Position.Row
                : piece.Position.Row;
            int col = piece.Position.Column;

            if (piece.Type == PieceType.King)
            {
                var table = phase == GamePhase.Endgame
                    ? KingEndgameTable
                    : KingMiddlegameTable;
                return table[row, col];
            }

            return GetPositionBonusForPiece(piece);
        }

        // ── Topovi na otvorenim linijama (bitmask) ────────────────

        private static int EvaluateRooksInline(List<Piece> whiteRooks, List<Piece> blackRooks,
                                                List<Piece> whitePawns, List<Piece> blackPawns)
        {
            if (whiteRooks.Count == 0 && blackRooks.Count == 0) return 0;

            int whitePawnCols = 0, blackPawnCols = 0;
            foreach (var p in whitePawns) whitePawnCols |= 1 << p.Position.Column;
            foreach (var p in blackPawns) blackPawnCols |= 1 << p.Position.Column;

            int score = 0;
            foreach (var r in whiteRooks)
            {
                int bit = 1 << r.Position.Column;
                if ((whitePawnCols & bit) == 0 && (blackPawnCols & bit) == 0) score += 25;
                else if ((whitePawnCols & bit) == 0) score += 10;
            }
            foreach (var r in blackRooks)
            {
                int bit = 1 << r.Position.Column;
                if ((blackPawnCols & bit) == 0 && (whitePawnCols & bit) == 0) score -= 25;
                else if ((blackPawnCols & bit) == 0) score -= 10;
            }
            return score;
        }

        // ── Sigurnost kralja ─────────────────────────────────────

        private static int EvaluateKingSafety(GameState state,
                                               Piece whiteKing, Piece blackKing,
                                               List<Piece> whitePawns, List<Piece> blackPawns,
                                               GamePhase phase)
        {
            int score = 0;
            score += KingSafetyForColor(state, whiteKing, whitePawns, phase, isWhite: true);
            score -= KingSafetyForColor(state, blackKing, blackPawns, phase, isWhite: false);
            return score;
        }

        private static int KingSafetyForColor(GameState state, Piece king,
                                               List<Piece> ownPawns, GamePhase phase, bool isWhite)
        {
            var color    = isWhite ? PieceColor.White : PieceColor.Black;
            int kingCol  = king.Position.Column;
            int kingRow  = king.Position.Row;
            int dir      = isWhite ? 1 : -1;
            int safety   = 0;

            // Bonus za pješake koji direktno štite kralja
            for (int dc = -1; dc <= 1; dc++)
            {
                int c = kingCol + dc;
                int r = kingRow + dir;
                if ((uint)c > 7 || (uint)r > 7) continue;

                var piece = state.Board.GetPiece(new Square(r, c));
                if (piece?.Type == PieceType.Pawn && piece.Color == color)
                    safety += 10;
            }

            // Malus za kralja u centru u middlegameu
            if (kingCol >= 2 && kingCol <= 5)
            {
                int penalty = phase == GamePhase.Middlegame ? 30 : 15;
                safety -= penalty;
            }

            return safety;
        }

        // ── Opozicija kraljeva (samo endgame) ────────────────────

        private static int EvaluateKingOpposition(GameState state, Piece wk, Piece bk)
        {
            int dr = Math.Abs(wk.Position.Row    - bk.Position.Row);
            int dc = Math.Abs(wk.Position.Column - bk.Position.Column);

            int bonus;
            if ((dr == 0 && dc == 2) || (dc == 0 && dr == 2)) bonus = 30;
            else if (dr == 2 && dc == 2)                        bonus = 20;
            else if ((dr == 0 && dc == 3) || (dc == 0 && dr == 3) || (dr == 3 && dc == 3))
                                                                 bonus = 15;
            else return 0;

            return state.CurrentPlayer == PieceColor.Black ? bonus : -bonus;
        }

        // ── Kvadrat pješaka (samo endgame) ───────────────────────

        private static int EvaluatePawnSquare(GameState state,
                                               List<Piece> whitePawns, List<Piece> blackPawns,
                                               Piece? whiteKing, Piece? blackKing)
        {
            int score = 0;

            foreach (var pawn in whitePawns)
            {
                if (!IsPassedPawn(pawn, blackPawns, PieceColor.White)) continue;
                int steps = 7 - pawn.Position.Row;
                if (steps <= 0 || blackKing == null) continue;

                int dist = Math.Max(
                    Math.Abs(blackKing.Position.Row    - pawn.Position.Row),
                    Math.Abs(blackKing.Position.Column - pawn.Position.Column));

                bool outside = state.CurrentPlayer == PieceColor.White
                    ? dist > steps
                    : dist > steps + 1;

                if (outside) score += 150 + steps * 30;
            }

            foreach (var pawn in blackPawns)
            {
                if (!IsPassedPawn(pawn, whitePawns, PieceColor.Black)) continue;
                int steps = pawn.Position.Row;
                if (steps <= 0 || whiteKing == null) continue;

                int dist = Math.Max(
                    Math.Abs(whiteKing.Position.Row    - pawn.Position.Row),
                    Math.Abs(whiteKing.Position.Column - pawn.Position.Column));

                bool outside = state.CurrentPlayer == PieceColor.Black
                    ? dist > steps
                    : dist > steps + 1;

                if (outside) score -= 150 + steps * 30;
            }

            return score;
        }

        // ── K+P vs K specijalna evaluacija ───────────────────────

        private static int EvaluateKPvK(GameState state,
                                         List<Piece> whitePawns, List<Piece> blackPawns)
        {
            if (whitePawns.Count == 1 && blackPawns.Count == 0)
            {
                var wk = state.Board.GetKing(PieceColor.White);
                var bk = state.Board.GetKing(PieceColor.Black);
                if (wk == null || bk == null) return 0;
                return EvaluateKPKForSide(state, whitePawns[0], wk, bk, pawnIsWhite: true);
            }
            if (blackPawns.Count == 1 && whitePawns.Count == 0)
            {
                var wk = state.Board.GetKing(PieceColor.White);
                var bk = state.Board.GetKing(PieceColor.Black);
                if (wk == null || bk == null) return 0;
                return -EvaluateKPKForSide(state, blackPawns[0], bk, wk, pawnIsWhite: false);
            }
            return 0;
        }

        private static int EvaluateKPKForSide(GameState state, Piece pawn,
                                               King atkKing, King defKing, bool pawnIsWhite)
        {
            int Norm(int r) => pawnIsWhite ? r : 7 - r;

            int pr  = Norm(pawn.Position.Row);
            int pc  = pawn.Position.Column;
            int akr = Norm(atkKing.Position.Row);
            int akc = atkKing.Position.Column;
            int dkr = Norm(defKing.Position.Row);
            int dkc = defKing.Position.Column;

            bool atkToMove = state.CurrentPlayer == (pawnIsWhite ? PieceColor.White : PieceColor.Black);
            bool rookPawn  = pc == 0 || pc == 7;

            int score = 0;

            int aheadOfPawn = akr - pr;
            score += aheadOfPawn > 0 ? 60 + aheadOfPawn * 25 :
                     aheadOfPawn == 0 ? 10 : -20;

            if (!rookPawn && akr >= pr + 2 && Math.Abs(akc - pc) <= 1)
                score += 200;

            if (dkr > pr && Math.Abs(dkc - pc) <= 1)
            {
                score -= 60;
                if (dkr == pr + 1 && dkc == pc && !atkToMove)
                    score -= 100;
            }

            int dr = Math.Abs(akr - dkr);
            int dc = Math.Abs(akc - dkc);

            int oppBonus = (dr == 2 && dc == 0) ? 200 :
                           (dr == 2 && dc == 2) ? 160 :
                           (dr == 0 && dc == 2) ? 120 :
                           (dr == 3 && dc == 0) ?  80 :
                           (dr == 3 && dc == 3) ?  60 :
                           (dr == 0 && dc == 3) ?  50 :
                           0;

            if (oppBonus > 0)
                score += atkToMove ? -oppBonus : oppBonus;

            if (rookPawn && dkr > pr && Math.Abs(dkc - pc) <= 1)
                score -= 120;

            return score;
        }

        // ── KPK teorijski ishod — override za jasne pozicije ─────

        private static int? KPKOutcomeOverride(GameState state,
                                               List<Piece> whitePawns, List<Piece> blackPawns)
        {
            if (whitePawns.Count + blackPawns.Count != 1) return null;

            bool wPawn = whitePawns.Count == 1;
            var pawn   = wPawn ? whitePawns[0] : blackPawns[0];
            var atk    = state.Board.GetKing(wPawn ? PieceColor.White : PieceColor.Black);
            var def    = state.Board.GetKing(wPawn ? PieceColor.Black : PieceColor.White);
            if (atk == null || def == null) return null;

            int Norm(int r) => wPawn ? r : 7 - r;

            int pr  = Norm(pawn.Position.Row);
            int pc  = pawn.Position.Column;
            int akr = Norm(atk.Position.Row);
            int akc = atk.Position.Column;
            int dkr = Norm(def.Position.Row);
            int dkc = def.Position.Column;

            bool atkToMove = state.CurrentPlayer == (wPawn ? PieceColor.White : PieceColor.Black);
            bool rookPawn  = pc == 0 || pc == 7;
            int  stepsLeft = 7 - pr;

            if (stepsLeft <= 0) return null;

            if (rookPawn)
            {
                if (dkr == 7 && dkc == pc)
                    return 0;
                if (!atkToMove && dkr >= 6 && Math.Abs(dkc - pc) <= 1)
                    return 0;
            }

            if (!rookPawn &&
                akr == pr + 1 && akc == pc &&
                dkr == akr + 2 && dkc == pc &&
                atkToMove)
                return 0;

            int defDist = Math.Max(Math.Abs(dkr - 7), Math.Abs(dkc - pc));
            int atkDist = Math.Max(Math.Abs(akr - pr), Math.Abs(akc - pc));
            int threshold = atkToMove ? stepsLeft : stepsLeft + 2;
            if (defDist >= threshold && atkDist >= 2)
                return wPawn ? 700 : -700;

            if (!rookPawn && akr >= pr + 2 && Math.Abs(akc - pc) <= 1)
            {
                if (defDist >= 3 || pr >= 5)
                    return wPawn ? 700 : -700;
            }

            return null;
        }

        // ── Teorija pješačkih završnica ──────────────────────────

        private static bool HasTempo(Piece pawn)
        {
            return pawn.Color == PieceColor.White
                ? pawn.Position.Row == 1
                : pawn.Position.Row == 6;
        }

        private static int? EvaluatePawnEndgameTheory(GameState state,
            List<Piece> whitePawns, List<Piece> blackPawns, Piece? whiteKing, Piece? blackKing)
        {
            if (whitePawns.Count == 0 && blackPawns.Count == 0) return null;

            // ── Jedan bijeli pješak, crni bez pješaka ──────────────
            if (whitePawns.Count == 1 && blackPawns.Count == 0)
            {
                if (whiteKing == null || blackKing == null) return null;
                var pawn = whitePawns[0];

                int pr  = pawn.Position.Row;
                int pc  = pawn.Position.Column;
                int wkr = whiteKing.Position.Row;
                int wkc = whiteKing.Position.Column;
                int bkr = blackKing.Position.Row;
                int bkc = blackKing.Position.Column;
                bool rookPawn = pc == 0 || pc == 7;

                bool kingInFront    = wkr == pr + 1 && wkc == pc;
                bool blackDirectOpp = bkr == wkr + 2 && bkc == wkc;
                if (kingInFront && blackDirectOpp && state.CurrentPlayer == PieceColor.White)
                    return 0;
                if (kingInFront && blackDirectOpp &&
                    state.CurrentPlayer == PieceColor.Black && !HasTempo(pawn))
                    return 0;

                bool blackBlocksPawn   = bkc == pc && bkr == pr + 1;
                bool whiteOnKeySquares = wkr >= pr + 2 && Math.Abs(wkc - pc) <= 1;
                if (blackBlocksPawn && !whiteOnKeySquares &&
                    state.CurrentPlayer == PieceColor.White)
                    return 0;

                if (rookPawn)
                {
                    if (bkr == 7 && bkc == pc) return 0;
                    if (state.CurrentPlayer == PieceColor.Black &&
                        bkr >= 6 && Math.Abs(bkc - pc) <= 1)
                        return 0;
                }

                bool whiteLeads  = wkr == pr + 1 && wkc == pc;
                bool blackColOpp = bkc == pc && bkr > pr && bkr == wkr + 2;
                if (!whiteLeads && blackColOpp)
                    return 0;
            }

            // ── Jedan crni pješak, bijeli bez pješaka (simetričan) ─
            if (blackPawns.Count == 1 && whitePawns.Count == 0)
            {
                if (whiteKing == null || blackKing == null) return null;
                var pawn = blackPawns[0];

                int pr  = 7 - pawn.Position.Row;
                int pc  = pawn.Position.Column;
                int akr = 7 - blackKing.Position.Row;
                int akc = blackKing.Position.Column;
                int dkr = 7 - whiteKing.Position.Row;
                int dkc = whiteKing.Position.Column;
                bool rookPawn = pc == 0 || pc == 7;

                bool kingInFront  = akr == pr + 1 && akc == pc;
                bool defDirectOpp = dkr == akr + 2 && dkc == akc;
                if (kingInFront && defDirectOpp && state.CurrentPlayer == PieceColor.Black)
                    return 0;
                if (kingInFront && defDirectOpp &&
                    state.CurrentPlayer == PieceColor.White && !HasTempo(pawn))
                    return 0;

                bool defBlocksPawn  = dkc == pc && dkr == pr + 1;
                bool atkOnKeySquare = akr >= pr + 2 && Math.Abs(akc - pc) <= 1;
                if (defBlocksPawn && !atkOnKeySquare &&
                    state.CurrentPlayer == PieceColor.Black)
                    return 0;

                if (rookPawn)
                {
                    if (dkr == 7 && dkc == pc) return 0;
                    if (state.CurrentPlayer == PieceColor.White &&
                        dkr >= 6 && Math.Abs(dkc - pc) <= 1)
                        return 0;
                }

                bool atkLeads  = akr == pr + 1 && akc == pc;
                bool defColOpp = dkc == pc && dkr > pr && dkr == akr + 2;
                if (!atkLeads && defColOpp)
                    return 0;
            }

            return null;
        }

        // ── Pješačka struktura ────────────────────────────────────

        private static int EvaluatePawnStructure(List<Piece> whitePawns, List<Piece> blackPawns)
        {
            int score = 0;
            score += ScorePawns(whitePawns, blackPawns, PieceColor.White);
            score -= ScorePawns(blackPawns, whitePawns, PieceColor.Black);
            return score;
        }

        private static int ScorePawns(List<Piece> own, List<Piece> enemy, PieceColor color)
        {
            if (own.Count == 0) return 0;

            int score = 0;
            var perFile = new int[8];
            foreach (var p in own) perFile[p.Position.Column]++;

            foreach (var pawn in own)
            {
                int col = pawn.Position.Column;

                if (perFile[col] > 1) score -= 20;

                bool hasNeighbour = (col > 0 && perFile[col - 1] > 0) ||
                                    (col < 7 && perFile[col + 1] > 0);
                if (!hasNeighbour) score -= 15;

                if (IsPassedPawn(pawn, enemy, color))
                {
                    int rank = color == PieceColor.White
                        ? pawn.Position.Row
                        : 7 - pawn.Position.Row;
                    score += PassedPawnBonus[rank];
                }
            }

            return score;
        }

        private static bool IsPassedPawn(Piece pawn, List<Piece> enemyPawns, PieceColor color)
        {
            int col = pawn.Position.Column;
            int row = pawn.Position.Row;

            foreach (var ep in enemyPawns)
            {
                if (ep.Position.Column < col - 1 || ep.Position.Column > col + 1) continue;

                if (color == PieceColor.White && ep.Position.Row > row) return false;
                if (color == PieceColor.Black && ep.Position.Row < row) return false;
            }

            return true;
        }
    }
}
