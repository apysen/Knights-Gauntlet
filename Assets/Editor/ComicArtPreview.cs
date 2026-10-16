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
    // Uses the actual board renderer without starting GameManager or platform services.
    public static class ComicArtPreview
    {
        [MenuItem("Knight's Gauntlet/Art/Capture Graphic Board Preview")]
        public static void Capture()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameTheme theme = null;
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/GameTheme.asset");
                theme = asset != null ? UnityEngine.Object.Instantiate(asset) : ScriptableObject.CreateInstance<GameTheme>();
                theme.uiFont = theme.hudFont = theme.popupFont = UIFactory.ComicFont();
                var camera = new GameObject("ArtPreviewCamera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5.7f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.backgroundColor = UIFactory.Bg;
                camera.clearFlags = CameraClearFlags.SolidColor;
                var board = new GameObject("ArtPreviewBoard").AddComponent<BoardView>();
                board.cellW = board.cellSize;
                board.Init(theme);
                board.SyncBackground(0);
                var view = new GameObject("ArtPreviewPieces").AddComponent<GameView>();
                view.Init(theme, board);
                var sim = new GameSimulation();
                sim.Start(GameMode.Battle, Difficulty.Easy, 17);
                sim.Tutorial = true;
                sim.Pieces.Clear();
                sim.Pieces.Add(new GamePiece(PieceType.Rook, 0, 1));
                sim.Pieces.Add(new GamePiece(PieceType.Queen, 2, 1));
                sim.Pieces.Add(new GamePiece(PieceType.Bishop, 4, 1));
                sim.Pieces.Add(new GamePiece(PieceType.Knight, 1, 3));
                sim.Pieces.Add(new GamePiece(PieceType.Pawn, 3, 4));
                sim.Pieces.Add(new GamePiece(PieceType.Trap, 0, 5));
                sim.Pieces.Add(new GamePiece(PieceType.Trap, 4, 6));
                sim.Pieces.Add(new GamePiece(PieceType.Trap, 1, 8));
                sim.Pickups.Add(new Pickup(0, 3, 10));
                view.SetPlayerForm(PieceType.Knight, "gold");
                view.Sync(sim, true, 0f);

                var title = new GameObject("PreviewTitle").AddComponent<TextMeshPro>();
                title.font = UIFactory.ComicFont();
                title.text = "KNIGHT'S GAUNTLET";
                title.fontSize = 5.6f;
                title.color = UIFactory.Gold;
                title.alignment = TextAlignmentOptions.Center;
                title.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
                title.transform.position = new Vector3(0, 5.35f, 0);
                title.ForceMeshUpdate();

                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSource/GraphicChess"));
                Directory.CreateDirectory(dir);
                Render(camera, Path.Combine(dir, "gameplay-idle.png"));
                int fromCol = sim.PlayerCol;
                float fromY = sim.PlayerY;
                var result = sim.TryTap(3, 4);
                if (!result.Moved) throw new InvalidOperationException("Preview capture move was not legal.");
                view.PlayComicMove(fromCol, fromY, PieceType.Knight, sim);
                view.PlayComicCapture(sim);
                view.Sync(sim, true, 0.015f);
                Render(camera, Path.Combine(dir, "gameplay-impact.png"));
                Debug.Log("[Art preview] Captured idle/front and capture/back views in ArtSource/GraphicChess.");
            }
            finally
            {
                if (theme != null) UnityEngine.Object.DestroyImmediate(theme);
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        private static void Render(Camera camera, string path)
        {
            // The temporary scene shares the Editor world with the user's scene.
            // Isolate its renderers so the preview cannot include other scene art.
            camera.cullingMask = 1 << 31;
            foreach (var root in camera.gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var target = RenderTexture.GetTemporary(720, 1280, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(720, 1280, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 720f / 1280f;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
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
