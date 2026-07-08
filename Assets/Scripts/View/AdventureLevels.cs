namespace KnightsGauntlet.Core
{
    // Prototype content, all solver-validated (guaranteed solvable, 3-star = true
    // optimal move count). Reach puzzles first, then oscillating-enemy capture
    // puzzles, then a combined one. Enemy oscillation: rook = (0,-1) vertical,
    // bishop = (1,-1)/(−1,−1) diagonal, pawn = (0,0) stationary.
    public static class AdventureLevels
    {
        public static readonly LevelData[] All =
        {
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
                Id = "2-3", Name = "Clear the Path", Objective = Objective.Combined,
                StartCol = 2, StartRow = 9, GoalCol = 2, GoalRow = 0,
                Traps = new[] { new Int2(2,6), new Int2(0,5), new Int2(4,6) },
                Enemies = new[]
                {
                    new EnemySpec(PieceType.Rook, 1, 5, 0, -1),
                    new EnemySpec(PieceType.Bishop, 3, 5, 1, -1),
                    new EnemySpec(PieceType.Pawn, 2, 7, 0, 0)
                },
                ThreeStar = 7, TwoStar = 9
            },
            new LevelData
            {
                Id = "3-1", Name = "The Climb", Objective = Objective.Reach, MapRows = 20,
                StartCol = 2, StartRow = 18, GoalCol = 2, GoalRow = 1,
                Traps = new[] { new Int2(0,1), new Int2(0,5), new Int2(0,10), new Int2(0,12), new Int2(1,2), new Int2(1,3), new Int2(1,4), new Int2(1,7), new Int2(1,10), new Int2(1,11), new Int2(2,3), new Int2(2,5), new Int2(2,7), new Int2(2,14), new Int2(2,15), new Int2(3,2), new Int2(3,3), new Int2(3,8), new Int2(4,2), new Int2(4,11), new Int2(4,12), new Int2(4,16) },
                ThreeStar = 11, TwoStar = 13
            }
        };
    }
}
