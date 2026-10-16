using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using KnightsGauntlet.Core;
using KnightsGauntlet.View;

namespace KnightsGauntlet.Editor
{
    // An isolated art review using the same board, piece renderer and phone camera.
    public static class CeramicPiecePreview
    {
        private const string TogglePath = "Knight's Gauntlet/Art/Use Ceramic Trio Prototype";

        [MenuItem("Knight's Gauntlet/Art/Capture Ceramic Trio Comparison")]
        public static void CaptureMenu() => Debug.Log("[Ceramic prototype] " + Capture());

        [MenuItem(TogglePath)]
        public static void TogglePrototype()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/GameTheme.asset");
            if (theme == null) return;
            Undo.RecordObject(theme, "Toggle ceramic trio prototype");
            theme.ceramicPiecePrototype = !theme.ceramicPiecePrototype;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
        }

        [MenuItem(TogglePath, true)]
        private static bool ValidateToggle()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/GameTheme.asset");
            Menu.SetChecked(TogglePath, theme != null && theme.ceramicPiecePrototype);
            return !EditorApplication.isPlayingOrWillChangePlaymode && theme != null;
        }

        public static string Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before capturing the ceramic art comparison.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameTheme current = null, ceramic = null;
            try
            {
                current = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/GameTheme.asset"));
                current.comicStyle = true;
                current.inkPorcelainPieces = false;
                current.ceramicPiecePrototype = false;
                ceramic = UnityEngine.Object.Instantiate(current);
                ceramic.ceramicPiecePrototype = true;
                var camera = new GameObject("CeramicReviewCamera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.nearClipPlane = .1f;
                var board = new GameObject("ProductionBoard").AddComponent<BoardView>();
                board.Init(current);
                board.SyncBackground(0);
                board.FitCamera(camera, .15f, .06f);
                var pieces = new GameObject("ComparisonPieces");
                var labels = new GameObject("ComparisonLabels");
                var types = new[] { PieceType.Rook, PieceType.Bishop, PieceType.Knight };
                for (int i = 0; i < types.Length; i++)
                {
                    float row = 1.7f + i * 2.6f;
                    for (int facing = 0; facing < 2; facing++)
                    {
                        AddPiece(pieces.transform, board, current, types[i], facing, row, facing != 0);
                        AddPiece(pieces.transform, board, ceramic, types[i], facing + 3, row, facing != 0);
                    }
                    var viewport = camera.WorldToViewportPoint(board.CellToWorld(2, row));
                    Label(labels.transform, camera, viewport.x, viewport.y - .018f,
                        types[i].ToString().ToUpperInvariant(), 1.5f, .19f, UIFactory.Muted);
                }
                float top = camera.WorldToViewportPoint(board.CellToWorld(2, -.5f)).y;
                Label(labels.transform, camera, .5f, Mathf.Min(.96f, top + .13f), "CERAMIC TRIO", 4.2f, .9f, UIFactory.Text);
                Label(labels.transform, camera, .25f, top + .045f, "CURRENT", 2.4f, .42f, UIFactory.Muted);
                Label(labels.transform, camera, .75f, top + .045f, "PROTOTYPE", 2.4f, .42f, UIFactory.Gold);
                Label(labels.transform, camera, .5f, .06f, "FRONT + BACK  /  SAME BOARD AND CAMERA", 1.9f, .95f, UIFactory.Muted);
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSource/CeramicPrototype"));
                Directory.CreateDirectory(directory);
                int height = 1560;
                int width = Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1, Screen.height));
                Render(camera, Path.Combine(directory, "board-comparison.png"), width, height);

                pieces.SetActive(false);
                labels.SetActive(false);
                var trio = new GameObject("CeramicLineup");
                for (int i = 0; i < types.Length; i++)
                    AddPiece(trio.transform, board, ceramic, types[i], i * 2, 5f, false);
                camera.orthographicSize = 1.55f;
                camera.transform.position = camera.transform.up * .3f - camera.transform.forward * 16f;
                Render(camera, Path.Combine(directory, "ceramic-lineup.png"), 1400, 700);
                return directory;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                if (current != null) UnityEngine.Object.DestroyImmediate(current);
                if (ceramic != null) UnityEngine.Object.DestroyImmediate(ceramic);
            }
        }

        private static PieceView AddPiece(Transform parent, BoardView board, GameTheme theme,
            PieceType type, int col, float row, bool back)
        {
            var go = new GameObject((theme.ceramicPiecePrototype ? "Ceramic_" : "Current_") + type);
            go.transform.SetParent(parent, false);
            go.transform.position = board.CellToWorld(col, row);
            var piece = go.AddComponent<PieceView>();
            piece.spriteRenderer = go.GetComponent<SpriteRenderer>();
            piece.spriteRenderer.sortingOrder = 5;
            piece.Apply(theme, type);
            piece.SetFacing(back ? PieceFacing.Back : PieceFacing.Front);
            go.transform.localScale = Vector3.one * .9f;
            if (go.GetComponentsInChildren<MeshRenderer>().Length == 0)
                throw new InvalidOperationException("Missing mesh for " + go.name);
            return piece;
        }

        private static void Label(Transform parent, Camera camera, float x, float y,
            string value, float size, float widthFraction, Color color)
        {
            var go = new GameObject("Caption");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            text.font = UIFactory.BodyFont();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(camera.orthographicSize * 2 * camera.aspect * widthFraction, .5f);
            go.transform.position = camera.ViewportToWorldPoint(new Vector3(x, y, 8));
            go.transform.rotation = camera.transform.rotation;
            text.ForceMeshUpdate();
        }

        private static void Render(Camera camera, string path, int width, int height)
        {
            camera.cullingMask = 1 << 31;
            foreach (var root in camera.gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var shadows = new Vector4[24];
            Shader.SetGlobalInt("_RoyalShadowCount", PieceView.WriteBoardShadows(shadows));
            Shader.SetGlobalVectorArray("_RoyalShadows", shadows);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
