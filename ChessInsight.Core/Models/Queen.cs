using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Dama — najjača figura. Kombinuje kretanje Topa i Lovca.
    /// Klizi u svih 8 smjerova bilo koji broj polja.
    /// </summary>
    public class Queen : Piece
    {
        // Svih 8 smjerova — horizontalno, vertikalno i dijagonalno
        private static readonly (int row, int col)[] _directions =
        {
            (+1,  0),   // gore
            (-1,  0),   // dolje
            ( 0, +1),   // desno
            ( 0, -1),   // lijevo
            (+1, +1),   // gore-desno
            (+1, -1),   // gore-lijevo
            (-1, +1),   // dolje-desno
            (-1, -1)    // dolje-lijevo
        };

        public Queen(PieceColor color, Square position)
            : base(color, PieceType.Queen, position) { }

        public override List<Move> GetPseudoLegalMoves(Board board)
        {
            var moves = new List<Move>();

            foreach (var (rowDir, colDir) in _directions)
            {
                int r = Position.Row + rowDir;
                int c = Position.Column + colDir;

                while (true)
                {
                    var target = new Square(r, c);

                    if (!target.IsValid()) break;
                    if (board.HasFriendly(target, Color)) break;

                    if (board.HasEnemy(target, Color))
                    {
                        moves.Add(new Move(Position, target, MoveType.Capture));
                        break;
                    }

                    moves.Add(new Move(Position, target, MoveType.Normal));
                    r += rowDir;
                    c += colDir;
                }
            }

            return moves;
        }

        public override Piece Clone() =>
            new Queen(Color, new Square(Position.Row, Position.Column));
    }
}