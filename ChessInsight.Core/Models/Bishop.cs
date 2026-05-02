using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Lovac — klizi dijagonalno bilo koji broj polja.
    /// Uvijek ostaje na istoj boji polja.
    /// </summary>
    public class Bishop : Piece
    {
        // 4 dijagonalna smjera
        private static readonly (int row, int col)[] _directions =
        {
            (+1, +1),   // gore-desno
            (+1, -1),   // gore-lijevo
            (-1, +1),   // dolje-desno
            (-1, -1)    // dolje-lijevo
        };

        public Bishop(PieceColor color, Square position)
            : base(color, PieceType.Bishop, position) { }

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
            new Bishop(Color, new Square(Position.Row, Position.Column));
    }
}