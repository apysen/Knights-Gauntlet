using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KnightsGauntlet.Core;
using KnightsGauntlet.View;

namespace KnightsGauntlet.Editor
{
    public static class InkPorcelainPreview
    {
        [MenuItem("Knight's Gauntlet/Art/Capture Porcelain Set")]
        public static void CaptureMenu() => Debug.Log(Capture());

        public static string Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before rendering the art review.");
            var shader = Resources.Load<Shader>("Pieces/InkPorcelain/InkPorcelain");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Porcelain shader is missing or has compile errors.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameTheme theme = null;
            try
            {
                theme = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/GameTheme.asset"));
                theme.comicStyle = true;
                theme.inkPorcelainPieces = true;
                var camera = new GameObject("PorcelainReviewCamera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f,.075f,.105f);
                camera.nearClipPlane = .1f;
                var light = new GameObject("ReviewLight").AddComponent<Light>();
                light.type = LightType.Directional;
                var board = new GameObject("ProductionBoard").AddComponent<BoardView>();
                board.Init(theme);
                board.SyncBackground(0);
                board.FitCamera(camera,.15f,.06f);
                board.SyncBackground(0);
                var root = new GameObject("PorcelainFrontAndBack");
                var types = new[] { PieceType.Pawn, PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen };
                for (int i = 0; i < types.Length; i++)
                {
                    AddPiece(root.transform,board,theme,types[i],i,3.4f,false);
                    AddPiece(root.transform,board,theme,types[i],i,6.5f,true);
                }
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath,"../ArtSource/Models/InkPorcelain"));
                Directory.CreateDirectory(directory);
                int height = 1800;
                int width = Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1,Screen.height));
                Render(camera,Path.Combine(directory,"board-front-back.png"),width,height);
                camera.orthographicSize = 1.9f;
                camera.transform.position = board.CellToWorld(2,4.9f) + Vector3.back * .3f - camera.transform.forward * 16;
                Render(camera,Path.Combine(directory,"porcelain-set-unity.png"),1500,950);
                root.SetActive(false);
                var skins = new GameObject("PorcelainSkinReview");
                for (int i=0; i<types.Length; i++)
                    AddPiece(skins.transform,board,theme,types[i],i,4.8f,false,true,"emerald");
                camera.orthographicSize = 1.3f;
                camera.transform.position = board.CellToWorld(2,4.8f) + Vector3.back * .45f - camera.transform.forward * 16;
                Render(camera,Path.Combine(directory,"equipped-skin-unity.png"),1500,650);
                return "Porcelain meshes, front/back views and skin accent render complete: " + directory;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                if (theme != null) UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        private static void AddPiece(Transform parent,BoardView board,GameTheme theme,
            PieceType type,int col,float row,bool back,bool player=false,string skin=null)
        {
            var go = new GameObject("Porcelain_"+type);
            go.transform.SetParent(parent,false);
            go.transform.position = board.CellToWorld(col,row);
            var piece = go.AddComponent<PieceView>();
            piece.spriteRenderer = go.GetComponent<SpriteRenderer>();
            piece.spriteRenderer.sortingOrder = 5;
            piece.Apply(theme,type,isPlayer:player,skinId:skin);
            piece.SetFacing(back ? PieceFacing.Back : PieceFacing.Front);
            go.transform.localScale = Vector3.one * .9f * board.cellSize;
            var meshes = go.GetComponentsInChildren<MeshRenderer>();
            if (meshes.Length == 0 || piece.spriteRenderer.enabled)
                throw new InvalidOperationException("Missing porcelain mesh for " + type);
            foreach (var mesh in meshes)
                foreach (var material in mesh.sharedMaterials)
                    if (material == null || material.shader.name != "Knight's Gauntlet/Ink Porcelain")
                        throw new InvalidOperationException("Wrong material on " + type);
        }

        private static void Render(Camera camera,string path,int width,int height)
        {
            camera.cullingMask = 1 << 31;
            foreach (var root in camera.gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var shadows = new Vector4[24];
            Shader.SetGlobalInt("_RoyalShadowCount",PieceView.WriteBoardShadows(shadows));
            Shader.SetGlobalVectorArray("_RoyalShadows",shadows);
            var target = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;
                camera.aspect=(float)width/height;
                camera.Render();
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);
                pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;
                RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
