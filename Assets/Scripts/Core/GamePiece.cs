namespace KnightsGauntlet.Core
{
    // One obstacle/enemy on the board. Replaces the anonymous objects the JS
    // pushed into the `obstacles` array. Position is (Col, Y) where Y is in
    // cell units from the top of the play area, increasing downward, scrolling
    // with the board each tick.
    public class GamePiece
    {
        public PieceType Type;
        public int Col;
        public float Y;
        public bool Alive = true;

        // Grid-lock: Y is derived as (Base + ScrollCells) each tick in Trap mode,
        // so traps stay perfectly aligned to the checkerboard instead of drifting
        // from independent per-piece float accumulation. NaN = not yet initialised.
        public float Base = float.NaN;

        // Battle-mode AI bookkeeping (unused in Trap mode; wired in Phase 2).
        public int Dir = 1;       // bishop diagonal direction
        public int? PlanDC;       // pre-planned knight jump, shown as a threat tile
        public int? PlanDR;

        public GamePiece(PieceType type, int col, float y)
        {
            Type = type;
            Col = col;
            Y = y;
        }
    }
}
