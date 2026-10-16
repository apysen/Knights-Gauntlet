using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    public enum EscapeReason
    {
        Forward,   // a clean line out using on-screen tiles
        DeepBack,  // escapable, but only by doubling back through off-screen tiles
        Sealed     // genuinely trapped, no legal route out
    }

    // One step of the suggested escape route, in player-relative rows
    // (player = row 0; forward/up = negative).
    public struct EscapeStep
    {
        public int Col;
        public int RelRow;
        public int DRow;   // the row component of the L-move taken to get here
    }

    // Everything the "Was I trapped?" screen needs, all in player-relative rows
    // so the renderer doesn't have to know about scroll or pixels.
    public struct DeathAnalysis
    {
        public bool Escaped;
        public EscapeReason Reason;
        public int PlayerCol;
        public List<EscapeStep> Path;
        public List<(int col, int relRow)> Traps;
        public List<(int col, int relRow, int moveNum)> Trail; // last ~10 landings
    }

    // Port of computeEscapePath(). BFS over knight L-moves looking for a genuine
    // route that threads PAST the wall of traps ahead, allowing the knight to
    // double back as deep as the retained board allows.
    public static class EscapeSolver
    {
        private const int OnscreenBack = 4; // rows visible below the knight at death
        private const int MaxSteps = 70;
        private struct Node
        {
            public int col, row, parent, depth, maxBack;
        }

        public static (List<EscapeStep> path, bool escaped, EscapeReason reason) Compute(
            int pCol, List<(int col, int relRow)> traps)
        {
            var occ = new HashSet<(int col, int row)>();
            int minTrapRel = int.MaxValue, maxTrapRel = int.MinValue;
            foreach (var t in traps)
            {
                occ.Add((t.col, t.relRow));
                if (t.relRow < minTrapRel) minTrapRel = t.relRow;
                if (t.relRow > maxTrapRel) maxTrapRel = t.relRow;
            }

            if (traps.Count == 0)
                return (new List<EscapeStep>(), true, EscapeReason.Forward);

            int escapeRel = Math.Min(minTrapRel - 1, -1); // step past the furthest trap ahead
            int upLimit = escapeRel - 1;
            int downLimit = Math.Max(4, Math.Min(maxTrapRel, 22));

            var visited = new HashSet<(int col, int row)> { (pCol, 0) };
            // The growing list is both a FIFO queue and a predecessor table.
            // Only the winning route is materialized, instead of copying a path
            // for every explored square. Insertion order preserves tie-breaking.
            var nodes = new List<Node>(128) { new Node { col = pCol, parent = -1 } };

            for (int head = 0; head < nodes.Count; head++)
            {
                var cur = nodes[head];
                if (cur.row <= escapeRel)
                {
                    var reason = cur.maxBack > OnscreenBack ? EscapeReason.DeepBack : EscapeReason.Forward;
                    var path = new List<EscapeStep>(cur.depth);
                    for (int index = head; nodes[index].parent >= 0; index = nodes[index].parent)
                    {
                        var step = nodes[index];
                        path.Add(new EscapeStep { Col = step.col, RelRow = step.row,
                            DRow = step.row - nodes[step.parent].row });
                    }
                    path.Reverse();
                    return (path, true, reason);
                }
                if (cur.depth >= MaxSteps) continue;

                foreach (var d in GameConfig.LDeltas)
                {
                    int nc = cur.col + d.Col;
                    int nr = cur.row + d.Row;
                    if (nc < 0 || nc >= GameConfig.Cols || nr < upLimit || nr > downLimit) continue;
                    var key = (nc, nr);
                    if (occ.Contains(key) || !visited.Add(key)) continue;
                    nodes.Add(new Node { col = nc, row = nr, parent = head,
                        depth = cur.depth + 1, maxBack = Math.Max(cur.maxBack, nr) });
                }
            }

            return (new List<EscapeStep>(), false, EscapeReason.Sealed);
        }
    }
}
