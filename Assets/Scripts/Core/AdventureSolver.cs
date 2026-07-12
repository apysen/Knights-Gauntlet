using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Finds the shortest sequence of moves that solves an adventure level, for the "Divine
    // Path" hint. It replays the same rules as GameSimulation: land on a legal (in-bounds,
    // non-trap) cell, capture any enemy there, then the enemies react. Only traps block
    // movement, since adventure enemies aren't in Pieces, so sliders and knights can land on them.
    //
    //   Solve:     Reach / CaptureAll / Combined, plus World-5 transform (form + pips).
    //   SolveBoss: one queen-boss phase (king-step chasing queen, shield, flag goal).
    //
    // Cracked tiles (World 4) are modelled too: stepping OFF a cracked tile collapses it
    // into a permanent trap, so the route must not rely on a tile it has already burnt.
    // That makes the blocked-set path-dependent, which the plain visited-set can't express
    // because two routes to the same cell differ by which tiles they destroyed getting there.
    // So each state carries a `coll` bitmask of collapsed tiles, and we prune by dominance:
    // arriving at the same (cell, phase, captures, …) having collapsed a SUBSET of the
    // tiles is strictly better, so any state whose collapsed set is a superset of one we
    // already reached (at no greater depth, which BFS guarantees) is dropped.
    //
    // The state spaces are small, so BFS returns a genuinely optimal route. Returns null
    // when unsolvable from the given state (caller refunds).
    public static class AdventureSolver
    {
        private static readonly Int2[] Ortho = { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) };
        private static readonly Int2[] Diag  = { new Int2(1, 1), new Int2(1, -1), new Int2(-1, 1), new Int2(-1, -1) };

        // A `coll` bitmask is a long, so only the first 64 cracked tiles of a level are
        // tracked. Levels are nowhere near that; any beyond it simply never collapse in
        // the model (the path may then be optimistic, never wrong about traps).
        private const int MaxCracked = 64;

        private static bool InBounds(int c, int r, int maxRow) => c >= 0 && c < GameConfig.Cols && r >= 0 && r <= maxRow;

        // Index of (c,r) in the level's live cracked list, or -1. Linear, but the list is
        // tiny and this is only hit while expanding a node.
        private static int CrackIndex(IReadOnlyList<Int2> cracked, int c, int r)
        {
            if (cracked == null) return -1;
            int n = cracked.Count < MaxCracked ? cracked.Count : MaxCracked;
            for (int i = 0; i < n; i++)
                if (cracked[i].Col == c && cracked[i].Row == r) return i;
            return -1;
        }

        // A cell is impassable if it's a trap, or a cracked tile this route already left.
        private static bool Blocked(int c, int r, System.Func<int, int, bool> isTrap,
                                    IReadOnlyList<Int2> cracked, long coll)
        {
            if (isTrap(c, r)) return true;
            int i = CrackIndex(cracked, c, r);
            return i >= 0 && (coll & (1L << i)) != 0;
        }

        // Leaving (c,r) collapses it, if it's still-intact cracked ground.
        private static long Collapse(int c, int r, IReadOnlyList<Int2> cracked, long coll)
        {
            int i = CrackIndex(cracked, c, r);
            return i >= 0 ? coll | (1L << i) : coll;
        }

        // Dominance check + insert. `seen` maps a state key to the antichain of collapsed
        // sets already reached there. Returns true if this state is no better than one we
        // have (so it should be dropped).
        private static bool Dominated(Dictionary<long, List<long>> seen, long key, long coll)
        {
            if (!seen.TryGetValue(key, out var sets))
            {
                seen[key] = new List<long> { coll };
                return false;
            }
            for (int i = 0; i < sets.Count; i++)
                if ((sets[i] & coll) == sets[i]) return true;   // reached it having burnt less (or the same)
            for (int i = sets.Count - 1; i >= 0; i--)
                if ((coll & sets[i]) == coll) sets.RemoveAt(i);  // this one is strictly better, so drop supersets
            sets.Add(coll);
            return false;
        }
        private static int Cheb(int ac, int ar, int bc, int br)
        {
            int dc = ac - bc; if (dc < 0) dc = -dc;
            int dr = ar - br; if (dr < 0) dr = -dr;
            return dc > dr ? dc : dr;
        }
        private static bool EnemyAt(AdvEnemy e, int phase, int col, int row)
            => (phase == 0 ? e.HomeCol : e.HomeCol + e.OffCol) == col
            && (phase == 0 ? e.HomeRow : e.HomeRow + e.OffRow) == row;

        // Would this route step onto ground it already burnt? Replays the path applying
        // the collapse rule. (Only knight levels have cracked tiles, so landing cells are
        // the whole story: no ray needs to pass over a collapsed tile.)
        private static bool CollapseSafe(int startCol, int startRow, List<Int2> path, IReadOnlyList<Int2> cracked)
        {
            long coll = 0L;
            int c = startCol, r = startRow;
            for (int i = 0; i < path.Count; i++)
            {
                int j = CrackIndex(cracked, path[i].Col, path[i].Row);
                if (j >= 0 && (coll & (1L << j)) != 0) return false;  // lands on ground it collapsed
                coll = Collapse(c, r, cracked, coll);
                c = path[i].Col; r = path[i].Row;
            }
            return true;
        }

        // ── Normal + transform levels ────────────────────────────────────────
        // Cracked tiles make the search far more expensive (the blocked-set becomes
        // path-dependent, so states can't be collapsed into one visited key). In practice
        // the plain route almost never doubles back onto its own broken ground, so we try
        // the cheap search first and only pay for the collapse-aware one when the cheap
        // route would actually burn itself. Same optimal answer, ~0 cost in the common case.
        public static List<Int2> Solve(
            int startCol, int startRow, int startPhase,
            IReadOnlyList<AdvEnemy> enemies, System.Func<int, int, bool> isTrap,
            int maxRow, bool hasGoal, int goalCol, int goalRow, bool transform,
            PieceType startForm, int startPips, IReadOnlyList<Int2> cracked = null)
        {
            var plain = SolveCore(startCol, startRow, startPhase, enemies, isTrap, maxRow,
                                  hasGoal, goalCol, goalRow, transform, startForm, startPips, null, int.MaxValue);
            if (cracked == null || cracked.Count == 0) return plain;
            if (plain != null && CollapseSafe(startCol, startRow, plain, cracked)) return plain;

            // The cheap route would collapse under itself, so search properly, under a node
            // budget so a pathological board can't stall the game. If the budget runs out
            // we fall back to the plain route: it may dead-end, but it's better than no hint.
            var aware = SolveCore(startCol, startRow, startPhase, enemies, isTrap, maxRow,
                                  hasGoal, goalCol, goalRow, transform, startForm, startPips, cracked, NodeBudget);
            return aware ?? plain;
        }

        // Expanded-node ceiling for the collapse-aware search, so the worst case stays a
        // hitch rather than a freeze.
        private const int NodeBudget = 40000;

        private static List<Int2> SolveCore(
            int startCol, int startRow, int startPhase,
            IReadOnlyList<AdvEnemy> enemies, System.Func<int, int, bool> isTrap,
            int maxRow, bool hasGoal, int goalCol, int goalRow, bool transform,
            PieceType startForm, int startPips, IReadOnlyList<Int2> cracked, int budget)
        {
            var caps = new List<AdvEnemy>();
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Hunter) return null; // boss, use SolveBoss
                caps.Add(enemies[i]);
            }
            if (caps.Count > 20) return null;

            int all = (1 << caps.Count) - 1;
            int startMask = 0;
            for (int i = 0; i < caps.Count; i++) if (!caps[i].Alive) startMask |= (1 << i);

            // form: 0 Knight, 1 Rook, 2 Bishop, 3 Queen. Only used when transform.
            int FormOf(PieceType t) => t == PieceType.Rook ? 1 : t == PieceType.Bishop ? 2 : t == PieceType.Queen ? 3 : 0;
            long Key(int c, int r, int mask, int p, int form, int pips)
            {
                long v = r * GameConfig.Cols + c;
                v = v * (all + 1) + mask;
                v = v * 2 + p;
                v = v * 4 + form;
                v = v * 2 + pips;
                return v;
            }

            int sForm = transform ? FormOf(startForm) : 0;
            int sPips = transform ? startPips : 0;
            var visited = new Dictionary<long, List<long>>();
            Dominated(visited, Key(startCol, startRow, startMask, startPhase, sForm, sPips), 0L);
            var nodes = new List<TNode> { new TNode { C = startCol, R = startRow, Mask = startMask, P = startPhase, Form = sForm, Pips = sPips, Coll = 0L, Parent = -1 } };
            var queue = new Queue<int>(); queue.Enqueue(0);
            if (startMask == all && (!hasGoal || (startCol == goalCol && startRow == goalRow))) return new List<Int2>();

            int expanded = 0;
            while (queue.Count > 0)
            {
                if (++expanded > budget) return null;   // caller falls back
                int ni = queue.Dequeue();
                var s = nodes[ni];
                // Stepping off this tile collapses it, so it's gone for everything downstream.
                long ncoll = Collapse(s.C, s.R, cracked, s.Coll);
                foreach (var m in Moves(s.C, s.R, transform ? s.Form : 0, isTrap, maxRow, cracked, s.Coll))
                {
                    int nc = m.Col, nr = m.Row;
                    // capture the enemy on the landing tile (at most one meaningful)
                    int mask = s.Mask; int form = s.Form, pips = s.Pips; bool captured = false; PieceType capType = PieceType.Pawn;
                    for (int i = 0; i < caps.Count; i++)
                    {
                        if ((mask & (1 << i)) != 0) continue;
                        if (EnemyAt(caps[i], s.P, nc, nr)) { mask |= (1 << i); captured = true; capType = caps[i].Type; }
                    }
                    if (transform && captured)
                    {
                        pips++;
                        if (pips >= GameConfig.AdventureTransformPips)
                        {
                            pips = 0;
                            if (capType == PieceType.Rook || capType == PieceType.Bishop || capType == PieceType.Queen) form = FormOf(capType);
                        }
                    }
                    int np = 1 - s.P;
                    bool dead = false;
                    for (int i = 0; i < caps.Count && !dead; i++)
                        if ((mask & (1 << i)) == 0 && EnemyAt(caps[i], np, nc, nr)) dead = true;
                    if (dead) continue;

                    long key = Key(nc, nr, mask, np, form, pips);
                    if (Dominated(visited, key, ncoll)) continue;
                    nodes.Add(new TNode { C = nc, R = nr, Mask = mask, P = np, Form = form, Pips = pips, Coll = ncoll, Parent = ni });
                    int idx = nodes.Count - 1;
                    if (mask == all && (!hasGoal || (nc == goalCol && nr == goalRow))) return Reconstruct(nodes, idx);
                    queue.Enqueue(idx);
                }
            }
            return null;
        }

        // Every legal landing cell for a piece of `form` at (c,r): knight jumps, or slider
        // rays that stop before a trap / the map edge. Enemies never block (not in Pieces).
        private static IEnumerable<Int2> Moves(int c, int r, int form, System.Func<int, int, bool> isTrap, int maxRow,
                                               IReadOnlyList<Int2> cracked, long coll)
        {
            if (form == 0) // knight
            {
                foreach (var d in GameConfig.LDeltas)
                {
                    int nc = c + d.Col, nr = r + d.Row;
                    if (InBounds(nc, nr, maxRow) && !Blocked(nc, nr, isTrap, cracked, coll)) yield return new Int2(nc, nr);
                }
                yield break;
            }
            bool ortho = form == 1 || form == 3, diag = form == 2 || form == 3;
            if (ortho) foreach (var cell in Ray(c, r, Ortho, isTrap, maxRow, cracked, coll)) yield return cell;
            if (diag) foreach (var cell in Ray(c, r, Diag, isTrap, maxRow, cracked, coll)) yield return cell;
        }
        private static IEnumerable<Int2> Ray(int c, int r, Int2[] dirs, System.Func<int, int, bool> isTrap, int maxRow,
                                             IReadOnlyList<Int2> cracked, long coll)
        {
            foreach (var d in dirs)
                for (int k = 1; k <= GameConfig.RayMax; k++)
                {
                    int nc = c + d.Col * k, nr = r + d.Row * k;
                    // stop before the edge, a trap, or ground this route already collapsed
                    if (!InBounds(nc, nr, maxRow) || Blocked(nc, nr, isTrap, cracked, coll)) break;
                    yield return new Int2(nc, nr);
                }
        }

        // ── One queen-boss phase ─────────────────────────────────────────────
        // The player (always a knight) must capture every minion, which drops the queen's
        // shield, then capture the (now frozen) queen, and finally, on a phase with a
        // flag, reach it. Models the queen's king-step chase exactly (GameSimulation.MoveHunter).
        public static List<Int2> SolveBoss(
            int startCol, int startRow, int startPhase,
            IReadOnlyList<AdvEnemy> enemies, System.Func<int, int, bool> isTrap,
            int maxRow, bool requireGoal, int goalCol, int goalRow, IReadOnlyList<Int2> cracked = null)
        {
            // Cheap search first, then the collapse-aware one only if that route would burn
            // itself (see Solve). The queen's chase is modelled in both.
            var plain = SolveBossCore(startCol, startRow, startPhase, enemies, isTrap, maxRow,
                                      requireGoal, goalCol, goalRow, null, int.MaxValue);
            if (cracked == null || cracked.Count == 0) return plain;
            if (plain != null && CollapseSafe(startCol, startRow, plain, cracked)) return plain;
            var aware = SolveBossCore(startCol, startRow, startPhase, enemies, isTrap, maxRow,
                                      requireGoal, goalCol, goalRow, cracked, NodeBudget);
            return aware ?? plain;
        }

        private static List<Int2> SolveBossCore(
            int startCol, int startRow, int startPhase,
            IReadOnlyList<AdvEnemy> enemies, System.Func<int, int, bool> isTrap,
            int maxRow, bool requireGoal, int goalCol, int goalRow,
            IReadOnlyList<Int2> cracked, int budget)
        {
            AdvEnemy queen = null;
            var minions = new List<AdvEnemy>();
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Hunter) { queen = enemies[i]; }
                else minions.Add(enemies[i]);
            }
            if (queen == null || minions.Count > 16) return null;

            int all = (1 << minions.Count) - 1;
            int startMask = 0;
            for (int i = 0; i < minions.Count; i++) if (!minions[i].Alive) startMask |= (1 << i);
            int qColStart = queen.CurCol, qRowStart = queen.CurRow;

            int cells = GameConfig.Cols * (maxRow + 1);
            long Key(int c, int r, int mask, int qc, int qr, int p)
            {
                int qi = qc < 0 ? 0 : qr * GameConfig.Cols + qc + 1; // 0 = captured
                long v = r * GameConfig.Cols + c;
                v = v * (all + 1) + mask;
                v = v * (cells + 1) + qi;
                v = v * 2 + p;
                return v;
            }

            bool MinionAt(int mask, int phase, int col, int row)
            {
                for (int i = 0; i < minions.Count; i++)
                    if ((mask & (1 << i)) == 0 && EnemyAt(minions[i], phase, col, row)) return true;
                return false;
            }

            bool Won(int c, int r, int qc) => qc < 0 && (!requireGoal || (c == goalCol && r == goalRow));

            var startNode = new BNode { C = startCol, R = startRow, Mask = startMask, Qc = qColStart, Qr = qRowStart, P = startPhase, Coll = 0L, Parent = -1 };
            if (Won(startCol, startRow, startNode.Mask)) return new List<Int2>();
            var visited = new Dictionary<long, List<long>>();
            Dominated(visited, Key(startCol, startRow, startMask, qColStart, qRowStart, startPhase), 0L);
            var nodes = new List<BNode> { startNode };
            var queue = new Queue<int>(); queue.Enqueue(0);

            int expanded = 0;
            while (queue.Count > 0)
            {
                if (++expanded > budget) return null;   // caller falls back
                int ni = queue.Dequeue();
                var s = nodes[ni];
                // Stepping off this tile collapses it (for the queen's chase too, since a
                // collapsed tile is a real trap, and traps block her).
                long ncoll = Collapse(s.C, s.R, cracked, s.Coll);
                foreach (var d in GameConfig.LDeltas)
                {
                    int nc = s.C + d.Col, nr = s.R + d.Row;
                    if (!InBounds(nc, nr, maxRow) || Blocked(nc, nr, isTrap, cracked, s.Coll)) continue;

                    bool shielded = s.Mask != all;                       // minions remain -> shielded
                    int mask = s.Mask;
                    for (int i = 0; i < minions.Count; i++)
                        if ((mask & (1 << i)) == 0 && EnemyAt(minions[i], s.P, nc, nr)) mask |= (1 << i);
                    int qc = s.Qc, qr = s.Qr;
                    if (qc >= 0 && !shielded && qc == nc && qr == nr) { qc = -1; qr = -1; } // capture the exposed queen

                    int np = 1 - s.P;
                    bool stillShielded = mask != all;
                    bool dead = false;
                    if (qc >= 0 && stillShielded)
                    {
                        // Queen king-step toward the knight (exactly GameSimulation.MoveHunter).
                        int bestC = qc, bestR = qr, bestDist = Cheb(qc, qr, nc, nr);
                        foreach (var qd in GameConfig.QueenAdj)
                        {
                            int cc = qc + qd.Col, rr = qr + qd.Row;
                            if (!InBounds(cc, rr, maxRow)) continue;
                            if (cc == nc && rr == nr) { dead = true; break; }   // steps onto the knight
                            if (Blocked(cc, rr, isTrap, cracked, ncoll) || MinionAt(mask, np, cc, rr)) continue;
                            int dist = Cheb(cc, rr, nc, nr);
                            if (dist < bestDist) { bestDist = dist; bestC = cc; bestR = rr; }
                        }
                        if (!dead) { qc = bestC; qr = bestR; }
                    }
                    if (dead) continue;

                    // Kill-check: a surviving minion (or the queen) now sitting on the knight.
                    if (MinionAt(mask, np, nc, nr)) continue;
                    if (qc == nc && qr == nr) continue;

                    long key = Key(nc, nr, mask, qc, qr, np);
                    if (Dominated(visited, key, ncoll)) continue;
                    nodes.Add(new BNode { C = nc, R = nr, Mask = mask, Qc = qc, Qr = qr, P = np, Coll = ncoll, Parent = ni });
                    int idx = nodes.Count - 1;
                    if (Won(nc, nr, qc)) return ReconstructB(nodes, idx);
                    queue.Enqueue(idx);
                }
            }
            return null;
        }

        private static List<Int2> Reconstruct(List<TNode> nodes, int idx)
        {
            var rev = new List<Int2>();
            for (int i = idx; i > 0; i = nodes[i].Parent) rev.Add(new Int2(nodes[i].C, nodes[i].R));
            rev.Reverse();
            return rev;
        }
        private static List<Int2> ReconstructB(List<BNode> nodes, int idx)
        {
            var rev = new List<Int2>();
            for (int i = idx; i > 0; i = nodes[i].Parent) rev.Add(new Int2(nodes[i].C, nodes[i].R));
            rev.Reverse();
            return rev;
        }

        private struct TNode { public int C, R, Mask, P, Form, Pips, Parent; public long Coll; }
        private struct BNode { public int C, R, Mask, Qc, Qr, P, Parent; public long Coll; }
    }
}
