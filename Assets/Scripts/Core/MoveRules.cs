using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Port of getValidMoves(). Given the board and the player's current form,
    // returns every legal destination. In Trap mode the form is always Knight.
    // In Battle mode the player can be a sliding piece or a nightrider, so the
    // table-driven ray logic from the original is preserved. Adventure runs in
    // Trap mode, but its transformation world lets the knight become a sliding
    // piece too, so it opts in via the adventureForm flag.
    public static class MoveRules
    {
        private static readonly Int2[] Ortho =
            { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) };
        private static readonly Int2[] Diag =
            { new Int2(1, 1), new Int2(1, -1), new Int2(-1, 1), new Int2(-1, -1) };

        // Allocating convenience wrapper, used on tap (not per frame).
        public static List<MoveTarget> GetValidMoves(
            IReadOnlyList<GamePiece> pieces, int playerCol, float playerY,
            GameMode mode, PieceType playerForm, bool adventureForm = false,
            float rayLo = -1f, float rayHi = GameConfig.Rows + 1f)
        {
            var outMoves = new List<MoveTarget>();
            Fill(outMoves, pieces, playerCol, playerY, mode, playerForm, adventureForm, rayLo, rayHi);
            return outMoves;
        }

        // Allocation-free: clears and fills the caller's buffer. Use this every
        // frame (move hints) so it doesn't churn the GC. No dedup set is needed:
        // rays from a single origin never revisit the same cell.
        // rayLo/rayHi bound how far a ray (or knight jump) may reach in Y. They default to
        // the ±1 off-board SCREEN margin used by the scrolling modes. Tall adventure maps
        // pass the whole map's extent (in screen space) instead, so a slider's reach depends
        // on the board, not the transient follow/manual camera — otherwise scrolling the view
        // would clip your legal moves, and long slides couldn't cross the visible window.
        public static void Fill(
            List<MoveTarget> outMoves, IReadOnlyList<GamePiece> pieces,
            int playerCol, float playerY, GameMode mode, PieceType playerForm,
            bool adventureForm = false, float rayLo = -1f, float rayHi = GameConfig.Rows + 1f)
        {
            outMoves.Clear();

            // Mirrors the inner tryAdd(): bounds-check, collision-check, then add.
            // Returns false when the cell is blocked/off-board, stopping a ray.
            bool TryAdd(int nc, float ny)
            {
                if (nc < 0 || nc >= GameConfig.Cols) return false;
                if (ny < rayLo || ny > rayHi) return false;

                for (int j = 0; j < pieces.Count; j++)
                {
                    var o = pieces[j];
                    if (!o.Alive) continue;
                    if (o.Col == nc && Math.Abs(o.Y - ny) < 0.5f)
                    {
                        // In Battle a non-trap piece is capturable and does NOT
                        // block the cell. Everything else (incl. traps) blocks.
                        bool capturable = mode == GameMode.Battle && o.Type != PieceType.Trap;
                        if (!capturable) return false;
                    }
                }

                outMoves.Add(new MoveTarget(nc, ny));
                return true;
            }

            PieceType form = (mode == GameMode.Battle || adventureForm) ? playerForm : PieceType.Knight;

            if (form == PieceType.Knight)
            {
                for (int i = 0; i < GameConfig.LDeltas.Length; i++)
                    TryAdd(playerCol + GameConfig.LDeltas[i].Col, playerY + GameConfig.LDeltas[i].Row);
            }
            else if (form == PieceType.Nightrider)
            {
                for (int i = 0; i < GameConfig.LDeltas.Length; i++)
                {
                    var d = GameConfig.LDeltas[i];
                    for (int k = 1; k <= GameConfig.RayMax; k++)
                        if (!TryAdd(playerCol + d.Col * k, playerY + d.Row * k)) break;
                }
            }
            else
            {
                if (form == PieceType.Rook || form == PieceType.Queen)
                    for (int i = 0; i < Ortho.Length; i++)
                    {
                        var d = Ortho[i];
                        for (int k = 1; k <= GameConfig.RayMax; k++)
                            if (!TryAdd(playerCol + d.Col * k, playerY + d.Row * k)) break;
                    }
                if (form == PieceType.Bishop || form == PieceType.Queen)
                    for (int i = 0; i < Diag.Length; i++)
                    {
                        var d = Diag[i];
                        for (int k = 1; k <= GameConfig.RayMax; k++)
                            if (!TryAdd(playerCol + d.Col * k, playerY + d.Row * k)) break;
                    }
            }
        }
    }
}
