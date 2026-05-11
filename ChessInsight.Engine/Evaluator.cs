using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    /// <summary>
    /// Heuristička evaluacija šahovske pozicije.
    /// Pozitivan skor = prednost bijelog, negativan = prednost crnog.
    /// </summary>
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

        // Middlegame: kralj treba biti sklon
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

        // Endgame: kralj treba biti aktivan, centraliziran
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

        // Bonus za prolaznog pješaka po ranku (0 = vlastita strana, 7 = promocija)
        private static readonly int[] PassedPawnBonus = { 0, 10, 20, 35, 55, 80, 120, 0 };

        // ── Faza igre ────────────────────────────────────────────

        private enum GamePhase { Opening, Middlegame, Endgame }

        private static GamePhase GetGamePhase(GameState state)
        {
            int material = 0;
            foreach (var p in state.Board.GetAllPieces())
            {
                if (p.Type == PieceType.King || p.Type == PieceType.Pawn) continue;
                material += PieceValues[p.Type];
            }
            // Početak (puno materijala) → središnjica → završnica
            if (material > 5000) return GamePhase.Opening;
            if (material > 2600) return GamePhase.Middlegame;
            return GamePhase.Endgame;
        }

        // ── Glavna evaluacijska funkcija ─────────────────────────

        public int Evaluate(GameState state)
        {
            var phase = GetGamePhase(state);
            int score = 0;
            int nonPawnMaterial = 0;

            var whitePawns = new List<Piece>(8);
            var blackPawns = new List<Piece>(8);
            int whiteBishops = 0, blackBishops = 0;

            foreach (var piece in state.Board.GetAllPieces())
            {
                int val = PieceValues[piece.Type] + GetPositionBonus(piece, phase);

                if (piece.Color == PieceColor.White)
                {
                    score += val;
                    if (piece.Type == PieceType.Pawn)   whitePawns.Add(piece);
                    if (piece.Type == PieceType.Bishop)  whiteBishops++;
                }
                else
                {
                    score -= val;
                    if (piece.Type == PieceType.Pawn)   blackPawns.Add(piece);
                    if (piece.Type == PieceType.Bishop)  blackBishops++;
                }

                if (piece.Type != PieceType.King && piece.Type != PieceType.Pawn)
                    nonPawnMaterial += PieceValues[piece.Type];
            }

            // Pješačka struktura
            score += EvaluatePawnStructure(whitePawns, blackPawns);

            // Par lovaca
            if (whiteBishops >= 2) score += 30;
            if (blackBishops >= 2) score -= 30;

            // Topovi na otvorenim linijama
            score += EvaluateRooks(state, whitePawns, blackPawns);

            // Sigurnost kralja (ne u završnici — tamo je kralj aktivna figura)
            if (phase != GamePhase.Endgame)
                score += EvaluateKingSafety(state, whitePawns, blackPawns, phase);

            // Završnička evaluacija
            if (phase == GamePhase.Endgame)
            {
                // K+P vs K teorijski ishod — provjeri PRIJE gradijentne evaluacije.
                // Vraća 0 za remi ili ±700 za pobjedu, zaobilazeći materijal/PST.
                if (nonPawnMaterial == 0)
                {
                    int? kpkOverride = KPKOutcomeOverride(state, whitePawns, blackPawns);
                    if (kpkOverride.HasValue)
                        return kpkOverride.Value;
                }

                score += EvaluateKingOpposition(state);
                score += EvaluatePawnSquare(state, whitePawns, blackPawns);

                if (nonPawnMaterial == 0)
                    score += EvaluateKPvK(state, whitePawns, blackPawns);

                // Teorijski remiji u čistim pješačkim završnicama —
                // override cijelog skora kada je pozicija teorijski remizirana.
                if (IsPureKingPawnEndgame(state))
                {
                    int? theory = EvaluatePawnEndgameTheory(state);
                    if (theory.HasValue)
                        return theory.Value;
                }
            }

            return score;
        }

        // ── Pozicijski bonus (PST) ────────────────────────────────

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

        // ── Topovi na otvorenim/polu-otvorenim linijama ──────────

        private static int EvaluateRooks(GameState state,
                                         List<Piece> whitePawns, List<Piece> blackPawns)
        {
            var whitePawnCols = new HashSet<int>(whitePawns.Select(p => p.Position.Column));
            var blackPawnCols = new HashSet<int>(blackPawns.Select(p => p.Position.Column));

            int score = 0;
            foreach (var piece in state.Board.GetAllPieces())
            {
                if (piece.Type != PieceType.Rook) continue;
                int col = piece.Position.Column;

                if (piece.Color == PieceColor.White)
                {
                    if (!whitePawnCols.Contains(col) && !blackPawnCols.Contains(col))
                        score += 25; // otvorena linija
                    else if (!whitePawnCols.Contains(col))
                        score += 10; // polu-otvorena (nema vlastitog pješaka)
                }
                else
                {
                    if (!blackPawnCols.Contains(col) && !whitePawnCols.Contains(col))
                        score -= 25;
                    else if (!blackPawnCols.Contains(col))
                        score -= 10;
                }
            }
            return score;
        }

        // ── Sigurnost kralja ─────────────────────────────────────

        private static int EvaluateKingSafety(GameState state,
                                               List<Piece> whitePawns, List<Piece> blackPawns,
                                               GamePhase phase)
        {
            int score = 0;
            score += KingSafetyForColor(state, PieceColor.White, whitePawns, phase);
            score -= KingSafetyForColor(state, PieceColor.Black, blackPawns, phase);
            return score;
        }

        private static int KingSafetyForColor(GameState state, PieceColor color,
                                               List<Piece> ownPawns, GamePhase phase)
        {
            var king = state.Board.GetKing(color);
            if (king == null) return 0;

            int kingCol = king.Position.Column;
            int kingRow = king.Position.Row;
            int direction = color == PieceColor.White ? 1 : -1;
            int safety = 0;

            // Bonus za pješake koji direktno štite kralja (3 polja ispred)
            for (int dc = -1; dc <= 1; dc++)
            {
                int c = kingCol + dc;
                int r = kingRow + direction;
                if ((uint)c > 7 || (uint)r > 7) continue;

                var piece = state.Board.GetPiece(new Square(r, c));
                if (piece?.Type == PieceType.Pawn && piece.Color == color)
                    safety += 10;
            }

            // Malus za sklonjenog (skrautiranog) kralja koji ostane u centru
            // U middlegameu je dvaput važnije nego u otvaranju
            if (kingCol >= 2 && kingCol <= 5)
            {
                int penalty = phase == GamePhase.Middlegame ? 30 : 15;
                safety -= penalty;
            }

            return safety;
        }

        // ── Opozicija kraljeva (samo endgame) ────────────────────

        private static int EvaluateKingOpposition(GameState state)
        {
            var wk = state.Board.GetKing(PieceColor.White);
            var bk = state.Board.GetKing(PieceColor.Black);
            if (wk == null || bk == null) return 0;

            int dr = Math.Abs(wk.Position.Row    - bk.Position.Row);
            int dc = Math.Abs(wk.Position.Column - bk.Position.Column);

            int bonus;

            // Direktna opozicija — jedno polje između
            if ((dr == 0 && dc == 2) || (dc == 0 && dr == 2))
                bonus = 30;
            else if (dr == 2 && dc == 2)
                bonus = 20;  // dijagonalna direktna
            // Daleka opozicija — dva polja između
            else if ((dr == 0 && dc == 3) || (dc == 0 && dr == 3) || (dr == 3 && dc == 3))
                bonus = 15;
            else
                return 0;

            // Igrač koji NIJE na potezu ima opoziciju (prednost)
            return state.CurrentPlayer == PieceColor.Black ? bonus : -bonus;
        }

        // ── Kvadrat pješaka (samo endgame) ───────────────────────

        private static int EvaluatePawnSquare(GameState state,
                                               List<Piece> whitePawns, List<Piece> blackPawns)
        {
            int score = 0;
            var blackKing = state.Board.GetKing(PieceColor.Black);
            var whiteKing = state.Board.GetKing(PieceColor.White);

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
        // Aktivira se samo kad nema figure osim kraljeva i pješaka.
        // Standardni bonusi od 30cp su premali da engine vidi opozicijsku taktiku —
        // ovdje su bonusi 5-7x veći jer opozicija određuje razliku između pobjede i remija.

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
                // Negiramo jer je simetričan slučaj
                return -EvaluateKPKForSide(state, blackPawns[0], bk, wk, pawnIsWhite: false);
            }
            return 0;
        }

        private static int EvaluateKPKForSide(GameState state, Piece pawn,
                                               King atkKing, King defKing, bool pawnIsWhite)
        {
            // Normalizacija: pješak uvijek ide prema većim redovima (pr=1 → pr=7)
            // Za bijelog: row ostaje isti; za crnog: row se invertuje (7-row)
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

            // ── A. Napadački kralj ispred pješaka ─────────────────
            // Neophodan preduvjet za pobjedu — bez toga ne može protisnuti pješaka
            int aheadOfPawn = akr - pr;
            score += aheadOfPawn > 0 ? 60 + aheadOfPawn * 25 :
                     aheadOfPawn == 0 ? 10 : -20;

            // ── B. Ključna polja (key squares) ────────────────────
            // Ako napadački kralj zauzme ključno polje (2+ ranka ispred, ±1 kolona),
            // promocija je garantovana za ne-rob-pješake
            if (!rookPawn && akr >= pr + 2 && Math.Abs(akc - pc) <= 1)
                score += 200;

            // ── C. Obrambeni kralj ispred pješaka (Philidor obrana) ─
            // Branilac ispred pješaka = potencijalni remi
            if (dkr > pr && Math.Abs(dkc - pc) <= 1)
            {
                score -= 60;
                // Klasična Philidor pozicija: branilac direktno ispred pješaka s opozicijom
                if (dkr == pr + 1 && dkc == pc && !atkToMove)
                    score -= 100; // jak signal za remi
            }

            // ── D. Opozicija — srce K+P vs K evaluacije ──────────
            // Bonusi su 5-7x veći nego globalna opozicija (30cp) jer ovdje
            // opozicija direktno odlučuje o pobjedi ili remiju
            int dr = Math.Abs(akr - dkr);
            int dc = Math.Abs(akc - dkc);

            int oppBonus = (dr == 2 && dc == 0) ? 200 :  // vertikalna direktna (najvažnija)
                           (dr == 2 && dc == 2) ? 160 :  // dijagonalna direktna
                           (dr == 0 && dc == 2) ? 120 :  // horizontalna direktna
                           (dr == 3 && dc == 0) ?  80 :  // vertikalna daleka
                           (dr == 3 && dc == 3) ?  60 :  // dijagonalna daleka
                           (dr == 0 && dc == 3) ?  50 :  // horizontalna daleka
                           0;

            if (oppBonus > 0)
            {
                // Napadač NIJE na potezu → napadač ima opoziciju → dobro za napadača
                // Napadač JESTE na potezu → branilac ima opoziciju → loše za napadača
                score += atkToMove ? -oppBonus : oppBonus;
            }

            // ── E. Rob-pješak (a ili h kolona) ────────────────────
            // Rob-pješak + branilac u uglu = remi (pat pri promociji)
            if (rookPawn && dkr > pr && Math.Abs(dkc - pc) <= 1)
                score -= 120;

            return score;
        }

        // ── KPK teorijski ishod — override za jasne pozicije ─────
        // Vraća 0 (remi) ili ±700 (pobjeda) za teorijski jasne KPK pozicije,
        // null za neodređene (prepušta se gradijentnoj evaluaciji).
        private static int? KPKOutcomeOverride(GameState state,
                                               List<Piece> whitePawns, List<Piece> blackPawns)
        {
            if (whitePawns.Count + blackPawns.Count != 1) return null;

            bool wPawn = whitePawns.Count == 1;
            var pawn   = wPawn ? whitePawns[0] : blackPawns[0];
            var atk    = state.Board.GetKing(wPawn ? PieceColor.White : PieceColor.Black);
            var def    = state.Board.GetKing(wPawn ? PieceColor.Black : PieceColor.White);
            if (atk == null || def == null) return null;

            // Normalizacija: pješak uvijek napreduje prema row 7
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

            // ── 1. Rob-pješak: branilac kontroliše promocioni ugao ──────────
            // Remi kada branilac stoji na promocionom polju ili ga može odmah zauzeti
            if (rookPawn)
            {
                if (dkr == 7 && dkc == pc)
                    return 0;
                // Branilac 1 korak od ugla i on je na potezu — siguran remi
                if (!atkToMove && dkr >= 6 && Math.Abs(dkc - pc) <= 1)
                    return 0;
            }

            // ── 2. Kralj ispred pješaka + opozicija = teorijski remi ─────────
            // Napadački kralj direktno ispred pješaka (isti stub, 1 red ispred).
            // Crni uzima direktnu vertikalnu opoziciju i napadač je na potezu.
            // Napadač ne može napredovati: svaki pomak kralja crni prati opozicijom.
            //   - Kuda god napadač ide u stranu → crni uzima opoziciju na istom stubu.
            //   - Napadač ne može doći na ključna polja jer je branilac uvijek 2 ispred.
            // Uvjet: akr = pr+1 (ispred), akc = pc (isti stub), dkr = akr+2 (opozicija),
            //        dkc = pc (isti stub), napadač na potezu — teorijski remi.
            if (!rookPawn &&
                akr == pr + 1 && akc == pc &&
                dkr == akr + 2 && dkc == pc &&
                atkToMove)
                return 0;

            // ── 2. Pravilo kvadrata — pješak trči sam ───────────────────────
            // Branilac mora biti dovoljno daleko od promocionog polja.
            // Pješak pobijedi ako: atkToMove → defDist ≥ stepsLeft
            //                      defToMove → defDist ≥ stepsLeft + 2
            // (defToMove: branilac ima 1 ekstra potez za ući u kvadrat → treba 2 više)
            int defDist = Math.Max(Math.Abs(dkr - 7), Math.Abs(dkc - pc));
            int atkDist = Math.Max(Math.Abs(akr - pr), Math.Abs(akc - pc));
            int threshold = atkToMove ? stepsLeft : stepsLeft + 2;
            // Primijeni samo kada napadački kralj ne može značajno pomoći
            // (daleko je od pješaka), inače je evaluacija složenija
            if (defDist >= threshold && atkDist >= 2)
                return wPawn ? 700 : -700;

            // ── 3. Ključna polja (key squares) — pobjeda bez pravila kvadrata ─
            // Za ne-rob-pješake: napadački kralj na ključnim poljima (≥2 reda
            // ispred pješaka, ±1 kolona) garantuje promociju
            if (!rookPawn && akr >= pr + 2 && Math.Abs(akc - pc) <= 1)
            {
                // Siguran override samo ako branilac daleko ili pješak blizu promocije
                if (defDist >= 3 || pr >= 5)
                    return wPawn ? 700 : -700;
            }

            return null;
        }

        // ── Teorija pješačkih završnica ──────────────────────────

        private static bool IsPureKingPawnEndgame(GameState state)
        {
            return !state.Board.GetAllPieces().Any(p =>
                p.Type == PieceType.Queen  ||
                p.Type == PieceType.Rook   ||
                p.Type == PieceType.Bishop ||
                p.Type == PieceType.Knight) &&
                state.Board.GetAllPieces().Count(p => p.Type == PieceType.Pawn) <= 2;
        }

        private static bool HasTempo(Piece pawn)
        {
            // Pješak još nije pomjerio → može ići 2 polja → bijeli ima tempo potez
            return pawn.Color == PieceColor.White
                ? pawn.Position.Row == 1
                : pawn.Position.Row == 6;
        }

        // Vraća 0 za teorijski remi, null za neodređeno.
        // Poziva se samo u čistim pješačkim završnicama (IsPureKingPawnEndgame).
        private static int? EvaluatePawnEndgameTheory(GameState state)
        {
            var allPawns = state.Board.GetAllPieces()
                               .Where(p => p.Type == PieceType.Pawn).ToList();
            if (allPawns.Count == 0) return null;

            var whitePawns = allPawns.Where(p => p.Color == PieceColor.White).ToList();
            var blackPawns = allPawns.Where(p => p.Color == PieceColor.Black).ToList();

            // ── Jedan bijeli pješak, crni bez pješaka ──────────────
            if (whitePawns.Count == 1 && blackPawns.Count == 0)
            {
                var pawn = whitePawns[0];
                var wk   = state.Board.GetKing(PieceColor.White);
                var bk   = state.Board.GetKing(PieceColor.Black);
                if (wk == null || bk == null) return null;

                int pr  = pawn.Position.Row;
                int pc  = pawn.Position.Column;
                int wkr = wk.Position.Row;
                int wkc = wk.Position.Column;
                int bkr = bk.Position.Row;
                int bkc = bk.Position.Column;
                bool rookPawn = pc == 0 || pc == 7;

                // SLUČAJ 1 — Bijeli kralj ispred pješaka, crni ima direktnu opoziciju
                // Direktna opozicija: kraljevi 2 reda razmaknuti (1 polje između), isti stub.
                // Bijeli na potezu ne može napredovati → remi.
                bool kingInFront    = wkr == pr + 1 && wkc == pc;
                bool blackDirectOpp = bkr == wkr + 2 && bkc == wkc;
                if (kingInFront && blackDirectOpp && state.CurrentPlayer == PieceColor.White)
                    return 0;
                // Nema tempa: isti raspored, crni na potezu, pješak već pomjerio → remi.
                if (kingInFront && blackDirectOpp &&
                    state.CurrentPlayer == PieceColor.Black && !HasTempo(pawn))
                    return 0;

                // SLUČAJ 2 — Crni kralj direktno blokira pješak (Lucena obrana)
                // Crni kralj na istom stubu, 1 red ispred pješaka.
                // Bijeli kralj nije na ključnim poljima (pr+2+, ±1 kolona) → remi.
                // Primjer: bijeli Kd5, Pd6, crni Kd7, bijeli na potezu → remi.
                bool blackBlocksPawn   = bkc == pc && bkr == pr + 1;
                bool whiteOnKeySquares = wkr >= pr + 2 && Math.Abs(wkc - pc) <= 1;
                if (blackBlocksPawn && !whiteOnKeySquares &&
                    state.CurrentPlayer == PieceColor.White)
                    return 0;

                // SLUČAJ 3 — Rob-pješak: crni kontroliše promociono ugaono polje
                if (rookPawn)
                {
                    if (bkr == 7 && bkc == pc) return 0;
                    if (state.CurrentPlayer == PieceColor.Black &&
                        bkr >= 6 && Math.Abs(bkc - pc) <= 1)
                        return 0;
                }

                // SLUČAJ 4 — Crni na stubu pješaka s direktnom opozicijom, bijeli ne predvodi
                bool whiteLeads    = wkr == pr + 1 && wkc == pc;
                bool blackColOpp   = bkc == pc && bkr > pr && bkr == wkr + 2;
                if (!whiteLeads && blackColOpp)
                    return 0;
            }

            // ── Jedan crni pješak, bijeli bez pješaka (simetričan) ─
            if (blackPawns.Count == 1 && whitePawns.Count == 0)
            {
                var pawn = blackPawns[0];
                var bk   = state.Board.GetKing(PieceColor.Black);
                var wk   = state.Board.GetKing(PieceColor.White);
                if (wk == null || bk == null) return null;

                // Normalizacija: crni pješak ide prema manjim redovima → invertujemo
                int pr  = 7 - pawn.Position.Row;
                int pc  = pawn.Position.Column;
                int akr = 7 - bk.Position.Row;
                int akc = bk.Position.Column;
                int dkr = 7 - wk.Position.Row;
                int dkc = wk.Position.Column;
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

                bool atkLeads   = akr == pr + 1 && akc == pc;
                bool defColOpp  = dkc == pc && dkr > pr && dkr == akr + 2;
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

                // Dvostruki pješak
                if (perFile[col] > 1) score -= 20;

                // Izolirani pješak
                bool hasNeighbour = (col > 0 && perFile[col - 1] > 0) ||
                                    (col < 7 && perFile[col + 1] > 0);
                if (!hasNeighbour) score -= 15;

                // Prolazni pješak
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
