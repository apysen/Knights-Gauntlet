using UnityEngine;

namespace KnightsGauntlet.View
{
    // Velvet stage curtains in the side bars on screens wider than the play column
    // (tablets). Every tablet plays in one fixed column shaped like a modern iPhone
    // (ColumnAspect, used by BoardView.FitCamera and the HUD), so the board and HUD look
    // exactly like the phone game scaled up; the extra width becomes two bars, and these
    // curtains fill them. Phones (up to 16:9) keep their full width and no curtains.
    //
    // They hang just in front of the main camera (as its children), so they track
    // the camera, screen shake included, and sit in front of the board. The HUD and
    // menus are overlay canvases and draw on top of them, so menus need no special case.
    //
    // Part() starts a run with the curtains drawn closed in the middle and springs
    // them open to the bars, like a stage opening.
    //
    // Model: Resources/Curtains/StageCurtain.fbx (source ArtSource/Title/StageCurtain.blend),
    // a right-hand drape; the left one is the same model mirrored.
    [RequireComponent(typeof(Camera))]
    public sealed class StageCurtains : MonoBehaviour
    {
        // The play column's shape on tablets: a modern iPhone (1170 x 2532, about 9:19.5).
        public const float TabletColumnAspect = 1170f / 2532f;
        // Screens wider than this are tablets. Phones top out at 16:9 (0.5625).
        public const float TabletAspect = .6f;

        public static bool IsTablet => Screen.height > 0 && (float)Screen.width / Screen.height > TabletAspect;
        // Width / height of the play column: the screen itself on phones, fixed on tablets.
        public static float ColumnAspect(float screenAspect) => screenAspect > TabletAspect ? TabletColumnAspect : screenAspect;

        // The play column's width in pixels (the whole screen on phones).
        public static float ColumnWidthPx => IsTablet ? Screen.height * TabletColumnAspect : Screen.width;
        // Width of one side bar in pixels (0 on phones).
        public static float SideBarPx => Mathf.Max(0f, (Screen.width - ColumnWidthPx) * .5f);

        private Camera _cam;
        private readonly Transform[] _pivots = new Transform[2];
        private Bounds _bounds;           // the model's bounds inside a pivot, at scale 1
        private bool _built, _shown;
        private float _open = 1f, _vel, _time;

        private void Start() { _cam = GetComponent<Camera>(); Build(); }

        private void Build()
        {
            var asset = Resources.Load<GameObject>("Curtains/StageCurtain");
            var velvetShader = Resources.Load<Shader>("Curtains/Velvet");
            if (asset == null || velvetShader == null) return;
            var velvet = new Material(velvetShader) { name = "Curtain velvet" };
            for (int side = 0; side < 2; side++)
            {
                var pivot = new GameObject(side == 0 ? "CurtainLeft" : "CurtainRight").transform;
                pivot.SetParent(transform, false);
                var model = Instantiate(asset, pivot, false);
                // The FBX faces away from a camera looking down +Z (as the 3D icons do).
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    bool cloth = r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Velvet");
                    if (cloth)
                    {
                        r.sharedMaterial = velvet;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                    else PowerupModelIcon.Convert(r);
                    // The 2D renderer draws by sorting order: these go last, over the board.
                    r.sortingOrder = 32000;
                }
                _pivots[side] = pivot;
            }
            _bounds = LocalBounds(_pivots[1]);
            _built = true;
            SetShown(false);
        }

        // Bounds of everything under a pivot, in the pivot's own space.
        private static Bounds LocalBounds(Transform pivot)
        {
            var toPivot = pivot.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in pivot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = toPivot * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        private void SetShown(bool on)
        {
            if (_shown == on) return;
            _shown = on;
            foreach (var p in _pivots) if (p != null) p.gameObject.SetActive(on);
        }

        // A run starts: draw the curtains closed and let them spring open to the bars.
        public void Part()
        {
            if (!_built || SideBarPx < 1f) return;
            if (SaveData.IsReducedEffects()) { _open = 1f; _vel = 0f; return; }
            _open = 0f; _vel = 0f;
            // Audio hook: a soft velvet swish could play here (see the audio plan).
        }

        private void LateUpdate()
        {
            if (!_built || _cam == null) return;
            float aspect = _cam.aspect;
            bool needed = aspect > TabletAspect;
            SetShown(needed);
            if (!needed) { _open = 1f; _vel = 0f; return; }

            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _time += dt;
            bool reduced = SaveData.IsReducedEffects();
            // An underdamped spring, so the curtains swing a touch past the bars and settle.
            _vel += (1f - _open) * 38f * dt;
            _vel *= Mathf.Exp(-6.5f * dt);
            _open += _vel * dt;
            if (reduced) { _open = 1f; _vel = 0f; }

            // A plane a little past the near clip, in front of everything else.
            float d = Mathf.Max(_cam.nearClipPlane * 2f, _cam.nearClipPlane + .4f);
            float halfH = _cam.orthographic ? _cam.orthographicSize : d * Mathf.Tan(_cam.fieldOfView * .5f * Mathf.Deg2Rad);
            float halfW = halfH * aspect;
            float column = halfH * TabletColumnAspect;

            float sway = reduced ? 0f : Mathf.Sin(_time * .55f) * halfH * .004f;
            float inner = Mathf.LerpUnclamped(0f, column - halfH * .006f, _open) + sway;
            float outer = halfW + halfH * .03f;
            float width = Mathf.Max(.0001f, outer - inner);
            float openWidth = outer - column;
            float height = halfH * 2.08f;          // fringe tips at the bottom edge; the top runs off screen

            var size = _bounds.size;
            float sx = width / Mathf.Max(.0001f, size.x);
            float sy = height / Mathf.Max(.0001f, size.y);
            // Depth follows the open width: drawn closed, the pleats spread out and flatten.
            float sz = openWidth / Mathf.Max(.0001f, size.x) * (1f + (reduced ? 0f : .06f * Mathf.Sin(_time * .9f)));

            for (int side = 0; side < 2; side++)
            {
                float dir = side == 0 ? -1f : 1f;
                var p = _pivots[side];
                p.localRotation = Quaternion.identity;
                p.localScale = new Vector3(sx * dir, sy, sz);
                // Model x [min, max] → [inner, outer] (mirrored on the left).
                p.localPosition = new Vector3(dir * (inner - _bounds.min.x * sx), -halfH - _bounds.min.y * sy, d - _bounds.center.z * sz);
            }
        }
    }
}
