using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Top — klizi horizontalno i vertikalno bilo koji broj polja.
    /// Zaustavlja se na prvoj prepreci.
    /// </summary>
    public class Rook : Piece
    {
        // 4 smjera: gore, dolje, lijevo, desno
        private static readonly (int row, int col)[] _directions =
        {
            (+1,  0),   // gore
            (-1,  0),   // dolje
            ( 0, -1),   // lijevo
            ( 0, +1)    // desno
        };

        public Rook(PieceColor color, Square position)
            : base(color, PieceType.Rook, position) { }

        public override List<Move> GetPseudoLegalMoves(Board board)
        {
            var moves = new List<Move>();

            foreach (var (rowDir, colDir) in _directions)
            {
                // Idemo korak po korak u ovom smjeru
                int r = Position.Row + rowDir;
                int c = Position.Column + colDir;

                while (true)
                {
                    var target = new Square(r, c);

                    // Izašli smo van table — stop
                    if (!target.IsValid()) break;

                    // Prijateljska figura blokira — stop, ne dodajemo
                    if (board.HasFriendly(target, Color)) break;

                    // Neprijateljska figura — dodajemo hvatanje, ali dalje ne idemo
                    if (board.HasEnemy(target, Color))
                    {
                        moves.Add(new Move(Position, target, MoveType.Capture));
                        break;
                    }

                    // Prazno polje — dodajemo normalan potez i nastavljamo
                    moves.Add(new Move(Position, target, MoveType.Normal));

                    r += rowDir;
                    c += colDir;
                }
            }

            return moves;
        }

        public override Piece Clone() =>
            new Rook(Color, new Square(Position.Row, Position.Column));
    }
}