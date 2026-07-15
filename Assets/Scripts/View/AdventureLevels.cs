namespace KnightsGauntlet.Core
{
    // Prototype content, all solver-validated (guaranteed solvable, 3-star = true
    // optimal move count; 2-star = optimal + 2). Six worlds of six levels each, plus
    // World 7: a single three-phase queen-boss encounter (see the "7-1" entry / Phases).
    // Each of the first five worlds introduces one mechanic: the first three levels
    // drill that mechanic in isolation, the last three combine it with every earlier
    // world's mechanic. World 6 is the gauntlet: all mechanics at once, escalating.
    // Difficulty (optimal move count) rises across each world.
    //   World 1 - Reach: maneuver around traps to the goal tile.
    //   World 2 - CaptureAll: capture every oscillating enemy (4-6 add the goal).
    //   World 3 - Tall maps: follow-camera vertical climbs (4-6 add enemies).
    //   World 4 - Cracked tiles: safe once, collapse to a trap on leaving
    //             (4-6 combine cracked + tall + enemies + goal).
    //   World 5 - Transform: the goal is unreachable by an L-move. Capture two
    //             enemies to take their form (2 pips; misses never reset them),
    //             then slide as a rook/bishop to the goal. Targets are stationary
    //             for 5-1/5-2, then move; 5-5/5-6 add a third target you capture
    //             after transforming.
    //   World 6 - The gauntlet: every earlier mechanic combined in one level each
    //             (traps, capture-all, tall climbs, cracked ground, transform),
    //             escalating to the tall transform finale "Apotheosis".
    // Enemy oscillation: rook = (0,±1) vertical, bishop = (±1,±1) diagonal,
    // pawn/stationary = (0,0).
    public static class AdventureLevels
    {
        public static readonly LevelData[] All =
        {
            // ── World 1 : maneuvering around traps to a goal ──────────────────
            new LevelData
            {
                Id = "1-1", Name = "First Steps", Objective = Objective.Reach,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[]
                {
                    new Int2(0,4), new Int2(1,1), new Int2(1,7), new Int2(2,3),
                    new Int2(2,5), new Int2(3,1), new Int2(3,7), new Int2(4,4)
                },
                ThreeStar = 5, TwoStar = 7
            },
            new LevelData
            {
                Id = "1-2", Name = "Squeeze", Objective = Objective.Reach,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[]
                {
                    new Int2(0,1), new Int2(1,1), new Int2(1,2), new Int2(1,3), new Int2(1,5),
                    new Int2(2,4), new Int2(2,8), new Int2(3,7), new Int2(4,1), new Int2(4,4), new Int2(4,9)
                },
                ThreeStar = 7, TwoStar = 9
            },
            new LevelData
            {
                Id = "1-3", Name = "Gauntlet", Objective = Objective.Reach,
                StartCol = 0, StartRow = 9, GoalCol = 4, GoalRow = 0,
                Traps = new[]
                {
                    new Int2(1,3), new Int2(1,5), new Int2(1,6), new Int2(1,7), new Int2(2,6),
                    new Int2(3,2), new Int2(3,3), new Int2(3,4), new Int2(3,5), new Int2(3,6),
                    new Int2(3,7), new Int2(4,7), new Int2(4,9)
                },
                ThreeStar = 9, TwoStar = 11
            },
            new LevelData
            {
                Id = "1-4", Name = "Threadneedle", Objective = Objective.Reach,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(4,6), new Int2(1,7), new Int2(3,7), new Int2(3,2), new Int2(4,1), new Int2(2,2), new Int2(0,0), new Int2(4,5), new Int2(2,7), new Int2(4,8), new Int2(2,4), new Int2(1,2), new Int2(2,5) },
                ThreeStar = 9, TwoStar = 11
            },
            new LevelData
            {
                Id = "1-5", Name = "Switchback", Objective = Objective.Reach,
                StartCol = 0, StartRow = 9, GoalCol = 4, GoalRow = 0,
                Traps = new[] { new Int2(3,3), new Int2(0,1), new Int2(1,3), new Int2(3,9), new Int2(0,4), new Int2(0,3), new Int2(3,1), new Int2(3,2), new Int2(2,4), new Int2(4,2), new Int2(3,0), new Int2(0,2), new Int2(1,4), new Int2(2,6) },
                ThreeStar = 11, TwoStar = 13
            },
            new LevelData
            {
                Id = "1-6", Name = "The Long Way", Objective = Objective.Reach,
                StartCol = 4, StartRow = 9, GoalCol = 0, GoalRow = 0,
                Traps = new[] { new Int2(2,4), new Int2(2,8), new Int2(4,7), new Int2(4,5), new Int2(1,2), new Int2(1,4), new Int2(4,2), new Int2(3,2), new Int2(2,6), new Int2(2,5), new Int2(0,2), new Int2(1,5), new Int2(3,5), new Int2(1,3) },
                ThreeStar = 11, TwoStar = 13
            },

            // ── World 2 : capture all enemies (4-6 add the goal) ──────────────
            new LevelData
            {
                Id = "2-1", Name = "First Capture", Objective = Objective.CaptureAll,
                StartCol = 2, StartRow = 9,
                Traps = new[] { new Int2(2,5), new Int2(0,4), new Int2(4,6) },
                Enemies = new[]
                {
                    new EnemySpec(PieceType.Rook, 1, 5, 0, -1),
                    new EnemySpec(PieceType.Bishop, 3, 5, 1, -1),
                    new EnemySpec(PieceType.Pawn, 2, 7, 0, 0)
                },
                ThreeStar = 4, TwoStar = 6
            },
            new LevelData
            {
                Id = "2-2", Name = "Crossfire", Objective = Objective.CaptureAll,
                StartCol = 2, StartRow = 9,
                Traps = new[] { new Int2(2,5), new Int2(1,4), new Int2(3,4), new Int2(2,6) },
                Enemies = new[]
                {
                    new EnemySpec(PieceType.Rook, 0, 4, 0, -1),
                    new EnemySpec(PieceType.Rook, 4, 4, 0, -1),
                    new EnemySpec(PieceType.Bishop, 2, 3, 1, -1)
                },
                ThreeStar = 6, TwoStar = 8
            },
            new LevelData
            {
                Id = "2-3", Name = "Roundup", Objective = Objective.CaptureAll,
                StartCol = 2, StartRow = 9,
                Traps = new[] { new Int2(3,1), new Int2(0,5), new Int2(0,9), new Int2(1,3) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 3, 3, -1, -1), new EnemySpec(PieceType.Bishop, 1, 4, 1, -1), new EnemySpec(PieceType.Pawn, 0, 7, 0, 0) },
                ThreeStar = 8, TwoStar = 10
            },
            new LevelData
            {
                Id = "2-4", Name = "Escort", Objective = Objective.Combined,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(2,7), new Int2(4,7), new Int2(0,1), new Int2(2,5) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 0, 9, 1, -1), new EnemySpec(PieceType.Bishop, 2, 1, -1, -1) },
                ThreeStar = 9, TwoStar = 11
            },
            new LevelData
            {
                Id = "2-5", Name = "Sweep and Clear", Objective = Objective.Combined,
                StartCol = 0, StartRow = 9, GoalCol = 4, GoalRow = 0,
                Traps = new[] { new Int2(0,8), new Int2(4,1), new Int2(0,0), new Int2(4,7) },
                Enemies = new[] { new EnemySpec(PieceType.Pawn, 1, 6, 0, 0), new EnemySpec(PieceType.Pawn, 4, 9, 0, 0), new EnemySpec(PieceType.Pawn, 1, 1, 0, 0) },
                ThreeStar = 9, TwoStar = 11
            },
            new LevelData
            {
                Id = "2-6", Name = "No Survivors", Objective = Objective.Combined,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(4,9), new Int2(4,7), new Int2(4,4), new Int2(2,3), new Int2(0,2) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 1, 9, 1, -1), new EnemySpec(PieceType.Pawn, 3, 3, 0, 0), new EnemySpec(PieceType.Bishop, 0, 1, 1, -1) },
                ThreeStar = 13, TwoStar = 15
            },

            // ── World 3 : tall follow-camera maps (4-6 add enemies) ───────────
            new LevelData
            {
                Id = "3-1", Name = "The Climb", Objective = Objective.Reach, MapRows = 20,
                StartCol = 2, StartRow = 18, GoalCol = 2, GoalRow = 1,
                Traps = new[] { new Int2(0,1), new Int2(0,5), new Int2(0,10), new Int2(0,12), new Int2(1,2), new Int2(1,3), new Int2(1,4), new Int2(1,7), new Int2(1,10), new Int2(1,11), new Int2(2,3), new Int2(2,5), new Int2(2,7), new Int2(2,14), new Int2(2,15), new Int2(3,2), new Int2(3,3), new Int2(3,8), new Int2(4,2), new Int2(4,11), new Int2(4,12), new Int2(4,16) },
                ThreeStar = 11, TwoStar = 13
            },
            new LevelData
            {
                Id = "3-2", Name = "Ascent", Objective = Objective.Reach, MapRows = 18,
                StartCol = 2, StartRow = 16, GoalCol = 2, GoalRow = 1,
                Traps = new[] { new Int2(0,13), new Int2(4,0), new Int2(2,13), new Int2(0,15), new Int2(1,14), new Int2(2,9), new Int2(3,14), new Int2(3,2), new Int2(4,15), new Int2(1,16), new Int2(3,0), new Int2(3,15), new Int2(4,3), new Int2(4,5), new Int2(1,9), new Int2(0,8), new Int2(1,13), new Int2(3,10) },
                ThreeStar = 11, TwoStar = 13
            },
            new LevelData
            {
                Id = "3-3", Name = "Vertigo", Objective = Objective.Reach, MapRows = 20,
                StartCol = 0, StartRow = 18, GoalCol = 4, GoalRow = 1,
                Traps = new[] { new Int2(2,8), new Int2(4,16), new Int2(4,7), new Int2(2,12), new Int2(3,11), new Int2(0,6), new Int2(4,4), new Int2(2,7), new Int2(2,2), new Int2(2,6), new Int2(3,8), new Int2(3,7), new Int2(2,11), new Int2(2,9), new Int2(1,7), new Int2(1,11), new Int2(1,10), new Int2(4,15), new Int2(0,8), new Int2(2,16), new Int2(4,12) },
                ThreeStar = 13, TwoStar = 15
            },
            new LevelData
            {
                Id = "3-4", Name = "High Ground", Objective = Objective.Combined, MapRows = 16,
                StartCol = 2, StartRow = 14, GoalCol = 2, GoalRow = 1,
                Traps = new[] { new Int2(4,14), new Int2(3,12), new Int2(2,2), new Int2(2,12), new Int2(0,10), new Int2(3,9), new Int2(0,7), new Int2(3,6), new Int2(3,7), new Int2(0,4), new Int2(4,5), new Int2(4,2) },
                Enemies = new[] { new EnemySpec(PieceType.Pawn, 3, 15, 0, 0), new EnemySpec(PieceType.Pawn, 3, 1, 0, 0) },
                ThreeStar = 13, TwoStar = 15
            },
            new LevelData
            {
                Id = "3-5", Name = "Skyfall", Objective = Objective.Combined, MapRows = 18,
                StartCol = 0, StartRow = 16, GoalCol = 4, GoalRow = 1,
                Traps = new[] { new Int2(2,9), new Int2(3,6), new Int2(3,16), new Int2(4,9), new Int2(4,13), new Int2(2,2), new Int2(4,15), new Int2(4,7), new Int2(4,3), new Int2(0,0), new Int2(0,5), new Int2(0,14), new Int2(2,14), new Int2(2,5) },
                Enemies = new[] { new EnemySpec(PieceType.Pawn, 1, 1, 0, 0), new EnemySpec(PieceType.Bishop, 3, 5, 1, -1), new EnemySpec(PieceType.Bishop, 3, 11, 1, -1) },
                ThreeStar = 15, TwoStar = 17
            },
            new LevelData
            {
                Id = "3-6", Name = "Summit", Objective = Objective.Combined, MapRows = 20,
                StartCol = 2, StartRow = 18, GoalCol = 2, GoalRow = 1,
                Traps = new[] { new Int2(2,14), new Int2(0,16), new Int2(4,18), new Int2(0,3), new Int2(2,8), new Int2(0,1), new Int2(1,18), new Int2(2,9), new Int2(4,14), new Int2(0,17), new Int2(4,7), new Int2(3,6), new Int2(4,9), new Int2(0,11), new Int2(1,15), new Int2(3,5) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 4, 4, -1, -1), new EnemySpec(PieceType.Bishop, 3, 12, 1, -1), new EnemySpec(PieceType.Bishop, 0, 2, 1, -1) },
                ThreeStar = 17, TwoStar = 19
            },

            // ── World 4 : cracked tiles (4-6 combine everything) ──────────────
            new LevelData
            {
                Id = "4-1", Name = "Fragile Footing", Objective = Objective.Reach,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(0,0), new Int2(0,1), new Int2(1,2), new Int2(2,6), new Int2(3,2), new Int2(3,3), new Int2(3,5), new Int2(4,7) },
                Cracked = new[] { new Int2(0,2), new Int2(0,3), new Int2(0,4), new Int2(0,5), new Int2(0,7), new Int2(1,0), new Int2(1,5), new Int2(1,6), new Int2(2,1), new Int2(2,5), new Int2(2,8), new Int2(3,1), new Int2(3,7), new Int2(3,9), new Int2(4,0), new Int2(4,1), new Int2(4,4), new Int2(4,6) },
                ThreeStar = 7, TwoStar = 9
            },
            new LevelData
            {
                Id = "4-2", Name = "One Way Up", Objective = Objective.Reach,
                StartCol = 0, StartRow = 9, GoalCol = 4, GoalRow = 0,
                Traps = new[] { new Int2(0,1), new Int2(1,3), new Int2(1,7), new Int2(1,9), new Int2(2,4), new Int2(3,6), new Int2(3,7), new Int2(4,1), new Int2(4,5) },
                Cracked = new[] { new Int2(0,4), new Int2(0,5), new Int2(0,8), new Int2(1,0), new Int2(1,1), new Int2(1,2), new Int2(1,4), new Int2(1,6), new Int2(1,8), new Int2(2,0), new Int2(2,3), new Int2(2,7), new Int2(2,8), new Int2(3,1), new Int2(3,2), new Int2(3,3), new Int2(3,9), new Int2(4,2), new Int2(4,3), new Int2(4,6) },
                ThreeStar = 7, TwoStar = 9
            },
            new LevelData
            {
                Id = "4-3", Name = "Thin Ice", Objective = Objective.Reach,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(3,2), new Int2(2,2), new Int2(1,2), new Int2(2,5), new Int2(3,9), new Int2(3,3), new Int2(3,4), new Int2(3,5), new Int2(0,5), new Int2(0,6), new Int2(4,3) },
                Cracked = new[] { new Int2(0,1), new Int2(0,2), new Int2(0,4), new Int2(0,8), new Int2(0,9), new Int2(1,0), new Int2(1,4), new Int2(1,5), new Int2(1,7), new Int2(1,9), new Int2(2,1), new Int2(2,3), new Int2(2,4), new Int2(2,6), new Int2(3,0), new Int2(3,7), new Int2(4,0), new Int2(4,1), new Int2(4,2), new Int2(4,5), new Int2(4,6), new Int2(4,7), new Int2(4,8), new Int2(4,9) },
                ThreeStar = 9, TwoStar = 11
            },
            new LevelData
            {
                Id = "4-4", Name = "Collapse", Objective = Objective.Combined, MapRows = 16,
                StartCol = 2, StartRow = 14, GoalCol = 2, GoalRow = 1,
                Traps = new[] { new Int2(2,4), new Int2(0,12), new Int2(0,6), new Int2(2,10), new Int2(4,15), new Int2(0,2), new Int2(4,9), new Int2(0,10), new Int2(1,5), new Int2(0,3) },
                Cracked = new[] { new Int2(0,4), new Int2(0,5), new Int2(0,8), new Int2(0,11), new Int2(0,13), new Int2(0,15), new Int2(1,1), new Int2(1,3), new Int2(1,4), new Int2(1,7), new Int2(1,11), new Int2(1,12), new Int2(1,13), new Int2(1,14), new Int2(1,15), new Int2(2,5), new Int2(2,6), new Int2(2,8), new Int2(3,0), new Int2(3,1), new Int2(3,2), new Int2(3,3), new Int2(3,5), new Int2(3,6), new Int2(3,7), new Int2(3,8), new Int2(3,11), new Int2(3,13), new Int2(4,3), new Int2(4,7), new Int2(4,8), new Int2(4,11), new Int2(4,13), new Int2(4,14) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 2, 15, -1, -1), new EnemySpec(PieceType.Bishop, 3, 9, -1, -1) },
                ThreeStar = 13, TwoStar = 15
            },
            new LevelData
            {
                Id = "4-5", Name = "Crumbling Heights", Objective = Objective.Combined, MapRows = 18,
                StartCol = 0, StartRow = 16, GoalCol = 4, GoalRow = 1,
                Traps = new[] { new Int2(3,11), new Int2(1,17), new Int2(2,0), new Int2(0,17), new Int2(1,5), new Int2(1,8), new Int2(2,17), new Int2(2,10), new Int2(1,13), new Int2(0,0), new Int2(3,6), new Int2(2,1) },
                Cracked = new[] { new Int2(0,1), new Int2(0,2), new Int2(0,3), new Int2(0,5), new Int2(0,6), new Int2(0,7), new Int2(0,8), new Int2(0,10), new Int2(0,13), new Int2(0,15), new Int2(1,4), new Int2(1,6), new Int2(1,7), new Int2(1,9), new Int2(1,10), new Int2(1,14), new Int2(2,2), new Int2(2,5), new Int2(2,6), new Int2(2,7), new Int2(2,9), new Int2(2,12), new Int2(2,15), new Int2(2,16), new Int2(3,0), new Int2(3,3), new Int2(3,4), new Int2(3,8), new Int2(3,15), new Int2(3,16), new Int2(3,17), new Int2(4,4), new Int2(4,5), new Int2(4,7), new Int2(4,8), new Int2(4,10), new Int2(4,11), new Int2(4,12), new Int2(4,13), new Int2(4,14), new Int2(4,15), new Int2(4,17) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 3, 4, 1, -1), new EnemySpec(PieceType.Bishop, 2, 13, -1, -1), new EnemySpec(PieceType.Bishop, 3, 1, 1, -1) },
                ThreeStar = 15, TwoStar = 17
            },
            new LevelData
            {
                Id = "4-6", Name = "Last Stand", Objective = Objective.Combined, MapRows = 20,
                StartCol = 2, StartRow = 18, GoalCol = 2, GoalRow = 1,
                Traps = new[] { new Int2(3,6), new Int2(0,12), new Int2(4,6), new Int2(1,3), new Int2(4,18), new Int2(2,8), new Int2(3,3), new Int2(3,9), new Int2(4,14), new Int2(3,12), new Int2(4,8), new Int2(3,19), new Int2(3,8), new Int2(2,11) },
                Cracked = new[] { new Int2(0,1), new Int2(0,2), new Int2(0,4), new Int2(0,5), new Int2(0,7), new Int2(0,8), new Int2(0,11), new Int2(0,14), new Int2(0,15), new Int2(0,16), new Int2(0,19), new Int2(1,1), new Int2(1,2), new Int2(1,4), new Int2(1,6), new Int2(1,7), new Int2(1,12), new Int2(1,14), new Int2(1,15), new Int2(1,16), new Int2(1,17), new Int2(1,19), new Int2(2,3), new Int2(2,5), new Int2(2,6), new Int2(2,10), new Int2(2,12), new Int2(2,14), new Int2(2,15), new Int2(2,16), new Int2(2,19), new Int2(3,0), new Int2(3,4), new Int2(3,5), new Int2(3,7), new Int2(3,10), new Int2(3,11), new Int2(3,13), new Int2(3,14), new Int2(3,15), new Int2(3,16), new Int2(3,17), new Int2(3,18), new Int2(4,0), new Int2(4,1), new Int2(4,5), new Int2(4,7), new Int2(4,13), new Int2(4,15), new Int2(4,17) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 0, 15, 1, -1), new EnemySpec(PieceType.Pawn, 0, 1, 0, 0), new EnemySpec(PieceType.Bishop, 0, 13, 1, -1) },
                ThreeStar = 17, TwoStar = 19
            },

            // ── World 5 : transformation (capture 2 enemies -> take their form -> slide to the goal) ──
            // The goal at (2,0) is knight-unreachable: its four on-board L-sources
            // (0,1),(4,1),(1,2),(3,2) are trapped, so only a rook/bishop slide reaches it.
            new LevelData
            {
                Id = "5-1", Name = "Metamorphosis", Objective = Objective.Combined, Transform = true,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(0,1), new Int2(4,1), new Int2(1,2), new Int2(3,2) },
                Enemies = new[] { new EnemySpec(PieceType.Rook, 1, 7, 0, 0), new EnemySpec(PieceType.Rook, 2, 5, 0, 0) },
                ThreeStar = 3, TwoStar = 5
            },
            new LevelData
            {
                Id = "5-2", Name = "The Bishop's Path", Objective = Objective.Combined, Transform = true,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(0,1), new Int2(4,1), new Int2(1,2), new Int2(3,2) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 1, 6, 0, 0), new EnemySpec(PieceType.Bishop, 2, 4, 0, 0) },
                ThreeStar = 5, TwoStar = 7
            },
            new LevelData
            {
                Id = "5-3", Name = "Moving Targets", Objective = Objective.Combined, Transform = true,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(0,1), new Int2(4,1), new Int2(1,2), new Int2(3,2) },
                Enemies = new[] { new EnemySpec(PieceType.Rook, 2, 4, 0, -1), new EnemySpec(PieceType.Rook, 2, 8, 0, 1) },
                ThreeStar = 6, TwoStar = 8
            },
            new LevelData
            {
                Id = "5-4", Name = "Shifting Diagonals", Objective = Objective.Combined, Transform = true,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(0,1), new Int2(4,1), new Int2(1,2), new Int2(3,2) },
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 2, 5, -1, 1), new EnemySpec(PieceType.Bishop, 2, 8, 1, -1) },
                ThreeStar = 6, TwoStar = 8
            },
            new LevelData
            {
                Id = "5-5", Name = "Three Forms", Objective = Objective.Combined, Transform = true,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(0,1), new Int2(4,1), new Int2(1,2), new Int2(3,2), new Int2(2,4) },
                Enemies = new[] { new EnemySpec(PieceType.Rook, 3, 7, 0, -1), new EnemySpec(PieceType.Bishop, 2, 8, -1, 1), new EnemySpec(PieceType.Rook, 0, 6, 0, 1) },
                ThreeStar = 8, TwoStar = 10
            },
            new LevelData
            {
                Id = "5-6", Name = "Ascension", Objective = Objective.Combined, Transform = true,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(0,1), new Int2(4,1), new Int2(1,2), new Int2(3,2), new Int2(2,4), new Int2(0,6), new Int2(4,6) },
                // First bishop oscillates (3,3)<->(2,2); its away tile was (2,4), a trap,
                // so it used to step onto a trap (and was uncapturable there). Flipped the
                // vertical offset so it stays on open tiles either side.
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 3, 3, -1, -1), new EnemySpec(PieceType.Bishop, 2, 8, 1, 1), new EnemySpec(PieceType.Rook, 1, 8, 0, -1) },
                ThreeStar = 11, TwoStar = 13
            },

            // ── World 6 : the gauntlet, every earlier mechanic at once ────────
            // The victory-lap world: traps, capture-all, tall follow-camera climbs,
            // cracked ground and transformation are combined in a single level each,
            // escalating. Transform levels keep the World-5 trick (the goal's knight
            // L-sources are trapped, so only a rook/bishop slide reaches it) layered on
            // top of cracked tiles and tall maps. Every layout is solver-validated:
            // 3-star = optimal move count, 2-star = optimal + 2.
            new LevelData
            {
                Id = "6-1", Name = "Convergence", Objective = Objective.Combined,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 1, 7, 1, -1), new EnemySpec(PieceType.Bishop, 3, 5, -1, -1) },
                Traps = new[] { new Int2(0,3), new Int2(4,3), new Int2(2,7), new Int2(1,1), new Int2(3,1), new Int2(0,8), new Int2(4,7) },
                Cracked = new[] { new Int2(1,5), new Int2(3,3), new Int2(2,2), new Int2(0,5), new Int2(4,5), new Int2(2,8), new Int2(1,3) },
                ThreeStar = 9, TwoStar = 11
            },
            new LevelData
            {
                Id = "6-2", Name = "Brittle Crown", Objective = Objective.Combined, Transform = true,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Enemies = new[] { new EnemySpec(PieceType.Rook, 2, 4, 0, 0), new EnemySpec(PieceType.Rook, 1, 7, 0, 1), new EnemySpec(PieceType.Bishop, 3, 6, -1, -1) },
                Traps = new[] { new Int2(0,1), new Int2(4,1), new Int2(1,2), new Int2(3,2), new Int2(0,6), new Int2(4,5) },
                Cracked = new[] { new Int2(1,4), new Int2(3,4), new Int2(0,4), new Int2(4,4), new Int2(1,8), new Int2(3,8) },
                ThreeStar = 5, TwoStar = 7
            },
            new LevelData
            {
                Id = "6-3", Name = "The Long Climb", Objective = Objective.Combined, MapRows = 16,
                StartCol = 2, StartRow = 14, GoalCol = 2, GoalRow = 1,
                Enemies = new[] { new EnemySpec(PieceType.Bishop, 2, 15, -1, -1), new EnemySpec(PieceType.Bishop, 3, 9, -1, -1), new EnemySpec(PieceType.Pawn, 4, 6, 0, 0) },
                Traps = new[] { new Int2(2,4), new Int2(0,12), new Int2(0,6), new Int2(2,10), new Int2(4,15), new Int2(0,2), new Int2(4,9), new Int2(0,10), new Int2(1,5), new Int2(0,3) },
                Cracked = new[] { new Int2(0,4), new Int2(0,5), new Int2(0,8), new Int2(0,11), new Int2(0,13), new Int2(0,15), new Int2(1,1), new Int2(1,3), new Int2(1,4), new Int2(1,7), new Int2(1,11), new Int2(1,12), new Int2(1,13), new Int2(1,14), new Int2(1,15), new Int2(2,5), new Int2(2,6), new Int2(2,8), new Int2(3,0), new Int2(3,1), new Int2(3,2), new Int2(3,3), new Int2(3,5), new Int2(3,6), new Int2(3,7), new Int2(3,8), new Int2(3,11), new Int2(3,13), new Int2(4,3), new Int2(4,7), new Int2(4,8), new Int2(4,11), new Int2(4,13), new Int2(4,14) },
                ThreeStar = 15, TwoStar = 17
            },
            new LevelData
            {
                Id = "6-4", Name = "Shifting Ascent", Objective = Objective.Combined, Transform = true, MapRows = 16,
                StartCol = 2, StartRow = 14, GoalCol = 2, GoalRow = 1,
                Enemies = new[] { new EnemySpec(PieceType.Rook, 2, 9, 0, 0), new EnemySpec(PieceType.Rook, 1, 12, 0, 1), new EnemySpec(PieceType.Bishop, 4, 7, -1, -1) },
                Traps = new[] { new Int2(1,3), new Int2(3,3), new Int2(0,2), new Int2(4,2), new Int2(0,0), new Int2(4,0), new Int2(0,7), new Int2(2,11) },
                Cracked = new[] { new Int2(1,5), new Int2(3,5), new Int2(0,11), new Int2(4,11), new Int2(1,13), new Int2(3,13) },
                ThreeStar = 7, TwoStar = 9
            },
            new LevelData
            {
                Id = "6-5", Name = "Everything", Objective = Objective.Combined, Transform = true, MapRows = 18,
                StartCol = 2, StartRow = 16, GoalCol = 2, GoalRow = 1,
                Enemies = new[] { new EnemySpec(PieceType.Rook, 2, 7, 0, -1), new EnemySpec(PieceType.Bishop, 4, 11, -1, -1), new EnemySpec(PieceType.Rook, 1, 13, 0, 1) },
                Traps = new[] { new Int2(1,3), new Int2(3,3), new Int2(0,2), new Int2(4,2), new Int2(0,0), new Int2(4,0), new Int2(0,9), new Int2(4,10), new Int2(2,12), new Int2(1,15), new Int2(3,15) },
                Cracked = new[] { new Int2(1,5), new Int2(3,5), new Int2(0,7), new Int2(4,7), new Int2(1,11), new Int2(3,9), new Int2(2,13), new Int2(0,13), new Int2(4,13), new Int2(1,16), new Int2(3,16) },
                ThreeStar = 10, TwoStar = 12
            },
            new LevelData
            {
                Id = "6-6", Name = "Apotheosis", Objective = Objective.Combined, Transform = true, MapRows = 20,
                StartCol = 2, StartRow = 18, GoalCol = 2, GoalRow = 1,
                Enemies = new[] { new EnemySpec(PieceType.Rook, 2, 8, 0, 0), new EnemySpec(PieceType.Bishop, 3, 12, -1, -1), new EnemySpec(PieceType.Rook, 1, 15, 0, 1), new EnemySpec(PieceType.Bishop, 0, 10, 1, -1) },
                Traps = new[] { new Int2(1,3), new Int2(3,3), new Int2(0,2), new Int2(4,2), new Int2(0,0), new Int2(4,0), new Int2(0,6), new Int2(4,6), new Int2(0,14), new Int2(4,14), new Int2(1,5), new Int2(3,5) },
                Cracked = new[] { new Int2(0,4), new Int2(4,4), new Int2(1,11), new Int2(3,11), new Int2(0,9), new Int2(4,9), new Int2(2,14), new Int2(1,17), new Int2(3,17), new Int2(0,16), new Int2(4,16), new Int2(1,7), new Int2(3,7) },
                // 3-star relaxed to optimal+2 (14): the 12-move optimal ends on a full-length
                // column slide that's fiddly to line up on the tall follow-camera, so give it
                // a little slack rather than demanding frame-perfect routing.
                ThreeStar = 14, TwoStar = 16
            },

            // ── World 7 : the crown, a three-phase queen boss ─────────────────
            // "The Queen" has 3 HP. Each phase she is shielded (uncapturable) by her
            // minions, ordinary pieces you must hunt down first. Clear the minions and
            // the shield drops; capture the queen to take a crown of health and get
            // transported to the next, harder phase. She chases you one square at a time
            // (king steps, blocked by traps), so she's slow. The challenge is cornering
            // her at a knight's distance while dodging her (and the minions). Star
            // thresholds are lenient (total moves across all three phases), since perfect
            // play isn't the point. Surviving the escalation is.
            new LevelData
            {
                Id = "7-1", Name = "Coronation", Objective = Objective.CaptureAll,
                ThreeStar = 40, TwoStar = 80,
                Phases = new[]
                {
                    // Phase 1: two stationary rooks, open board, light cover.
                    new LevelData
                    {
                        Objective = Objective.CaptureAll, StartCol = 2, StartRow = 8,
                        Traps = new[] { new Int2(2,5), new Int2(1,3), new Int2(3,3) },
                        Enemies = new[]
                        {
                            new EnemySpec(PieceType.Rook, 0, 4, 0, 0),
                            new EnemySpec(PieceType.Rook, 4, 4, 0, 0),
                            new EnemySpec(PieceType.Queen, 2, 1, 0, 0, hunter: true)
                        }
                    },
                    // Phase 2: three minions (one moving), a fragile tile, more traps.
                    new LevelData
                    {
                        Objective = Objective.CaptureAll, StartCol = 2, StartRow = 9,
                        Traps = new[] { new Int2(1,2), new Int2(3,2), new Int2(0,6), new Int2(4,4), new Int2(2,7), new Int2(1,8), new Int2(3,8) },
                        Cracked = new[] { new Int2(2,4) },
                        Enemies = new[]
                        {
                            new EnemySpec(PieceType.Rook, 0, 3, 0, 0),
                            new EnemySpec(PieceType.Pawn, 2, 5, 0, 0),
                            new EnemySpec(PieceType.Bishop, 4, 6, -1, -1),
                            new EnemySpec(PieceType.Queen, 2, 1, 0, 0, hunter: true)
                        }
                    },
                    // Phase 3: a tall climb, cracked tiles, four minions (two moving).
                    new LevelData
                    {
                        Objective = Objective.CaptureAll, StartCol = 2, StartRow = 12, MapRows = 14,
                        Traps = new[] { new Int2(1,2), new Int2(3,2), new Int2(0,4), new Int2(4,4), new Int2(2,6), new Int2(0,7), new Int2(1,9), new Int2(3,9), new Int2(2,10), new Int2(4,11) },
                        Cracked = new[] { new Int2(2,8), new Int2(1,11), new Int2(3,6) },
                        Enemies = new[]
                        {
                            new EnemySpec(PieceType.Rook, 0, 10, 0, 0),
                            new EnemySpec(PieceType.Pawn, 3, 3, 0, 0),
                            new EnemySpec(PieceType.Bishop, 4, 8, -1, -1),
                            new EnemySpec(PieceType.Bishop, 1, 5, 1, -1),
                            new EnemySpec(PieceType.Queen, 2, 2, 0, 0, hunter: true)
                        }
                    }
                }
            }
        };
    }
}
