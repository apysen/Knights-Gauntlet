using System;
using System.IO;
using System.Reflection;
using KnightsGauntlet.Core;
using KnightsGauntlet.View;
using UnityEditor;
using UnityEngine;

namespace KnightsGauntlet.EditorTools
{
    // Gameplay footage for the App Store search results video. In Play mode, pick a mode:
    // the Game view switches to the iPhone store size, a fresh run starts, the autopilot
    // plays the solver's best move on a steady beat, and every frame is saved as a JPG.
    // ArtSource/StoreScreenshots/build_search_video.py turns the frames into the video.
    //
    // Output: <repo parent>/Store Art/Footage/<mode>/0000.jpg ...
    public static class StoreFootage
    {
        const int Fps = 30;
        const int Seconds = 14;
        const int Warmup = 45;          // let the curtains part and the first enemies arrive
        const float MoveEvery = .62f;   // seconds between autopilot moves

        [MenuItem("Tools/Store Footage/Record Battle")]
        static void RecordBattle() => Record(GameMode.Battle, Difficulty.Easy);

        [MenuItem("Tools/Store Footage/Record Trap")]
        static void RecordTrap() => Record(GameMode.Trap, Difficulty.Easy);

        public static void Record(GameMode mode, Difficulty difficulty, int width = 1320, int height = 2868)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Footage] enter Play mode first"); return; }
            if (FootageCapture.Busy) { Debug.LogWarning("[Footage] already recording"); return; }
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm == null) { Debug.LogWarning("[Footage] no GameManager"); return; }

            // Reuse the screenshot tool's Game view sizing.
            typeof(StoreScreenshots).GetMethod("SetGameViewSize", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, new object[] { width, height, "Store " + width + "x" + height });

            Application.runInBackground = true; // keep frames coming while the editor is unfocused
            gm.StartGame(mode, difficulty);
            GameManager.AutopilotInterval = MoveEvery;
            string root = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            string dir = Path.Combine(root, "Store Art", "Footage", mode.ToString());
            if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.jpg")) File.Delete(f);
            FootageCapture.Begin(dir, Fps, Fps * Seconds, Warmup);
            Debug.Log("[Footage] recording " + mode + " to " + dir);
        }
    }
}
