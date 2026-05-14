using ChessInsight.Core.Engine;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    /// <summary>
    /// Knjiga otvaranja s najčešćim teorijskim potezima.
    /// Ključ rječnika: prva dva FEN polja (postava + boja na potezu).
    /// Potezi se biraju proporcionalno težinama (weighted random).
    /// </summary>
    public class OpeningBook
    {
        // FEN key → lista (UCI potez, težina)
        private static readonly Dictionary<string, List<(string uci, int weight)>> _book = new()
        {
            // ── Bijeli prvi potez ─────────────────────────────────────────────
            ["rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w"] = new()
            {
                ("e2e4", 100), ("d2d4", 90), ("g1f3", 75)
            },

            // ── Crni odgovor na 1.e4 ─────────────────────────────────────────
            // g8f6 nije preporučen kao prvi odgovor na e4
            ["rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b"] = new()
            {
                ("e7e5", 100), ("c7c5", 95), ("e7e6", 80), ("c7c6", 75), ("d7d5", 70)
            },

            // ── Crni odgovor na 1.d4 ─────────────────────────────────────────
            ["rnbqkbnr/pppppppp/8/8/3P4/8/PPP1PPPP/RNBQKBNR b"] = new()
            {
                ("d7d5", 95), ("g8f6", 90), ("e7e6", 85), ("c7c5", 75)
            },

            // ── Crni odgovor na 1.c4 (Engleska) ──────────────────────────────
            ["rnbqkbnr/pppppppp/8/8/2P5/8/PP1PPPPP/RNBQKBNR b"] = new()
            {
                ("e7e5", 100), ("c7c5", 90), ("g8f6", 85), ("e7e6", 75)
            },

            // ── Bijeli nakon 1.e4 e5 ─────────────────────────────────────────
            ["rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w"] = new()
            {
                ("g1f3", 100), ("f1c4", 85), ("b1c3", 70)
            },

            // ── Bijeli nakon 1.e4 c5 (Sicilijska) ───────────────────────────
            ["rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w"] = new()
            {
                ("g1f3", 100), ("b1c3", 85), ("c2c3", 70)
            },

            // ── Bijeli nakon 1.e4 e6 (Francuska) ────────────────────────────
            ["rnbqkbnr/pppp1ppp/4p3/8/4P3/8/PPPP1PPP/RNBQKBNR w"] = new()
            {
                ("d2d4", 100), ("b1c3", 80)
            },

            // ── Bijeli nakon 1.e4 c6 (Caro-Kann) ────────────────────────────
            ["rnbqkbnr/pp1ppppp/2p5/8/4P3/8/PPPP1PPP/RNBQKBNR w"] = new()
            {
                ("d2d4", 100), ("b1c3", 75)
            },

            // ── Crni nakon 1.e4 e5 2.Sf3 ─────────────────────────────────────
            ["rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b"] = new()
            {
                ("b8c6", 100), ("g8f6", 85), ("f7f5", 40)
            },

            // ── Bijeli nakon 1.e4 e5 2.Sf3 Sc6 ──────────────────────────────
            // f1b5 = Ruy Lopez (Španjolska), f1c4 = Italijanska
            ["r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w"] = new()
            {
                ("f1b5", 100), ("f1c4", 95), ("d2d4", 80), ("b1c3", 70)
            },

            // ── Bijeli nakon 1.e4 e5 2.Sf3 Sf6 (Petrov) ─────────────────────
            // Sf3 je teorijski potez — ne uzimaj e5 odmah (f3e5)
            ["rnbqkb1r/pppp1ppp/5n2/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w"] = new()
            {
                ("d2d4", 100), ("b1c3", 80)
            },

            // ── Crni nakon 1.e4 e5 2.Sf3 Sc6 3.Lb5 (Ruy Lopez) ──────────────
            ["r1bqkbnr/pppp1ppp/2n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R b"] = new()
            {
                ("a7a6", 100), ("g8f6", 90), ("f8c5", 80)
            },

            // ── Crni nakon 1.e4 e5 2.Sf3 Sc6 3.Lc4 (Italijanska) ────────────
            ["r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b"] = new()
            {
                ("f8c5", 100), ("g8f6", 90), ("f8e7", 70)
            },

            // ── Bijeli nakon 1.d4 d5 ─────────────────────────────────────────
            ["rnbqkbnr/ppp1pppp/8/3p4/3P4/8/PPP1PPPP/RNBQKBNR w"] = new()
            {
                ("c2c4", 100), ("g1f3", 90), ("b1c3", 75)
            },

            // ── Bijeli nakon 1.d4 Sf6 ────────────────────────────────────────
            ["rnbqkb1r/pppppppp/5n2/8/3P4/8/PPP1PPPP/RNBQKBNR w"] = new()
            {
                ("c2c4", 100), ("g1f3", 90), ("b1c3", 75)
            },

            // ── Damin Gambit Odbijeni (QGD) — glavna linija (potezi 3–8) ────

            // Nakon 2.c4 (crni igra 2...e6)
            ["rnbqkbnr/ppp1pppp/8/3p4/2PP4/8/PP2PPPP/RNBQKBNR b"] = new()
            {
                ("e7e6", 100)
            },

            // Nakon 2...e6 (bijeli igra 3.Sc3)
            ["rnbqkbnr/ppp2ppp/4p3/3p4/2PP4/8/PP2PPPP/RNBQKBNR w"] = new()
            {
                ("b1c3", 100)
            },

            // Nakon 3.Sc3 (crni igra 3...Sf6)
            ["rnbqkbnr/ppp2ppp/4p3/3p4/2PP4/2N5/PP2PPPP/R1BQKBNR b"] = new()
            {
                ("g8f6", 100)
            },

            // Nakon 3...Sf6 (bijeli igra 4.Lg5)
            ["rnbqkb1r/ppp2ppp/4pn2/3p4/2PP4/2N5/PP2PPPP/R1BQKBNR w"] = new()
            {
                ("c1g5", 100)
            },

            // Nakon 4.Lg5 (crni igra 4...Le7)
            ["rnbqkb1r/ppp2ppp/4pn2/3p2B1/2PP4/2N5/PP2PPPP/R2QKBNR b"] = new()
            {
                ("f8e7", 100)
            },

            // Nakon 4...Le7 (bijeli igra 5.e3)
            ["rnbqk2r/ppp1bppp/4pn2/3p2B1/2PP4/2N5/PP2PPPP/R2QKBNR w"] = new()
            {
                ("e2e3", 100)
            },

            // Nakon 5.e3 (crni igra 5...0-0)
            ["rnbqk2r/ppp1bppp/4pn2/3p2B1/2PP4/2N1P3/PP3PPP/R2QKBNR b"] = new()
            {
                ("e8g8", 100)
            },

            // Nakon 5...0-0 (bijeli igra 6.Sf3)
            ["rnbq1rk1/ppp1bppp/4pn2/3p2B1/2PP4/2N1P3/PP3PPP/R2QKBNR w"] = new()
            {
                ("g1f3", 100)
            },

            // Nakon 6.Sf3 (crni igra 6...Sbd7)
            ["rnbq1rk1/ppp1bppp/4pn2/3p2B1/2PP4/2N1PN2/PP3PPP/R2QKB1R b"] = new()
            {
                ("b8d7", 100)
            },

            // Nakon 6...Sbd7 (bijeli igra 7.Dc2)
            ["r1bq1rk1/pppnbppp/4pn2/3p2B1/2PP4/2N1PN2/PP3PPP/R2QKB1R w"] = new()
            {
                ("d1c2", 100)
            },

            // Nakon 7.Dc2 (crni igra 7...c5)
            ["r1bq1rk1/pppnbppp/4pn2/3p2B1/2PP4/2N1PN2/PPQ2PPP/R3KB1R b"] = new()
            {
                ("c7c5", 100)
            },

            // Nakon 7...c5 (bijeli igra 8.cxd5)
            ["r1bq1rk1/pp1nbppp/4pn2/2pp2B1/2PP4/2N1PN2/PPQ2PPP/R3KB1R w"] = new()
            {
                ("c4d5", 100)
            },

            // Nakon 8.cxd5 (crni igra 8...exd5)
            ["r1bq1rk1/pp1nbppp/4pn2/2pP2B1/3P4/2N1PN2/PPQ2PPP/R3KB1R b"] = new()
            {
                ("e6d5", 100)
            },

            // ── Sicilijska Najdorf — glavna linija (potezi 2–8) ─────────────

            // Nakon 2.Sf3 (crni igra 2...d6)
            ["rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b"] = new()
            {
                ("d7d6", 100)
            },

            // Nakon 2...d6 (bijeli igra 3.d4)
            ["rnbqkbnr/pp2pppp/3p4/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R w"] = new()
            {
                ("d2d4", 100)
            },

            // Nakon 3.d4 (crni igra 3...cxd4)
            ["rnbqkbnr/pp2pppp/3p4/2p5/3PP3/5N2/PPP2PPP/RNBQKB1R b"] = new()
            {
                ("c5d4", 100)
            },

            // Nakon 3...cxd4 (bijeli igra 4.Sxd4)
            ["rnbqkbnr/pp2pppp/3p4/8/3pP3/5N2/PPP2PPP/RNBQKB1R w"] = new()
            {
                ("f3d4", 100)
            },

            // Nakon 4.Sxd4 (crni igra 4...Sf6)
            ["rnbqkbnr/pp2pppp/3p4/8/3NP3/8/PPP2PPP/RNBQKB1R b"] = new()
            {
                ("g8f6", 100)
            },

            // Nakon 4...Sf6 (bijeli igra 5.Sc3)
            ["rnbqkb1r/pp2pppp/3p1n2/8/3NP3/8/PPP2PPP/RNBQKB1R w"] = new()
            {
                ("b1c3", 100)
            },

            // Nakon 5.Sc3 (crni igra 5...a6)
            ["rnbqkb1r/pp2pppp/3p1n2/8/3NP3/2N5/PPP2PPP/R1BQKB1R b"] = new()
            {
                ("a7a6", 100)
            },

            // Nakon 5...a6 (bijeli igra 6.Lg5)
            ["rnbqkb1r/1p2pppp/p2p1n2/8/3NP3/2N5/PPP2PPP/R1BQKB1R w"] = new()
            {
                ("c1g5", 100)
            },

            // Nakon 6.Lg5 (crni igra 6...e6)
            ["rnbqkb1r/1p2pppp/p2p1n2/6B1/3NP3/2N5/PPP2PPP/R2QKB1R b"] = new()
            {
                ("e7e6", 100)
            },

            // Nakon 6...e6 (bijeli igra 7.f4)
            ["rnbqkb1r/1p3ppp/p2ppn2/6B1/3NP3/2N5/PPP2PPP/R2QKB1R w"] = new()
            {
                ("f2f4", 100)
            },

            // Nakon 7.f4 (crni igra 7...Le7)
            ["rnbqkb1r/1p3ppp/p2ppn2/6B1/3NPP2/2N5/PPP3PP/R2QKB1R b"] = new()
            {
                ("f8e7", 100)
            },

            // Nakon 7...Le7 (bijeli igra 8.Df3)
            ["rnbqk2r/1p2bppp/p2ppn2/6B1/3NPP2/2N5/PPP3PP/R2QKB1R w"] = new()
            {
                ("d1f3", 100)
            },

            // Nakon 8.Df3 (crni igra 8...Dc7)
            ["rnbqk2r/1p2bppp/p2ppn2/6B1/3NPP2/2N2Q2/PPP3PP/R3KB1R b"] = new()
            {
                ("d8c7", 100)
            },

            // ── Ruy Lopez — proširena glavna linija (potezi 4–9) ─────────────

            // Nakon 3...a6 (bijeli igra 4.La4)
            ["r1bqkbnr/1ppp1ppp/p1n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w"] = new()
            {
                ("b5a4", 100)
            },

            // Nakon 4.La4 (crni igra 4...Sf6)
            ["r1bqkbnr/1ppp1ppp/p1n5/4p3/B3P3/5N2/PPPP1PPP/RNBQK2R b"] = new()
            {
                ("g8f6", 100)
            },

            // Nakon 4...Sf6 (bijeli igra 5.0-0)
            ["r1bqkb1r/1ppp1ppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQK2R w"] = new()
            {
                ("e1g1", 100)
            },

            // Nakon 5.0-0 (crni igra 5...Le7)
            ["r1bqkb1r/1ppp1ppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQ1RK1 b"] = new()
            {
                ("f8e7", 100)
            },

            // Nakon 5...Le7 (bijeli igra 6.Te1)
            ["r1bqk2r/1pppbppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQ1RK1 w"] = new()
            {
                ("f1e1", 100)
            },

            // Nakon 6.Te1 (crni igra 6...b5)
            ["r1bqk2r/1pppbppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQR1K1 b"] = new()
            {
                ("b7b5", 100)
            },

            // Nakon 6...b5 (bijeli igra 7.Lb3)
            ["r1bqk2r/2ppbppp/p1n2n2/1p2p3/B3P3/5N2/PPPP1PPP/RNBQR1K1 w"] = new()
            {
                ("a4b3", 100)
            },

            // Nakon 7.Lb3 (crni igra 7...d6)
            ["r1bqk2r/2ppbppp/p1n2n2/1p2p3/4P3/1B3N2/PPPP1PPP/RNBQR1K1 b"] = new()
            {
                ("d7d6", 100)
            },

            // Nakon 7...d6 (bijeli igra 8.c3)
            ["r1bqk2r/2p1bppp/p1np1n2/1p2p3/4P3/1B3N2/PPPP1PPP/RNBQR1K1 w"] = new()
            {
                ("c2c3", 100)
            },

            // Nakon 8.c3 (crni igra 8...0-0)
            ["r1bqk2r/2p1bppp/p1np1n2/1p2p3/4P3/1BP2N2/PP1P1PPP/RNBQR1K1 b"] = new()
            {
                ("e8g8", 100)
            },

            // Nakon 8...0-0 (bijeli igra 9.h3)
            ["r1bq1rk1/2p1bppp/p1np1n2/1p2p3/4P3/1BP2N2/PP1P1PPP/RNBQR1K1 w"] = new()
            {
                ("h2h3", 100)
            },
        };

        /// <summary>Vraća true ako pozicija ima zapis u knjizi otvaranja.</summary>
        public bool HasBookMove(GameState state) => _book.ContainsKey(GetFenKey(state));

        /// <summary>Vraća sve (uci, težina) parove za poziciju, ili praznu listu.</summary>
        public IReadOnlyList<(string uci, int weight)> GetBookEntries(GameState state)
        {
            var key = GetFenKey(state);
            return _book.TryGetValue(key, out var entries)
                ? entries
                : Array.Empty<(string, int)>();
        }

        /// <summary>
        /// Vraća potez iz knjige otvaranja za danu poziciju, ili null ako pozicija nije u knjizi.
        /// Potez je garantovano legalan. Selekcija je proporcionalna težinama (weighted random).
        /// </summary>
        public Move? GetBookMove(GameState state)
        {
            var key = GetFenKey(state);
            if (!_book.TryGetValue(key, out var candidates))
                return null;

            var legalMoves = new MoveGenerator().GetLegalMoves(state);

            var valid = new List<(Move move, int weight)>(candidates.Count);
            foreach (var (uci, weight) in candidates)
            {
                var move = TryParseUci(uci, legalMoves);
                if (move != null)
                    valid.Add((move, weight));
            }

            if (valid.Count == 0) return null;

            int total = valid.Sum(v => v.weight);
            int roll = Random.Shared.Next(total);
            int cumulative = 0;
            foreach (var (move, weight) in valid)
            {
                cumulative += weight;
                if (roll < cumulative) return move;
            }
            return valid[0].move;
        }

        private static string GetFenKey(GameState state)
        {
            var fen = state.ToFen();
            var parts = fen.Split(' ');
            return parts[0] + " " + parts[1];
        }

        private static Move? TryParseUci(string uci, List<Move> legalMoves)
        {
            if (uci.Length < 4) return null;
            var from = Square.FromAlgebraic(uci[..2]);
            var to = Square.FromAlgebraic(uci[2..4]);
            return legalMoves.FirstOrDefault(m => m.From.Equals(from) && m.To.Equals(to));
        }
    }
}
