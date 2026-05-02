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
    /// Odgovorna za čuvanje figura, premještanje i kloniranje stanja.
    /// Ne sadrži pravila igre — to je odgovornost GameState klase.
    /// </summary>
    public class Board
    {
        private readonly Piece?[,] _squares = new Piece?[8, 8];

        /// <summary>
        /// Polje na koje je moguć en passant potez, ili null ako nije dostupan.
        /// </summary>
        public Square? EnPassantSquare { get; set; }

        // ── Osnovne operacije ────────────────────────────────────

        /// <summary>Vraća figuru na zadanom polju, ili null ako je prazno.</summary>
        public Piece? GetPiece(Square square) =>
            _squares[square.Row, square.Column];

        /// <summary>Postavlja figuru na zadano polje.</summary>
        public void SetPiece(Square square, Piece? piece)
        {
            _squares[square.Row, square.Column] = piece;
            if (piece != null)
                piece.Position = square;
        }

        /// <summary>Uklanja figuru s polja i vraća je.</summary>
        public Piece? RemovePiece(Square square)
        {
            var piece = GetPiece(square);
            _squares[square.Row, square.Column] = null;
            return piece;
        }

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

        // ── Pretraga figura ──────────────────────────────────────

        /// <summary>Vraća sve figure na tabli.</summary>
        public IEnumerable<Piece> GetAllPieces()
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (_squares[r, c] != null)
                        yield return _squares[r, c]!;
        }

        /// <summary>Vraća sve figure određene boje.</summary>
        public IEnumerable<Piece> GetPieces(PieceColor color) =>
            GetAllPieces().Where(p => p.Color == color);

        /// <summary>Pronalazi kralja određene boje.</summary>
        public King? GetKing(PieceColor color) =>
            GetPieces(color).OfType<King>().FirstOrDefault();

        // ── Inicijalizacija početne pozicije ─────────────────────

        /// <summary>
        /// Postavlja sve figure na početne pozicije.
        /// Bijeli su na redovima 1-2 (indeksi 0-1),
        /// crni na redovima 7-8 (indeksi 6-7).
        /// </summary>
        public void SetupStartingPosition()
        {
            // Bijeli pješaci — red 2 (indeks 1)
            for (int c = 0; c < 8; c++)
                SetPiece(new Square(1, c), new Pawn(PieceColor.White, new Square(1, c)));

            // Crni pješaci — red 7 (indeks 6)
            for (int c = 0; c < 8; c++)
                SetPiece(new Square(6, c), new Pawn(PieceColor.Black, new Square(6, c)));

            // Bijele figure — red 1 (indeks 0)
            PlaceBackRank(PieceColor.White, 0);

            // Crne figure — red 8 (indeks 7)
            PlaceBackRank(PieceColor.Black, 7);
        }

        private void PlaceBackRank(PieceColor color, int row)
        {
            SetPiece(new Square(row, 0), new Rook(color, new Square(row, 0)));
            SetPiece(new Square(row, 1), new Knight(color, new Square(row, 1)));
            SetPiece(new Square(row, 2), new Bishop(color, new Square(row, 2)));
            SetPiece(new Square(row, 3), new Queen(color, new Square(row, 3)));
            SetPiece(new Square(row, 4), new King(color, new Square(row, 4)));
            SetPiece(new Square(row, 5), new Bishop(color, new Square(row, 5)));
            SetPiece(new Square(row, 6), new Knight(color, new Square(row, 6)));
            SetPiece(new Square(row, 7), new Rook(color, new Square(row, 7)));
        }

        // ── Kloniranje ───────────────────────────────────────────

        /// <summary>
        /// Kreira potpunu kopiju table.
        /// Potrebno za simulaciju poteza u Minimax algoritmu
        /// bez mijenjanja originalnog stanja.
        /// </summary>
        public Board Clone()
        {
            var clone = new Board();
            clone.EnPassantSquare = EnPassantSquare;

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (_squares[r, c] != null)
                        clone._squares[r, c] = _squares[r, c]!.Clone();

            return clone;
        }
    }
}