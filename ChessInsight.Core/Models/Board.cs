using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Predstavlja šahovsku tablu 8x8.
    /// Stub verzija — puna implementacija dolazi nakon figura.
    /// </summary>
    public class Board
    {
        private readonly Piece?[,] _squares = new Piece?[8, 8];

        /// <summary>Vraća figuru na zadanom polju, ili null ako je polje prazno.</summary>
        public Piece? GetPiece(Square square) =>
            _squares[square.Row, square.Column];

        /// <summary>Postavlja figuru na zadano polje.</summary>
        public void SetPiece(Square square, Piece? piece) =>
            _squares[square.Row, square.Column] = piece;

        /// <summary>Provjerava da li je polje prazno.</summary>
        public bool IsEmpty(Square square) =>
            GetPiece(square) == null;

        /// <summary>Provjerava da li polje sadrži figuru protivničke boje.</summary>
        public bool HasEnemy(Square square, PieceColor friendlyColor)
        {
            var piece = GetPiece(square);
            return piece != null && piece.Color != friendlyColor;
        }

        /// <summary>Provjerava da li polje sadrži figuru iste boje.</summary>
        public bool HasFriendly(Square square, PieceColor friendlyColor)
        {
            var piece = GetPiece(square);
            return piece != null && piece.Color == friendlyColor;
        }
    }
}