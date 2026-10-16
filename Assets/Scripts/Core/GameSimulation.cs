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
        public float ElapsedTime => _time; // presentation timing for scrolling biome changes

        // Collectable shillings on the board, and the total collected this run.
        public readonly List<Pickup> Pickups = new List<Pickup>();
        public readonly List<AdvEnemy> AdvEnemies = new List<AdvEnemy>();
        public readonly List<Int2> Cracked = new List<Int2>(); // fragile tiles: safe once, collapse to a trap on leaving
        public int RunShillings { get; private set; }

        public bool IsMimic => _transformIsMimic;
        public bool IsInvulnerable => _time < _battleInvulnUntil;
        public bool TransformActive => _transformEndsAt > 0f && _time < _transformEndsAt;
        public float TransformRemaining => TransformActive ? Math.Max(0f, _transformEndsAt - _time) : 0f;
        public int CaptureChain => _captureChain; // 0..TransformTrigger-1 while building toward a transform
        public int AdvPips => _advPips;            // adventure transform progress (misses never reset it)
        public bool AdvTransform;                  // this adventure level is a transformation puzzle

        // Progress toward the next enemy move, 0..1 (for the move-timer bar).
        public float EnemyMoveProgress =>
            Mode == GameMode.Battle && _battleMoveInterval > 0f
                ? Math.Min(1f, (_time - _lastBattleMove) / _battleMoveInterval)
                : 0f;

        // ── Events (view subscribes) ─────────────────────────────────────────
        public event Action<PieceType> OnFormChanged; // transform / expiry / shield
        public event Action OnShielded;                // a transform absorbed a hit
        public event Action OnDeath;
        public event Action OnPickupCollected;         // a shilling pickup was collected
        public event Action OnTransformExpired;        // form ran out on the timer (not a shield break)

        // Last few landings in scroll-invariant world space, for the snapshot.
        public readonly List<(int col, float worldY)> Trail = new List<(int, float)>();

        // ── Internals ────────────────────────────────────────────────────────
        private int _trapScore;
        private float _speed, _trapBaseSpeed, _trapPerLevel;
        private int _trapLevel;
        public int TrapLevel => _trapLevel;
        public event System.Action<int> OnTrapLevelUp; // fired when trap speed steps up a level
        private float _playerBase; // grid-lock base for the player (Trap mode)
        private float _nextSpawnCells;
        // Presentation can reserve hidden rows behind the curved entrance. Generated
        // rows remain part of the same simulation and are never rerolled by the view.
        public float IncomingRowLead { get; set; }

        // ── Interactive tutorial ─────────────────────────────────────────────
        // When Tutorial is true the board is frozen (no scroll/spawn/AI) so the
        // director can arrange scripted scenarios; taps are restricted to a single
        // locked target cell so the player can't take a wrong move.
        public bool Tutorial;
        public bool Adventure; // scrolling adventure: constant speed, no level-up ramp
        public int MapRows;    // tall follow-camera maps: >0 = map height; 0 = normal 10 rows

        // Tall-map follow camera: content sits at map-row Bases; ScrollCells is driven
        // externally (by the knight's position) rather than by time. Re-derive screen
        // positions from the current ScrollCells.
        public void AdventureSetScroll(float s) { ScrollCells = s; } // follow-camera offset (tall maps)
        public void AdventureFollowDerive()
        {
            PlayerY = _playerBase + ScrollCells;
            for (int i = 0; i < Pieces.Count; i++)
            {
                var p = Pieces[i];
                if (!float.IsNaN(p.Base)) p.Y = p.Base + ScrollCells;
            }
        }
        // Keep the knight on the playable board. On scrolling modes (Trap/Battle) the
        // board runs off-screen and the ±1 row margin from MoveRules is intentional, so
        // we leave those alone. On STATIC boards (adventure/tutorial, which set Tutorial
        // true) a knight L-jump that lands past an edge would carry the player clean off
        // the visible board, so clamp those to the map: rows 0..MapRows-1 on tall maps, or
        // the visible 0..Rows-1 on normal maps.
        private void FilterMapBounds(List<MoveTarget> moves)
        {
            if (!Tutorial && MapRows <= 0) return; // scrolling modes keep the off-board margin
            int maxRow = MapRows > 0 ? MapRows - 1 : GameConfig.Rows - 1;
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                int mr = (int)Math.Round(moves[i].Y - ScrollCells);
                if (mr < 0 || mr > maxRow) moves.RemoveAt(i);
            }
        }
        private int? _lockCol, _lockRow;
        private float _time;

        private int _comboMissStreak, _captureChain, _advPips;
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
            _enemyAI.ClearPlans();
            Pickups.Clear();
            // Adventure state must be wiped too, otherwise a previous non-scrolling
            // adventure level's oscillating enemies (and fragile tiles) leak into the
            // next Trap/Battle run, where they'd render but never move or be capturable.
            AdvEnemies.Clear();
            Cracked.Clear();
            Trail.Clear();
            Score = 0;
            RunShillings = 0;
            _pickupsEnabled = pickups;
            _trapScore = 0;
            ScrollCells = 0;
            _time = 0;
            Alive = true;
            Tutorial = false;
            Adventure = false;
            AdvTransform = false;
            MapRows = 0;

            ComboMultiplier = 1;
            _comboMissStreak = 0;
            _captureChain = 0;
            _advPips = 0;
            _lastCaptured = null;
            _transformEndsAt = 0;
            _transformIsMimic = false;
            _battleInvulnUntil = 0;
            _lastBattleMove = 0;
            PlayerForm = PieceType.Knight;
            _hasBest = false; _moveEpoch = 0; _bestEpoch = -1; // reset the best-move hint cache

            PlayerCol = 2;
            PlayerY = GameConfig.Rows - 4; // 6 with Rows=10
            _playerBase = PlayerY - ScrollCells;

            if (mode == GameMode.Trap)
            {
                var speedDiff = mediumSpeed ? Difficulty.Medium : difficulty;
                _trapBaseSpeed = GameConfig.TrapBaseSpeed(speedDiff);
                _trapPerLevel = GameConfig.TrapSpeedPerLevel(speedDiff);
                _trapLevel = 0;
                _speed = _trapBaseSpeed;
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
                _enemyAI.InitializePlans(Pieces, ScrollCells);
                _nextSpawnCells = ScrollCells + 1f;
            }
        }

        // Chronokinesis: scroll the board back `cells` (Trap/Battle). Everything's Y derives
        // from ScrollCells, so lowering it shifts the knight AND the board up together,
        // relative positions (and thus the hazard layout) are unchanged, but the knight
        // ends further from the fixed bottom death line, giving room to retreat.
        public void ScrollBack(float cells)
        {
            if (cells <= 0f || (Mode != GameMode.Trap && Mode != GameMode.Battle)) return;
            ScrollCells -= cells;
            PlayerY = _playerBase + ScrollCells;
            for (int i = 0; i < Pieces.Count; i++)
                if (!float.IsNaN(Pieces[i].Base)) Pieces[i].Y = Pieces[i].Base + ScrollCells;
            for (int i = 0; i < Pickups.Count; i++)
                if (!float.IsNaN(Pickups[i].Base)) Pickups[i].Y = Pickups[i].Base + ScrollCells;
        }

        public void Tick(float dt)
        {
            if (!Alive) return;
            if (Tutorial) return; // frozen scripted board; only player taps change it
            if (dt > 1f / 30f) dt = 1f / 30f; // clamp so a frame hitch can't lurch the board
            _time += dt;

            float delta = _speed * dt;
            ScrollCells += delta;

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

            while (ScrollCells + Math.Max(0f, IncomingRowLead) >= _nextSpawnCells)
            {
                int firstNewPiece = Pieces.Count;
                if (Mode == GameMode.Trap)
                    _trapGen.SpawnRow(Pieces, -1f, PlayerCol, _nextSpawnCells);
                else
                    _battleGen.SpawnRow(Pieces, -1f, Difficulty, TransformActive);
                TrySpawnPickup(-1f);
                // Commit the incoming row to the shared grid in the same frame it is
                // generated. Hitches/overshoot must not make it jump on its next tick.
                float rowBase = -1f - _nextSpawnCells;
                for (int i = firstNewPiece; i < Pieces.Count; i++)
                {
                    Pieces[i].Base = rowBase;
                    Pieces[i].Y = rowBase + ScrollCells;
                }
                if (Mode == GameMode.Battle)
                    _enemyAI.FilterNewSpawns(Pieces, firstNewPiece, ScrollCells);
                for (int i = 0; i < Pickups.Count; i++)
                    if (float.IsNaN(Pickups[i].Base))
                    {
                        Pickups[i].Base = rowBase;
                        Pickups[i].Y = rowBase + ScrollCells;
                    }
                _nextSpawnCells += 1f;
            }

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
                    OnTransformExpired?.Invoke();
                }

                if (_time - _lastBattleMove >= _battleMoveInterval)
                {
                    _lastBattleMove = _time;
                    bool enemyHit = _enemyAI.MoveEnemies(Pieces, PlayerCol, PlayerY, ScrollCells);
                    _hasBest = false; // the committed threats changed at this boundary
                    if (enemyHit)
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
                    // EnemyAI commits both Base and Y from the saved board-space plan.
                }
            }

            if (PlayerY > GameConfig.Rows + 1f) { Alive = false; OnDeath?.Invoke(); return; }
            CheckCollision();
        }

        // The screen-Y window a move ray may extend over. Scrolling modes (Trap/Battle) keep
        // the ±1 off-board margin. On a tall follow-camera adventure map the window is the
        // whole map (in screen space), so slider reach follows the board, not the camera —
        // FilterMapBounds then trims to the exact 0..MapRows-1 rows.
        private void MoveRayBounds(out float lo, out float hi)
        {
            if (MapRows <= 0) { lo = -1f; hi = GameConfig.Rows + 1f; return; }
            lo = ScrollCells - 0.5f;
            hi = (MapRows - 1) + ScrollCells + 0.5f;
        }

        public List<MoveTarget> GetValidMoves()
        {
            MoveRayBounds(out float lo, out float hi);
            var m = MoveRules.GetValidMoves(Pieces, PlayerCol, PlayerY, Mode, PlayerForm, AdvTransform, lo, hi);
            FilterMapBounds(m);
            return m;
        }

        // Allocation-free variant for per-frame callers (move hints). Returns a
        // shared buffer, so do not hold the reference across frames.
        private readonly List<MoveTarget> _movesBuffer = new List<MoveTarget>();
        public List<MoveTarget> GetValidMovesNonAlloc()
        {
            MoveRayBounds(out float lo, out float hi);
            MoveRules.Fill(_movesBuffer, Pieces, PlayerCol, PlayerY, Mode, PlayerForm, AdvTransform, lo, hi);
            FilterMapBounds(_movesBuffer);
            return _movesBuffer;
        }

        // ── Best-move hint (Divine Intervention powerup) ─────────────────────
        // The single legal move the heuristic rates highest, or null if there are none.
        // Only meaningful in Battle and Trap. Recomputed ONCE per player move (see
        // _moveEpoch) and cached as an offset from the knight, so the green highlight stays
        // locked to one spot instead of flickering between equally-good options each frame;
        // the offset is re-projected to the live board so it still follows the scroll.
        private int _moveEpoch;
        private int _bestEpoch = -1, _bestDCol, _bestDRow;
        private bool _hasBest;

        public MoveTarget? BestMove()
        {
            if (_hasBest && _bestEpoch == _moveEpoch)
            {
                var kept = FindValidMove(PlayerCol + _bestDCol, PlayerY + _bestDRow);
                // Keep the same choice until the next move, unless a scrolling trap has since
                // landed on that cell (Trap mode), in which case fall through and recompute.
                if (kept.HasValue && (Mode != GameMode.Trap || !LandsOnHazard(kept.Value))) return kept;
            }

            MoveTarget? best = Mode == GameMode.Trap ? BestTrapMove() : BestBattleMove();
            if (best.HasValue)
            {
                _hasBest = true; _bestEpoch = _moveEpoch;
                _bestDCol = best.Value.Col - PlayerCol;
                _bestDRow = (int)Math.Round(best.Value.Y - PlayerY);
            }
            else _hasBest = false;
            return best;
        }

        private MoveTarget? FindValidMove(int col, float y)
        {
            var moves = GetValidMovesNonAlloc();
            for (int i = 0; i < moves.Count; i++)
                if (moves[i].Col == col && Near(moves[i].Y, y)) return moves[i];
            return null;
        }

        private MoveTarget? BestBattleMove()
        {
            var moves = GetValidMovesNonAlloc();
            MoveTarget? best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < moves.Count; i++)
            {
                float s = ScoreBattleMove(moves[i]);
                if (s > bestScore) { bestScore = s; best = moves[i]; }
            }
            return best;
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.5f;

        // Trap mode: reuse the "Was I trapped?" escape solver (EscapeSolver, a BFS over
        // knight L-moves that threads past the wall of traps ahead). Its first step is the
        // move that leads up and out, i.e. the one that maximises forward progress / score.
        private MoveTarget? BestTrapMove()
        {
            var moves = GetValidMovesNonAlloc();
            if (moves.Count == 0) return null;

            // The cell we just came from (scroll-invariant Trail base -> current Y). We never
            // suggest stepping back onto it: a back-step loses score AND is what makes the
            // greedy solver oscillate between the same two squares.
            bool hasPrev = Trail.Count >= 2;
            int prevCol = hasPrev ? Trail[Trail.Count - 2].col : -1;
            float prevY = hasPrev ? Trail[Trail.Count - 2].worldY + ScrollCells : float.NaN;
            bool IsBackstep(MoveTarget m) => hasPrev && m.Col == prevCol && Near(m.Y, prevY);

            var traps = new List<(int col, int relRow)>();
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (o.Alive && o.Type == PieceType.Trap)
                    traps.Add((o.Col, (int)Math.Round(o.Y - PlayerY)));
            }
            // Prefer the escape route's first step, but only if it makes forward progress
            // (up = negative relRow) and isn't a step back onto the previous cell.
            var (path, _, _) = EscapeSolver.Compute(PlayerCol, traps);
            if (path != null && path.Count > 0 && path[0].RelRow < 0)
            {
                int wantCol = path[0].Col;
                float wantY = PlayerY + path[0].RelRow;
                for (int i = 0; i < moves.Count; i++)
                    if (moves[i].Col == wantCol && Near(moves[i].Y, wantY) && !IsBackstep(moves[i]))
                        return moves[i];
            }

            // Otherwise: the most forward (smallest Y) safe move that isn't the cell we came
            // from. This keeps net upward progress and can't form an A<->B loop.
            MoveTarget? best = null;
            float bestY = float.MaxValue;
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                if (LandsOnHazard(m) || IsBackstep(m)) continue;
                if (m.Y < bestY) { bestY = m.Y; best = m; }
            }
            if (best.HasValue) return best;

            // Last resort (every forward/lateral option is a hazard): any safe move, even a
            // backward one, so the hint still points somewhere survivable.
            for (int i = 0; i < moves.Count; i++)
                if (!LandsOnHazard(moves[i]) && (!best.HasValue || moves[i].Y < best.Value.Y)) best = moves[i];
            return best ?? moves[0];
        }

        private bool LandsOnHazard(MoveTarget m)
        {
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (o.Alive && o.Col == m.Col && Near(o.Y, m.Y)) return true;
            }
            return false;
        }

        private float ScoreBattleMove(MoveTarget m)
        {
            // Battle: prefer the highest-value capture; avoid squares an enemy strikes next.
            int capturedValue = 0;
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (o.Alive && o.Type != PieceType.Trap && o.Col == m.Col && Near(o.Y, m.Y))
                    capturedValue = Math.Max(capturedValue, GameConfig.PiecePoints(o.Type));
            }
            float s = capturedValue * 10f;
            if (IsBattleThreatened(m.Col, m.Y) && !TransformActive) s -= 100f; // deadly unless a shield is up
            if (capturedValue == 0) s -= NearestEnemyDist(m.Col, m.Y) * 0.5f;  // else edge toward a capture
            return s;
        }

        // The view reads the committed plan; it never guesses a direction or
        // asks the planner to roll a destination while drawing a threat marker.
        public bool TryGetBattleMove(GamePiece piece, out MoveTarget destination)
        {
            if (Mode == GameMode.Battle && _enemyAI.TryGetNextMove(piece, out var cell))
            {
                destination = new MoveTarget(cell.Col, cell.Row + ScrollCells);
                return true;
            }
            destination = default;
            return false;
        }

        // Keep the coach's danger evaluation in agreement with the committed
        // movement markers and the queen's existing adjacent strike area.
        private bool IsBattleThreatened(int col, float y)
        {
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (!o.Alive || o.Type == PieceType.Trap) continue;
                if (o.Type == PieceType.Queen)
                {
                    foreach (var adj in GameConfig.QueenAdj)
                        if (o.Col + adj.Col == col && Near(o.Y + adj.Row, y)) return true;
                }
                else if (TryGetBattleMove(o, out var move) && move.Col == col && Near(move.Y, y)) return true;
            }
            return false;
        }

        private float NearestEnemyDist(int col, float y)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (!o.Alive || o.Type == PieceType.Trap) continue;
                float d = Math.Abs(o.Col - col) + Math.Abs(o.Y - y);
                if (d < best) best = d;
            }
            return best == float.MaxValue ? 0f : best;
        }

        // Tap at grid cell (col, y in cell units). Snaps to the nearest legal move
        // in that column within ~1.2 cells.
        // ── Interactive tutorial API ─────────────────────────────────────────
        public void TutorialSetup(GameMode mode)
        {
            Adventure = false;
            Mode = mode; Tutorial = true; Alive = true; MapRows = 0;
            AdvTransform = false; _advPips = 0;
            Difficulty = Difficulty.Medium; // consistent combo grace (2) for the miss lesson
            Pieces.Clear(); Pickups.Clear(); Trail.Clear();
            AdvEnemies.Clear();
            Cracked.Clear();
            ScrollCells = 0; _time = 0;
            Score = 0; _trapScore = 0; RunShillings = 0;
            ComboMultiplier = 1; _comboMissStreak = 0; _captureChain = 0; _advPips = 0;
            _lastCaptured = null; _transformEndsAt = 0; _transformIsMimic = false;
            _battleInvulnUntil = 0; _lastBattleMove = 0;
            _hasBest = false; _moveEpoch = 0; _bestEpoch = -1; // reset the best-move hint cache
            PlayerForm = PieceType.Knight;
            _lockCol = null; _lockRow = null;
            _speed = 0f;
            PlayerCol = 2; PlayerY = GameConfig.Rows - 4; _playerBase = PlayerY;
        }
        public void TutorialClear() { Pieces.Clear(); Pickups.Clear(); }
        public void TutorialSetPlayer(int col, int row) { PlayerCol = col; PlayerY = row; _playerBase = row - ScrollCells; }
        public void TutorialAddTrap(int col, int row)
            => Pieces.Add(new GamePiece(PieceType.Trap, col, row) { Alive = true, Base = row - ScrollCells });
        public void TutorialAddPiece(PieceType type, int col, int row)
            => Pieces.Add(new GamePiece(type, col, row) { Alive = true, Dir = 1, Base = row - ScrollCells });
        // Adventure oscillating enemies (capture targets that also threaten), or a Hunter
        // queen that chases the knight (a hazard, not a capture target).
        public void AdventureAddEnemy(PieceType type, int col, int row, int offCol, int offRow, bool hunter = false)
            => AdvEnemies.Add(new AdvEnemy
            {
                Type = type, HomeCol = col, HomeRow = row, OffCol = offCol, OffRow = offRow,
                Phase = 0, Alive = true, Hunter = hunter, CurCol = col, CurRow = row
            });
        public void AdventureAddCracked(int col, int row) => Cracked.Add(new Int2(col, row));
        // When the knight leaves a cracked tile, it collapses into a solid trap.
        private void CollapseCrackedIfLeft(int fromCol, int fromRow)
        {
            for (int i = 0; i < Cracked.Count; i++)
            {
                if (Cracked[i].Col == fromCol && Cracked[i].Row == fromRow)
                {
                    Cracked.RemoveAt(i);
                    Pieces.Add(new GamePiece(PieceType.Trap, fromCol, fromRow + ScrollCells) { Alive = true, Base = fromRow });
                    return;
                }
            }
        }
        // A currency coin at a fixed cell (frozen board, so ScrollCells is 0).
        public void AdventureAddShilling(int col, int row, int value)
            => Pickups.Add(new Pickup(col, row, value) { Base = row - ScrollCells });
        // Force a constant scroll speed (scrolling adventure levels).
        public void AdventureSetSpeed(float speed) { _speed = speed; _trapBaseSpeed = speed; }
        // Hunters (the queen boss) are hazards, not capture targets, so the minions
        // (the ordinary pieces) drive CaptureAll / the remaining-enemy HUD.
        public int AdvRemaining { get { int n = 0; for (int i = 0; i < AdvEnemies.Count; i++) if (AdvEnemies[i].Alive && !AdvEnemies[i].Hunter) n++; return n; } }
        public bool AdvAllCaptured() { for (int i = 0; i < AdvEnemies.Count; i++) if (AdvEnemies[i].Alive && !AdvEnemies[i].Hunter) return false; return true; }
        // The queen is shielded (uncapturable) while any minion (non-hunter enemy) lives.
        public bool QueenShielded { get { for (int i = 0; i < AdvEnemies.Count; i++) if (AdvEnemies[i].Alive && !AdvEnemies[i].Hunter) return true; return false; } }
        public bool HasHunter { get { for (int i = 0; i < AdvEnemies.Count; i++) if (AdvEnemies[i].Hunter) return true; return false; } }
        public bool QueenDownThisMove;   // set for one move when the (unshielded) queen is captured

        // Run after a knight move in Capture/Combined: capture an enemy on the landing
        // tile, then oscillate the survivors; if one lands on the knight, it dies.
        // Returns true if the knight was killed.
        public bool AdventureAfterMove()
        {
            QueenDownThisMove = false;
            // Enemies are stored in base-row (unscrolled) coordinates, but PlayerY
            // includes the follow-camera offset on tall maps; subtract it so capture
            // and kill checks compare like-for-like.
            int kc = PlayerCol, kr = (int)Math.Round(PlayerY - ScrollCells);
            bool shielded = QueenShielded;
            for (int i = 0; i < AdvEnemies.Count; i++)
            {
                var e = AdvEnemies[i];
                // Capture the queen only once her shield is down (all minions gone); landing
                // on her while shielded doesn't capture her. The kill-check below gets you.
                if (e.Alive && e.Hunter && !shielded && e.Col == kc && e.Row == kr)
                {
                    e.Alive = false; QueenDownThisMove = true;
                    continue;
                }
                if (e.Alive && !e.Hunter && e.Col == kc && e.Row == kr)
                {
                    e.Alive = false;
                    // Transformation world: each capture is a pip toward changing form.
                    // Misses never reset _advPips (unlike Battle's capture chain), so
                    // the count only ever climbs. At the threshold the knight takes the
                    // captured piece's movement. The goal is designed to be reachable
                    // only that way. Other worlds ignore this entirely.
                    if (AdvTransform)
                    {
                        _lastCaptured = e.Type;
                        _advPips++;
                        if (_advPips >= GameConfig.AdventureTransformPips)
                        {
                            _advPips = 0;
                            // Guard against non-sliding forms (a Pawn form has no moves
                            // and would soft-lock); transform targets are always Rook/Bishop.
                            if (e.Type == PieceType.Rook || e.Type == PieceType.Bishop || e.Type == PieceType.Queen)
                                SetForm(e.Type);
                        }
                    }
                }
            }
            // Oscillators bob. The queen takes one king-step toward the knight WHILE
            // shielded; once her minions (and shield) are gone she stops chasing and holds
            // still, and is vulnerable, so a knight can line up the capture.
            bool stillShielded = QueenShielded;
            for (int i = 0; i < AdvEnemies.Count; i++)
            {
                var e = AdvEnemies[i];
                if (!e.Alive) continue;
                if (e.Hunter) { if (stillShielded) MoveHunter(e, kc, kr); }
                else e.Phase = 1 - e.Phase;
            }
            for (int i = 0; i < AdvEnemies.Count; i++) { var e = AdvEnemies[i]; if (e.Alive && e.Col == kc && e.Row == kr) { Alive = false; return true; } }
            return false;
        }

        // True if a solid trap sits at base cell (col, row). Blocks a hunter's slide.
        public bool AdvTrapAt(int col, int row)
        {
            for (int i = 0; i < Pieces.Count; i++)
            {
                var o = Pieces[i];
                if (o.Alive && o.Type == PieceType.Trap && o.Col == col &&
                    (int)Math.Round(o.Y - ScrollCells) == row) return true;
            }
            return false;
        }

        private int MaxAdvRow => MapRows > 0 ? MapRows - 1 : GameConfig.Rows - 1;

        // Queen chase: one king-like step (into any of the 8 surrounding cells that isn't a
        // trap or off the map) toward the knight, or straight onto the knight if adjacent
        // (a kill). Chebyshev distance, since a diagonal step closes both axes at once.
        private void MoveHunter(AdvEnemy q, int kc, int kr)
        {
            int bestCol = q.CurCol, bestRow = q.CurRow;
            int bestDist = Math.Max(Math.Abs(q.CurCol - kc), Math.Abs(q.CurRow - kr));
            foreach (var d in GameConfig.QueenAdj)
            {
                int c = q.CurCol + d.Col, r = q.CurRow + d.Row;
                if (c < 0 || c >= GameConfig.Cols || r < 0 || r > MaxAdvRow) continue; // off the map
                if (c == kc && r == kr) { q.CurCol = kc; q.CurRow = kr; return; }       // steps onto the knight: kill
                if (AdvTrapAt(c, r)) continue;                                          // can't step onto a trap
                if (AdvEnemyAt(c, r, q)) continue;                                      // can't step onto another piece
                int dist = Math.Max(Math.Abs(c - kc), Math.Abs(r - kr));
                if (dist < bestDist) { bestDist = dist; bestCol = c; bestRow = r; }
            }
            q.CurCol = bestCol; q.CurRow = bestRow;
        }

        // True if another alive adventure enemy occupies base cell (col, row).
        private bool AdvEnemyAt(int col, int row, AdvEnemy except)
        {
            for (int i = 0; i < AdvEnemies.Count; i++)
            {
                var e = AdvEnemies[i];
                if (e.Alive && e != except && e.Col == col && e.Row == row) return true;
            }
            return false;
        }

        public void TutorialLock(int col, int row) { _lockCol = col; _lockRow = row; }
        public void TutorialUnlock() { _lockCol = null; _lockRow = null; }
        public void TutorialEnd() { Tutorial = false; _lockCol = null; _lockRow = null; }

        public MoveResult TryTap(int col, float y)
        {
            if (!Alive) return new MoveResult { Moved = false, Captures = null };
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
            if (_lockCol.HasValue &&
                (best.Value.Col != _lockCol.Value || (int)Math.Round(best.Value.Y) != _lockRow.Value))
                return new MoveResult { Moved = false, Captures = null };
            return ApplyMove(best.Value);
        }

        private MoveResult ApplyMove(MoveTarget m)
        {
            _moveEpoch++; // invalidates the cached best-move hint (recomputed once per move)
            var result = new MoveResult { Moved = true, Captures = new List<CaptureInfo>() };

            if (Mode == GameMode.Trap)
            {
                int fromCol = PlayerCol, fromRow = (int)Math.Round(PlayerY - ScrollCells); // cell being left
                int rowDelta = (int)Math.Round(PlayerY - m.Y); // forward (up) positive
                _trapScore = Math.Max(0, _trapScore + rowDelta);
                Score = _trapScore;
                // Tetris-style progression: step up a level (and speed) once score
                // crosses each threshold. Monotonic, so backtracking never slows it.
                bool leveledUp = false;
                if (!Tutorial && !Adventure)
                {
                    int lvl = Score / GameConfig.TrapLevelInterval;
                    if (lvl > _trapLevel)
                    {
                        _trapLevel = lvl;
                        float newSpeed = Math.Min(GameConfig.TrapMaxSpeed, _trapBaseSpeed + _trapLevel * _trapPerLevel);
                        if (newSpeed > _speed) // only flash on a real increase (stops once capped)
                        {
                            _speed = newSpeed;
                            leveledUp = true;
                        }
                    }
                }
                result.RowAdvance = rowDelta;

                PlayerCol = m.Col; PlayerY = m.Y;
                _playerBase = PlayerY - ScrollCells;
                if (Cracked.Count > 0) CollapseCrackedIfLeft(fromCol, fromRow);
                Trail.Add((PlayerCol, PlayerY - ScrollCells));
                if (Trail.Count > 12) Trail.RemoveAt(0);
                CollectPickups();
                CheckCollision();
                // Fire AFTER the knight has moved so listeners (e.g. the level-up burst) read
                // the destination tile, not the one it left.
                if (leveledUp) OnTrapLevelUp?.Invoke(_trapLevel);
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
            int[] vals = GameConfig.PickupValues(Difficulty);
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
                    OnPickupCollected?.Invoke();
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
            // safe pocket below is cleared around where they actually land. Snap the
            // base to an integer (like trap bases) so the player stays grid-locked;
            // otherwise the fractional ScrollCells knocks it off the checkerboard.
            if (PlayerY > GameConfig.Rows - 1f)
            {
                _playerBase = (float)Math.Round((GameConfig.Rows - 4) - ScrollCells);
                PlayerY = _playerBase + ScrollCells;
            }

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
