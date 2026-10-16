using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public partial class BoardView
    {
        public const float RollStart = 3f;
        public const float RollRadius = 2.4f;
        public const float RollEnd = 8.8f;
        // FBX converts Blender +Y to Unity -Z. Turn the asymmetric entrance
        // toward the far end of the board before standing it on the XY plane.
        private static Quaternion HorizonOrientation => SurfaceOrientation * Quaternion.Euler(0f, 180f, 0f);
        private Transform _frameRoot, _drumRoot;
        private Transform _levelTopBorder;
        private float? _levelFirstRow;
        private float _surfaceStart = RollStart, _surfaceEnd = RollEnd;

        public void SetLevelEnd(float? firstRow) => _levelFirstRow = firstRow;

        private void SyncLevelEnd(float scroll)
        {
            float edge = _levelFirstRow.HasValue ? GameConfig.Rows * cellSize * .5f - (_levelFirstRow.Value + scroll) * cellSize : RollEnd;
            float flatten = _levelFirstRow.HasValue ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RollEnd, GameConfig.Rows * cellSize * .5f, edge)) : 0f;
            _surfaceStart = Mathf.Lerp(RollStart, GameConfig.Rows * cellSize * .5f, flatten);
            _surfaceEnd = Mathf.Min(RollEnd, edge);
            Shader.SetGlobalVector("_KGBoardRoll", _legacy2D ? Vector4.zero : new Vector4(_surfaceStart, RollRadius, _surfaceEnd, 1f));
            Shader.SetGlobalVector("_KGBoardEnd", new Vector4(edge, _levelFirstRow.HasValue && !_legacy2D ? 1f : 0f, 0f, 0f));
            if (_drumRoot != null) _drumRoot.gameObject.SetActive(flatten < .001f);
            if (_levelTopBorder != null)
            {
                _levelTopBorder.gameObject.SetActive(_levelFirstRow.HasValue && !_legacy2D && edge <= RollEnd);
                _levelTopBorder.localPosition = new Vector3(0f, edge + .09f, 0f);
                _levelTopBorder.localScale = new Vector3(cellW, 1f, 1f);
            }
        }
        private Transform _castleRoot, _hallEnd;
        // Far sections must already exist beyond the visible vanishing point.
        private readonly Transform[] _hallBays = new Transform[10];
        private readonly GameObject[] _hallLanterns = new GameObject[10];
        private readonly System.Collections.Generic.Dictionary<Material, Material> _castleMaterials = new System.Collections.Generic.Dictionary<Material, Material>();
        private float _gateBlend, _hallBlend, _sceneryTime;
        private const float CastleScale = .8f, HallBayLength = 26f, CastleEyeHeight = 2.4f;

        private void SetRollingMaterials(Renderer[] renderers)
        {
            var block = new MaterialPropertyBlock();
            block.SetFloat("_RollBoard", 1f);
            foreach (var renderer in renderers) renderer.SetPropertyBlock(block);
        }

        private void BuildHorizon()
        {
            var drum = Resources.Load<GameObject>("Horizon/HorizonDrum");
            if (drum != null)
            {
                _drumRoot = new GameObject("HorizonDrum").transform;
                _drumRoot.SetParent(_modeledEnvironmentRoot.transform, false);
                _drumRoot.localRotation = HorizonOrientation;
                var model = Instantiate(drum, _drumRoot, false);
                ConfigureModelRenderers(model.GetComponentsInChildren<Renderer>(true), -22, false);
            }
            var gate = Resources.Load<GameObject>("Horizon/CastleApproachGate");
            var hall = Resources.Load<GameObject>("Horizon/CastleHallBay");
            var lantern = Resources.Load<GameObject>("Horizon/CastleHallLantern");
            if (gate == null || hall == null)
            {
                Debug.LogWarning("Missing connected castle approach models.");
                return;
            }
            _castleRoot = new GameObject("ConnectedCastleApproach").transform;
            _castleRoot.SetParent(_modeledEnvironmentRoot.transform, false);
            _castleRoot.localScale = Vector3.one * CastleScale;
            ConfigureCastle(Instantiate(gate, _castleRoot, false));
            var interior = new Material(Resources.Load<Shader>("HorizonScenery")) { name = "Enclosed hall shadow" };
            interior.SetColor("_BaseColor", new Color(.035f, .045f, .065f));
            interior.SetFloat("_BoardClearance", -.003f);
            interior.SetFloat("_Interior", 1f);
            _materials.Add((interior, false), interior);
            for (int i = 0; i < _hallBays.Length; i++)
            {
                var bay = new GameObject("ConnectedHallBay_" + i).transform;
                bay.SetParent(_castleRoot, false);
                _hallBays[i] = bay;
                ConfigureCastle(Instantiate(hall, bay, false));
                AddHallEnclosure("Continuous hall floor", bay, new Vector3(0f, -.1f, -HallBayLength * .5f),
                    new Vector3(12f, .2f, HallBayLength), interior);
                for (int side = -1; side <= 1; side += 2)
                    AddHallEnclosure("Vault wall shadow seal", bay, new Vector3(side * 5.85f, 4.1f, -HallBayLength * .5f),
                        new Vector3(.5f, .9f, HallBayLength), interior);
                if (lantern != null)
                {
                    _hallLanterns[i] = Instantiate(lantern, bay, false);
                    ConfigureCastle(_hallLanterns[i]);
                }
            }
            // A solid distant closure prevents the outdoor sky from showing
            // through the open portal. It stays beyond the visible hall bays.
            _hallEnd = AddHallEnclosure("Hall darkness beyond vault", _castleRoot, Vector3.zero,
                new Vector3(12f, 12f, .2f), interior);
            _castleRoot.gameObject.SetActive(false);
        }

        private Transform AddHallEnclosure(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var enclosure = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enclosure.name = name;
            enclosure.transform.SetParent(parent, false);
            enclosure.transform.localPosition = position;
            enclosure.transform.localScale = size;
            Destroy(enclosure.GetComponent<Collider>());
            var renderer = enclosure.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -22;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return enclosure.transform;
        }

        private void ConfigureCastle(GameObject model)
        {
            var shader = Resources.Load<Shader>("HorizonScenery");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingOrder = -22;
                var sources = renderer.sharedMaterials;
                var converted = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (!_castleMaterials.TryGetValue(source, out var material))
                    {
                        material = new Material(shader) { name = source.name + " (Castle approach)" };
                        material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color);
                        material.SetFloat("_Wind", source.name.Contains("Banner Teal") || source.name.Contains("Royal Banner") ? 1f : 0f);
                        material.SetFloat("_BoardClearance", -.003f);
                        material.SetFloat("_Glow", source.name.Contains("Lantern Glass") ? .55f : source.name.Contains("Torch Flame") || source.name.Contains("Candle Flame") ? 1f : 0f);
                        material.SetFloat("_Glass", source.name.Contains("Stained Glass") || source.name.Contains("Lantern Glass") ? 1f : 0f);
                        material.SetFloat("_Metal", source.name.Contains("Gilt") || source.name.Contains("Gold") ? 1f : 0f);
                        material.SetFloat("_Interior", source.name.StartsWith("Royal") || source.name.StartsWith("Stained Glass") ? 1f : 0f);
                        _castleMaterials.Add(source, material);
                        _materials.Add((material, false), material);
                    }
                    converted[i] = material;
                }
                renderer.sharedMaterials = converted;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private bool HorizonBoundary(BoardBiome biome, float scroll, out float y)
        {
            return _biomeFlow.Region(biome, scroll, cellSize, GameConfig.Rows * cellSize * .5f, out y, out float max);
        }

        private void SyncHorizon(float scroll, float elapsedSeconds, BoardBiome? biomeOverride)
        {
            if (_castleRoot == null) return;
            var camera = Camera.main;
            if (camera == null) return;
            bool hasBridge = HorizonBoundary(BoardBiome.Drawbridge, scroll, out float bridgeY);
            bool hasStone = HorizonBoundary(BoardBiome.CastleStone, scroll, out float stoneY);
            float depth = 240f;
            if (hasBridge)
            {
                float travel = Mathf.Max(0f, (RollEnd - bridgeY) / cellSize);
                // Keep moving at every distance; the old clamp held the gate still.
                depth = 220f * Mathf.Exp(-travel * .047f);
            }
            if (hasStone)
            {
                float crossing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RollEnd, -1f, stoneY));
                depth = Mathf.Lerp(depth, -16f, crossing) - Mathf.Max(0f, -1f - stoneY) * 1.2f;
            }
            if (biomeOverride.HasValue)
            {
                hasBridge = biomeOverride.Value != BoardBiome.Grass;
                depth = biomeOverride.Value == BoardBiome.Drawbridge ? 220f * Mathf.Exp(-scroll * .03f - _sceneryTime * .0035f) :
                    biomeOverride.Value == BoardBiome.CastleStone ? -16f - scroll * 1.2f - _sceneryTime * .75f : 240f;
            }
            _gateBlend = Mathf.InverseLerp(220f, 52f, depth);
            _hallBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, -6f, depth));
            _castleRoot.gameObject.SetActive(hasBridge && !_legacy2D);
            // Model depth follows the camera's forward axis. The camera actually
            // crosses the open portal; its roof and pillars move past the view.
            // Match the architecture's vanishing point to the rolling crest.
            // A camera-centred hall converges below it, revealing distant flags
            // a strip at a time through the board's architectural cutaway.
            float horizon = RollingCrest(camera) + .012f;
            var approach = camera.ViewportPointToRay(new Vector3(.5f, horizon, 0f)).direction;
            var alignment = Quaternion.FromToRotation(camera.transform.forward, approach);
            var approachUp = alignment * camera.transform.up;
            _castleRoot.rotation = alignment * Quaternion.AngleAxis(90f - CameraTilt, Vector3.right) * HorizonOrientation;
            // At the first bridge boundary the entire finite model is below
            // the crest. Forward travel lifts its towers into view continuously.
            float emergence = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(220f, 80f, depth));
            float burial = Mathf.Lerp(14f, 0f, emergence);
            _castleRoot.position = camera.transform.position + approach * depth - approachUp * (CastleEyeHeight + burial);
            // Recycle only whole bays behind the near plane. Remaining ribs keep
            // their exact positions, including when Chronokinesis reverses scroll.
            float worldBayLength = HallBayLength * CastleScale;
            int firstBay = Mathf.Max(0, Mathf.FloorToInt((-depth - 1.76f) / worldBayLength));
            _hallEnd.localPosition = new Vector3(0f, 5f, -(2.2f + (firstBay + _hallBays.Length) * HallBayLength));
            for (int i = 0; i < _hallBays.Length; i++)
            {
                _hallBays[i].localPosition = Vector3.back * (2.2f + (firstBay + i) * HallBayLength);
                if (_hallLanterns[i] != null) _hallLanterns[i].SetActive(((firstBay + i) & 1) == 0);
            }
        }

        // Active-play clock keeps clouds and static adventure scenery moving.
        public void TickScenery(float dt) => _sceneryTime += Mathf.Clamp(dt, 0f, .1f);

        private void FitBoardWidth()
        {
            if (_frameRoot != null) _frameRoot.localScale = new Vector3(cellW, cellSize, cellSize);
            if (_drumRoot != null) _drumRoot.localScale = new Vector3(cellW / cellSize, 1f, 1f);
            foreach (var material in _materials.Values)
                if (material.HasProperty("_BoardHalfSize"))
                    material.SetVector("_BoardHalfSize", new Vector4(GameConfig.Cols * cellW * .5f, GameConfig.Rows * cellSize * .5f, Mathf.Tan(CameraTilt * Mathf.Deg2Rad), 0f));
        }

        private float RollingCrest(Camera camera)
        {
            float crest = 0f;
            for (int i = 0; i <= 24; i++)
            {
                float angle = i / 24f * Mathf.Clamp((_surfaceEnd - _surfaceStart) / RollRadius, 0f, Mathf.PI * .5f);
                var point = new Vector3(0f, _surfaceStart + RollRadius * Mathf.Sin(angle), RollRadius * (1f - Mathf.Cos(angle)));
                crest = Mathf.Max(crest, camera.WorldToViewportPoint(point).y);
            }
            return crest;
        }

        private void UpdateSkyProjection()
        {
            if (_legacy2D) return;
            var camera = Camera.main;
            if (camera == null) return;
            float crest = RollingCrest(camera);
            Shader.SetGlobalVector("_KGHorizon", new Vector4(crest, _gateBlend, _hallBlend, 0f));
            bool reduced = SaveData.IsReducedEffects();
            Shader.SetGlobalVector("_KGScenery", new Vector4(reduced ? 0f : _sceneryTime, reduced ? 0f : 1f, _hallBlend, 0f));
            Shader.SetGlobalVector("_KGSceneryForward", camera.transform.forward);
            Shader.SetGlobalVector("_KGSceneryRight", camera.transform.right);
            Shader.SetGlobalVector("_KGSceneryUp", camera.transform.up);
        }

        // Return a point in the original simulation plane. Only the front-facing
        // part of the cylinder accepts taps; the hidden return is never playable.
        private Vector3 UnrollPointer(Ray ray, Vector3 flat)
        {
            if (flat.y <= _surfaceStart) return flat;
            float oy = ray.origin.y - _surfaceStart, oz = ray.origin.z - RollRadius;
            float a = ray.direction.y * ray.direction.y + ray.direction.z * ray.direction.z;
            float b = 2f * (oy * ray.direction.y + oz * ray.direction.z);
            float overlayRadius = RollRadius - SurfaceOverlayZ;
            float c = oy * oy + oz * oz - overlayRadius * overlayRadius;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f || a < .00001f) return new Vector3(flat.x, 1000f, 0f);
            float distance = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            if (distance < 0f) return new Vector3(flat.x, 1000f, 0f);
            var hit = ray.GetPoint(distance);
            float angle = Mathf.Atan2(hit.y - _surfaceStart, RollRadius - hit.z);
            if (angle < 0f) return flat;
            return new Vector3(hit.x, _surfaceStart + angle * RollRadius, SurfaceOverlayZ);
        }
    }
}
