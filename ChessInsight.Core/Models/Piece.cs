using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChessInsight.Core.Enums;


namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Apstraktna bazna klasa za sve šahovske figure.
    /// Svaka konkretna figura nasljeđuje ovu klasu i
    /// implementira GetPseudoLegalMoves().
    /// </summary>
    public abstract class Piece
    {
        /// <summary>Boja figure — bijela ili crna.</summary>
        public PieceColor Color { get; }

        /// <summary>Tip figure — kralj, dama, top...</summary>
        public PieceType Type { get; }

        /// <summary>Trenutna pozicija figure na tabli.</summary>
        public Square Position { get; set; }

        protected Piece(PieceColor color, PieceType type, Square position)
        {
            Color = color;
            Type = type;
            Position = position;
        }

        /// <summary>
        /// Vraća listu pseudo-legalnih poteza za ovu figuru.
        /// Pseudo-legalni znači: ispravni po kretanju figure,
        /// ali bez provjere da li kralj ostaje u šahu.
        /// MoveGenerator će naknadno filtrirati ove poteze.
        /// </summary>
        public abstract List<Move> GetPseudoLegalMoves(Board board);

        /// <summary>
        /// Provjerava da li figura može napasti određeno polje.
        /// Koristi se za detekciju šaha.
        /// </summary>
        public virtual bool CanAttackSquare(Square target, Board board)
        {
            return GetPseudoLegalMoves(board)
                .Any(m => m.To.Equals(target));
        }

        /// <summary>
        /// Kreira duboku kopiju figure — potrebno za simulaciju poteza.
        /// </summary>
        public abstract Piece Clone();

        public override string ToString() =>
            $"{Color} {Type} on {Position}";
    }
}
