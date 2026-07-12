using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Procedural trap-row generation with the guaranteed-escape repair from the
    // original. Stateful: it carries the reachability "frontier" (the open cells
    // the knight can still reach in the two most recently spawned rows) so it can
    // detect a row that would seal the board and pry one trap open to fix it.
    //
    // RNG is injected so generation is deterministic in tests: you can seed a
    // Random, spawn thousands of rows, and assert the board is never sealed.
    public class TrapGenerator
    {
        private readonly Random _rng;
        private List<int> _reachA = new List<int>(); // open cols reachable in the row just behind
        private List<int> _reachB = new List<int>(); // ...two rows behind

        public TrapGenerator(Random rng)
        {
            _rng = rng;
        }

        // Seed the frontier from the knight's starting column (matches
        // `trapReachA = [playerCol]` at game start).
        public void Reset(int playerCol)
        {
            _reachA = new List<int> { playerCol };
            _reachB = new List<int>();
        }

        // Which of the new row's open columns can the knight actually reach from
        // the previous two rows? A one-row L-move shifts +/-2 columns; a two-row
        // L-move shifts +/-1. (rA = row just behind, rB = two rows behind.)
        private static List<int> ReachableNew(List<int> openCols, List<int> rA, List<int> rB)
        {
            var nr = new List<int>();
            foreach (var c in openCols)
            {
                if (rA.Contains(c - 2) || rA.Contains(c + 2) ||
                    rB.Contains(c - 1) || rB.Contains(c + 1))
                    nr.Add(c);
            }
            return nr;
        }

        // Generate one trap row at world cell-row `y`, appending trap pieces to
        // `pieces`. `scrolledCells` drives the difficulty ramp (more closed cols
        // over time); `playerCol` biases the open gaps toward the knight.
        public void SpawnRow(List<GamePiece> pieces, float y, int playerCol, float scrolledCells)
        {
            float progress = Math.Min(1f, scrolledCells / 40f);
            int numOpen = _rng.NextDouble() < progress ? 2 : 3;

            // Candidate open columns: a knight's-reach away from the player.
            int[] dcs = { -2, -1, 1, 2 };
            var reachable = new List<int>();
            foreach (var dc in dcs)
            {
                int nc = playerCol + dc;
                if (nc >= 0 && nc < GameConfig.Cols) reachable.Add(nc);
            }

            // Shuffle (Fisher-Yates) and open the first `numOpen`.
            for (int i = reachable.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (reachable[i], reachable[j]) = (reachable[j], reachable[i]);
            }

            var open = new HashSet<int>();
            int take = Math.Min(numOpen, reachable.Count);
            for (int k = 0; k < take; k++) open.Add(reachable[k]);
            if (open.Count < 2 && reachable.Count >= 2)
            {
                open.Add(reachable[0]);
                open.Add(reachable[1]);
            }

            // Solvability repair: if NONE of this row's open cells are reachable
            // from the frontier, the row would seal the board, so open one extra
            // cell that restores a forward route. Fires on ~0.3% of rows.
            var openCols = new List<int>();
            for (int c = 0; c < GameConfig.Cols; c++)
                if (open.Contains(c)) openCols.Add(c);

            var nr = ReachableNew(openCols, _reachA, _reachB);
            if (nr.Count == 0 && (_reachA.Count > 0 || _reachB.Count > 0))
            {
                int? cand = null;
                foreach (var a in _reachA)
                {
                    if (a - 2 >= 0) { cand = a - 2; break; }
                    if (a + 2 < GameConfig.Cols) { cand = a + 2; break; }
                }
                if (cand == null)
                {
                    foreach (var b in _reachB)
                    {
                        if (b - 1 >= 0) { cand = b - 1; break; }
                        if (b + 1 < GameConfig.Cols) { cand = b + 1; break; }
                    }
                }
                if (cand != null && !open.Contains(cand.Value))
                {
                    open.Add(cand.Value);
                    nr.Add(cand.Value);
                }
            }

            // Advance the frontier, then lay traps on every closed column.
            _reachB = _reachA;
            _reachA = nr;

            for (int c = 0; c < GameConfig.Cols; c++)
                if (!open.Contains(c))
                    pieces.Add(new GamePiece(PieceType.Trap, c, y));
        }
    }
}
