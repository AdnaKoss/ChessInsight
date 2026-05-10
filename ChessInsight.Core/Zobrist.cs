using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Core
{
    /// <summary>
    /// Zobrist hash — brzi fingerprint pozicije za detekciju ponavljanja.
    /// Fiksan seed garantuje iste vrijednosti pri svakom pokretanju.
    /// </summary>
    public static class Zobrist
    {
        // [boja 0/1, tip 0-5, polje 0-63]
        private static readonly ulong[,,] Pieces = new ulong[2, 6, 64];
        private static readonly ulong BlackToMove;
        private static readonly ulong[] Castling    = new ulong[4];
        private static readonly ulong[] EnPassant   = new ulong[8];

        static Zobrist()
        {
            var rng = new Random(0x5A3C9B12);
            for (int c = 0; c < 2; c++)
                for (int t = 0; t < 6; t++)
                    for (int s = 0; s < 64; s++)
                        Pieces[c, t, s] = NextULong(rng);

            BlackToMove = NextULong(rng);
            for (int i = 0; i < 4; i++) Castling[i]  = NextULong(rng);
            for (int i = 0; i < 8; i++) EnPassant[i] = NextULong(rng);
        }

        private static ulong NextULong(Random rng)
        {
            var buf = new byte[8];
            rng.NextBytes(buf);
            return BitConverter.ToUInt64(buf, 0);
        }

        public static ulong Compute(GameState state)
        {
            ulong h = 0;

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    var piece = state.Board.GetPiece(new Square(r, c));
                    if (piece == null) continue;
                    int ci = piece.Color == PieceColor.White ? 0 : 1;
                    int ti = TypeIndex(piece.Type);
                    h ^= Pieces[ci, ti, r * 8 + c];
                }

            if (state.CurrentPlayer == PieceColor.Black)
                h ^= BlackToMove;

            if (state.WhiteCanCastleKingside)  h ^= Castling[0];
            if (state.WhiteCanCastleQueenside) h ^= Castling[1];
            if (state.BlackCanCastleKingside)  h ^= Castling[2];
            if (state.BlackCanCastleQueenside) h ^= Castling[3];

            if (state.Board.EnPassantSquare != null)
                h ^= EnPassant[state.Board.EnPassantSquare.Column];

            return h;
        }

        private static int TypeIndex(PieceType t) => t switch
        {
            PieceType.King   => 0,
            PieceType.Queen  => 1,
            PieceType.Rook   => 2,
            PieceType.Bishop => 3,
            PieceType.Knight => 4,
            PieceType.Pawn   => 5,
            _ => 0
        };
    }
}
