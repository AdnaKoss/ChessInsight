using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Core
{
    public static class FenParser
    {
        // Standardna početna pozicija
        public const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        /// <summary>
        /// Parsira FEN string i vraća GameState.
        /// Format: "figure aktivan rokada enPassant halfMove fullMove"
        /// Primjer: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
        /// </summary>
        public static GameState Parse(string fen)
        {
            if (string.IsNullOrWhiteSpace(fen))
                throw new ArgumentException("FEN string ne smije biti prazan.");

            var parts = fen.Trim().Split(' ');
            if (parts.Length < 1)
                throw new ArgumentException("Nevažeći FEN format.");

            var board = new Board();
            ParsePieces(board, parts[0]);

            var currentPlayer = parts.Length > 1 && parts[1] == "b"
                ? PieceColor.Black
                : PieceColor.White;

            bool wCK, wCQ, bCK, bCQ;
            if (parts.Length > 2)
            {
                wCK = parts[2].Contains('K');
                wCQ = parts[2].Contains('Q');
                bCK = parts[2].Contains('k');
                bCQ = parts[2].Contains('q');
            }
            else
            {
                wCK = wCQ = bCK = bCQ = true;
            }

            if (parts.Length > 3 && parts[3] != "-")
            {
                try { board.EnPassantSquare = Square.FromAlgebraic(parts[3]); }
                catch { /* ignoriši nevažeće ep polje */ }
            }

            int halfMove = parts.Length > 4 && int.TryParse(parts[4], out int hm) ? hm : 0;
            int fullMove = parts.Length > 5 && int.TryParse(parts[5], out int fm) ? fm : 1;

            return GameState.FromFen(board, currentPlayer, wCK, wCQ, bCK, bCQ, halfMove, fullMove);
        }

        private static void ParsePieces(Board board, string placement)
        {
            var rows = placement.Split('/');
            if (rows.Length != 8)
                throw new ArgumentException("FEN raspored figura mora imati tačno 8 redova odvojenih sa '/'.");

            for (int fenRow = 0; fenRow < 8; fenRow++)
            {
                // FEN počinje od reda 8 (indeks 7 u našem sistemu)
                int boardRow = 7 - fenRow;
                int col = 0;

                foreach (char c in rows[fenRow])
                {
                    if (char.IsDigit(c))
                    {
                        col += c - '0';
                        continue;
                    }

                    if (col > 7)
                        throw new ArgumentException($"Previše figura u redu {8 - fenRow} FEN stringa.");

                    var color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
                    var square = new Square(boardRow, col);

                    Piece piece = char.ToLower(c) switch
                    {
                        'p' => new Pawn(color, square),
                        'n' => new Knight(color, square),
                        'b' => new Bishop(color, square),
                        'r' => new Rook(color, square),
                        'q' => new Queen(color, square),
                        'k' => new King(color, square),
                        _ => throw new ArgumentException($"Nepoznati FEN karakter za figuru: '{c}'")
                    };

                    board.SetPiece(square, piece);
                    col++;
                }
            }
        }
    }
}
