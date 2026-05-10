using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    internal enum TtFlag : byte { Exact, LowerBound, UpperBound }

    internal struct TtEntry
    {
        public ulong Hash;
        public int   Score;
        public short Depth;
        public TtFlag Flag;
        public byte  _pad;
        public Move? BestMove;
    }

    /// <summary>
    /// Transposition table — cache već evaluiranih pozicija.
    /// Sprečava ponovnu pretragu istih pozicija do kojih se dolazi
    /// različitim redoslijedom poteza (transpozicije).
    /// Veličina: 2^21 = 2 097 152 unosa ≈ 64 MB
    /// </summary>
    internal sealed class TranspositionTable
    {
        private const int  Size = 1 << 21;
        private const uint Mask = Size - 1;

        private readonly TtEntry[] _table = new TtEntry[Size];

        /// <summary>
        /// Pokušaj dohvatiti evaluaciju iz tabele.
        /// Uvijek postavlja <paramref name="bestMove"/> ako unos postoji
        /// (korisno za move ordering čak i kad score nije iskoristiv).
        /// Vraća true samo kad se score može direktno koristiti.
        /// </summary>
        public bool TryProbe(ulong hash, int depth, int alpha, int beta,
            out int score, out Move? bestMove)
        {
            ref var e = ref _table[hash & Mask];
            bestMove = null;
            score    = 0;

            if (e.Hash != hash) return false;

            bestMove = e.BestMove;
            if (e.Depth < depth) return false;

            score = e.Score;
            return e.Flag switch
            {
                TtFlag.Exact      => true,
                TtFlag.LowerBound => score >= beta,
                TtFlag.UpperBound => score <= alpha,
                _ => false
            };
        }

        /// <summary>Spremi evaluaciju u tabelu.</summary>
        public void Store(ulong hash, int depth, int score, TtFlag flag, Move? bestMove)
        {
            ref var e = ref _table[hash & Mask];

            // Zamijeni: prazan slot, veća dubina, ili isti hash (osvježi)
            if (e.Hash == 0 || depth >= e.Depth || e.Hash == hash)
            {
                e.Hash     = hash;
                e.Depth    = (short)depth;
                e.Score    = score;
                e.Flag     = flag;
                e.BestMove = bestMove;
            }
        }

        public void Clear() => Array.Clear(_table, 0, Size);
    }
}
