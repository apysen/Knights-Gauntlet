using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Reads the simulation each frame and renders it: enemy/trap pieces (pooled),
    // the player piece, and move-hint highlights. Builds all GameObjects at
    // runtime so no prefab authoring is required to get moving.
    public class GameView : MonoBehaviour
    {
        private GameTheme _theme;
        private BoardView _board;
        private Transform _piecesRoot, _hintsRoot;

        private readonly List<PieceView> _piecePool = new List<PieceView>();
        private readonly List<HintCell> _hintPool = new List<HintCell>();
        private readonly List<ThreatCell> _threatPool = new List<ThreatCell>();
        private readonly List<SpriteRenderer> _pickupPool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _crackedPool = new List<SpriteRenderer>();

        // Animated silver-coin pickup (Trap mode). Frames load once from Resources;
        // all coins share one clock so they spin in sync.
        private Sprite[] _coinFrames;
        private float _coinUnitH = 0.32f; // world height of a coin frame, for scaling
        private float _coinAnimT;
        private const float CoinFps = 10f;
        private readonly List<PopupItem> _popups = new List<PopupItem>();
        private readonly List<Particle> _particles = new List<Particle>();
        private Transform _particlesRoot;
        private PieceView _player;
        private SpriteRenderer _playerGlow; // soft radial shine behind the knight
        private Transform _popupsRoot, _threatsRoot, _pickupsRoot;
        private SpriteRenderer _xformBarBg, _xformBarFill;
        private readonly SpriteRenderer[] _pips = new SpriteRenderer[GameConfig.TransformTrigger];
        private int _pCol, _pRow;
        private bool _colorblind;
        private SpriteRenderer _coachFill, _coachRing;
        private SpriteRenderer _bestMoveFill, _bestMoveBorder; // Divine Intervention green marker
        private SpriteRenderer _queenShield;   // bubble over the shielded boss queen
        private Sprite[] _bubbleFrames;        // bubble_sheet frames (0..19)
        private bool _shieldWasUp;             // for detecting the shield-drop (pop) trigger
        private bool _popping;                 // shield-drop pop animation in progress
        private float _popTimer;               // seconds into the pop animation

        // ── Boss queen bubble tuning ─────────────────────────────────────────
        // Change these to tweak the bubble. BubbleSize is the on-board size (multiple of a
        // board cell); raise it for a bigger bubble, lower it for a smaller one.
        private const float BubbleSize = 1f;          // bubble size, in board cells
        private const int BubbleShieldFrame = 13;     // steady (shielded) bubble frame
        private const int BubblePopStart = 14;        // pop animation: frames 14..19
        private const int BubblePopEnd = 19;
        private const float BubblePopDuration = 0.35f; // seconds for the whole pop
        private const float BubbleOpacity = 0.5f;      // 1.0 = full; 0.8 = 20% more transparent
        private const float BubbleDownPixels = 4f;     // nudge the bubble down this many art pixels
        private const float BubbleFramePixels = 28f;   // bubble frame height (px), for the nudge
        private bool _coachOn; private int _coachCol; private float _coachRow;
        // Animated goal flag (adventure goal tiles). Replaces the coach ring there.
        private SpriteRenderer _goalFlag;
        private Sprite[] _flagFrames;
        private float _flagUnitH = 0.52f, _flagAnimT;
        private const float FlagFps = 8f;
        private bool _flagOn; private int _flagCol; private float _flagRow;
        private static readonly Color CoachGold = new Color(0.941f, 0.753f, 0.251f, 1f);

        private static readonly Color XformBlue = new Color(0.561f, 0.878f, 1f, 1f);   // #8FE0FF
        private static readonly Color XformGold = new Color(1f, 0.816f, 0.376f, 1f);   // #FFD060
        private static readonly Color XformRed  = new Color(1f, 0.439f, 0.314f, 1f);   // #FF7050
        private static readonly Color XformBg   = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color PipOn  = new Color(0.941f, 0.753f, 0.251f, 0.95f); // filled, gold
        private static readonly Color PipOff = new Color(1f, 1f, 1f, 0.20f);             // empty, dim

        // Don't render pieces well outside the viewport (traps linger off-screen
        // for the death snapshot but shouldn't be drawn).
        private const float VisibleLo = -1.5f;
        private static readonly float VisibleHi = GameConfig.Rows + 1.5f;

        public void Init(GameTheme theme, BoardView board)
        {
            _theme = theme;
            _board = board;
            _piecesRoot = new GameObject("EnemyPieces").transform; _piecesRoot.SetParent(transform, false);
            _hintsRoot = new GameObject("MoveHints").transform; _hintsRoot.SetParent(transform, false);
            _threatsRoot = new GameObject("ThreatTiles").transform; _threatsRoot.SetParent(transform, false);
            _pickupsRoot = new GameObject("Pickups").transform; _pickupsRoot.SetParent(transform, false);
            _popupsRoot = new GameObject("Popups").transform; _popupsRoot.SetParent(transform, false);
            _particlesRoot = new GameObject("CaptureParticles").transform; _particlesRoot.SetParent(transform, false);
            _player = CreatePieceView("Player", _piecesRoot, pieceOrder: 10, glyphOrder: 11);

            // Soft radial glow behind the knight (the web's shadowBlur "shine"). Tinted
            // to the equipped skin in SetPlayerForm; sits above the faint tile backing
            // but below the glyph, so it reads as a gradient halo, not a hot square.
            var glowGo = new GameObject("PlayerGlow");
            glowGo.transform.SetParent(_piecesRoot, false);
            _playerGlow = glowGo.AddComponent<SpriteRenderer>();
            _playerGlow.sprite = UISprites.Glow();
            _playerGlow.sortingOrder = 8; // behind the knight AND behind a behind-knight VFX (order 9)
            _playerGlow.color = new Color(0.941f, 0.753f, 0.251f, 0.15f); // gold until a skin sets it

            _xformBarBg = MakeBar("XformBarBg", 12, XformBg);
            _xformBarFill = MakeBar("XformBarFill", 13, XformBlue);
            for (int p = 0; p < _pips.Length; p++) _pips[p] = MakePip("XformPip" + p);

            _coachFill = MakeBar("CoachFill", 8, CoachGold);
            _coachRing = MakeBar("CoachRing", 9, CoachGold);
            _coachRing.sprite = UISprites.RoundedBorder();

            // Best-move marker (Divine Intervention): a bright green cell drawn over the
            // normal hints. Hidden unless the powerup is active. Fill + border for clarity.
            _bestMoveFill = MakeBar("BestMoveFill", 8, new Color(0.235f, 0.863f, 0.471f, 0.30f));
            _bestMoveFill.sprite = BoardView.UnitSquare();
            _bestMoveBorder = MakeBar("BestMoveBorder", 9, new Color(0.235f, 0.863f, 0.471f, 0.95f));
            _bestMoveBorder.sprite = UISprites.SquareBorder();
            _bestMoveFill.gameObject.SetActive(false);
            _bestMoveBorder.gameObject.SetActive(false);

            // Bubble drawn over the boss queen while her minions shield her (frame 14),
            // then a pop animation (frames 15-19) when the shield drops. Art already blue.
            _queenShield = MakeBar("QueenShield", 12, Color.white);
            _bubbleFrames = LoadBubbleFrames();
            _queenShield.gameObject.SetActive(false);

            var flagGo = new GameObject("GoalFlag");
            flagGo.transform.SetParent(transform, false);
            _goalFlag = flagGo.AddComponent<SpriteRenderer>();
            _goalFlag.sortingOrder = 3; // above the board, below the pieces/knight
            flagGo.SetActive(false);
        }

        public void SetPlayerForm(PieceType form, string skinId)
        {
            if (_player != null) _player.Apply(_theme, form, isPlayer: true, skinId: skinId);
            if (_playerGlow != null)
            {
                // Glow colour follows the skin, except the secret black knight glows red.
                var glow = Skins.GlowOf(skinId ?? "gold");
                float a = skinId == Skins.Secret.Id ? 0.22f : 0.15f; // a touch stronger for the red eye
                _playerGlow.color = new Color(glow.r, glow.g, glow.b, a);
            }
        }

        // Keeps the piece/glyph square (1:1) so it never looks stretched, while
        // widening only the translucent cell backing to fill the (stretched) cell.
        private void ScalePiece(PieceView pv)
        {
            pv.transform.localScale = Vector3.one * _board.cellSize;
            if (pv.backing != null)
                pv.backing.transform.localScale =
                    new Vector3(_board.cellW / Mathf.Max(0.0001f, _board.cellSize), 1f, 1f);
        }

        // Plays the equipped movement VFX once, parented to the knight so it stays at
        // its base (no scroll drift). Called on each successful move (a valid tap).
        // BehindKnight effects draw between the board and the knight; the rest on top.
        public void PlayMoveVfx()
        {
            string id = SaveData.GetVfx();
            if (id == Vfx.NoneId || _player == null) return;
            var v = Vfx.Find(id);
            if (v == null) return;
            int order = v.BehindKnight ? 9 : 15; // glow=8, VFX=9, knight piece=10, glyph=11
            // Parent is the player piece (localScale = cellSize), so local units are
            // cell units: sit ~0.32 cells below centre, ~1.13 cells across.
            VfxPlayer.Play(v, _player.transform, new Vector3(0f, 0f, 0f), 1.13f, order);
        }

        public void Sync(GameSimulation sim, bool showHints, float dt, MoveTarget? bestMove = null, IReadOnlyList<Int2> advPath = null, bool advPathNextOnly = false)
        {
            _colorblind = SaveData.IsColorblind();
            // Enemy / trap pieces.
            int p = 0;
            var pieces = sim.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (!piece.Alive || piece.Y < VisibleLo || piece.Y > VisibleHi) continue;
                var pv = GetPiece(p++);
                pv.gameObject.SetActive(true);
                pv.Apply(_theme, piece.Type);
                pv.transform.position = _board.CellToWorld(piece.Col, piece.Y);
                ScalePiece(pv);
            }
            // Adventure oscillating enemies (drawn with the piece pool).
            var adv = sim.AdvEnemies;
            for (int i = 0; i < adv.Count; i++)
            {
                var e = adv[i];
                if (!e.Alive) continue;
                // Enemies live in board cells; add the follow-camera scroll offset so
                // they track the map on tall levels (traps/player/cracked all do this).
                float ey = e.Row + sim.ScrollCells;
                if (ey < VisibleLo || ey > VisibleHi) continue;
                var pv = GetPiece(p++);
                pv.gameObject.SetActive(true);
                pv.Apply(_theme, e.Type);
                pv.transform.position = _board.CellToWorld(e.Col, ey);
                ScalePiece(pv);
            }
            for (int k = p; k < _piecePool.Count; k++) _piecePool[k].gameObject.SetActive(false);

            // Threat tiles: red cells showing where each enemy strikes next. Cells are
            // de-duplicated and COUNTED first, so a square threatened by two pieces (a
            // queen and a rook, say) renders a shade darker than one threatened by one.
            _threatAgg.Clear();
            if (sim.Mode == GameMode.Battle && sim.Alive && !sim.Tutorial)
            {
                _pCol = sim.PlayerCol; _pRow = Mathf.RoundToInt(sim.PlayerY);
                for (int i = 0; i < pieces.Count; i++)
                {
                    var o = pieces[i];
                    if (!o.Alive || o.Type == PieceType.Trap || o.Y < VisibleLo || o.Y > VisibleHi) continue;
                    switch (o.Type)
                    {
                        case PieceType.Rook:
                            AddThreat(o.Col, o.Y + 1f);
                            break;
                        case PieceType.Bishop:
                            int dir = o.Dir != 0 ? o.Dir : 1;
                            int bc = o.Col + dir;
                            if (bc < 0 || bc >= GameConfig.Cols) bc = o.Col - dir;
                            AddThreat(bc, o.Y + 1f);
                            break;
                        case PieceType.Knight:
                            if (o.PlanDC.HasValue && o.PlanDR.HasValue)
                                AddThreat(o.Col + o.PlanDC.Value, o.Y + o.PlanDR.Value);
                            break;
                        case PieceType.Queen:
                            foreach (var adj in GameConfig.QueenAdj)
                                AddThreat(o.Col + adj.Col, o.Y + adj.Row);
                            break;
                    }
                }
            }
            // Adventure telegraph: outline where each oscillating enemy moves next; for a
            // hunter queen, outline every square it currently attacks (its lines, stopped by
            // traps and the map edge) so the player can find a safe knight jump.
            if (adv.Count > 0)
            {
                _pCol = sim.PlayerCol; _pRow = Mathf.RoundToInt(sim.PlayerY);
                for (int i = 0; i < adv.Count; i++)
                {
                    var e = adv[i];
                    if (!e.Alive || !e.Moves) continue; // pawns don't move, no threat
                    if (e.Hunter)
                    {
                        // Once her minions are gone she's frozen and harmless, so drop her threat.
                        if (!sim.QueenShielded) continue;
                        // King-step queen: the 8 surrounding cells she could step to next.
                        int maxRow = sim.MapRows > 0 ? sim.MapRows - 1 : GameConfig.Rows - 1;
                        foreach (var d in GameConfig.QueenAdj)
                        {
                            int c = e.Col + d.Col, r = e.Row + d.Row;
                            if (c < 0 || c >= GameConfig.Cols || r < 0 || r > maxRow) continue;
                            if (sim.AdvTrapAt(c, r)) continue;
                            AddThreat(c, r + sim.ScrollCells);
                        }
                    }
                    else AddThreat(e.NextCol, e.NextRow + sim.ScrollCells);
                }
            }
            int t = 0;
            for (int i = 0; i < _threatAgg.Count; i++) t = DrawThreat(t, _threatAgg[i]);
            for (int k = t; k < _threatPool.Count; k++) _threatPool[k].go.SetActive(false);

            // Cracked/fragile tiles (safe once; collapse to a trap when left).
            int ck = 0;
            var crackedCells = sim.Cracked;
            for (int i = 0; i < crackedCells.Count; i++)
            {
                float sy = crackedCells[i].Row + sim.ScrollCells;
                if (sy < VisibleLo || sy > VisibleHi) continue;
                var cc = GetCracked(ck++);
                cc.gameObject.SetActive(true);
                cc.transform.position = _board.CellToWorld(crackedCells[i].Col, sy);
                cc.transform.localScale = _board.CellScale(0.94f);
            }
            for (int k = ck; k < _crackedPool.Count; k++) _crackedPool[k].gameObject.SetActive(false);

            // Pickups (shillings). Trap mode only; Battle pays out on capture.
            // Rendered as an animated silver coin, held centred on its tile.
            int pk = 0;
            var pickups = sim.Pickups;
            var coin = CoinFrames();
            _coinAnimT += dt;
            int coinIdx = coin.Length > 0 ? (int)(_coinAnimT * CoinFps) % coin.Length : 0;
            for (int i = 0; i < pickups.Count; i++)
            {
                var pu = pickups[i];
                if (!pu.Alive || pu.Y < VisibleLo || pu.Y > VisibleHi) continue;
                var sr = GetPickup(pk++);
                sr.gameObject.SetActive(true);
                if (coin.Length > 0)
                {
                    // Full-colour coin frame (no tint), scaled so its constant-height
                    // frames read at ~0.54 of a cell. Centre pivots keep the spinning
                    // coin from wandering across its trimmed frames.
                    sr.sprite = coin[coinIdx];
                    sr.color = Color.white;
                    sr.transform.localScale = Vector3.one * (_board.cellSize * 0.54f / _coinUnitH);
                }
                else
                {
                    sr.transform.localScale = Vector3.one * (_board.cellSize * 0.5f);
                }
                // Re-anchored to the tile each frame, so it never drifts with scroll.
                sr.transform.position = _board.CellToWorld(pu.Col, pu.Y);
            }
            for (int k = pk; k < _pickupPool.Count; k++) _pickupPool[k].gameObject.SetActive(false);

            // Player.
            _player.gameObject.SetActive(true);
            _player.transform.position = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            ScalePiece(_player);
            if (_playerGlow != null)
            {
                _playerGlow.transform.position = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
                _playerGlow.transform.localScale = _board.CellScale(1f); // fills the cell, clipped at its edges
            }

            // Transformation timer bar, just under the player (battle only).
            bool showBar = sim.Mode == GameMode.Battle && sim.TransformActive;
            _xformBarBg.gameObject.SetActive(showBar);
            _xformBarFill.gameObject.SetActive(showBar);
            if (showBar)
            {
                float cw = _board.cellW, ch = _board.cellSize;
                Vector3 c = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
                float barW = cw * 0.66f, barH = ch * 0.06f;
                float barY = c.y - ch * 0.52f;
                float frac = Mathf.Clamp01(sim.TransformRemaining / GameConfig.TransformSeconds);

                _xformBarBg.transform.position = new Vector3(c.x, barY, 0f);
                _xformBarBg.transform.localScale = new Vector3(barW, barH, 1f);

                float fillW = Mathf.Max(0.0001f, barW * frac);
                _xformBarFill.transform.position = new Vector3(c.x - barW * 0.5f + fillW * 0.5f, barY, 0f);
                _xformBarFill.transform.localScale = new Vector3(fillW, barH, 1f);
                _xformBarFill.color = frac > 0.4f ? XformBlue : (frac > 0.2f ? XformGold : XformRed);
            }

            // Capture-progress pips: in Battle they fill toward the transform, then
            // hand off to the timer bar once it triggers. Adventure's transformation
            // world reuses them with a shorter (2-pip) track that persists across
            // misses, shown only until the knight has transformed.
            bool battlePips = sim.Mode == GameMode.Battle && !sim.TransformActive;
            bool advPips = sim.AdvTransform && sim.PlayerForm == PieceType.Knight;
            bool showPips = battlePips || advPips;
            int pipCount = advPips ? GameConfig.AdventureTransformPips : _pips.Length;
            int pipFilled = advPips ? sim.AdvPips : sim.CaptureChain;
            for (int pi = 0; pi < _pips.Length; pi++)
                _pips[pi].gameObject.SetActive(showPips && pi < pipCount);
            if (showPips)
            {
                float cw = _board.cellW, ch = _board.cellSize;
                Vector3 pc = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
                float gap = cw * 0.24f, size = cw * 0.20f, py = pc.y - ch * 0.52f;
                float mid = (pipCount - 1) * 0.5f;
                for (int pi = 0; pi < pipCount; pi++)
                {
                    _pips[pi].transform.position = new Vector3(pc.x + (pi - mid) * gap, py, 0f);
                    _pips[pi].transform.localScale = Vector3.one * size; // round (1:1)
                    _pips[pi].color = pi < pipFilled ? PipOn : PipOff;
                }
            }

            // Move hints.
            int h = 0;
            if (showHints && sim.Alive)
            {
                var moves = sim.GetValidMovesNonAlloc();
                for (int i = 0; i < moves.Count; i++)
                {
                    var m = moves[i];
                    if (m.Y < VisibleLo || m.Y > VisibleHi) continue;
                    var hint = GetHint(h++);
                    hint.go.SetActive(true);
                    // Web "danger" gradient: gold (#f0c040) up top, blending toward white
                    // the lower the tile, i.e. a cream in between, never pure white.
                    float danger = Mathf.Clamp01(m.Y / GameConfig.Rows);
                    var c = new Color(Mathf.Lerp(240f, 255f, danger) / 255f,
                                      Mathf.Lerp(192f, 255f, danger) / 255f,
                                      Mathf.Lerp(64f, 255f, danger) / 255f, 1f);
                    hint.fill.color = new Color(c.r, c.g, c.b, 0.125f);
                    hint.dot.color = new Color(c.r, c.g, c.b, 0.72f);
                    hint.go.transform.position = _board.CellToWorld(m.Col, m.Y);
                    hint.go.transform.localScale = _board.CellScale();
                }
            }
            for (int k = h; k < _hintPool.Count; k++) _hintPool[k].go.SetActive(false);

            // Divine Intervention: glow the single recommended cell green over the hints.
            bool showBest = bestMove.HasValue && sim.Alive &&
                            bestMove.Value.Y >= VisibleLo && bestMove.Value.Y <= VisibleHi;
            if (_bestMoveFill != null) _bestMoveFill.gameObject.SetActive(showBest);
            if (_bestMoveBorder != null) _bestMoveBorder.gameObject.SetActive(showBest);
            if (showBest)
            {
                var pos = _board.CellToWorld(bestMove.Value.Col, bestMove.Value.Y);
                var scale = _board.CellScale();
                _bestMoveFill.transform.position = pos; _bestMoveFill.transform.localScale = scale;
                _bestMoveBorder.transform.position = pos; _bestMoveBorder.transform.localScale = scale;
            }

            DrawAdvPath(sim, advPath, advPathNextOnly);
            UpdateQueenShield(sim, dt);
            UpdateCoachMark();
            UpdateGoalFlag(dt);
            AnimatePopups(dt);
            AnimateParticles(dt);
        }

        // Divine Path: the next tile of the solved optimal route (nextOnly), drawn with the
        // SAME green fill + border as Divine Intervention's best-move marker in trap/battle
        // (base-row coords; add the follow-camera scroll to reach screen rows). The pool
        // still handles n markers, so passing the whole route would light it all up.
        private static readonly Color PathFillColor = new Color(0.235f, 0.863f, 0.471f, 0.30f);
        private static readonly Color PathBorderColor = new Color(0.235f, 0.863f, 0.471f, 0.95f);
        private readonly List<SpriteRenderer> _pathFillPool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _pathBorderPool = new List<SpriteRenderer>();
        private void DrawAdvPath(GameSimulation sim, IReadOnlyList<Int2> path, bool nextOnly)
        {
            int n = 0;
            if (path != null)
            {
                int count = nextOnly ? System.Math.Min(1, path.Count) : path.Count; // boss: just the next step
                for (int i = 0; i < count; i++)
                {
                    float y = path[i].Row + sim.ScrollCells;
                    if (y < VisibleLo || y > VisibleHi) continue;
                    var pos = _board.CellToWorld(path[i].Col, y);
                    var scale = _board.CellScale();
                    var fill = GetPathMarker(_pathFillPool, n, "PathFill", 8, PathFillColor, BoardView.UnitSquare());
                    var border = GetPathMarker(_pathBorderPool, n, "PathBorder", 9, PathBorderColor, UISprites.SquareBorder());
                    fill.gameObject.SetActive(true); fill.transform.position = pos; fill.transform.localScale = scale;
                    border.gameObject.SetActive(true); border.transform.position = pos; border.transform.localScale = scale;
                    n++;
                }
            }
            for (int k = n; k < _pathFillPool.Count; k++) _pathFillPool[k].gameObject.SetActive(false);
            for (int k = n; k < _pathBorderPool.Count; k++) _pathBorderPool[k].gameObject.SetActive(false);
        }

        private SpriteRenderer GetPathMarker(List<SpriteRenderer> pool, int i, string name, int order, Color color, Sprite sprite)
        {
            while (i >= pool.Count)
            {
                var sr = MakeBar(name, order, color);
                sr.sprite = sprite;
                pool.Add(sr);
            }
            return pool[i];
        }

        // Boss queen shield: the steady bubble (frame 14) while any minion lives; when the
        // last minion is captured, play the pop animation (frames 15-19) once, then hide.
        private void UpdateQueenShield(GameSimulation sim, float dt)
        {
            if (_queenShield == null || _bubbleFrames == null || _bubbleFrames.Length == 0) return;
            AdvEnemy queen = null;
            var adv = sim.AdvEnemies;
            for (int i = 0; i < adv.Count; i++) if (adv[i].Alive && adv[i].Hunter) { queen = adv[i]; break; }
            if (queen == null) { _queenShield.gameObject.SetActive(false); _shieldWasUp = false; _popping = false; return; }

            if (sim.QueenShielded)
            {
                _shieldWasUp = true; _popping = false;
                DrawShield(queen, sim, BubbleShieldFrame);
                return;
            }

            // Shield is down.
            if (_shieldWasUp) { _shieldWasUp = false; _popping = true; _popTimer = 0f; } // just dropped -> pop
            if (_popping)
            {
                _popTimer += dt;
                float t = Mathf.Clamp01(_popTimer / BubblePopDuration);
                int span = BubblePopEnd - BubblePopStart; // 5 (frames 14..19)
                int frame = BubblePopStart + Mathf.Min(span, Mathf.FloorToInt(t * (span + 1)));
                DrawShield(queen, sim, frame);
                if (_popTimer >= BubblePopDuration) { _popping = false; _queenShield.gameObject.SetActive(false); }
            }
            else _queenShield.gameObject.SetActive(false);
        }

        private void DrawShield(AdvEnemy queen, GameSimulation sim, int frame)
        {
            _queenShield.gameObject.SetActive(true);
            _queenShield.sprite = _bubbleFrames[Mathf.Clamp(frame, 0, _bubbleFrames.Length - 1)];
            // Nudge down by BubbleDownPixels art pixels (a cell = one row; the bubble is
            // BubbleFramePixels tall, drawn at BubbleSize cells). Larger row = lower on screen.
            float ey = queen.Row + sim.ScrollCells + BubbleDownPixels / BubbleFramePixels * BubbleSize;
            _queenShield.transform.position = _board.CellToWorld(queen.Col, ey);
            _queenShield.transform.localScale = _board.CellScale(BubbleSize); // see BubbleSize to resize
            var c = _queenShield.color; c.a = BubbleOpacity; _queenShield.color = c;
        }

        // Slice Resources/VFX/bubble_sheet.png (a single horizontal strip) into 20 frames.
        private Sprite[] LoadBubbleFrames()
        {
            var tex = Resources.Load<Texture2D>("VFX/bubble_sheet");
            if (tex == null) { Debug.LogWarning("[VFX] Missing Resources/VFX/bubble_sheet.png"); return new Sprite[0]; }
            const int frames = 20;
            int fw = tex.width / frames, fh = tex.height;
            var arr = new Sprite[frames];
            for (int i = 0; i < frames; i++)
                arr[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), new Vector2(0.5f, 0.5f), fh);
            return arr;
        }

        // ── Popups (+score / xCombo / shielded!) ─────────────────────────────
        // Floating text that rises and fades, mirroring the web version's popups.
        public void SpawnPopup(int col, float y, string text, Color color)
        {
            var item = GetPopup();
            item.active = true;
            item.alpha = 1.25f;
            item.color = color;
            item.tmp.text = text;
            var c = color; c.a = 1f; item.tmp.color = c;
            item.tmp.gameObject.SetActive(true);
            item.tmp.transform.position = _board.CellToWorld(col, y);
        }

        private void AnimatePopups(float dt)
        {
            if (_popups.Count == 0) return;
            float drift = (_board != null ? _board.cellSize : 1f) * 0.9f;
            for (int i = 0; i < _popups.Count; i++)
            {
                var it = _popups[i];
                if (!it.active) continue;
                it.alpha -= dt * 1.4f;
                if (it.alpha <= 0f) { it.active = false; it.tmp.gameObject.SetActive(false); continue; }
                var pos = it.tmp.transform.position; pos.y += drift * dt; it.tmp.transform.position = pos;
                var c = it.color; c.a = Mathf.Clamp01(it.alpha); it.tmp.color = c;
            }
        }

        private PopupItem GetPopup()
        {
            for (int i = 0; i < _popups.Count; i++) if (!_popups[i].active) return _popups[i];
            var go = new GameObject("Popup");
            go.transform.SetParent(_popupsRoot, false);
            go.transform.localScale = Vector3.one * _board.cellSize;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Normal;
            // Halved vs the original: the pixel popup font renders far chunkier, so
            // +score / xCombo were overwhelming the board.
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 1; tmp.fontSizeMax = 4.5f;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 1f);
            if (_theme != null && _theme.popupFont != null) tmp.font = _theme.popupFont;
            else if (_theme != null && _theme.uiFont != null) tmp.font = _theme.uiFont;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 20;
            var item = new PopupItem { tmp = tmp };
            _popups.Add(item);
            return item;
        }

        private class PopupItem { public TextMeshPro tmp; public float alpha; public bool active; public Color color; }

        // ── Capture particles (piece "disintegrates" into flying pixels) ──────
        // On a capture we scatter a handful of tiny square shards, tinted to the
        // captured piece, from its tile. They explode outward, arc under gravity,
        // shrink, and fade. All pooled sprites, animated in AnimateParticles.
        private const int CaptureBits = 28;          // shards per captured piece
        private const float CaptureSpeedMin = 1.6f;  // outward speed, in cells/sec
        private const float CaptureSpeedMax = 4.2f;
        private const float CaptureUpBias = 0.4f;    // slight lift so they arc up before falling
        private const float CaptureGravity = 9f;     // cells/sec^2 pull-down
        private const float CaptureLifeMin = 0.35f;  // seconds before a shard vanishes
        private const float CaptureLifeMax = 0.6f;

        // Spawn a burst at (col, y), tinted to `color`. Safe to call every capture;
        // shards are pooled so rapid combos never allocate.
        public void SpawnCaptureBurst(int col, float y, Color color)
        {
            if (_board == null) return;
            float cell = _board.cellSize;
            Vector3 center = _board.CellToWorld(col, y);
            for (int j = 0; j < CaptureBits; j++)
            {
                var p = GetParticle();
                p.active = true;
                // Start scattered within the piece's ink, not from a single point.
                Vector2 jitter = Random.insideUnitCircle * (cell * 0.22f);
                p.tr.position = center + new Vector3(jitter.x, jitter.y, 0f);

                float ang = Random.value * Mathf.PI * 2f;
                float speed = cell * Random.Range(CaptureSpeedMin, CaptureSpeedMax);
                p.vel = new Vector3(Mathf.Cos(ang) * speed,
                                    Mathf.Sin(ang) * speed + cell * CaptureUpBias, 0f);
                p.spin = Random.Range(-360f, 360f);
                p.maxLife = Random.Range(CaptureLifeMin, CaptureLifeMax);
                p.life = p.maxLife;
                p.size = cell * Random.Range(0.09f, 0.15f);

                // Small per-shard colour variation so it reads as shattered pixels,
                // not a flat blob.
                float v = Random.Range(-0.12f, 0.12f);
                p.color = new Color(Mathf.Clamp01(color.r + v), Mathf.Clamp01(color.g + v),
                                    Mathf.Clamp01(color.b + v), 1f);
                p.sr.color = p.color;
                p.tr.localScale = new Vector3(p.size, p.size, 1f);
                p.sr.gameObject.SetActive(true);
            }
        }

        private void AnimateParticles(float dt)
        {
            if (_particles.Count == 0) return;
            float g = (_board != null ? _board.cellSize : 1f) * CaptureGravity;
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                if (!p.active) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.active = false; p.sr.gameObject.SetActive(false); continue; }
                p.vel.y -= g * dt;
                p.tr.position += p.vel * dt;
                p.tr.Rotate(0f, 0f, p.spin * dt);
                float k = p.life / p.maxLife;               // 1 -> 0 over the shard's life
                // Stay fully opaque for most of the life, then fade only over the last
                // ~35%, so the shards read solid instead of ghostly.
                var c = p.color; c.a = Mathf.Clamp01(k / 0.35f); p.sr.color = c;
                float s = p.size * Mathf.Lerp(0.35f, 1f, k); // shrink as it dies
                p.tr.localScale = new Vector3(s, s, 1f);
            }
        }

        private Particle GetParticle()
        {
            for (int i = 0; i < _particles.Count; i++) if (!_particles[i].active) return _particles[i];
            var go = new GameObject("CaptureBit");
            go.transform.SetParent(_particlesRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BoardView.UnitSquare();
            sr.sortingOrder = 15; // above pieces (10), below popups (20)
            var item = new Particle { tr = go.transform, sr = sr };
            _particles.Add(item);
            return item;
        }

        private class Particle
        {
            public Transform tr; public SpriteRenderer sr;
            public Vector3 vel; public float spin, life, maxLife, size;
            public Color color; public bool active;
        }

        // De-duplicated threat accumulator (rebuilt each frame). Keeps the exact
        // fractional Y of the first contributor so tiles still scroll smoothly with
        // the board, while Count drives how dark a multiply-threatened cell renders.
        private struct ThreatAgg { public int Col; public int Row; public float Y; public int Count; }
        private readonly List<ThreatAgg> _threatAgg = new List<ThreatAgg>();

        private void AddThreat(int col, float y)
        {
            if (col < 0 || col >= GameConfig.Cols) return;
            int row = Mathf.RoundToInt(y);
            for (int i = 0; i < _threatAgg.Count; i++)
                if (_threatAgg[i].Col == col && _threatAgg[i].Row == row)
                {
                    var a = _threatAgg[i]; a.Count++; _threatAgg[i] = a; return;
                }
            _threatAgg.Add(new ThreatAgg { Col = col, Row = row, Y = y, Count = 1 });
        }

        private int DrawThreat(int i, ThreatAgg agg)
        {
            var tc = GetThreat(i);
            tc.go.SetActive(true);
            bool onPlayer = (agg.Col == _pCol && agg.Row == _pRow);
            tc.go.transform.position = _board.CellToWorld(agg.Col, agg.Y);
            tc.go.transform.localScale = _board.CellScale();

            // Web palette: fill rgba(90,20,20,.55), straight outline rgba(150,40,40,.55).
            // Overlapping threats compound (as the web's alpha blend does) so a cell
            // threatened by two pieces reads a shade darker: 1 → .55, 2 → ~.80.
            tc.fillSR.enabled = true;
            tc.borderSR.enabled = true;

            float a = Mathf.Min(0.85f, 1f - Mathf.Pow(1f - 0.55f, agg.Count));
            tc.fillSR.color = new Color(0.353f, 0.078f, 0.078f, a);
            tc.borderSR.color = _colorblind ? new Color(0.95f, 0.32f, 0.32f, 1f)
                                            : new Color(0.588f, 0.157f, 0.157f, 0.55f);
            if (onPlayer)
            {
                // Pulse the outline on the knight's own square so an incoming hit reads.
                float pulse = 0.55f + 0.45f * Mathf.PingPong(Time.time * 3f, 1f);
                tc.borderSR.color = new Color(1f, 0.45f, 0.45f, pulse);
            }
            return i + 1;
        }

        private ThreatCell GetThreat(int i)
        {
            while (i >= _threatPool.Count)
            {
                var go = new GameObject("Threat");
                go.transform.SetParent(_threatsRoot, false);
                go.transform.localScale = Vector3.one * _board.cellSize;
                var fillSR = go.AddComponent<SpriteRenderer>();
                fillSR.sprite = _theme != null && _theme.threatSprite != null ? _theme.threatSprite : BoardView.UnitSquare();
                fillSR.sortingOrder = 3;
                var bgo = new GameObject("Border");
                bgo.transform.SetParent(go.transform, false);
                var borderSR = bgo.AddComponent<SpriteRenderer>();
                borderSR.sprite = UISprites.SquareBorder(); // straight-edged, not a rounded ring
                borderSR.sortingOrder = 4;
                _threatPool.Add(new ThreatCell { go = go, fillSR = fillSR, borderSR = borderSR });
            }
            return _threatPool[i];
        }

        private class ThreatCell { public GameObject go; public SpriteRenderer fillSR; public SpriteRenderer borderSR; }

        private SpriteRenderer MakeBar(string name, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BoardView.UnitSquare();
            sr.color = color;
            sr.sortingOrder = order;
            go.SetActive(false);
            return sr;
        }

        private SpriteRenderer MakePip(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _theme != null && _theme.hintDotSprite != null ? _theme.hintDotSprite : UISprites.Circle();
            sr.sortingOrder = 14;
            go.SetActive(false);
            return sr;
        }

        // Tutorial coach mark: a pulsing gold highlight + ring on the one cell the
        // player should tap next.
        public void SetCoachMark(int col, float row) { _coachOn = true; _coachCol = col; _coachRow = row; }
        public void ClearCoachMark()
        {
            _coachOn = false;
            if (_coachFill != null) _coachFill.gameObject.SetActive(false);
            if (_coachRing != null) _coachRing.gameObject.SetActive(false);
            ClearGoalFlag();
        }

        // Animated goal flag on an adventure goal tile (used instead of the coach ring).
        public void SetGoalFlag(int col, float row) { _flagOn = true; _flagCol = col; _flagRow = row; }
        public void ClearGoalFlag()
        {
            _flagOn = false;
            if (_goalFlag != null) _goalFlag.gameObject.SetActive(false);
        }

        private Sprite[] FlagFrames()
        {
            if (_flagFrames != null) return _flagFrames;
            var sprites = Resources.LoadAll<Sprite>("Flag/flag animation");
            if (sprites != null && sprites.Length > 0)
            {
                var list = new List<Sprite>(sprites);
                list.Sort((a, b) => VfxPlayer.NaturalCompare(a.name, b.name));
                _flagFrames = list.ToArray();
                _flagUnitH = Mathf.Max(0.0001f, _flagFrames[0].bounds.size.y);
            }
            else _flagFrames = new Sprite[0];
            return _flagFrames;
        }

        private void UpdateGoalFlag(float dt)
        {
            if (!_flagOn) { if (_goalFlag != null && _goalFlag.gameObject.activeSelf) _goalFlag.gameObject.SetActive(false); return; }
            var frames = FlagFrames();
            if (frames.Length == 0) { _goalFlag.gameObject.SetActive(false); return; }
            _flagAnimT += dt;
            var sprite = frames[(int)(_flagAnimT * FlagFps) % frames.Length];
            _goalFlag.gameObject.SetActive(true);
            _goalFlag.sprite = sprite;
            _goalFlag.color = Color.white;
            // Frames are sliced to uniform cells with a fixed bottom pivot at the flag's
            // centre, so every frame aligns with no left/right jitter. Plant it standing up
            // from the bottom of the goal cell, ~0.95 cell tall.
            float scale = _board.cellSize * 0.95f / _flagUnitH;
            _goalFlag.transform.localScale = Vector3.one * scale;
            Vector3 center = _board.CellToWorld(_flagCol, _flagRow);
            _goalFlag.transform.position = new Vector3(center.x, center.y - _board.cellSize * 0.5f, 0f);
        }
        private void UpdateCoachMark()
        {
            if (!_coachOn) { if (_coachFill.gameObject.activeSelf) ClearCoachMark(); return; }
            float pulse = Mathf.PingPong(Time.time * 2f, 1f);
            Vector3 pos = _board.CellToWorld(_coachCol, _coachRow);
            _coachFill.gameObject.SetActive(true);
            _coachFill.transform.position = pos;
            _coachFill.transform.localScale = _board.CellScale();
            _coachFill.color = new Color(CoachGold.r, CoachGold.g, CoachGold.b, 0.14f + 0.16f * pulse);
            _coachRing.gameObject.SetActive(true);
            _coachRing.transform.position = pos;
            _coachRing.transform.localScale = _board.CellScale() * (0.92f + 0.08f * pulse);
            _coachRing.color = new Color(CoachGold.r, CoachGold.g, CoachGold.b, 0.6f + 0.4f * pulse);
        }

        private SpriteRenderer GetCracked(int i)
        {
            while (i >= _crackedPool.Count)
            {
                var go = new GameObject("Cracked");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = UISprites.Cracked();
                sr.sortingOrder = 2; // floor tile: under pieces/knight, over the board
                _crackedPool.Add(sr);
            }
            return _crackedPool[i];
        }
        // Silver-coin frames for the pickup, loaded once from Resources and sorted
        // naturally (0,1,…,10). Caches an empty array on miss so we don't re-hit disk.
        private Sprite[] CoinFrames()
        {
            if (_coinFrames != null) return _coinFrames;
            var sprites = Resources.LoadAll<Sprite>("Coin/Coin_Silver-Sheet");
            if (sprites != null && sprites.Length > 0)
            {
                var list = new List<Sprite>(sprites);
                list.Sort((a, b) => VfxPlayer.NaturalCompare(a.name, b.name));
                _coinFrames = list.ToArray();
                _coinUnitH = Mathf.Max(0.0001f, _coinFrames[0].bounds.size.y);
            }
            else _coinFrames = new Sprite[0];
            return _coinFrames;
        }
        private SpriteRenderer GetPickup(int i)
        {
            while (i >= _pickupPool.Count)
            {
                var go = new GameObject("Pickup");
                go.transform.SetParent(_pickupsRoot, false);
                go.transform.localScale = Vector3.one * (_board.cellSize * 0.5f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _theme != null && _theme.pickupSprite != null ? _theme.pickupSprite : UISprites.Circle();
                sr.color = _theme != null ? _theme.pickupColor : new Color(0.95f, 0.78f, 0.25f, 1f);
                sr.sortingOrder = 7;
                _pickupPool.Add(sr);
            }
            return _pickupPool[i];
        }

        // ── Pooling ──────────────────────────────────────────────────────────
        private PieceView GetPiece(int i)
        {
            while (i >= _piecePool.Count)
                _piecePool.Add(CreatePieceView("Piece", _piecesRoot, pieceOrder: 5, glyphOrder: 6));
            return _piecePool[i];
        }

        private class HintCell { public GameObject go; public SpriteRenderer fill; public SpriteRenderer dot; }

        private HintCell GetHint(int i)
        {
            while (i >= _hintPool.Count)
            {
                var go = new GameObject("Hint");
                go.transform.SetParent(_hintsRoot, false);
                go.transform.localScale = Vector3.one * _board.cellSize;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _theme != null && _theme.hintSprite != null ? _theme.hintSprite : BoardView.UnitSquare();
                sr.sortingOrder = 1;
                // Centre dot (mirrors the web hint marker); slightly bigger than the web's.
                var dot = new GameObject("Dot");
                dot.transform.SetParent(go.transform, false);
                dot.transform.localScale = Vector3.one * 0.22f;
                var dsr = dot.AddComponent<SpriteRenderer>();
                dsr.sprite = _theme != null && _theme.hintDotSprite != null ? _theme.hintDotSprite : UISprites.Circle();
                dsr.sortingOrder = 2;
                _hintPool.Add(new HintCell { go = go, fill = sr, dot = dsr });
            }
            return _hintPool[i];
        }

        // Builds a piece GameObject: SpriteRenderer + a translucent backing + a
        // world-space TMP label for the chess-glyph fallback. PieceView.Apply
        // then decides per piece whether the sprite or the glyph is shown.
        private PieceView CreatePieceView(string name, Transform parent, int pieceOrder, int glyphOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * _board.cellSize;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BoardView.UnitSquare();
            sr.sortingOrder = pieceOrder;

            var pv = go.AddComponent<PieceView>();
            pv.spriteRenderer = sr;

            var bgo = new GameObject("Backing");
            bgo.transform.SetParent(go.transform, false);
            var bsr = bgo.AddComponent<SpriteRenderer>();
            bsr.sprite = BoardView.UnitSquare();
            bsr.sortingOrder = pieceOrder - 1;
            pv.backing = bsr;

            var ggo = new GameObject("Glyph");
            ggo.transform.SetParent(go.transform, false);
            var tmp = ggo.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1; tmp.fontSizeMax = 24;
            tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);
            var mr = ggo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = glyphOrder;
            pv.glyphLabel = tmp;

            return pv;
        }
    }
}
