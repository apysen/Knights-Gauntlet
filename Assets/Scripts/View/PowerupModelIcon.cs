using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // A powerup's 3D model as a UI icon (HUD buttons and the shop). The model is
    // rendered by its own tiny camera into a texture, the same offscreen-stage
    // approach as SpinningCoinIcon, and sways gently so it reads as an object.
    //
    // Models come from Resources/PowerupModels (built by ArtSource/Powerups/
    // build_powerups.py). Their imported materials are swapped for the piece shader,
    // keeping each material's colour; any material named "Glass" gets the bubble film.
    [RequireComponent(typeof(RawImage))]
    public sealed class PowerupModelIcon : MonoBehaviour
    {
        private const float SwayAmplitude = 22f;  // degrees either side of the resting angle
        private const float SwayPeriod = 4f;      // seconds per full sway
        private const float RestYaw = 180f;       // turns the imported model's front to the camera
        private const float RestPitch = 12f;      // tips the top toward the camera a little
        private const float Fill = .9f;           // share of the icon the model fills
        private const int TextureSize = 160;
        private const int StageLayer = 31;        // shared with the other UI stages; each sits far apart
        private static int _stages;

        private static readonly Dictionary<Material, Material> Converted = new Dictionary<Material, Material>();

        private GameObject _stage;
        private Transform _pivot;
        private Camera _camera;
        private RenderTexture _texture;
        private float _time;

        public static string ModelOf(string key) =>
            key == "chronokinesis" ? "Hourglass" : key == "divine_path" ? "PathfinderMap" : "DivineCross";

        // Returns false when the model is missing, so callers can fall back to the flat icon.
        public bool Initialize(string powerupKey)
        {
            var asset = Resources.Load<GameObject>("PowerupModels/" + ModelOf(powerupKey));
            if (asset == null) return false;
            _stage = new GameObject("PowerupIconStage");
            _stage.transform.position = new Vector3(-1000f - 20f * _stages++, 40f, 0f);
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_stage.transform, false);
            var model = Instantiate(asset, _pivot, false);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StageLayer;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) Convert(r);

            // Centre the model on the pivot so the sway turns it in place.
            var bounds = WorldBounds(model);
            model.transform.position -= bounds.center - _pivot.position;

            var cameraObject = new GameObject("PowerupIconCamera", typeof(Camera));
            cameraObject.transform.SetParent(_stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f); // looks along +Z
            _camera = cameraObject.GetComponent<Camera>();
            _camera.enabled = false; // rendered by hand, only while visible
            _camera.orthographic = true;
            _camera.aspect = 1f;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 20f;
            _camera.cullingMask = 1 << StageLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = false; _camera.allowMSAA = false;
            // Fit the widest the model gets across its sway, not just its resting pose.
            float radius = bounds.extents.magnitude;
            _camera.orthographicSize = Mathf.Max(bounds.extents.y, radius * .82f) / Fill;

            _texture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
                { name = "Powerup icon", antiAliasing = 4 };
            _texture.Create();
            _camera.targetTexture = _texture;
            var image = GetComponent<RawImage>();
            image.texture = _texture;
            image.color = Color.white;
            image.raycastTarget = false;
            Pose(0f);
            return true;
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            _time += Time.unscaledDeltaTime;
            Pose(SwayAmplitude * Mathf.Sin(_time * Mathf.PI * 2f / SwayPeriod));
            _camera.Render();
        }

        private void Pose(float sway) =>
            _pivot.localRotation = Quaternion.Euler(RestPitch, RestYaw + sway, 0f);

        private static Bounds WorldBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void Convert(Renderer renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null) continue;
                if (!Converted.TryGetValue(source, out var converted) || converted == null)
                {
                    converted = source.name.StartsWith("Glass") ? Glass() : Solid(source);
                    Converted[source] = converted;
                }
                materials[i] = converted;
                // The 2D renderer draws by sorting order: glass after the sand inside it.
                if (source.name.StartsWith("Glass")) renderer.sortingOrder = 1;
            }
            renderer.sharedMaterials = materials;
        }

        private static Material Solid(Material source)
        {
            var m = new Material(Resources.Load<Shader>("Pieces/ChessPiece")) { name = source.name + " (Icon)" };
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            // The FBX carries Blender's colours gamma-encoded (gold 0.74 arrives as 0.88);
            // undo that so the icons match the palette in build_powerups.py.
            color = color.linear;
            color.a = 1f;
            m.SetColor("_BaseColor", color);
            float metal = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f;
            m.SetFloat("_Smoothness", metal > .5f ? .9f : .35f);
            m.SetFloat("_PieceClip", 0f);
            m.SetFloat("_RollBoard", 0f);
            return m;
        }

        private static Material Glass()
        {
            var m = new Material(Resources.Load<Shader>("QueenBubble")) { name = "Glass (Icon)" };
            m.SetColor("_Tint", new Color(.82f, .93f, 1f, 1f));
            m.SetFloat("_Opacity", .85f);
            m.SetFloat("_Board", 0f);
            return m;
        }

        private void OnDestroy()
        {
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            if (_stage != null) Destroy(_stage);
        }
    }
}
