namespace KnightsGauntlet.Core
{
    // A grid delta / offset (column, row). Used for the L-jump tables, queen
    // adjacency, and ray directions. Pure data, no UnityEngine dependency so the
    // whole Core layer stays testable outside the editor.
    public readonly struct Int2
    {
        public readonly int Col;
        public readonly int Row;
        public Int2(int col, int row) { Col = col; Row = row; }
    }

    // A legal destination the player could tap to. Y is in CELL units measured
    // from the top of the play area (0 = top, increases downward). It is a float
    // because the board scrolls continuously, so the player's row drifts between
    // integer values and only snaps on a move.
    public readonly struct MoveTarget
    {
        public readonly int Col;
        public readonly float Y;
        public MoveTarget(int col, float y) { Col = col; Y = y; }
    }

    // A collectable shillings drop sitting on the board (trap & battle modes).
    // Scrolls down with everything; collected when the player lands on its cell.
    public class Pickup
    {
        public int Col;
        public float Y;
        public int Value;
        public bool Alive = true;
        public float Base = float.NaN; // grid-lock base (see GamePiece.Base)
        public Pickup(int col, float y, int value) { Col = col; Y = y; Value = value; }
    }
}
