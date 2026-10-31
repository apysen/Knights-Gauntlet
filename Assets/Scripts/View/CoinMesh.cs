using System.Collections.Generic;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // The 3D shilling: a revolved coin with a recessed field, raised rim and bevelled
    // edge, so the key light picks out its relief. Built once in code and shared by
    // the board pickups, their collect bursts and the menu wallet icon.
    //
    // The coin's axis is local +Y; it is Diameter units across at scale 1.
    public static class CoinMesh
    {
        public const float Diameter = 1.8f;
        private const int Segments = 40;

        // Profile from the face centre out over the rim, down the edge and back in
        // under the other face, as (radius, height). Each pair is one hard-edged band.
        private static readonly Vector2[] Profile =
        {
            new Vector2(0f, .050f), new Vector2(.62f, .050f),   // recessed field
            new Vector2(.66f, .085f), new Vector2(.84f, .085f), // raised rim
            new Vector2(.90f, .055f),                           // outer bevel
            new Vector2(.90f, -.055f),                          // edge
            new Vector2(.84f, -.085f), new Vector2(.66f, -.085f),
            new Vector2(.62f, -.050f), new Vector2(0f, -.050f),
        };

        private static Mesh _mesh;
        private static Material _boardMaterial, _iconMaterial;

        public static Mesh Get()
        {
            if (_mesh != null) return _mesh;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int s = 0; s < Profile.Length - 1; s++)
            {
                Vector2 a = Profile[s], b = Profile[s + 1];
                Vector2 tangent = (b - a).normalized;
                Vector2 n = new Vector2(-tangent.y, tangent.x); // outward in the (radius, height) plane
                int start = vertices.Count;
                for (int i = 0; i <= Segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / Segments;
                    float c = Mathf.Cos(angle), sn = Mathf.Sin(angle);
                    vertices.Add(new Vector3(a.x * c, a.y, a.x * sn));
                    vertices.Add(new Vector3(b.x * c, b.y, b.x * sn));
                    var normal = new Vector3(n.x * c, n.y, n.x * sn);
                    normals.Add(normal); normals.Add(normal);
                }
                for (int i = 0; i < Segments; i++)
                {
                    int v0 = start + i * 2, v1 = v0 + 1, v2 = v0 + 2, v3 = v0 + 3;
                    AddFacing(vertices, normals, triangles, v0, v1, v2);
                    AddFacing(vertices, normals, triangles, v2, v1, v3);
                }
            }
            _mesh = new Mesh { name = "Shilling" };
            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            return _mesh;
        }

        // Keep winding consistent with the band's outward normal (Unity front faces
        // have Cross(b - a, c - a) pointing out), whichever way the profile turns.
        private static void AddFacing(List<Vector3> v, List<Vector3> n, List<int> t, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (face.sqrMagnitude < 1e-10f) return; // collapsed at the centre
            if (Vector3.Dot(face, n[a] + n[b] + n[c]) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }

        // Board coins follow the rolling conveyor and the board edge clip like the
        // chess pieces; the menu icon has no board, so it skips both.
        public static Material Material(bool onBoard)
        {
            ref Material cached = ref onBoard ? ref _boardMaterial : ref _iconMaterial;
            if (cached != null) return cached;
            var shader = Resources.Load<Shader>("Pieces/ChessPiece");
            if (shader == null) return null;
            cached = new Material(shader) { name = onBoard ? "Shilling (board)" : "Shilling (icon)" };
            cached.SetColor("_BaseColor", new Color(1f, .80f, .34f, 1f));
            cached.SetFloat("_Smoothness", .9f);
            cached.SetFloat("_PieceClip", onBoard ? 1f : 0f);
            return cached;
        }

        public static MeshRenderer Create(string name, Transform parent, bool onBoard)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = Get();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(onBoard);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // The vertex shader rolls board coins over the crest, outside their own bounds.
            if (onBoard) renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 60f);
            return renderer;
        }
    }
}
