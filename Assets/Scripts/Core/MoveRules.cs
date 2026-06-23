using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Port of getValidMoves(). Given the board and the player's current form,
    // returns every legal destination. In Trap mode the form is always Knight.
    // In Battle mode the player can be a sliding piece or a nightrider, so the
    // table-driven ray logic from the original is preserved.
    public static class MoveRules
    {
        public static List<MoveTarget> GetValidMoves(
            IReadOnlyList<GamePiece> pieces,
            int playerCol,
            float playerY,
            GameMode mode,
            PieceType playerForm)
        {
            var moves = new List<MoveTarget>();
            var seen = new HashSet<string>();

            // Mirrors the inner tryAdd(): bounds-check, collision-check, then add
            // (deduplicated by column + relative row). Returns false when the cell
            // is blocked/off-board, which stops a ray from continuing past it.
            bool TryAdd(int nc, float ny)
            {
                if (nc < 0 || nc >= GameConfig.Cols) return false;
                if (ny < -1f || ny > GameConfig.Rows + 1f) return false;

                for (int j = 0; j < pieces.Count; j++)
                {
                    var o = pieces[j];
                    if (!o.Alive) continue;
                    if (o.Col == nc && Math.Abs(o.Y - ny) < 0.5f)
                    {
                        // In Battle, a non-trap piece is capturable and does NOT
                        // block the cell. Everything else (incl. traps) blocks.
                        bool capturable = mode == GameMode.Battle && o.Type != PieceType.Trap;
                        if (!capturable) return false;
                    }
                }

                int relRow = (int)Math.Round(ny - playerY);
                string key = nc + "|" + relRow;
                if (seen.Contains(key)) return true; // already have this cell, but ray may continue
                seen.Add(key);
                moves.Add(new MoveTarget(nc, ny));
                return true;
            }

            PieceType form = mode == GameMode.Battle ? playerForm : PieceType.Knight;

            if (form == PieceType.Knight)
            {
                foreach (var d in GameConfig.LDeltas)
                    TryAdd(playerCol + d.Col, playerY + d.Row);
            }
            else if (form == PieceType.Nightrider)
            {
                // Rides along each L-direction until something blocks it.
                foreach (var d in GameConfig.LDeltas)
                    for (int k = 1; k <= GameConfig.RayMax; k++)
                        if (!TryAdd(playerCol + d.Col * k, playerY + d.Row * k)) break;
            }
            else
            {
                var dirs = new List<Int2>();
                if (form == PieceType.Rook || form == PieceType.Queen)
                {
                    dirs.Add(new Int2(1, 0)); dirs.Add(new Int2(-1, 0));
                    dirs.Add(new Int2(0, 1)); dirs.Add(new Int2(0, -1));
                }
                if (form == PieceType.Bishop || form == PieceType.Queen)
                {
                    dirs.Add(new Int2(1, 1)); dirs.Add(new Int2(1, -1));
                    dirs.Add(new Int2(-1, 1)); dirs.Add(new Int2(-1, -1));
                }
                foreach (var d in dirs)
                    for (int k = 1; k <= GameConfig.RayMax; k++)
                        if (!TryAdd(playerCol + d.Col * k, playerY + d.Row * k)) break;
            }

            return moves;
        }
    }
}
