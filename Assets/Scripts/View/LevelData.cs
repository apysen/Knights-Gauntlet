namespace KnightsGauntlet.Core
{
    public enum Objective { Reach, CaptureAll, Combined }

    // One oscillating enemy in a level: a piece at (Col,Row) that bobs between that
    // tile and (Col+OffCol, Row+OffRow) one step per knight move. Pawn: off = 0,0.
    public struct EnemySpec
    {
        public PieceType Type;
        public int Col, Row, OffCol, OffRow;
        public bool Hunter; // true = a queen that chases the knight (chess movement, trap-blocked), not a capture target
        public EnemySpec(PieceType type, int col, int row, int offCol, int offRow, bool hunter = false)
        { Type = type; Col = col; Row = row; OffCol = offCol; OffRow = offRow; Hunter = hunter; }
    }

    // An adventure level. Two flavors:
    //   ScrollSpeed == 0 : stationary puzzle. Objective (Reach/CaptureAll/Combined)
    //                      decides the win; Traps/Enemies/Goal are hand-placed.
    //   ScrollSpeed  > 0 : scrolling survival. The board auto-scrolls at ScrollSpeed
    //                      (cells/sec) over a deterministic (Seed) trap field; win by
    //                      surviving FinishCells of scroll. Speed ramps across worlds.
    // All scored by fewest knight moves. HasShilling drops one coin on a random
    // reachable cell each load (currency only, never affects stars).
    public class LevelData
    {
        public string Id;
        public string Name;
        public Objective Objective;   // stationary only; defaults to Reach
        public float ScrollSpeed;     // 0 = stationary; >0 = scrolling survival
        public int Seed;              // scrolling: deterministic trap field
        public float FinishCells;     // scrolling: scroll distance to complete
        public int StartCol, StartRow;
        public int GoalCol, GoalRow;  // Reach / Combined
        public Int2[] Traps;
        public Int2[] Cracked;        // fragile tiles: safe once, collapse to a trap on leaving
        public EnemySpec[] Enemies;   // CaptureAll / Combined
        public bool HasShilling;      // spawn one random reachable coin (currency only)
        public bool Transform;        // transformation world: capturing enemies changes the
                                      // knight's form (2 pips) so it can slide to an otherwise
                                      // unreachable goal. Misses never reset the pips.
        public int MapRows;           // 0 = normal 10-row board; >10 = tall follow-camera map
        // Queen-boss encounter: play these layouts in sequence. Each phase adds a queen
        // (an EnemySpec with Hunter = true) shielded by its minions; capturing the queen
        // clears the phase and transports you to the next. Null for ordinary levels.
        public LevelData[] Phases;
        public int ThreeStar;
        public int TwoStar;
    }

    public static class LevelDataExt
    {
        public static bool IsBoss(this LevelData lvl) => lvl != null && lvl.Phases != null && lvl.Phases.Length > 0;
    }
}
