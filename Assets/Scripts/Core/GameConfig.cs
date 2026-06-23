using System;

namespace KnightsGauntlet.Core
{
    // Direct port of the constants that lived at the top of the <script> block.
    // Everything that was a JS object literal keyed by string is a switch
    // expression here. Tune values here; nothing else hard-codes them.
    public static class GameConfig
    {
        public const int Cols = 5;
        public const int Rows = 10;

        public const int RayMax = 12;            // ray length cap for sliding forms
        public const int TransformTrigger = 3;   // captures in a row to transform
        public const float TransformSeconds = 5f;
        public const int ComboFormBonus = 3;     // bishop & nightrider build the x faster

        // ── Move tables ──────────────────────────────────────────────────────
        // Knight L-jumps used for the PLAYER's moves (dCol, dRow).
        public static readonly Int2[] LDeltas =
        {
            new Int2(-2, -1), new Int2(-2, 1), new Int2(2, -1), new Int2(2, 1),
            new Int2(-1, -2), new Int2(1, -2), new Int2(-1, 2), new Int2(1, 2)
        };

        // Knight jumps for ENEMY knight AI (same shape, different table in the
        // original so kept separate in case the orderings ever diverge).
        public static readonly Int2[] KnightJumps =
        {
            new Int2(1, 2), new Int2(-1, 2), new Int2(1, -2), new Int2(-1, -2),
            new Int2(2, 1), new Int2(2, -1), new Int2(-2, 1), new Int2(-2, -1)
        };

        // 8 cells around a queen, used for its threat + movement.
        public static readonly Int2[] QueenAdj =
        {
            new Int2(-1, -1), new Int2(0, -1), new Int2(1, -1), new Int2(-1, 0),
            new Int2(1, 0), new Int2(-1, 1), new Int2(0, 1), new Int2(1, 1)
        };

        // ── Scoring / combo ─────────────────────────────────────────────────
        public static int PiecePoints(PieceType t) => t switch
        {
            PieceType.Rook => 1,
            PieceType.Bishop => 3,
            PieceType.Knight => 5,
            PieceType.Queen => 10,
            _ => 1
        };

        // Inverse to point value: cheap pieces build the multiplier fastest.
        public static int ComboInc(PieceType t) => t switch
        {
            PieceType.Rook => 4,
            PieceType.Bishop => 3,
            PieceType.Knight => 2,
            PieceType.Queen => 1,
            _ => 1
        };

        // Captureless moves allowed before the multiplier breaks.
        public static int ComboGrace(Difficulty d) => d switch
        {
            Difficulty.Easy => 3,
            Difficulty.Medium => 2,
            _ => 1 // hard & grandmaster break instantly
        };

        // ── Scroll speed ─────────────────────────────────────────────────────
        // FLAG: the original speeds (TRAP_DIFF) were in PIXELS/sec, so the felt
        // pace changed with screen size. Here they are CELLS/sec, which keeps the
        // feel identical across phones. Values below are the px figures converted
        // at a ~10-row reference height; expect to retune by feel on device.
        // Ramp matches the original exactly: speed grows 0.01 cells/s per cell
        // scrolled, capped at max.
        public static (float start, float max) TrapSpeed(Difficulty d) => d switch
        {
            Difficulty.Easy => (1.0f, 2.6f),
            Difficulty.Medium => (1.6f, 3.4f),
            _ => (2.1f, 4.4f) // hard & grandmaster
        };

        public const float TrapSpeedRampPerCell = 0.01f;

        // ── Battle pacing / density (used in Phase 2) ───────────────────────
        public static int BattleMoveIntervalMs(Difficulty d) => d switch
        {
            Difficulty.Easy => 3500,
            Difficulty.Medium => 2200,
            _ => 800
        };

        public struct SpawnDensity
        {
            public float Chance; // a row has a piece
            public float Extra;  // chance of a 2nd piece that row
            public SpawnDensity(float chance, float extra) { Chance = chance; Extra = extra; }
        }

        public static SpawnDensity BattleSpawn(Difficulty d) => d switch
        {
            Difficulty.Easy => new SpawnDensity(0.90f, 0.50f),
            Difficulty.Medium => new SpawnDensity(0.72f, 0.25f),
            _ => new SpawnDensity(0.55f, 0.0f)
        };

        // ── Presentation hints (consumed by the Unity view layer) ───────────
        // Kept as hex strings so Core stays UnityEngine-free. The view converts
        // these to Color. FLAG: the original drew unicode chess glyphs on the
        // canvas; those render inconsistently across devices. For the App Store
        // build I'd swap to sprites and use these only as an editor fallback.
        public static string PieceColorHex(PieceType t) => t switch
        {
            PieceType.Rook => "#4080E0",
            PieceType.Bishop => "#9060E0",
            PieceType.Knight => "#50C878",
            PieceType.Queen => "#E04080",
            PieceType.Trap => "#CC3333",
            _ => "#F0C040"
        };

        public static string Glyph(PieceType t) => t switch
        {
            PieceType.Rook => "\u265C",       // ♜
            PieceType.Bishop => "\u265D",      // ♝
            PieceType.Knight => "\u265E",      // ♞
            PieceType.Queen => "\u265B",       // ♛
            PieceType.Trap => "\u2715",        // ✕
            PieceType.Nightrider => "\u2658",  // ♘
            _ => "\u265E"
        };
    }
}
