using System.Collections.Generic;
using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public partial class GameView
    {
        // Pool indices change when enemies are captured or culled. Keep travel
        // state on the simulation object's identity rather than its pooled view.
        private sealed class EnemyMotion
        {
            public Vector2 from, target, current;
            public float age, lift, fromLift;
            public bool seen;
        }

        private readonly Dictionary<object, EnemyMotion> _enemyMotions = new Dictionary<object, EnemyMotion>();
        private readonly List<object> _expiredEnemyMotions = new List<object>();
        private GameSimulation _enemyMotionSimulation;

        private void BeginEnemyMotionFrame(GameSimulation sim)
        {
            if (!ReferenceEquals(_enemyMotionSimulation, sim))
            {
                _enemyMotions.Clear();
                _enemyMotionSimulation = sim;
            }
            foreach (var motion in _enemyMotions.Values) motion.seen = false;
        }

        private void PositionEnemy(PieceView view, object identity, int col, float row,
            PieceType type, float scroll, float dt)
        {
            Vector2 target = new Vector2(col, row);
            bool reduced = SaveData.IsReducedEffects();
            float duration = reduced ? 0.08f : PieceView.HopDuration;
            if (!_enemyMotions.TryGetValue(identity, out var motion))
            {
                motion = new EnemyMotion { from = target, target = target, current = target, age = duration };
                _enemyMotions.Add(identity, motion);
            }
            else if ((motion.target - target).sqrMagnitude > 0.0001f)
            {
                // A second move starts at the currently displayed position.
                motion.from = motion.current;
                motion.fromLift = motion.lift;
                motion.target = target;
                motion.age = 0f;
            }
            motion.seen = true;
            float progress = Mathf.Clamp01(motion.age / duration);
            float remaining = 1f - progress;
            motion.current = motion.target + (motion.from - motion.target) * (remaining * remaining);
            // Match the player's quick eased hop and soft landing. Reduced effects
            // and legacy sprites retain the travel with a flat trajectory.
            motion.lift = motion.fromLift * remaining +
                (!reduced && !_legacy2D && type != PieceType.Trap
                    ? Mathf.Sin(progress * Mathf.PI) * 0.24f * _pieceScale * _board.cellSize : 0f);
            Vector3 position = _board.CellToWorld(0, motion.current.y + scroll);
            position.x += motion.current.x * _board.cellW;
            position.z -= motion.lift;
            view.transform.position = position;
            motion.age += Mathf.Max(0f, dt);
        }

        private void EndEnemyMotionFrame()
        {
            _expiredEnemyMotions.Clear();
            foreach (var pair in _enemyMotions)
                if (!pair.Value.seen) _expiredEnemyMotions.Add(pair.Key);
            foreach (var identity in _expiredEnemyMotions) _enemyMotions.Remove(identity);
        }
    }
}
