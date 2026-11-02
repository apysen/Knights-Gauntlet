using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // A 3D gold star for UI star rows (level select tiles, the adventure result).
    //
    // Every star on screen shows the SAME render: one hidden stage holds a faceted
    // five-point star (built in code, no asset), one camera renders it into a shared
    // texture while any star is visible, and each star's RawImage just points at that
    // texture. 18 stars cost one tiny render, not 18. Unearned stars use the same
    // texture tinted dark, so they read as an unlit star.
    [RequireComponent(typeof(RawImage))]
    public sealed class StarIcon3D : MonoBehaviour
    {
        private const int TextureSize = 128;
        private const int StageLayer = 31;            // the UI stage layer (see PowerupModelIcon)
        private const float SwayAmplitude = 32f;      // degrees either side, shows the facets
        private const float SwayPeriod = 3.2f;
        private static readonly Color Unearned = new Color(.26f, .27f, .33f, 1f);

        private static GameObject _stage;
        private static Transform _pivot;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static int _visible, _lastFrame = -1;
        private static float _time;

        // Turn an image slot into a 3D star. Returns false if the stage can't be built.
        public static bool Apply(RawImage image, bool earned)
        {
            if (image == null || !EnsureStage()) return false;
            image.texture = _texture;
            image.color = earned ? Color.white : Unearned;
            image.raycastTarget = false;
            if (image.GetComponent<StarIcon3D>() == null) image.gameObject.AddComponent<StarIcon3D>();
            return true;
        }

        private void OnEnable()
        {
            _visible++;
            // Domain reload is off in this project: after a Play session the static stage
            // can be a destroyed object, so re-check and re-point the image.
            if (EnsureStage()) GetComponent<RawImage>().texture = _texture;
        }
        private void OnDisable() => _visible = Mathf.Max(0, _visible - 1);

        // Any visible star drives the shared render, once per frame.
        private void LateUpdate()
        {
            if (_lastFrame == Time.frameCount || _camera == null) return;
            _lastFrame = Time.frameCount;
            _time += Time.unscaledDeltaTime;
            float a = SwayAmplitude * Mathf.Sin(_time * Mathf.PI * 2f / SwayPeriod);
            _pivot.localRotation = Quaternion.Euler(8f, a, 0f);
            _camera.Render();
        }

        private static bool EnsureStage()
        {
            if (_stage != null && _camera != null && _texture != null && _texture.IsCreated()) return true;
            if (_stage != null) Destroy(_stage);
            var shader = Resources.Load<Shader>("Pieces/ChessPiece");
            if (shader == null) return false;

            _stage = new GameObject("StarIconStage");
            _stage.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(_stage);
            _stage.transform.position = new Vector3(-3000f, 40f, 0f); // far from the other icon stages
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_stage.transform, false);

            var star = new GameObject("Star", typeof(MeshFilter), typeof(MeshRenderer));
            star.layer = StageLayer;
            star.transform.SetParent(_pivot, false);
            star.GetComponent<MeshFilter>().sharedMesh = BuildStarMesh(1f, .46f, .34f);
            var mr = star.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = new Material(shader) { name = "Star (Icon)" };
            mat.SetColor("_BaseColor", new Color(1f, .74f, .16f, 1f));
            mat.SetFloat("_Smoothness", .88f);
            mat.SetFloat("_PieceClip", 0f);
            mat.SetFloat("_RollBoard", 0f);
            mr.sharedMaterial = mat;

            var camGo = new GameObject("StarIconCamera", typeof(Camera));
            camGo.transform.SetParent(_stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            _camera = camGo.GetComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true; _camera.orthographicSize = 1.08f; _camera.aspect = 1f;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 20f;
            _camera.cullingMask = 1 << StageLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = false; _camera.allowMSAA = false;

            _texture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
                { name = "Star icon", antiAliasing = 4 };
            _texture.Create();
            _camera.targetTexture = _texture;
            _pivot.localRotation = Quaternion.Euler(8f, 0f, 0f);
            _camera.Render();
            return true;
        }

        // A faceted five-point star: ten rim points (outer R / inner r) and a raised
        // centre on each face, every triangle with its own flat normal so the facets
        // catch the light like a cut gem.
        private static Mesh BuildStarMesh(float outer, float inner, float depth)
        {
            var rim = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI * .5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? outer : inner;
                rim[i] = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
            }
            var front = new Vector3(0f, 0f, -depth); // toward the camera
            var back = new Vector3(0f, 0f, depth);

            var verts = new Vector3[10 * 2 * 3];
            var tris = new int[verts.Length];
            int v = 0;
            for (int i = 0; i < 10; i++)
            {
                var a = rim[i]; var b = rim[(i + 1) % 10];
                // Front face (clockwise as seen from the camera at -z).
                verts[v] = front; verts[v + 1] = b; verts[v + 2] = a; v += 3;
                // Back face.
                verts[v] = back; verts[v + 1] = a; verts[v + 2] = b; v += 3;
            }
            for (int i = 0; i < tris.Length; i++) tris[i] = i;
            var mesh = new Mesh { name = "FacetedStar" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals(); // unshared vertices, so every facet stays flat
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
