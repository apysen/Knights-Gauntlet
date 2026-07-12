using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Enemy movement for Battle mode. The two-phase design (a piece CLAIMS its
    // destination cell before any later piece evaluates its move) is what stops
    // pieces from stacking. The single-loop version had persistent overlap bugs.
    public class EnemyAI
    {
        private readonly Random _rng;
        public EnemyAI(Random rng) { _rng = rng; }

        private static int RowOf(float y) => (int)Math.Round(y);

        // Reused occupancy sets (integer-keyed) so a battle move tick doesn't
        // allocate two HashSets + a string per cell every time.
        private readonly HashSet<int> _occ = new HashSet<int>();
        private readonly HashSet<int> _claimed = new HashSet<int>();
        private static int Key(int col, int row) => col * 64 + (row + 32);

        // Advance every enemy one step toward the player. Returns true if any
        // enemy lands on (or threatens, for the queen) the knight's square.
        public bool MoveEnemies(IList<GamePiece> pieces, int playerCol, float playerY)
        {
            bool dead = false;
            int knightRow = RowOf(playerY);

            // Cells occupied at the START of this turn (alive, non-trap).
            _occ.Clear();
            foreach (var oa in pieces)
            {
                if (!oa.Alive || oa.Type == PieceType.Trap) continue;
                _occ.Add(Key(oa.Col, RowOf(oa.Y)));
            }

            // Cells claimed by pieces that have already moved this turn.
            _claimed.Clear();

            bool CellFree(int col, int row, int sc, int sr)
            {
                if (col < 0 || col >= GameConfig.Cols) return false;
                int key = Key(col, row);
                if (_claimed.Contains(key)) return false;
                if (_occ.Contains(key) && !(col == sc && row == sr)) return false;
                return true;
            }
            bool KnightAt(int col, int row) => col == playerCol && knightRow == row;
            bool Targetable(int col, int row, int sc, int sr) => KnightAt(col, row) || CellFree(col, row, sc, sr);

            // Resolve in a fixed type order so behaviour is deterministic given the RNG.
            PieceType[] order = { PieceType.Rook, PieceType.Bishop, PieceType.Knight, PieceType.Queen };
            foreach (var ptype in order)
            {
                foreach (var o in pieces)
                {
                    if (!o.Alive || o.Type != ptype) continue;
                    int cr = RowOf(o.Y), fwd = cr + 1;

                    if (o.Type == PieceType.Rook)
                    {
                        if (Targetable(o.Col, fwd, o.Col, cr))
                        {
                            o.Y += 1; _claimed.Add(Key(o.Col, fwd));
                        }
                        else
                        {
                            bool moved = false;
                            var sides = new List<int> { -1, 1 };
                            if (_rng.NextDouble() < 0.5) sides.Reverse();
                            foreach (var sdir in sides)
                            {
                                int sc = o.Col + sdir;
                                if (CellFree(sc, cr, o.Col, cr)) { o.Col = sc; _claimed.Add(Key(sc, cr)); moved = true; break; }
                            }
                            if (!moved) _claimed.Add(Key(o.Col, cr));
                        }
                    }
                    else if (o.Type == PieceType.Bishop)
                    {
                        int dir = o.Dir != 0 ? o.Dir : 1;
                        int tc = o.Col + dir;
                        if (tc < 0 || tc >= GameConfig.Cols || !Targetable(tc, fwd, o.Col, cr))
                        {
                            dir = -dir; tc = o.Col + dir;
                        }
                        if (tc >= 0 && tc < GameConfig.Cols && Targetable(tc, fwd, o.Col, cr))
                        {
                            o.Dir = dir; o.Col = tc; o.Y += 1; _claimed.Add(Key(tc, fwd));
                        }
                        else { o.Dir = dir; _claimed.Add(Key(o.Col, cr)); }
                    }
                    else if (o.Type == PieceType.Knight)
                    {
                        Int2? ex = null;
                        if (o.PlanDC.HasValue && o.PlanDR.HasValue)
                        {
                            int ptc = o.Col + o.PlanDC.Value, ptr = cr + o.PlanDR.Value;
                            if (ptc >= 0 && ptc < GameConfig.Cols && Targetable(ptc, ptr, o.Col, cr))
                                ex = new Int2(o.PlanDC.Value, o.PlanDR.Value);
                        }
                        if (ex == null)
                        {
                            var opts = new List<Int2>();
                            foreach (var m in GameConfig.KnightJumps)
                                if (o.Col + m.Col >= 0 && o.Col + m.Col < GameConfig.Cols &&
                                    Targetable(o.Col + m.Col, cr + m.Row, o.Col, cr))
                                    opts.Add(m);
                            var down = opts.FindAll(m => m.Row > 0);
                            var pool = down.Count > 0 ? down : opts;
                            if (pool.Count > 0) ex = pool[_rng.Next(pool.Count)];
                        }
                        if (ex != null)
                        {
                            o.Col += ex.Value.Col; o.Y += ex.Value.Row;
                            _claimed.Add(Key(o.Col, cr + ex.Value.Row));
                        }
                        else _claimed.Add(Key(o.Col, cr));
                        o.PlanDC = null; o.PlanDR = null;
                    }
                    else if (o.Type == PieceType.Queen)
                    {
                        foreach (var adj in GameConfig.QueenAdj)
                            if (KnightAt(o.Col + adj.Col, cr + adj.Row)) dead = true;

                        var qopts = new List<Int2>();
                        foreach (var adj in GameConfig.QueenAdj)
                            if (o.Col + adj.Col >= 0 && o.Col + adj.Col < GameConfig.Cols &&
                                CellFree(o.Col + adj.Col, cr + adj.Row, o.Col, cr))
                                qopts.Add(adj);
                        var qfwd = qopts.FindAll(adj => adj.Row >= 0);
                        var qpool = qfwd.Count > 0 ? qfwd : qopts;
                        if (qpool.Count > 0)
                        {
                            var qm = qpool[_rng.Next(qpool.Count)];
                            o.Col += qm.Col; o.Y += qm.Row;
                            _claimed.Add(Key(o.Col, cr + qm.Row));
                        }
                        else _claimed.Add(Key(o.Col, cr));
                    }

                    if (o.Col == playerCol && Math.Abs(o.Y - playerY) < 0.5f) dead = true;
                }
            }

            PlanKnightMoves(pieces, playerCol, playerY, true);
            return dead;
        }

        // Pre-pick each enemy knight's next jump so it can be shown as a threat
        // tile a turn ahead. force=true re-plans even pieces with a valid plan.
        public void PlanKnightMoves(IList<GamePiece> pieces, int playerCol, float playerY, bool force)
        {
            _occ.Clear();
            foreach (var oa in pieces)
            {
                if (!oa.Alive || oa.Type == PieceType.Trap) continue;
                _occ.Add(Key(oa.Col, RowOf(oa.Y)));
            }
            int knightRow = RowOf(playerY);

            bool FreeOrKnight(int col, int row, int sc, int sr)
            {
                if (col < 0 || col >= GameConfig.Cols) return false;
                if (col == playerCol && knightRow == row) return true;
                if (_occ.Contains(Key(col, row)) && !(col == sc && row == sr)) return false;
                return true;
            }

            foreach (var o in pieces)
            {
                if (!o.Alive || o.Type != PieceType.Knight) continue;
                int cr = RowOf(o.Y);

                if (!force && o.PlanDC.HasValue && o.PlanDR.HasValue)
                {
                    if (FreeOrKnight(o.Col + o.PlanDC.Value, cr + o.PlanDR.Value, o.Col, cr)) continue;
                }

                var opts = new List<Int2>();
                foreach (var m in GameConfig.KnightJumps)
                    if (FreeOrKnight(o.Col + m.Col, cr + m.Row, o.Col, cr)) opts.Add(m);
                var down = opts.FindAll(m => m.Row > 0);
                var pool = down.Count > 0 ? down : opts;
                if (pool.Count > 0) { var ch = pool[_rng.Next(pool.Count)]; o.PlanDC = ch.Col; o.PlanDR = ch.Row; }
                else { o.PlanDC = null; o.PlanDR = null; }
            }
        }
    }
}
