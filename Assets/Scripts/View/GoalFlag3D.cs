using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KnightsGauntlet.View
{
    // A small runtime-built low-poly flag for Adventure goals. The banner is a
    // real mesh with depth, shaded color panels, and a restrained cloth wave.
    public sealed class GoalFlag3D : MonoBehaviour
    {
        private const int Columns = 5;
        private const int Rows = 3;
        private const float Left = 0f;
        private const float Width = 0.50f;
        private const float Top = 1.12f;
        private const float Bottom = 0.69f;

        private static Material _ink, _gold, _brightRed, _red, _shadowRed;
        private static readonly Color InkColor = Hex("#0A1732");
        private static readonly Color GoldColor = Hex("#DDBA68");
        private static readonly Color BrightRedColor = Hex("#305798");
        private static readonly Color RedColor = Hex("#172F65");
        private static readonly Color ShadowRedColor = Hex("#0B193D");

        private Mesh _clothMesh, _outlineMesh;
        private Vector3[] _clothVertices, _outlineVertices;
        private Transform _clothRoot, _outlineRoot;
        private int _sortingOrder;
        private float _startTime;

        public void Initialize(int sortingOrder)
        {
            _sortingOrder = sortingOrder;
            _startTime = Time.unscaledTime;
            EnsureMaterials();
            BuildPole();
            BuildBanner();
        }

        public void SetCellSize(float cellSize)
        {
            transform.localScale = Vector3.one * (cellSize * 1.08f);
            transform.localRotation = BoardView.SurfaceOrientation;
        }

        private void Update()
        {
            if (_clothMesh == null || _outlineMesh == null) return;
            float time = Time.unscaledTime - _startTime;
            UpdateVertices(_clothVertices, time, 0f);
            UpdateVertices(_outlineVertices, time, 1f);
            _clothMesh.vertices = _clothVertices;
            _outlineMesh.vertices = _outlineVertices;
        }

        private void BuildPole()
        {
            CreatePrimitive("FlagPole", PrimitiveType.Cylinder, _gold,
                new Vector3(Left, 0.62f, 0.035f), new Vector3(0.035f, 0.62f, 0.035f));
            CreatePrimitive("FlagFoot", PrimitiveType.Cylinder, _gold,
                new Vector3(Left, 0.035f, 0f), new Vector3(0.78f, 0.035f, 0.78f));
            CreatePrimitive("FlagPlinth", PrimitiveType.Cylinder, _ink,
                new Vector3(Left, 0.078f, 0f), new Vector3(0.70f, 0.022f, 0.70f));
            CreatePrimitive("FlagPedestalBand", PrimitiveType.Cylinder, _gold,
                new Vector3(Left, 0.102f, 0f), new Vector3(0.61f, 0.008f, 0.61f));
            CreatePrimitive("FlagPedestalCap", PrimitiveType.Cylinder, _ink,
                new Vector3(Left, 0.114f, 0f), new Vector3(0.56f, 0.008f, 0.56f));
            CreatePrimitive("FlagFinial", PrimitiveType.Sphere, _gold,
                new Vector3(Left, 1.24f, 0.025f), Vector3.one * 0.075f);
            CreatePrimitive("FlagCollar", PrimitiveType.Cylinder, _gold,
                new Vector3(Left, 1.16f, 0.025f), new Vector3(0.047f, 0.018f, 0.047f));
            CreatePrimitive("FlagBaseBand", PrimitiveType.Cylinder, _gold,
                new Vector3(Left, 0.09f, 0.025f), new Vector3(0.065f, 0.012f, 0.065f));
        }

        private void BuildBanner()
        {
            _outlineRoot = new GameObject("FlagInkOutline").transform;
            _outlineRoot.SetParent(transform, false);
            _outlineRoot.localPosition = new Vector3(0f, 0f, 0.012f);
            _outlineRoot.localScale = new Vector3(1.065f, 1.075f, 1f);
            _outlineMesh = CreateClothMesh("FlagInkMesh", out _outlineVertices);
            var outlineFilter = _outlineRoot.gameObject.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = _outlineMesh;
            SetRenderer(outlineFilter.gameObject.AddComponent<MeshRenderer>(), new[] { _ink });

            _clothRoot = new GameObject("FlagCloth").transform;
            _clothRoot.SetParent(transform, false);
            _clothRoot.localPosition = new Vector3(0f, 0f, -0.006f);
            _clothMesh = CreateClothMesh(out _clothVertices);
            var clothFilter = _clothRoot.gameObject.AddComponent<MeshFilter>();
            clothFilter.sharedMesh = _clothMesh;
            SetRenderer(clothFilter.gameObject.AddComponent<MeshRenderer>(),
                new[] { _gold, _brightRed, _red, _shadowRed });
            var seal = CreatePrimitive("RoyalBannerSeal", PrimitiveType.Cube, _gold,
                new Vector3(Width * .43f, (Top + Bottom) * .5f, -.055f), new Vector3(.085f,.085f,.012f));
            seal.transform.localRotation = Quaternion.Euler(0f,0f,45f);
        }

        private Mesh CreateClothMesh(string meshName, out Vector3[] vertices)
        {
            var triangles = new List<int>();
            var mesh = new Mesh { name = meshName };
            vertices = MakeVertices(0f);
            for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
            {
                int a = row * (Columns + 1) + col;
                int b = a + 1;
                int c = a + Columns + 1;
                int d = c + 1;
                AddQuad(triangles, a, b, c, d);
            }
            mesh.vertices = vertices;
            SetClothNormals(mesh, vertices.Length);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(triangles, 0);
            mesh.MarkDynamic();
            mesh.bounds = new Bounds(new Vector3(.26f,.90f,0f), new Vector3(.60f,.55f,.10f));
            return mesh;
        }

        private Mesh CreateClothMesh(out Vector3[] vertices)
        {
            var mesh = new Mesh { name = "GoalFlagClothMesh" };
            vertices = MakeVertices(0f);
            var submeshes = new List<int>[4];
            for (int i = 0; i < submeshes.Length; i++) submeshes[i] = new List<int>();

            for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
            {
                int material = row == Rows - 1 ? 0 : col == 0 ? 1 : col == 2 ? 3 : 2;
                int a = row * (Columns + 1) + col;
                int b = a + 1;
                int c = a + Columns + 1;
                int d = c + 1;
                AddQuad(submeshes[material], a, b, c, d);
            }

            mesh.vertices = vertices;
            SetClothNormals(mesh, vertices.Length);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.MarkDynamic();
            mesh.bounds = new Bounds(new Vector3(.26f,.90f,0f), new Vector3(.60f,.55f,.10f));
            return mesh;
        }

        private Vector3[] MakeVertices(float time)
        {
            var vertices = new Vector3[(Columns + 1) * (Rows + 1)];
            UpdateVertices(vertices, time, 0f);
            return vertices;
        }

        private static void SetClothNormals(Mesh mesh, int count)
        {
            var normals = new Vector3[count];
            for (int i = 0; i < count; i++) normals[i] = Vector3.back;
            mesh.normals = normals;
        }

        private void UpdateVertices(Vector3[] vertices, float time, float expand)
        {
            float width = Width + expand * 0.018f;
            float height = Top - Bottom + expand * 0.018f;
            for (int row = 0; row <= Rows; row++)
            for (int col = 0; col <= Columns; col++)
            {
                float u = col / (float)Columns;
                float v = row / (float)Rows;
                float tailCut = col == Columns && row % 2 == 1 ? 0.88f : 1f;
                float x = Left + width * u * tailCut;
                float wave = Mathf.Sin(time * 4.2f - u * 5.3f) * u * u;
                float y = Top + expand * 0.009f - height * v + wave * 0.018f;
                float z = Mathf.Sin(time * 4.2f - u * 5.3f) * u * 0.035f;
                vertices[row * (Columns + 1) + col] = new Vector3(x, y, z);
            }
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            // Main face points toward the camera at negative Z; reverse winding also
            // keeps the banner visible as it ripples through its slight depth wave.
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(b); triangles.Add(d); triangles.Add(c);
            triangles.Add(c); triangles.Add(b); triangles.Add(a);
            triangles.Add(c); triangles.Add(d); triangles.Add(b);
        }

        private GameObject CreatePrimitive(string name, PrimitiveType type, Material material,
            Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            SetRenderer(go.GetComponent<MeshRenderer>(), new[] { material });
            return go;
        }

        private void SetRenderer(MeshRenderer renderer, Material[] materials)
        {
            renderer.sharedMaterials = materials;
            // Shader deformation happens after culling; reserve the rolled path.
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 60f);
            renderer.sortingOrder = _sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void EnsureMaterials()
        {
            if (_ink != null) return;
            _ink = CreateMaterial("Goal Flag Ink", InkColor);
            _gold = CreateMaterial("Goal Flag Gold", GoldColor);
            _brightRed = CreateMaterial("Goal Flag Highlight", BrightRedColor);
            _red = CreateMaterial("Goal Flag Red", RedColor);
            _shadowRed = CreateMaterial("Goal Flag Shadow", ShadowRedColor);
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            var shader = Resources.Load<Shader>("Pieces/ChessPiece") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var material = new Material(shader) { name = materialName, color = color };
            material.SetFloat("_PieceClip", 1f);
            return material;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var color);
            return color;
        }

        private void OnDestroy()
        {
            if (_clothMesh != null) Destroy(_clothMesh);
            if (_outlineMesh != null) Destroy(_outlineMesh);
        }
    }
}
