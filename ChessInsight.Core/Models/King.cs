using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Kralj — najvažnija figura. Kreće se jedno polje u bilo kojem smjeru.
    /// Rokada se ovdje dodaje kao pseudo-legalan potez,
    /// MoveGenerator će provjeriti da li je legalna.
    /// </summary>
    public class King : Piece
    {
        // Svih 8 smjerova, ali samo 1 korak
        private static readonly (int row, int col)[] _directions =
        {
            (+1,  0), (-1,  0),
            ( 0, +1), ( 0, -1),
            (+1, +1), (+1, -1),
            (-1, +1), (-1, -1)
        };

        public King(PieceColor color, Square position)
            : base(color, PieceType.King, position) { }

        public override List<Move> GetPseudoLegalMoves(Board board)
        {
            var moves = new List<Move>();

            // Normalni potezi — jedno polje u svim smjerovima
            foreach (var (rowDir, colDir) in _directions)
            {
                var target = new Square(
                    Position.Row + rowDir,
                    Position.Column + colDir
                );

                if (!target.IsValid()) continue;
                if (board.HasFriendly(target, Color)) continue;

                var type = board.HasEnemy(target, Color)
                    ? MoveType.Capture
                    : MoveType.Normal;

                moves.Add(new Move(Position, target, type));
            }

            // Rokada — dodajemo kao kandidate, MoveGenerator provjerava uslove
            int baseRow = Color == PieceColor.White ? 0 : 7;

            if (Position.Equals(new Square(baseRow, 4)))
            {
                // Mali rok (kingside) — kralj ide na kolonu 6
                moves.Add(new Move(
                    Position,
                    new Square(baseRow, 6),
                    MoveType.CastleKingside
                ));

                // Veliki rok (queenside) — kralj ide na kolonu 2
                moves.Add(new Move(
                    Position,
                    new Square(baseRow, 2),
                    MoveType.CastleQueenside
                ));
            }

            return moves;
        }

        public override Piece Clone() =>
            new King(Color, new Square(Position.Row, Position.Column));
    }
}