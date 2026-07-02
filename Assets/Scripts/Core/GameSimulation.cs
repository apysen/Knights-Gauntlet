using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Per-capture detail the view uses to spawn the +score / xCombo popups.
    public struct CaptureInfo
    {
        public PieceType Type;
        public int PointsGained;
        public int NewMultiplier;
        public int MultiplierDelta;
        public int Col;
        public float Y;
    }

    // What happened on a single tap. For Trap mode Captures is empty.
    public struct MoveResult
    {
        public bool Moved;
        public List<CaptureInfo> Captures;
        public int RowAdvance;   // trap mode: rows gained (+) or lost (-) this move
    }

    // Headless game simulation for BOTH modes. No UnityEngine, no rendering.
    // The Unity layer calls Start/Tick/TryTap and reads public state + events.
    //
    // Timers are driven by an internal clock accumulated from dt (not wall time),
    // so the sim is deterministic and pauses automatically when Tick stops.
    public class GameSimulation
    {
        // ── Public state ─────────────────────────────────────────────────────
        public GameMode Mode { get; private set; }
        public Difficulty Difficulty { get; private set; }
        public readonly List<GamePiece> Pieces = new List<GamePiece>();
        public int PlayerCol { get; private set; }
        public float PlayerY { get; private set; }
        public PieceType PlayerForm { get; private set; } = PieceType.Knight;
        public int Score { get; private set; }
        public int ComboMultiplier { get; private set; } = 1;
        public bool Alive { get; private set; }
        public float ScrollCells { get; private set; }

        // Collectable shillings on the board, and the total collected this run.
        public readonly List<Pickup> Pickups = new List<Pickup>();
        public int RunShillings { get; private set; }

        public bool IsMimic => _transformIsMimic;
        public bool IsInvulnerable => _time < _battleInvulnUntil;
        public bool TransformActive => _transformEndsAt > 0f && _time < _transformEndsAt;
        public float TransformRemaining => TransformActive ? Math.Max(0f, _transformEndsAt - _time) : 0f;
        public int CaptureChain => _captureChain; // 0..TransformTrigger-1 while building toward a transform

        // Progress toward the next enemy move, 0..1 (for the move-timer bar).
        public float EnemyMoveProgress =>
            Mode == GameMode.Battle && _battleMoveInterval > 0f
                ? Math.Min(1f, (_time - _lastBattleMove) / _battleMoveInterval)
                : 0f;

        // ── Events (view subscribes) ─────────────────────────────────────────
        public event Action<PieceType> OnFormChanged; // transform / expiry / shield
        public event Action OnShielded;                // a transform absorbed a hit
        public event Action OnDeath;

        // Last few landings in scroll-invariant world space, for the snapshot.
        public readonly List<(int col, float worldY)> Trail = new List<(int, float)>();

        // ── Internals ────────────────────────────────────────────────────────
        private int _trapScore;
        private float _speed, _speedStart, _speedMax;
        private float _playerBase; // grid-lock base for the player (Trap mode)
        private float _nextSpawnCells;
        private float _time;

        private int _comboMissStreak, _captureChain;
        private PieceType? _lastCaptured;
        private float _transformEndsAt, _battleInvulnUntil, _lastBattleMove, _battleMoveInterval;
        private bool _transformIsMimic;
        private bool _pickupsEnabled = true;

        private Random _rng;
        private TrapGenerator _trapGen;
        private BattleGenerator _battleGen;
        private EnemyAI _enemyAI;

        public GameSimulation(Random rng = null)
        {
            _rng = rng ?? new Random();
            _trapGen = new TrapGenerator(_rng);
            _battleGen = new BattleGenerator(_rng);
            _enemyAI = new EnemyAI(_rng);
        }

        public void Start(GameMode mode, Difficulty difficulty,
                          int? seed = null, bool mediumSpeed = false, bool pickups = true)
        {
            // Daily challenge passes a fixed seed so the board is identical for
            // everyone; reseed the generators so the run is reproducible.
            if (seed.HasValue)
            {
                _rng = new Random(seed.Value);
                _trapGen = new TrapGenerator(_rng);
                _battleGen = new BattleGenerator(_rng);
                _enemyAI = new EnemyAI(_rng);
            }

            Mode = mode;
            Difficulty = difficulty;
            Pieces.Clear();
            Pickups.Clear();
            Trail.Clear();
            Score = 0;
            RunShillings = 0;
            _pickupsEnabled = pickups;
            _trapScore = 0;
            ScrollCells = 0;
            _time = 0;
            Alive = true;

            ComboMultiplier = 1;
            _comboMissStreak = 0;
            _captureChain = 0;
            _lastCaptured = null;
            _transformEndsAt = 0;
            _transformIsMimic = false;
            _battleInvulnUntil = 0;
            _lastBattleMove = 0;
            PlayerForm = PieceType.Knight;

            PlayerCol = 2;
            PlayerY = GameConfig.Rows - 4; // 6 with Rows=10
            _playerBase = PlayerY - ScrollCells;

            if (mode == GameMode.Trap)
            {
                (_speedStart, _speedMax) = GameConfig.TrapSpeed(mediumSpeed ? Difficulty.Medium : difficulty);
                _speed = _speedStart;
                _trapGen.Reset(PlayerCol);
                for (int r = 1; r < GameConfig.Rows - 1; r++)
                    _trapGen.SpawnRow(Pieces, PlayerY - r, PlayerCol, ScrollCells);
                _nextSpawnCells = ScrollCells + 2f;
            }
            else // Battle
            {
                _speed = 1.0f; // battle scrolls at a constant gentle pace
                _battleMoveInterval = GameConfig.BattleMoveIntervalMs(difficulty) / 1000f;
                for (int r = 1; r < GameConfig.Rows - 1; r++)
                    _battleGen.SpawnRow(Pieces, PlayerY - r, difficulty, false);
                // Keep the opening clear: drop anything not safely above the knight.
                Pieces.RemoveAll(o => o.Y >= PlayerY - 2.5f);
                _enemyAI.PlanKnightMoves(Pieces, PlayerCol, PlayerY, true);
                _nextSpawnCells = ScrollCells + 1f;
            }
        }

        public void Tick(float dt)
        {
            if (!Alive) return;
            if (dt > 1f / 30f) dt = 1f / 30f; // clamp so a frame hitch can't lurch the board
            _time += dt;

            float delta = _speed * dt;
            ScrollCells += delta;
            if (Mode == GameMode.Trap)
                _speed = Math.Min(_speedMax, _speedStart + ScrollCells * GameConfig.TrapSpeedRampPerCell);

            // Grid-locked scroll: derive every position from the single shared
            // ScrollCells + a fixed base, so nothing drifts off the grid at any
            // speed or run length. Battle enemies get their base refreshed after
            // each AI move (below), since the AI shifts them by whole cells.
            PlayerY = _playerBase + ScrollCells;
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (float.IsNaN(o.Base)) o.Base = (float)Math.Round(o.Y - ScrollCells);
                o.Y = o.Base + ScrollCells;
            }
            for (int i = 0; i < Pickups.Count; i++)
            {
                var pu = Pickups[i];
                if (float.IsNaN(pu.Base)) pu.Base = (float)Math.Round(pu.Y - ScrollCells);
                pu.Y = pu.Base + ScrollCells;
            }

            bool spawnedRow = false;
            while (ScrollCells >= _nextSpawnCells)
            {
                if (Mode == GameMode.Trap)
                    _trapGen.SpawnRow(Pieces, -1f, PlayerCol, ScrollCells);
                else
                    _battleGen.SpawnRow(Pieces, -1f, Difficulty, TransformActive);
                TrySpawnPickup(-1f);
                _nextSpawnCells += 1f;
                spawnedRow = true;
            }
            // Give newly-spawned enemy knights a planned jump right away, so their
            // red threat tile shows before their first move (not a turn late).
            if (spawnedRow && Mode == GameMode.Battle)
                _enemyAI.PlanKnightMoves(Pieces, PlayerCol, PlayerY, false);

            // Trap keeps a long tail behind the knight for the death snapshot.
            float cullY = Mode == GameMode.Trap ? PlayerY + 26f : GameConfig.Rows + 1f;
            // Stable in-place compaction (no per-frame lambda/closure allocation).
            int wp = 0;
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (o.Alive && o.Y < cullY) Pieces[wp++] = o;
            }
            if (wp < Pieces.Count) Pieces.RemoveRange(wp, Pieces.Count - wp);
            int wk = 0;
            for (int i = 0; i < Pickups.Count; i++)
            {
                var p = Pickups[i];
                if (p.Alive && p.Y < GameConfig.Rows + 1f) Pickups[wk++] = p;
            }
            if (wk < Pickups.Count) Pickups.RemoveRange(wk, Pickups.Count - wk);
            CollectPickups();

            if (Mode == GameMode.Battle)
            {
                if (_transformEndsAt > 0f && _time >= _transformEndsAt)
                {
                    _transformEndsAt = 0; _transformIsMimic = false; SetForm(PieceType.Knight);
                }

                if (_time - _lastBattleMove >= _battleMoveInterval)
                {
                    _lastBattleMove = _time;
                    if (_enemyAI.MoveEnemies(Pieces, PlayerCol, PlayerY))
                    {
                        if (_time < _battleInvulnUntil)
                        {
                            ClearEnemiesOnPlayer(); // shrug it off during i-frames
                        }
                        else if (_transformEndsAt > 0f)
                        {
                            // A transform absorbs the hit: lose form + multiplier, survive.
                            _transformEndsAt = 0; _transformIsMimic = false; SetForm(PieceType.Knight);
                            ComboMultiplier = 1; _comboMissStreak = 0; _captureChain = 0;
                            _battleInvulnUntil = _time + 1.2f;
                            ClearEnemiesOnPlayer();
                            OnShielded?.Invoke();
                        }
                        else { Alive = false; OnDeath?.Invoke(); return; }
                    }
                    // The AI shifted enemies by whole cells; re-anchor their grid
                    // base so the derived scroll keeps them exactly on their squares.
                    for (int i = 0; i < Pieces.Count; i++)
                        Pieces[i].Base = (float)Math.Round(Pieces[i].Y - ScrollCells);
                }
            }

            if (PlayerY > GameConfig.Rows + 1f) { Alive = false; OnDeath?.Invoke(); return; }
            CheckCollision();
        }

        public List<MoveTarget> GetValidMoves()
            => MoveRules.GetValidMoves(Pieces, PlayerCol, PlayerY, Mode, PlayerForm);

        // Allocation-free variant for per-frame callers (move hints). Returns a
        // shared buffer — do not hold the reference across frames.
        private readonly List<MoveTarget> _movesBuffer = new List<MoveTarget>();
        public List<MoveTarget> GetValidMovesNonAlloc()
        {
            MoveRules.Fill(_movesBuffer, Pieces, PlayerCol, PlayerY, Mode, PlayerForm);
            return _movesBuffer;
        }

        // Tap at grid cell (col, y in cell units). Snaps to the nearest legal move
        // in that column within ~1.2 cells.
        public MoveResult TryTap(int col, float y)
        {
            var moves = GetValidMoves();
            MoveTarget? best = null;
            float bestDist = 1.2f;
            foreach (var m in moves)
            {
                if (m.Col != col) continue;
                float d = Math.Abs(m.Y - y);
                if (d < bestDist) { bestDist = d; best = m; }
            }
            if (best == null) return new MoveResult { Moved = false, Captures = null };
            return ApplyMove(best.Value);
        }

        private MoveResult ApplyMove(MoveTarget m)
        {
            var result = new MoveResult { Moved = true, Captures = new List<CaptureInfo>() };

            if (Mode == GameMode.Trap)
            {
                int rowDelta = (int)Math.Round(PlayerY - m.Y); // forward (up) positive
                _trapScore = Math.Max(0, _trapScore + rowDelta);
                Score = _trapScore;
                result.RowAdvance = rowDelta;

                PlayerCol = m.Col; PlayerY = m.Y;
                _playerBase = PlayerY - ScrollCells;
                Trail.Add((PlayerCol, PlayerY - ScrollCells));
                if (Trail.Count > 12) Trail.RemoveAt(0);
                CollectPickups();
                CheckCollision();
                return result;
            }

            // ── Battle ───────────────────────────────────────────────────────
            bool captured = false;
            for (int j = 0; j < Pieces.Count; j++)
            {
                var o = Pieces[j];
                if (!o.Alive || o.Type == PieceType.Trap) continue;
                if (o.Col != m.Col || Math.Abs(o.Y - m.Y) >= 0.5f) continue;

                o.Alive = false;
                captured = true;
                _lastCaptured = o.Type;

                int pts = GameConfig.PiecePoints(o.Type);
                int gained = pts * ComboMultiplier;
                Score += gained;
                RunShillings += pts;   // battle earns shillings from captures (piece value)

                int inc = GameConfig.ComboInc(o.Type);
                if (PlayerForm == PieceType.Bishop || PlayerForm == PieceType.Nightrider)
                    inc *= GameConfig.ComboFormBonus;
                ComboMultiplier += inc;

                result.Captures.Add(new CaptureInfo
                {
                    Type = o.Type, PointsGained = gained,
                    NewMultiplier = ComboMultiplier, MultiplierDelta = inc,
                    Col = m.Col, Y = m.Y
                });
            }

            if (captured)
            {
                _comboMissStreak = 0;
                if (_transformEndsAt > 0f && _time < _transformEndsAt)
                {
                    // Already transformed: each capture refills the clock.
                    _transformEndsAt = _time + GameConfig.TransformSeconds;
                    if (_transformIsMimic)
                        SetForm(_lastCaptured == PieceType.Knight ? PieceType.Nightrider : _lastCaptured.Value);
                }
                else
                {
                    _captureChain++;
                    if (_captureChain >= GameConfig.TransformTrigger)
                    {
                        _transformEndsAt = _time + GameConfig.TransformSeconds;
                        _captureChain = 0;
                        if (_lastCaptured == PieceType.Knight) { _transformIsMimic = true; SetForm(PieceType.Nightrider); }
                        else { _transformIsMimic = false; SetForm(_lastCaptured.Value); }
                    }
                }
            }
            else
            {
                _comboMissStreak++;
                if (_comboMissStreak > GameConfig.ComboGrace(Difficulty))
                {
                    ComboMultiplier = 1; _comboMissStreak = 0; _captureChain = 0;
                }
            }

            PlayerCol = m.Col; PlayerY = m.Y;
            _playerBase = PlayerY - ScrollCells;
            CollectPickups();
            CheckCollision();
            return result;
        }

        private void SetForm(PieceType form)
        {
            if (PlayerForm == form) return;
            PlayerForm = form;
            OnFormChanged?.Invoke(form);
        }

        private void CheckCollision()
        {
            if (_time < _battleInvulnUntil) return; // i-frames (battle hit / revive grace)
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (!o.Alive) continue;
                if (o.Col == PlayerCol && Math.Abs(o.Y - PlayerY) < 0.5f)
                {
                    Alive = false; OnDeath?.Invoke(); return;
                }
            }
        }

        private void ClearEnemiesOnPlayer()
        {
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (o.Alive && o.Type != PieceType.Trap &&
                    o.Col == PlayerCol && Math.Abs(o.Y - PlayerY) < 0.6f)
                    o.Alive = false;
            }
        }

        // ── Pickups (shillings) ──────────────────────────────────────────────
        private void TrySpawnPickup(float y)
        {
            // Coins only drop in Trap mode; Battle earns shillings from captures.
            if (!_pickupsEnabled || Mode != GameMode.Trap) return;
            if (_rng.NextDouble() >= 0.12) return; // ~1 per 8 rows
            int row = (int)Math.Round(y);
            var occupied = new HashSet<int>();
            foreach (var o in Pieces)
                if (o.Alive && (int)Math.Round(o.Y) == row) occupied.Add(o.Col);
            foreach (var pk in Pickups)
                if (pk.Alive && (int)Math.Round(pk.Y) == row) occupied.Add(pk.Col);
            var open = new List<int>();
            for (int c = 0; c < GameConfig.Cols; c++) if (!occupied.Contains(c)) open.Add(c);
            if (open.Count == 0) return;
            int col = open[_rng.Next(open.Count)];
            int[] vals = { 10, 15, 20, 25 };
            Pickups.Add(new Pickup(col, y, vals[_rng.Next(vals.Length)]));
        }

        private void CollectPickups()
        {
            for (int i = 0; i < Pickups.Count; i++)
            {
                var pk = Pickups[i];
                if (!pk.Alive) continue;
                if (pk.Col == PlayerCol && Math.Abs(pk.Y - PlayerY) < 0.5f)
                {
                    pk.Alive = false;
                    RunShillings += pk.Value;
                }
            }
        }

        // ── Extra life (revive) ──────────────────────────────────────────────
        // Bring the player back and clear a safe pocket around them so they don't
        // instantly die again. Not used in the daily challenge.
        public void Revive()
        {
            if (Alive) return;
            Alive = true;

            // If they fell off the bottom, bring them back on-screen FIRST so the
            // safe pocket below is cleared around where they actually land.
            if (PlayerY > GameConfig.Rows - 1f) { PlayerY = GameConfig.Rows - 4f; _playerBase = PlayerY - ScrollCells; }

            if (Mode == GameMode.Trap)
                Pieces.RemoveAll(o => o.Type == PieceType.Trap && Math.Abs(o.Y - PlayerY) < 3.0f);
            else
            {
                Pieces.RemoveAll(o => o.Type != PieceType.Trap && Math.Abs(o.Y - PlayerY) < 2.5f);
                _lastBattleMove = _time;
            }

            // Brief grace so the very next tick can't re-kill (honored in both modes).
            _battleInvulnUntil = _time + 1.5f;
        }

        // Build the "Was I trapped?" analysis from the current (death) state.
        // Call this in Trap mode right after Alive turns false.
        public DeathAnalysis BuildDeathAnalysis()
        {
            var traps = new List<(int col, int relRow)>();
            foreach (var o in Pieces)
                if (o.Alive && o.Type == PieceType.Trap)
                    traps.Add((o.Col, (int)Math.Round(o.Y - PlayerY)));

            var (path, escaped, reason) = EscapeSolver.Compute(PlayerCol, traps);

            var trail = new List<(int col, int relRow, int moveNum)>();
            int start = Math.Max(0, Trail.Count - 10);
            for (int i = start; i < Trail.Count; i++)
            {
                float deathY = Trail[i].worldY + ScrollCells;
                trail.Add((Trail[i].col, (int)Math.Round(deathY - PlayerY), i - start + 1));
            }

            return new DeathAnalysis
            {
                Escaped = escaped, Reason = reason, PlayerCol = PlayerCol,
                Path = path, Traps = traps, Trail = trail
            };
        }
    }
}
