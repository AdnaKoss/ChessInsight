using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessInsight.Core.Enums
{
    public enum MoveType
    {
        Normal,
        Capture,
        EnPassant,
        CastleKingside,
        CastleQueenside,
        PawnPromotion
    }
}
