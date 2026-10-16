#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

// Measures imported, optimized meshes after FBX axis conversion. Regenerate after
// replacing runtime FBXs; these measurements deliberately avoid Blender axis guesses.
public static class PieceModelMetricsBuilder
{
    [Serializable] private class MetricsFile { public List<Metrics> items = new List<Metrics>(); }
    [Serializable] private class Metrics
    {
        public string name;
        public float height, diameter, centerX, centerZ, bottom;
    }

    [MenuItem("Tools/Knight's Gauntlet/Rebuild Piece Model Metrics")]
    public static void Build()
    {
        var result = new MetricsFile();
        foreach (string name in new[] { "knight", "rook", "bishop", "queen", "pawn" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Pieces/Optimized/" + name + ".fbx");
            if (asset == null) throw new InvalidOperationException("Missing optimized model: " + name);
            var instance = UnityEngine.Object.Instantiate(asset);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var vertices = new List<Vector3>();
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                using (var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
                {
                    var buffer = new NativeArray<Vector3>(filter.sharedMesh.vertexCount, Allocator.Temp);
                    try
                    {
                        data[0].GetVertices(buffer);
                        foreach (var vertex in buffer) vertices.Add(filter.transform.TransformPoint(vertex));
                    }
                    finally { buffer.Dispose(); }
                }
                if (vertices.Count == 0) throw new InvalidOperationException("Empty model: " + name);
                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (var vertex in vertices) bounds.Encapsulate(vertex);
                var pedestal = new Bounds();
                bool first = true;
                foreach (var vertex in vertices)
                {
                    if (vertex.y > bounds.min.y + bounds.size.y * .1f) continue;
                    if (first) { pedestal = new Bounds(vertex, Vector3.zero); first = false; }
                    else pedestal.Encapsulate(vertex);
                }
                result.items.Add(new Metrics { name = name, height = bounds.size.y,
                    diameter = Mathf.Max(pedestal.size.x, pedestal.size.z),
                    centerX = pedestal.center.x, centerZ = pedestal.center.z, bottom = bounds.min.y });
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        const string path = "Assets/Resources/Pieces/model-metrics.json";
        File.WriteAllText(path, JsonUtility.ToJson(result, true));
        AssetDatabase.ImportAsset(path);
    }
}
#endif
