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
        private PieceView _player;

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
            _player = CreatePieceView("Player", _piecesRoot, pieceOrder: 10, glyphOrder: 11);
        }

        public void SetPlayerForm(PieceType form, string skinId)
        {
            if (_player != null) _player.Apply(_theme, form, isPlayer: true, skinId: skinId);
        }

        public void Sync(GameSimulation sim, string skinId, bool showHints)
        {
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
