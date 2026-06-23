using System.Collections.Generic;
using UnityEngine;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Reads the simulation each frame and renders it: enemy/trap pieces, the
    // player, move hints, battle threat previews, the transform + enemy-move
    // timer bars, and floating popups. All GameObjects are built at runtime.
    public class GameView : MonoBehaviour
    {
        private GameTheme _theme;
        private BoardView _board;
        private Transform _piecesRoot, _hintsRoot, _threatsRoot, _popupRoot, _barsRoot;

        private readonly List<PieceView> _piecePool = new List<PieceView>();
        private readonly List<SpriteRenderer> _hintPool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _threatPool = new List<SpriteRenderer>();
        private readonly List<(int col, float y)> _threats = new List<(int, float)>();
        private PieceView _player;

        private SpriteRenderer _xformBarBg, _xformBarFill;
        private SpriteRenderer _enemyBarTrack, _enemyBarFill;

        private class Popup { public GameObject go; public TextMeshPro tmp; public float age, life; public Vector3 vel; public bool active; }
        private readonly List<Popup> _popups = new List<Popup>();

        private const float VisibleLo = -1.5f;
        private static readonly float VisibleHi = GameConfig.Rows + 1.5f;

        // Palette
        private static readonly Color XformBlue = new Color32(0x8F, 0xE0, 0xFF, 0xFF);
        private static readonly Color XformGold = new Color32(0xFF, 0xD0, 0x60, 0xFF);
        private static readonly Color XformRed = new Color32(0xFF, 0x70, 0x50, 0xFF);
        private static readonly Color BarTrack = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color EnemyTrack = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color EnemyGreen = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color EnemyRed = new Color(0.94f, 0.2f, 0.2f);

        public void Init(GameTheme theme, BoardView board)
        {
            _theme = theme;
            _board = board;
            _piecesRoot = MakeRoot("EnemyPieces");
            _hintsRoot = MakeRoot("MoveHints");
            _threatsRoot = MakeRoot("Threats");
            _popupRoot = MakeRoot("Popups");
            _barsRoot = MakeRoot("Bars");
            _player = CreatePieceView("Player", _piecesRoot, 10, 11);

            _xformBarBg = MakeBar("XformBarBg", 13);
            _xformBarFill = MakeBar("XformBarFill", 14);
            _enemyBarTrack = MakeBar("EnemyBarTrack", 13);
            _enemyBarFill = MakeBar("EnemyBarFill", 14);
        }

        private Transform MakeRoot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        public void SetPlayerForm(PieceType form, string skinId)
        {
            if (_player != null) _player.Apply(_theme, form, isPlayer: true, skinId: skinId);
        }

        public void Sync(GameSimulation sim, string skinId, bool showHints, float dt)
        {
            bool battle = sim.Mode == GameMode.Battle;

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
            }
            for (int k = p; k < _piecePool.Count; k++) _piecePool[k].gameObject.SetActive(false);

            // Player.
            _player.gameObject.SetActive(true);
            _player.transform.position = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);

            // Move hints.
            int h = 0;
            if (showHints && sim.Alive)
            {
                var moves = sim.GetValidMoves();
                for (int i = 0; i < moves.Count; i++)
                {
                    var m = moves[i];
                    if (m.Y < VisibleLo || m.Y > VisibleHi) continue;
                    var hint = GetHint(h++);
                    hint.gameObject.SetActive(true);
                    hint.color = _theme != null ? _theme.hintColor : new Color(0.941f, 0.753f, 0.251f, 0.25f);
                    hint.transform.position = _board.CellToWorld(m.Col, m.Y);
                }
            }
            for (int k = h; k < _hintPool.Count; k++) _hintPool[k].gameObject.SetActive(false);

            SyncThreats(sim, battle);
            SyncTransformBar(sim, battle);
            SyncEnemyBar(sim, battle && sim.Alive);
            UpdatePopups(dt);
        }

        // ── Threat previews ──────────────────────────────────────────────────
        private void SyncThreats(GameSimulation sim, bool battle)
        {
            _threats.Clear();
            if (battle)
            {
                foreach (var o in sim.Pieces)
                {
                    if (!o.Alive || o.Type == PieceType.Trap) continue;
                    switch (o.Type)
                    {
                        case PieceType.Rook:
                            _threats.Add((o.Col, o.Y + 1f));
                            break;
                        case PieceType.Bishop:
                            int dir = o.Dir != 0 ? o.Dir : 1;
                            int btc = o.Col + dir;
                            if (btc < 0 || btc >= GameConfig.Cols) btc = o.Col - dir;
                            _threats.Add((btc, o.Y + 1f));
                            break;
                        case PieceType.Knight:
                            if (o.PlanDC.HasValue && o.PlanDR.HasValue)
                                _threats.Add((o.Col + o.PlanDC.Value, o.Y + o.PlanDR.Value));
                            break;
                        case PieceType.Queen:
                            foreach (var adj in GameConfig.QueenAdj)
                                _threats.Add((o.Col + adj.Col, o.Y + adj.Row));
                            break;
                    }
                }
            }

            int t = 0;
            for (int i = 0; i < _threats.Count; i++)
            {
                var (col, y) = _threats[i];
                if (col < 0 || col >= GameConfig.Cols || y < VisibleLo || y > VisibleHi) continue;
                var sr = GetThreat(t++);
                sr.gameObject.SetActive(true);
                sr.color = _theme != null ? _theme.threatFill : new Color(0.353f, 0.078f, 0.078f, 0.55f);
                sr.transform.position = _board.CellToWorld(col, y);
            }
            for (int k = t; k < _threatPool.Count; k++) _threatPool[k].gameObject.SetActive(false);
        }

        // ── Transform timer bar (under the player) ───────────────────────────
        private void SyncTransformBar(GameSimulation sim, bool battle)
        {
            bool show = battle && sim.TransformActive;
            _xformBarBg.gameObject.SetActive(show);
            _xformBarFill.gameObject.SetActive(show);
            if (!show) return;

            float frac = sim.TransformRemaining / GameConfig.TransformSeconds;
            float cs = _board.cellSize;
            Vector3 pw = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            float barW = 0.66f * cs, barH = 0.08f * cs;
            float by = pw.y - 0.5f * cs - barH * 0.6f;
            float leftX = pw.x - barW * 0.5f;

            _xformBarBg.color = BarTrack;
            _xformBarBg.transform.position = new Vector3(pw.x, by, 0f);
            _xformBarBg.transform.localScale = new Vector3(barW, barH, 1f);

            float fw = Mathf.Max(0f, frac) * barW;
            _xformBarFill.color = frac > 0.4f ? XformBlue : (frac > 0.2f ? XformGold : XformRed);
            _xformBarFill.transform.position = new Vector3(leftX + fw * 0.5f, by, 0f);
            _xformBarFill.transform.localScale = new Vector3(fw, barH, 1f);
        }

        // ── Enemy-move countdown bar (near board bottom) ─────────────────────
        private void SyncEnemyBar(GameSimulation sim, bool show)
        {
            _enemyBarTrack.gameObject.SetActive(show);
            _enemyBarFill.gameObject.SetActive(show);
            if (!show) return;

            float cs = _board.cellSize;
            float prog = sim.EnemyMoveProgress;
            float barW = 0.55f * GameConfig.Cols * cs, barH = 0.10f * cs;
            float by = _board.CellToWorld(0, GameConfig.Rows - 0.4f).y;
            float leftX = -barW * 0.5f;

            _enemyBarTrack.color = EnemyTrack;
            _enemyBarTrack.transform.position = new Vector3(0f, by, 0f);
            _enemyBarTrack.transform.localScale = new Vector3(barW, barH, 1f);

            float fw = prog * barW;
            _enemyBarFill.color = Color.Lerp(EnemyGreen, EnemyRed, prog);
            _enemyBarFill.transform.position = new Vector3(leftX + fw * 0.5f, by, 0f);
            _enemyBarFill.transform.localScale = new Vector3(fw, barH, 1f);
        }

        // ── Popups ───────────────────────────────────────────────────────────
        public void SpawnPopup(int col, float simY, string text, Color color)
        {
            var pu = GetFreePopup();
            pu.tmp.text = text;
            pu.tmp.color = color;
            pu.tmp.alpha = 1f;
            pu.go.transform.position = _board.CellToWorld(col, simY);
            pu.age = 0f; pu.life = 0.9f; pu.vel = Vector3.up * (0.9f * _board.cellSize);
            pu.active = true;
            pu.go.SetActive(true);
        }

        private void UpdatePopups(float dt)
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                var pu = _popups[i];
                if (!pu.active) continue;
                pu.age += dt;
                float k = pu.age / pu.life;
                if (k >= 1f) { pu.active = false; pu.go.SetActive(false); continue; }
                pu.go.transform.position += pu.vel * dt;
                pu.tmp.alpha = 1f - k;
            }
        }

        private Popup GetFreePopup()
        {
            for (int i = 0; i < _popups.Count; i++) if (!_popups[i].active) return _popups[i];

            var go = new GameObject("Popup");
            go.transform.SetParent(_popupRoot, false);
            go.transform.localScale = Vector3.one * _board.cellSize;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1; tmp.fontSizeMax = 12;
            tmp.rectTransform.sizeDelta = new Vector2(2.6f, 0.8f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 20;
            go.SetActive(false);
            var pu = new Popup { go = go, tmp = tmp, active = false };
            _popups.Add(pu);
            return pu;
        }

        // ── Pooling / construction ───────────────────────────────────────────
        private PieceView GetPiece(int i)
        {
            while (i >= _piecePool.Count)
                _piecePool.Add(CreatePieceView("Piece", _piecesRoot, 5, 6));
            return _piecePool[i];
        }

        private SpriteRenderer GetHint(int i)
        {
            while (i >= _hintPool.Count) _hintPool.Add(MakeQuad("Hint", _hintsRoot, _theme != null && _theme.hintSprite != null ? _theme.hintSprite : BoardView.UnitSquare(), 1));
            return _hintPool[i];
        }

        private SpriteRenderer GetThreat(int i)
        {
            while (i >= _threatPool.Count) _threatPool.Add(MakeQuad("Threat", _threatsRoot, _theme != null && _theme.threatSprite != null ? _theme.threatSprite : BoardView.UnitSquare(), 2));
            return _threatPool[i];
        }

        private SpriteRenderer MakeQuad(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * _board.cellSize;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private SpriteRenderer MakeBar(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_barsRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BoardView.UnitSquare();
            sr.sortingOrder = order;
            go.SetActive(false);
            return sr;
        }

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