using System;
using System.Collections.Generic;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Watches a running game and fires achievement unlocks. Conditions only count
    // on Hard / Grandmaster, mirroring the original. Reads what it needs from the
    // sim (mode, difficulty, form, score) plus the per-move MoveResult, so no
    // achievement logic lives in the pure Core layer.
    public class AchievementTracker
    {
        private readonly GameSimulation _sim;
        private readonly Action<string> _onUnlock;

        private int _capStreak, _advance;
        private float _marathon;
        private PieceType _prevForm;
        private readonly Dictionary<PieceType, int> _formCounts = new Dictionary<PieceType, int>();
        private readonly HashSet<string> _fired = new HashSet<string>();

        public AchievementTracker(GameSimulation sim, Action<string> onUnlock)
        {
            _sim = sim;
            _onUnlock = onUnlock;
        }

        private bool Active =>
            _sim.Difficulty == Difficulty.Hard || _sim.Difficulty == Difficulty.Grandmaster;

        public void Reset()
        {
            _capStreak = 0; _advance = 0; _marathon = 0;
            _prevForm = PieceType.Knight;
            _formCounts.Clear();
            _fired.Clear();
        }

        public void OnMove(MoveResult r)
        {
            if (!Active) return;

            if (_sim.Mode == GameMode.Trap)
            {
                if (r.RowAdvance > 0) { _advance += r.RowAdvance; if (_advance >= 25) Fire("trap_advance"); }
                else if (r.RowAdvance < 0) _advance = 0;
                if (_sim.Score >= 150) Fire("trap_score");
            }
            else
            {
                int caps = r.Captures != null ? r.Captures.Count : 0;
                if (caps > 0) _capStreak++; else _capStreak = 0;
                if (_capStreak >= 5) Fire("battle_streak");
            }
        }

        public void Tick(float dt)
        {
            if (!Active || _sim.Mode != GameMode.Battle) return;

            var f = _sim.PlayerForm;
            if (f != _prevForm)
            {
                if (f != PieceType.Knight)
                {
                    _formCounts.TryGetValue(f, out int n);
                    _formCounts[f] = n + 1;
                    if (Count(PieceType.Rook) >= 2 && Count(PieceType.Bishop) >= 2 &&
                        Count(PieceType.Queen) >= 2 && Count(PieceType.Nightrider) >= 2)
                        Fire("battle_polymorph");
                }
                _prevForm = f;
            }

            if (f != PieceType.Knight) _marathon += dt; else _marathon = 0f;
            if (_marathon >= 60f) Fire("battle_marathon");
        }

        private int Count(PieceType t) { _formCounts.TryGetValue(t, out int n); return n; }

        private void Fire(string id)
        {
            if (_fired.Contains(id)) return;
            _fired.Add(id);
            _onUnlock?.Invoke(id);
        }
    }
}
