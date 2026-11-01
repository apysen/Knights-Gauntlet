using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KnightsGauntlet.View;

namespace KnightsGauntlet.Editor
{
    // Renders every 3D UI icon (each skin's knight via SkinKnightIcon, the settings
    // gear via PowerupModelIcon) onto one contact sheet for art review, without Play
    // mode. Output: ArtSource/Icons/ui-icons-unity.png
    //   Unity -batchmode -projectPath . -executeMethod KnightsGauntlet.Editor.UiIconPreview.CaptureBatch -quit
    public static class UiIconPreview
    {
        private const int Cell = 160;

        [MenuItem("Knight's Gauntlet/Art/Capture UI Icons")]
        public static void CaptureMenu() => Debug.Log(Capture());

        public static void CaptureBatch() => Debug.Log("UI_ICON_PREVIEW " + Capture());

        public static string Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before rendering the icon review.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameTheme theme = null;
            var tiles = new List<Texture2D>();
            try
            {
                theme = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/GameTheme.asset"));
                var light = new GameObject("ReviewLight").AddComponent<Light>();
                light.type = LightType.Directional;

                foreach (var skin in Skins.All())
                {
                    var go = new GameObject("Skin_" + skin.Id, typeof(RectTransform), typeof(RawImage), typeof(SkinKnightIcon));
                    var icon = go.GetComponent<SkinKnightIcon>();
                    if (!icon.Initialize(theme, skin.Id))
                        throw new InvalidOperationException("Could not build the knight for skin " + skin.Id);
                    icon.Render();
                    tiles.Add(Grab(go.GetComponent<RawImage>().texture));
                }

                var gear = new GameObject("Gear", typeof(RectTransform), typeof(RawImage), typeof(PowerupModelIcon));
                if (!gear.GetComponent<PowerupModelIcon>().InitializeModel("IconModels/SettingsGear", spin: true))
                    throw new InvalidOperationException("Resources/IconModels/SettingsGear is missing.");
                GameObject.Find("PowerupIconCamera").GetComponent<Camera>().Render();
                tiles.Add(Grab(gear.GetComponent<RawImage>().texture));

                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSource/Icons"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "ui-icons-unity.png");
                File.WriteAllBytes(path, Sheet(tiles).EncodeToPNG());
                return "UI icon sheet: " + path;
            }
            finally
            {
                foreach (var t in tiles) UnityEngine.Object.DestroyImmediate(t);
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                if (theme != null) UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        private static Texture2D Grab(Texture source)
        {
            var rt = (RenderTexture)source;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            return copy;
        }

        // Tiles on the game's dark panel colour, 5 per row, so contrast reads as in game.
        private static Texture2D Sheet(List<Texture2D> tiles)
        {
            const int perRow = 5, pad = 12;
            int rows = (tiles.Count + perRow - 1) / perRow;
            int w = perRow * (Cell + pad) + pad, h = rows * (Cell + pad) + pad;
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var bg = new Color(.055f, .075f, .105f, 1f);
            var panel = new Color(.11f, .13f, .18f, 1f);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
            for (int t = 0; t < tiles.Count; t++)
            {
                int ox = pad + (t % perRow) * (Cell + pad);
                int oy = h - (pad + (t / perRow + 1) * (Cell + pad)) + pad;
                var src = tiles[t];
                for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    var c = src.GetPixel(x * src.width / Cell, y * src.height / Cell);
                    pixels[(oy + y) * w + ox + x] = Color.Lerp(panel, new Color(c.r, c.g, c.b, 1f), c.a);
                }
            }
            sheet.SetPixels(pixels);
            sheet.Apply();
            return sheet;
        }
    }
}
