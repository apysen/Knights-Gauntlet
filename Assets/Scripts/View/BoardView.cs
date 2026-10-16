using System.Collections.Generic;
using UnityEngine;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    public enum BoardBiome { Grass, Drawbridge, CastleStone }

    // Simulation stays in XY; the physical board lies in XY with height toward -Z.
    // One shared orientation stands imported Y-up models on that board.
    public partial class BoardView : MonoBehaviour
    {
        public static readonly Quaternion SurfaceOrientation = Quaternion.Euler(-90f, 0f, 0f);
        public const float SurfaceOverlayZ = -0.018f;
        public const float CameraTilt = 34f;
        public float cellSize = 1f;
        public float cellW = 1f;
        public Color WashColor = Color.clear;
        public float WashStrength;
        public Color cameraBackground = new Color32(0x17, 0x22, 0x28, 255);
        private Color _tileColorA = new Color32(0xF3, 0xE9, 0xD8, 255);
        private Color _tileColorB = new Color32(0xDE, 0xD2, 0xBB, 255);
        private static readonly Color LegacyTileColorA = new Color32(0x2D, 0x2D, 0x4E, 255);
        private static readonly Color LegacyTileColorB = new Color32(0x1E, 0x1E, 0x35, 255);
        private bool _legacy2D, _lastCameraLegacy2D;
        private GameTheme _theme;
        private GameObject _modeledTilesRoot, _modeledEnvironmentRoot, _legacyTilesRoot;

        private sealed class Tile
        {
            public Transform root;
            public SpriteRenderer legacyRenderer;
            public readonly GameObject[] variants = new GameObject[3];
            public readonly Renderer[][] renderers = new Renderer[3][];
            public GameObject rail;
            public int biome = -1;
            public Color tint;
        }
        private Tile[] _tiles;
        private readonly Dictionary<(Material, bool), Material> _materials = new Dictionary<(Material, bool), Material>();
        private MaterialPropertyBlock _properties;
        private MaterialPropertyBlock _sceneryProperties;
        private readonly BoardBiomeFlow _biomeFlow = new BoardBiomeFlow();
        private readonly Renderer[][] _sceneryRenderers = new Renderer[3][];
        private readonly GameObject[] _assets = new GameObject[3];
        private GameObject _railAsset;
        private readonly GameObject[] _surrounds = new GameObject[3];
        private int _surroundBiome = -1;
        public BoardBiome CurrentBiome => (BoardBiome)Mathf.Max(0, _surroundBiome);
        private readonly Vector4[] _shadows = new Vector4[24];
        private int _lastCamW, _lastCamH;
        private float _lastInset = -1f, _lastBottomInset = -1f;
        private static Sprite _unitSquare;

        public void Init(GameTheme theme)
        {
            _theme = theme;
            if (theme != null)
            {
                _tileColorA = theme.cellColorA;
                _tileColorB = theme.cellColorB;
            }
            _properties = new MaterialPropertyBlock();
            _sceneryProperties = new MaterialPropertyBlock();
            _legacy2D = theme != null && !theme.comicStyle;
            _modeledEnvironmentRoot = new GameObject("ModeledEnvironment");
            _modeledEnvironmentRoot.transform.SetParent(transform, false);
            _assets[0] = Resources.Load<GameObject>("Horizon/RollingGrassTile");
            _assets[1] = Resources.Load<GameObject>("Horizon/RollingWoodTile");
            _assets[2] = Resources.Load<GameObject>("Horizon/RollingStoneTile");
            _railAsset = Resources.Load<GameObject>("Horizon/RollingBridgeRail");
            BuildTiles();
            BuildFrame();
            BuildHorizon();
            _modeledTilesRoot.SetActive(!_legacy2D);
            _modeledEnvironmentRoot.SetActive(!_legacy2D);
            _legacyTilesRoot.SetActive(_legacy2D);
            SyncBackground(0f);
        }

        public void SetLegacy2D(bool enabled)
        {
            if (_legacy2D == enabled) return;
            _legacy2D = enabled;
            if (_theme != null)
            {
                _tileColorA = _theme.cellColorA;
                _tileColorB = _theme.cellColorB;
            }
            if (_modeledTilesRoot != null) _modeledTilesRoot.SetActive(!enabled);
            if (_modeledEnvironmentRoot != null) _modeledEnvironmentRoot.SetActive(!enabled);
            if (_legacyTilesRoot != null) _legacyTilesRoot.SetActive(enabled);
        }

        public Vector3 CellToWorld(int col, float simY)
        {
            float x = (col + 0.5f) * cellW - GameConfig.Cols * cellW * 0.5f;
            float y = GameConfig.Rows * 0.5f * cellSize - (simY + 0.5f) * cellSize;
            return new Vector3(x, y, _legacy2D ? 0f : SurfaceOverlayZ);
        }

        public (int col, float simY) WorldToCell(Vector3 world)
        {
            int col = Mathf.FloorToInt((world.x + GameConfig.Cols * cellW * 0.5f) / cellW);
            float simY = (GameConfig.Rows * 0.5f * cellSize - world.y) / cellSize - 0.5f;
            return (col, simY);
        }

        // Intersect the tilted camera ray with the board, including tutorial/tall-map taps.
        public Vector3 ScreenToWorld(Camera cam, Vector2 screenPosition)
        {
            Ray ray = cam.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, SurfaceOverlayZ));
            Vector3 flat = plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
            return _legacy2D ? flat : UnrollPointer(ray, flat);
        }

        public Vector3 CellScale(float mult = 1f) => new Vector3(cellW * mult, cellSize * mult, 1f);

        public void FitCamera(Camera cam, float topInsetFrac = 0f, float bottomInsetFrac = 0f)
        {
            if (cam == null) return;
            if (Screen.width == _lastCamW && Screen.height == _lastCamH &&
                Mathf.Approximately(topInsetFrac, _lastInset) && Mathf.Approximately(bottomInsetFrac, _lastBottomInset) &&
                _lastCameraLegacy2D == _legacy2D) return;
            _lastCamW = Screen.width; _lastCamH = Screen.height;
            _lastInset = topInsetFrac; _lastBottomInset = bottomInsetFrac;
            _lastCameraLegacy2D = _legacy2D;
            cellW = cellSize;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float top = Mathf.Clamp(topInsetFrac, 0f, 0.45f);
            float bottom = Mathf.Clamp(bottomInsetFrac, 0f, 0.45f);
            if (_legacy2D)
            {
                float safeHeight = Mathf.Max(0.1f, 1f - top - bottom);
                float size = GameConfig.Rows * cellSize * .5f / safeHeight;
                cam.orthographic = true;
                cam.orthographicSize = size;
                cellW = 2f * size * aspect / GameConfig.Cols;
                cam.transform.rotation = Quaternion.identity;
                cam.transform.position = new Vector3(0f, (top - bottom) * cam.orthographicSize, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x1A, 0x1A, 0x2E, 255);
                Shader.SetGlobalVector("_KGBoardClip", Vector4.zero);
                Shader.SetGlobalVector("_KGBoardRoll", Vector4.zero);
                return;
            }
            Shader.SetGlobalVector("_KGBoardClip", new Vector4(GameConfig.Cols * cellW * .5f,
                GameConfig.Rows * cellSize * .5f, Mathf.Tan(CameraTilt * Mathf.Deg2Rad), 1));
            Shader.SetGlobalVector("_KGBoardRoll", new Vector4(RollStart, RollRadius, RollEnd, 1));
            float tilt = CameraTilt * Mathf.Deg2Rad;
            float halfLength = GameConfig.Rows * cellSize * 0.5f;
            float min = -(halfLength + .2f) * Mathf.Cos(tilt) - 0.20f * cellSize * Mathf.Sin(tilt);
            float max = 6.5f * cellSize; // A compact horizon leaves more space for play.
            const float cameraDistance = 48f;
            float nearDepth = cameraDistance - (halfLength + .2f) * Mathf.Sin(tilt);
            float low = (-1f + 2f * bottom) * nearDepth / cameraDistance;
            float high = (1f - 2f * top) * (cameraDistance + 4f) / cameraDistance;
            float sizeForHeight = (max - min) / Mathf.Max(.1f, high - low);
            cam.orthographic = false;
            cam.orthographicSize = sizeForHeight;
            // Fit tiles close to the screen edges at the near end. Pieces retain
            // their own proportions while the surface fills the available width.
            cellW = Mathf.Clamp(2f * sizeForHeight * aspect * nearDepth / cameraDistance / (GameConfig.Cols + .08f), cellSize * .65f, cellSize * 2.2f);
            Shader.SetGlobalVector("_KGBoardClip", new Vector4(GameConfig.Cols * cellW * .5f,
                GameConfig.Rows * cellSize * .5f, Mathf.Tan(CameraTilt * Mathf.Deg2Rad), 1));
            FitBoardWidth();
            cam.fieldOfView = 2f * Mathf.Atan(cam.orthographicSize / cameraDistance) * Mathf.Rad2Deg;
            cam.transform.rotation = Quaternion.Euler(-CameraTilt, 0f, 0f);
            float projectedCenter = min - cam.orthographicSize * low;
            cam.transform.position = cam.transform.up * projectedCenter - cam.transform.forward * cameraDistance;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cameraBackground;
        }

        private void BuildTiles()
        {
            _tiles = new Tile[(GameConfig.Rows + 6) * GameConfig.Cols];
            _modeledTilesRoot = new GameObject("ModeledTiles");
            _modeledTilesRoot.transform.SetParent(transform, false);
            _legacyTilesRoot = new GameObject("Legacy2DTiles");
            _legacyTilesRoot.transform.SetParent(transform, false);
            for (int row = 0, i = 0; row < GameConfig.Rows + 6; row++)
            for (int col = 0; col < GameConfig.Cols; col++, i++)
            {
                var tile = new Tile { root = new GameObject("Tile_" + row + "_" + col).transform };
                tile.root.SetParent(_modeledTilesRoot.transform, false);
                tile.root.localRotation = HorizonOrientation;
                var flat = new GameObject("Cell_" + row + "_" + col);
                flat.transform.SetParent(_legacyTilesRoot.transform, false);
                tile.legacyRenderer = flat.AddComponent<SpriteRenderer>();
                tile.legacyRenderer.sprite = UnitSquare();
                tile.legacyRenderer.sortingOrder = -20;
                flat.transform.localScale = CellScale();
                for (int biome = 0; biome < 3; biome++)
                {
                    GameObject model;
                    if (_assets[biome] != null) model = Instantiate(_assets[biome], tile.root, false);
                    else
                    {
                        model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        model.transform.SetParent(tile.root, false);
                        model.transform.localPosition = new Vector3(0f, -0.08f, 0f);
                        model.transform.localScale = new Vector3(0.97f, 0.16f, 0.97f);
                        Destroy(model.GetComponent<Collider>());
                        Debug.LogWarning("[Knight's Gauntlet] Missing 3D board model for biome " + biome);
                    }
                    model.name = ((BoardBiome)biome).ToString();
                    tile.variants[biome] = model;
                    tile.renderers[biome] = model.GetComponentsInChildren<Renderer>(true);
                    ConfigureModelRenderers(tile.renderers[biome]);
                    model.SetActive(false);
                }
                if ((col == 0 || col == GameConfig.Cols - 1) && _railAsset != null)
                {
                    tile.rail = Instantiate(_railAsset, tile.root, false);
                    tile.rail.transform.localPosition = new Vector3(col == 0 ? 0.47f : -0.47f, 0f, 0f);
                    ConfigureModelRenderers(tile.rail.GetComponentsInChildren<Renderer>(true));
                    tile.rail.SetActive(false);
                }
                _tiles[i] = tile;
            }
        }

        private void BuildFrame()
        {
            var asset = Resources.Load<GameObject>("Horizon/OpenRollingFrame");
            if (asset != null)
            {
                var root = new GameObject("BoardFrame").transform;
                _frameRoot = root;
                root.SetParent(_modeledEnvironmentRoot.transform, false);
                root.localRotation = HorizonOrientation;
                root.localScale = Vector3.one * cellSize;
                var frame = Instantiate(asset, root, false);
                ConfigureModelRenderers(frame.GetComponentsInChildren<Renderer>(true), -21, false);
                SetRollingMaterials(frame.GetComponentsInChildren<Renderer>(true));
                _levelTopBorder = new GameObject("AdventureTopBorder").transform;
                _levelTopBorder.SetParent(_modeledEnvironmentRoot.transform, false);
                var borderMaterials = frame.GetComponentsInChildren<Renderer>(true)[0].sharedMaterials;
                for (int trim = 0; trim < 2; trim++)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = trim == 0 ? "Top rail" : "Top brass inlay";
                    bar.transform.SetParent(_levelTopBorder, false);
                    bar.transform.localPosition = new Vector3(0f, 0f, trim == 0 ? .045f : -.047f);
                    bar.transform.localScale = trim == 0 ? new Vector3(5.12f, .18f, .17f) : new Vector3(5.1f, .025f, .016f);
                    Destroy(bar.GetComponent<Collider>());
                    var renderer = bar.GetComponent<Renderer>();
                    renderer.sharedMaterial = borderMaterials[Mathf.Min(trim, borderMaterials.Length - 1)];
                    renderer.sortingOrder = -21;
                    SetRollingMaterials(new[] { renderer });
                }
                _levelTopBorder.gameObject.SetActive(false);
            }
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "TabletopBackdrop";
            backdrop.transform.SetParent(_modeledEnvironmentRoot.transform, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, 12f);
            backdrop.transform.localScale = new Vector3(100f, 100f, 1f);
            Destroy(backdrop.GetComponent<Collider>());
            var material = new Material(Resources.Load<Shader>("HorizonSky"));
            backdrop.GetComponent<Renderer>().sharedMaterial = material;
            backdrop.GetComponent<Renderer>().sortingOrder = -30;
            _materials.Add((material, false), material);
        }

        private void BuildSurrounds()
        {
            string[] names = { "GardenSurround", "BridgeSurround", "CastleSurround" };
            for (int i = 0; i < names.Length; i++)
            {
                var asset = Resources.Load<GameObject>("BoardModels/" + names[i]);
                if (asset == null) continue;
                var root = new GameObject(names[i]).transform;
                root.SetParent(_modeledEnvironmentRoot.transform, false);
                root.localRotation = SurfaceOrientation;
                root.localScale = Vector3.one * cellSize;
                var model = Instantiate(asset, root, false);
                _sceneryRenderers[i] = model.GetComponentsInChildren<Renderer>(true);
                ConfigureModelRenderers(_sceneryRenderers[i], -19, false);
                _surrounds[i] = root.gameObject;
                root.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            UpdateSkyProjection();
            int count = PieceView.WriteBoardShadows(_shadows);
            Shader.SetGlobalInt("_RoyalShadowCount", count);
            Shader.SetGlobalVectorArray("_RoyalShadows", _shadows);
        }

        private void OnDisable()
        {
            Shader.SetGlobalInt("_RoyalShadowCount", 0);
            Shader.SetGlobalVector("_KGBoardRoll", Vector4.zero);
        }

        public void ConfigureModelRenderers(Renderer[] renderers, int sortingOrder = -20, bool clipToBoard = true)
        {
            var shader = Resources.Load<Shader>("Pieces/ChessPiece");
            foreach (var renderer in renderers)
            {
                renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 60f);
                renderer.sortingOrder = sortingOrder;
                var sources = renderer.sharedMaterials;
                var converted = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (!_materials.TryGetValue((source, clipToBoard), out var material))
                    {
                        material = new Material(shader) { name = source.name + " (Board)" };
                        material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color);
                        material.SetFloat("_ClipBoard", clipToBoard ? 1f : 0f);
                        material.SetFloat("_RollBoard", clipToBoard ? 1f : 0f);
                        string name = source.name.ToLowerInvariant();
                        material.SetFloat("_Smoothness", name.Contains("gold") || name.Contains("bronze") ? .8f :
                            name.Contains("enamel") ? .65f : .28f);
                        material.SetVector("_BoardHalfSize", new Vector4(GameConfig.Cols * cellW * 0.5f,
                            GameConfig.Rows * cellSize * 0.5f, Mathf.Tan(CameraTilt * Mathf.Deg2Rad), 0f));
                        _materials.Add((source, clipToBoard), material);
                    }
                    converted[i] = material;
                }
                renderer.sharedMaterials = converted;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void ResetBackground()
        {
            _levelFirstRow = null;
            _biomeFlow.Reset();
            _sceneryTime = 0f;
        }

        public void SyncBackground(float scrollCells, float elapsedSeconds = 0f, BoardBiome? biomeOverride = null)
        {
            if (_tiles == null) return;
            SyncLevelEnd(scrollCells);
            if (!biomeOverride.HasValue) _biomeFlow.Advance(scrollCells, elapsedSeconds);
            int baseRow = Mathf.FloorToInt(scrollCells);
            SyncHorizon(scrollCells, elapsedSeconds, biomeOverride);
            _surroundBiome = (int)(biomeOverride ?? _biomeFlow.AtRow(GameConfig.Rows / 2 - baseRow));
            for (int i = 0; i < _surrounds.Length; i++)
            {
                if (_surrounds[i] == null) continue;
                float min = -10000f, max = 10000f;
                bool visible = biomeOverride.HasValue ? (int)biomeOverride.Value == i :
                    _biomeFlow.Region((BoardBiome)i, scrollCells, cellSize, GameConfig.Rows * cellSize * .5f, out min, out max);
                visible &= min < 5.5f * cellSize && max > -5.5f * cellSize;
                _surrounds[i].SetActive(visible);
                if (!visible) continue;
                _sceneryProperties.Clear();
                _sceneryProperties.SetFloat("_RegionClip", 1f);
                _sceneryProperties.SetFloat("_RollBoard", 1f);
                _sceneryProperties.SetVector("_RegionY", new Vector4(min, max, 0f, 0f));
                foreach (var renderer in _sceneryRenderers[i]) renderer.SetPropertyBlock(_sceneryProperties);
            }
            float fraction = scrollCells - baseRow;
            for (int rr = 0, i = 0; rr < GameConfig.Rows + 6; rr++)
            for (int col = 0; col < GameConfig.Cols; col++, i++)
            {
                int row = rr - 5;
                int biome = (int)(biomeOverride ?? _biomeFlow.AtRow(row - baseRow));
                var tile = _tiles[i];
                bool appearanceChanged = tile.biome != biome;
                var position = CellToWorld(col, row + fraction);
                position.z = 0f; // Imported top surface is exactly zero; overlays sit above it.
                tile.root.position = position;
                tile.root.localScale = new Vector3(cellW, cellSize, cellSize);
                var flatPosition = CellToWorld(col, row + fraction);
                tile.legacyRenderer.gameObject.SetActive(row >= -1);
                flatPosition.z = 0f;
                tile.legacyRenderer.transform.position = flatPosition;
                tile.legacyRenderer.transform.localScale = CellScale();
                bool alternate = ((baseRow + row + col) & 1) != 0;
                Color legacyTint = alternate ? LegacyTileColorB : LegacyTileColorA;
                if (WashStrength > 0.001f) legacyTint = Color.Lerp(legacyTint, WashColor, WashStrength);
                tile.legacyRenderer.color = legacyTint;
                if (tile.biome != biome)
                {
                    if (tile.biome >= 0) tile.variants[tile.biome].SetActive(false);
                    tile.variants[biome].SetActive(true);
                    if (tile.rail != null) tile.rail.SetActive(biome == (int)BoardBiome.Drawbridge);
                    tile.biome = biome;
                }
                // Apply the theme's quiet board palette to the modeled tiles too.
                // The serialized colors previously only described the flat-board fallback,
                // leaving the production models with a much harsher checker contrast.
                Color tint = alternate ? _tileColorB : _tileColorA;
                if (WashStrength > 0.001f) tint = Color.Lerp(tint, WashColor, WashStrength * 0.16f);
                if (!appearanceChanged && tile.tint == tint) continue;
                tile.tint = tint;
                _properties.Clear();
                _properties.SetColor("_BoardTint", tint);
                foreach (var renderer in tile.renderers[biome]) renderer.SetPropertyBlock(_properties);
            }
        }

        public static Sprite UnitSquare()
        {
            if (_unitSquare != null) return _unitSquare;
            var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels); tex.Apply();
            _unitSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _unitSquare;
        }

        private void OnDestroy()
        {
            foreach (var material in _materials.Values)
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
