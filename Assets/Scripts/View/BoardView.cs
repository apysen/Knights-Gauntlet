using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // The board surface: owns the cell<->world coordinate mapping, draws the
    // scrolling checkerboard, and sizes the camera so the whole 5x10 board fits.
    //
    // Sim space: Col in [0,Cols), Y in cells from the TOP (0 = top), increasing
    // downward. World space: centered on origin, Y up. CellToWorld bridges them.
    public class BoardView : MonoBehaviour
    {
        public float cellSize = 1f;
        public Color cameraBackground = new Color(0.102f, 0.102f, 0.18f); // #1a1a2e

        private GameTheme _theme;
        private SpriteRenderer[] _tiles;
        private const int TileRowLo = -1;              // first screen row drawn
        private static readonly int TileRows = GameConfig.Rows + 2;

        private static Sprite _unitSquare;

        public void Init(GameTheme theme)
        {
            _theme = theme;
            BuildTiles();
        }

        // ── Coordinate conversion ────────────────────────────────────────────
        public Vector3 CellToWorld(int col, float simY)
        {
            float bw = GameConfig.Cols * cellSize;
            float x = (col + 0.5f) * cellSize - bw * 0.5f;
            float y = GameConfig.Rows * 0.5f * cellSize - (simY + 0.5f) * cellSize;
            return new Vector3(x, y, 0f);
        }

        public (int col, float simY) WorldToCell(Vector3 world)
        {
            float bw = GameConfig.Cols * cellSize;
            int col = Mathf.FloorToInt((world.x + bw * 0.5f) / cellSize);
            float simY = (GameConfig.Rows * 0.5f * cellSize - world.y) / cellSize - 0.5f;
            return (col, simY);
        }

        // ── Camera ───────────────────────────────────────────────────────────
        public void FitCamera(Camera cam)
        {
            if (cam == null) return;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float halfHBoard = GameConfig.Rows * 0.5f * cellSize;
            float halfWBoard = GameConfig.Cols * 0.5f * cellSize;
            float size = Mathf.Max(halfHBoard, halfWBoard / Mathf.Max(0.01f, aspect));

            cam.orthographic = true;
            cam.orthographicSize = size * 1.04f; // 4% breathing room
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cameraBackground;
        }

        // ── Scrolling checkerboard ───────────────────────────────────────────
        private void BuildTiles()
        {
            _tiles = new SpriteRenderer[TileRows * GameConfig.Cols];
            var parent = new GameObject("Tiles").transform;
            parent.SetParent(transform, false);

            int i = 0;
            for (int r = 0; r < TileRows; r++)
            {
                for (int c = 0; c < GameConfig.Cols; c++)
                {
                    var go = new GameObject("Cell");
                    go.transform.SetParent(parent, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = TileSprite();
                    sr.sortingOrder = 0;
                    go.transform.localScale = Vector3.one * cellSize;
                    _tiles[i++] = sr;
                }
            }
        }

        private Sprite TileSprite() => _theme != null && _theme.cellSprite != null ? _theme.cellSprite : UnitSquare();

        public void SyncBackground(float scrollCells)
        {
            if (_tiles == null) return;
            int baseRow = Mathf.FloorToInt(scrollCells);
            float frac = scrollCells - baseRow;

            Color a = _theme != null ? _theme.cellColorA : new Color(0.176f, 0.176f, 0.306f);
            Color b = _theme != null ? _theme.cellColorB : new Color(0.118f, 0.118f, 0.208f);

            int i = 0;
            for (int rr = 0; rr < TileRows; rr++)
            {
                int r = TileRowLo + rr;
                for (int c = 0; c < GameConfig.Cols; c++)
                {
                    var sr = _tiles[i++];
                    sr.transform.position = CellToWorld(c, r + frac);
                    int parity = (((baseRow + r + c) % 2) + 2) % 2;
                    sr.color = parity == 0 ? a : b;
                }
            }
        }

        // ── Shared 1x1 white sprite (1 world unit) ───────────────────────────
        public static Sprite UnitSquare()
        {
            if (_unitSquare != null) return _unitSquare;
            var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _unitSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _unitSquare;
        }
    }
}
