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
        private readonly List<SpriteRenderer> _hintPool = new List<SpriteRenderer>();
        private readonly List<ThreatCell> _threatPool = new List<ThreatCell>();
        private readonly List<SpriteRenderer> _pickupPool = new List<SpriteRenderer>();
        private readonly List<PopupItem> _popups = new List<PopupItem>();
        private PieceView _player;
        private Transform _popupsRoot, _threatsRoot, _pickupsRoot;
        private SpriteRenderer _xformBarBg, _xformBarFill;
        private readonly SpriteRenderer[] _pips = new SpriteRenderer[GameConfig.TransformTrigger];
        private int _pCol, _pRow;
        private bool _colorblind;

        private static readonly Color XformBlue = new Color(0.561f, 0.878f, 1f, 1f);   // #8FE0FF
        private static readonly Color XformGold = new Color(1f, 0.816f, 0.376f, 1f);   // #FFD060
        private static readonly Color XformRed  = new Color(1f, 0.439f, 0.314f, 1f);   // #FF7050
        private static readonly Color XformBg   = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color PipOn  = new Color(0.941f, 0.753f, 0.251f, 0.95f); // filled — gold
        private static readonly Color PipOff = new Color(1f, 1f, 1f, 0.20f);             // empty — dim

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
            _player = CreatePieceView("Player", _piecesRoot, pieceOrder: 10, glyphOrder: 11);

            _xformBarBg = MakeBar("XformBarBg", 12, XformBg);
            _xformBarFill = MakeBar("XformBarFill", 13, XformBlue);
            for (int p = 0; p < _pips.Length; p++) _pips[p] = MakePip("XformPip" + p);
        }

        public void SetPlayerForm(PieceType form, string skinId)
        {
            if (_player != null) _player.Apply(_theme, form, isPlayer: true, skinId: skinId);
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

        public void Sync(GameSimulation sim, string skinId, bool showHints, float dt)
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
            for (int k = p; k < _piecePool.Count; k++) _piecePool[k].gameObject.SetActive(false);

            // Threat tiles (battle): red cells showing where each enemy strikes next.
            int t = 0;
            if (sim.Mode == GameMode.Battle && sim.Alive)
            {
                _pCol = sim.PlayerCol; _pRow = Mathf.RoundToInt(sim.PlayerY);
                for (int i = 0; i < pieces.Count; i++)
                {
                    var o = pieces[i];
                    if (!o.Alive || o.Type == PieceType.Trap || o.Y < VisibleLo || o.Y > VisibleHi) continue;
                    switch (o.Type)
                    {
                        case PieceType.Rook:
                            t = Threat(t, o.Col, o.Y + 1f);
                            break;
                        case PieceType.Bishop:
                            int dir = o.Dir != 0 ? o.Dir : 1;
                            int bc = o.Col + dir;
                            if (bc < 0 || bc >= GameConfig.Cols) bc = o.Col - dir;
                            t = Threat(t, bc, o.Y + 1f);
                            break;
                        case PieceType.Knight:
                            if (o.PlanDC.HasValue && o.PlanDR.HasValue)
                                t = Threat(t, o.Col + o.PlanDC.Value, o.Y + o.PlanDR.Value);
                            break;
                        case PieceType.Queen:
                            foreach (var adj in GameConfig.QueenAdj)
                                t = Threat(t, o.Col + adj.Col, o.Y + adj.Row);
                            break;
                    }
                }
            }
            for (int k = t; k < _threatPool.Count; k++) _threatPool[k].go.SetActive(false);

            // Pickups (shillings) — Trap mode only; Battle pays out on capture.
            int pk = 0;
            var pickups = sim.Pickups;
            for (int i = 0; i < pickups.Count; i++)
            {
                var pu = pickups[i];
                if (!pu.Alive || pu.Y < VisibleLo || pu.Y > VisibleHi) continue;
                var sr = GetPickup(pk++);
                sr.gameObject.SetActive(true);
                sr.transform.position = _board.CellToWorld(pu.Col, pu.Y);
                sr.transform.localScale = Vector3.one * (_board.cellSize * 0.5f);
            }
            for (int k = pk; k < _pickupPool.Count; k++) _pickupPool[k].gameObject.SetActive(false);

            // Player.
            _player.gameObject.SetActive(true);
            _player.transform.position = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            ScalePiece(_player);

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

            // Capture-progress pips (battle, while NOT transformed): they fill in
            // toward the transform, then hand off to the timer bar once it triggers.
            bool showPips = sim.Mode == GameMode.Battle && !sim.TransformActive;
            for (int pi = 0; pi < _pips.Length; pi++) _pips[pi].gameObject.SetActive(showPips);
            if (showPips)
            {
                float cw = _board.cellW, ch = _board.cellSize;
                Vector3 pc = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
                float gap = cw * 0.24f, size = cw * 0.20f, py = pc.y - ch * 0.52f;
                float mid = (_pips.Length - 1) * 0.5f;
                for (int pi = 0; pi < _pips.Length; pi++)
                {
                    _pips[pi].transform.position = new Vector3(pc.x + (pi - mid) * gap, py, 0f);
                    _pips[pi].transform.localScale = Vector3.one * size; // round (1:1)
                    _pips[pi].color = pi < sim.CaptureChain ? PipOn : PipOff;
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
                    hint.gameObject.SetActive(true);
                    hint.color = _theme != null ? _theme.hintColor : new Color(0.941f, 0.753f, 0.251f, 0.25f);
                    hint.transform.position = _board.CellToWorld(m.Col, m.Y);
                    hint.transform.localScale = _board.CellScale();
                }
            }
            for (int k = h; k < _hintPool.Count; k++) _hintPool[k].gameObject.SetActive(false);

            AnimatePopups(dt);
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
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 1; tmp.fontSizeMax = 9;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
            if (_theme != null && _theme.popupFont != null) tmp.font = _theme.popupFont;
            else if (_theme != null && _theme.uiFont != null) tmp.font = _theme.uiFont;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 20;
            var item = new PopupItem { tmp = tmp };
            _popups.Add(item);
            return item;
        }

        private class PopupItem { public TextMeshPro tmp; public float alpha; public bool active; public Color color; }

        private int Threat(int i, int col, float y)
        {
            if (col < 0 || col >= GameConfig.Cols) return i;
            var tc = GetThreat(i);
            tc.go.SetActive(true);
            bool onPlayer = (col == _pCol && Mathf.RoundToInt(y) == _pRow);
            if (onPlayer)
            {
                // A piece will land on YOU next move — flash a bright outline.
                float pulse = 0.55f + 0.45f * Mathf.PingPong(Time.time * 3f, 1f);
                tc.fillSR.color = new Color(0.92f, 0.18f, 0.18f, 0.62f);
                tc.borderSR.color = new Color(1f, 0.5f, 0.5f, pulse);
            }
            else
            {
                tc.fillSR.color = new Color(0.62f, 0.13f, 0.13f, 0.45f);
                tc.borderSR.color = new Color(0.86f, 0.30f, 0.30f, 0.75f);
            }
            tc.go.transform.position = _board.CellToWorld(col, y);
            tc.go.transform.localScale = _board.CellScale();
            // Colorblind mode: a shape-based "O" marker so enemy move-intent reads
            // without relying on the red tint. Counter the cell's horizontal
            // stretch so the ring stays circular.
            if (_colorblind)
            {
                tc.ring.gameObject.SetActive(true);
                const float f = 0.62f;
                tc.ring.transform.localScale = new Vector3(f * _board.cellSize / Mathf.Max(0.0001f, _board.cellW), f, 1f);
                tc.ring.color = onPlayer ? new Color(1f, 1f, 1f, 0.98f) : new Color(0.95f, 0.97f, 1f, 0.9f);
            }
            else if (tc.ring.gameObject.activeSelf) tc.ring.gameObject.SetActive(false);
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
                borderSR.sprite = UISprites.RoundedBorder();
                borderSR.sortingOrder = 4;
                var rgo = new GameObject("CBRing");
                rgo.transform.SetParent(go.transform, false);
                var ringSR = rgo.AddComponent<SpriteRenderer>();
                ringSR.sprite = UISprites.Ring();
                ringSR.sortingOrder = 5;
                rgo.SetActive(false);
                _threatPool.Add(new ThreatCell { go = go, fillSR = fillSR, borderSR = borderSR, ring = ringSR });
            }
            return _threatPool[i];
        }

        private class ThreatCell { public GameObject go; public SpriteRenderer fillSR; public SpriteRenderer borderSR; public SpriteRenderer ring; }

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

        private SpriteRenderer GetHint(int i)
        {
            while (i >= _hintPool.Count)
            {
                var go = new GameObject("Hint");
                go.transform.SetParent(_hintsRoot, false);
                go.transform.localScale = Vector3.one * _board.cellSize;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _theme != null && _theme.hintSprite != null ? _theme.hintSprite : BoardView.UnitSquare();
                sr.sortingOrder = 1;
                // small cream dot in the centre (mirrors the web hint marker)
                var dot = new GameObject("Dot");
                dot.transform.SetParent(go.transform, false);
                dot.transform.localScale = Vector3.one * 0.18f;
                var dsr = dot.AddComponent<SpriteRenderer>();
                dsr.sprite = _theme != null && _theme.hintDotSprite != null ? _theme.hintDotSprite : UISprites.Circle();
                dsr.color = new Color(0.941f, 0.753f, 0.251f, 0.55f); // muted gold, like the web hint
                dsr.sortingOrder = 2;
                _hintPool.Add(sr);
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
