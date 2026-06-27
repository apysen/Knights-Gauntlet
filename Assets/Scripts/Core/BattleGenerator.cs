using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Spawns enemy pieces for Battle mode. Parallels TrapGenerator. Keeps at most
    // one queen on the board at a time and, while the player is transformed on the
    // hard tiers, bumps density so targets keep coming during the 5s window.
    public class BattleGenerator
    {
        private readonly Random _rng;

        // Weighted pools (rook/bishop/knight appear twice each, queen once).
        private static readonly PieceType[] PoolWithQueen =
        {
            PieceType.Rook, PieceType.Rook, PieceType.Bishop, PieceType.Bishop,
            PieceType.Knight, PieceType.Knight, PieceType.Queen
        };
        private static readonly PieceType[] PoolNoQueen =
        {
            PieceType.Rook, PieceType.Rook, PieceType.Bishop, PieceType.Bishop,
            PieceType.Knight, PieceType.Knight
        };

        public BattleGenerator(Random rng) { _rng = rng; }

        public void SpawnRow(List<GamePiece> pieces, float y, Difficulty diff, bool transformActive)
        {
            var sp = GameConfig.BattleSpawn(diff);
            float chance = sp.Chance, extra = sp.Extra;

            if (transformActive && (diff == Difficulty.Hard || diff == Difficulty.Grandmaster))
            {
                chance = Math.Min(1f, chance + 0.25f);
                extra = Math.Max(extra, 0.2f);
            }

            if (_rng.NextDouble() >= chance) return;

            bool hasQueen = false;
            foreach (var o in pieces)
                if (o.Alive && o.Type == PieceType.Queen) { hasQueen = true; break; }

            // Columns already taken near the spawn line, so a fresh row never
            // lands on a piece that hasn't scrolled away yet.
            var nearTop = new HashSet<int>();
            foreach (var o in pieces)
                if (o.Alive && o.Type != PieceType.Trap && o.Y < 1.0f) nearTop.Add(o.Col);

            int n = 1 + (_rng.NextDouble() < extra ? 1 : 0); // sometimes a 2nd piece
            var used = new HashSet<int>();
            for (int bp = 0; bp < n; bp++)
            {
                var pool = hasQueen ? PoolNoQueen : PoolWithQueen;
                var t = pool[_rng.Next(pool.Length)];

                int col, tries = 0;
                do { col = _rng.Next(GameConfig.Cols); tries++; }
                while ((used.Contains(col) || nearTop.Contains(col)) && tries < 8);
                if (used.Contains(col) || nearTop.Contains(col)) continue; // no free column this row

                if (t == PieceType.Queen) hasQueen = true;
                used.Add(col);

                pieces.Add(new GamePiece(t, col, y) { Dir = _rng.NextDouble() < 0.5 ? 1 : -1 });
            }
        }
    }
}
