using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Predstavlja jedan šahovski potez — s kojeg polja na koje,
    /// tip poteza i eventualna figura za promociju pješaka.
    /// Move je immutable — jednom kreiran, ne mijenja se.
    /// </summary>
    public class Move
    {
        /// <summary>Polje s kojeg se figura premješta.</summary>
        public Square From { get; }

        /// <summary>Polje na koje se figura premješta.</summary>
        public Square To { get; }

        /// <summary>Tip poteza (normalan, hvatanje, rokada...).</summary>
        public MoveType Type { get; }

        /// <summary>
        /// Figura na koju se pješak promovira.
        /// Null za sve poteze osim PawnPromotion.
        /// </summary>
        public PieceType? PromotionPiece { get; }

        public Move(Square from, Square to,
                    MoveType type = MoveType.Normal,
                    PieceType? promotionPiece = null)
        {
            From = from;
            To = to;
            Type = type;
            PromotionPiece = promotionPiece;
        }

        /// <summary>
        /// Prikazuje potez u algebarskoj notaciji, npr. "e2→e4" ili "e7→e8(Q)".
        /// </summary>
        public override string ToString()
        {
            string move = $"{From}→{To}";

            if (Type == MoveType.PawnPromotion && PromotionPiece.HasValue)
                move += $"({PromotionPiece.Value})";
            else if (Type == MoveType.CastleKingside)
                move += " (O-O)";
            else if (Type == MoveType.CastleQueenside)
                move += " (O-O-O)";
            else if (Type == MoveType.EnPassant)
                move += " (e.p.)";

            return move;
        }

        public override bool Equals(object? obj) =>
            obj is Move other &&
            From.Equals(other.From) &&
            To.Equals(other.To) &&
            Type == other.Type;

        public override int GetHashCode() =>
            HashCode.Combine(From, To, Type);
    }
}
