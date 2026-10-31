using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public partial class GameView
    {
        // ── Boss queen 3D bubble tuning ──────────────────────────────────────
        // Sizes are in board cells. The queen is about 1.4 cells tall, so the
        // bubble is an upright egg that encloses her crown and sits on the tile.
        private const float Bubble3DWidth = 1.1f;    // diameter across the board
        private const float Bubble3DHeight = 1.7f;   // diameter up from the board
        private const float Bubble3DSink = 0.12f;    // bottom sits this far below the tile surface
        private const float Bubble3DOpacity = 1f;    // lower for a fainter film
        private const int Bubble3DSortingOrder = 12; // above enemy pieces (10) and glyphs (11)

        private MeshRenderer _bubble3D;
        private Material _bubbleMaterial;
        private static readonly int PopId = Shader.PropertyToID("_Pop");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private bool EnsureBubble3D()
        {
            if (_bubble3D != null) return true;
            var shader = Resources.Load<Shader>("QueenBubble");
            if (shader == null || !shader.isSupported) return false;
            _bubbleMaterial = new Material(shader) { name = "Queen bubble" };
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "QueenBubble3D";
            go.transform.SetParent(transform, false);
            Destroy(go.GetComponent<Collider>());
            _bubble3D = go.GetComponent<MeshRenderer>();
            _bubble3D.sharedMaterial = _bubbleMaterial;
            _bubble3D.sortingOrder = Bubble3DSortingOrder;
            _bubble3D.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bubble3D.receiveShadows = false;
            // The vertex shader rolls it over the crest, well outside the sphere's own bounds.
            _bubble3D.localBounds = new Bounds(Vector3.zero, Vector3.one * 60f);
            go.SetActive(false);
            return true;
        }

        private void DrawBubble3D(AdvEnemy queen, GameSimulation sim, float pop)
        {
            if (!EnsureBubble3D()) return;
            float ey = queen.Row + sim.ScrollCells;
            // Same cull window as the queen piece, so they leave the board together.
            if (ey < VisibleLo || ey > VisibleHi) { HideBubble3D(); return; }

            // Track her displayed position (including the hop) exactly like PositionEnemy.
            Vector3 center = _board.CellToWorld(queen.Col, ey);
            if (_enemyMotions.TryGetValue(queen, out var motion))
            {
                center = _board.CellToWorld(0, motion.current.y + sim.ScrollCells);
                center.x += motion.current.x * _board.cellW;
                center.z -= motion.lift;
            }
            float height = Bubble3DHeight * _board.cellSize;
            center.z += Bubble3DSink * _board.cellSize - height * 0.5f; // -z points up off the board

            var t = _bubble3D.transform;
            t.position = center;
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(Bubble3DWidth * _board.cellW, Bubble3DWidth * _board.cellSize, height);
            _bubbleMaterial.SetFloat(PopId, pop);
            _bubbleMaterial.SetFloat(OpacityId, Bubble3DOpacity);
            _bubble3D.gameObject.SetActive(true);
        }

        private void HideBubble3D()
        {
            if (_bubble3D != null) _bubble3D.gameObject.SetActive(false);
        }

        private void DisposeBubble3D()
        {
            if (_bubbleMaterial == null) return;
            if (Application.isPlaying) Destroy(_bubbleMaterial);
            else DestroyImmediate(_bubbleMaterial);
        }
    }
}
