using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Predstavlja jedno polje na šahovskoj tabli.
    /// Redovi i kolone su indeksirani od 0 do 7.
    /// Red 0 = red 1 (bijela strana), Kolona 0 = kolona A.
    /// </summary>
    public class Square
    {
        public int Row { get; }
        public int Column { get; }

        public Square(int row, int column)
        {
            Row = row;
            Column = column;
        }

        /// <summary>
        /// Provjerava da li je polje unutar granica table (0-7).
        /// </summary>
        public bool IsValid() =>
            Row >= 0 && Row <= 7 && Column >= 0 && Column <= 7;

        /// <summary>
        /// Pretvara polje u algebarsku notaciju, npr. (0,0) → "a1", (7,7) → "h8".
        /// </summary>
        public string ToAlgebraic()
        {
            char col = (char)('a' + Column);
            int row = Row + 1;
            return $"{col}{row}";
        }

        /// <summary>
        /// Kreira Square iz algebarske notacije, npr. "e4" → Square(3, 4).
        /// </summary>
        public static Square FromAlgebraic(string notation)
        {
            int column = notation[0] - 'a';
            int row = notation[1] - '1';
            return new Square(row, column);
        }

        // Dva polja su jednaka ako imaju isti red i kolonu
        public override bool Equals(object? obj) =>
            obj is Square other && Row == other.Row && Column == other.Column;

        public override int GetHashCode() => HashCode.Combine(Row, Column);

        public override string ToString() => ToAlgebraic();
    }
}
