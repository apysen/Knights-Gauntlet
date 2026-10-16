using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // Biomes belong to persistent scrolling rows, rather than the current frame.
    // Rewinding the board therefore also rewinds the same transition boundary.
    public sealed class BoardBiomeFlow
    {
        private struct Boundary { public int row; public BoardBiome biome; }
        private readonly List<Boundary> _boundaries = new List<Boundary>(2);
        public void Reset() => _boundaries.Clear();

        public void Advance(float scroll, float seconds)
        {
            int target = seconds < 60f ? 0 : seconds < 90f ? 1 : 2;
            while (_boundaries.Count < target)
                _boundaries.Add(new Boundary { row = -Mathf.FloorToInt(scroll) - 5,
                    biome = (BoardBiome)(_boundaries.Count + 1) });
        }

        public BoardBiome AtRow(int worldRow)
        {
            for (int i = _boundaries.Count - 1; i >= 0; i--)
                if (worldRow <= _boundaries[i].row) return _boundaries[i].biome;
            return BoardBiome.Grass;
        }

        public bool Region(BoardBiome biome, float scroll, float cellSize, float halfLength,
            out float minY, out float maxY)
        {
            int index = (int)biome;
            minY = -10000f; maxY = 10000f;
            if (index > _boundaries.Count) return false;
            if (index > 0) minY = halfLength - (_boundaries[index - 1].row + 1 + scroll) * cellSize;
            if (index < _boundaries.Count) maxY = halfLength - (_boundaries[index].row + 1 + scroll) * cellSize;
            return maxY > minY;
        }
    }
}
