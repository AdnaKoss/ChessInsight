using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Skakač — jedina figura koja preskače druge figure.
    /// Kreće se u L-obliku: 2 polja u jednom smjeru + 1 polje okomito.
    /// </summary>
    public class Knight : Piece
    {
        // Svih 8 mogućih L-pomaka skakača
        private static readonly (int row, int col)[] _moves =
        {
            (-2, -1), (-2, +1),
            (-1, -2), (-1, +2),
            (+1, -2), (+1, +2),
            (+2, -1), (+2, +1)
        };

        public Knight(PieceColor color, Square position)
            : base(color, PieceType.Knight, position) { }

        public override List<Move> GetPseudoLegalMoves(Board board)
        {
            var moves = new List<Move>();

            foreach (var (rowOffset, colOffset) in _moves)
            {
                var target = new Square(
                    Position.Row + rowOffset,
                    Position.Column + colOffset
                );

                // Preskoči polja izvan table
                if (!target.IsValid()) continue;

                // Preskoči polja s prijateljskom figurom
                if (board.HasFriendly(target, Color)) continue;

                // Hvatanje ili normalan potez
                var type = board.HasEnemy(target, Color)
                    ? MoveType.Capture
                    : MoveType.Normal;

                moves.Add(new Move(Position, target, type));
            }

            return moves;
        }

        public override Piece Clone() =>
            new Knight(Color, new Square(Position.Row, Position.Column));
    }
}