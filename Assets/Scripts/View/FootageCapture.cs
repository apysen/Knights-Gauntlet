#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

namespace KnightsGauntlet.View
{
    // Records store footage frame by frame (editor only, never in a build). Time is locked
    // with Time.captureFramerate, so every saved frame is exactly 1/fps of game time apart
    // however long a frame takes to save: the footage plays back perfectly smooth.
    // Started by Tools > Store Footage (Assets/Editor/StoreFootage.cs).
    public sealed class FootageCapture : MonoBehaviour
    {
        public static bool Busy { get; private set; }
        private string _dir;
        private int _frames, _warmup, _fps;

        public static void Begin(string dir, int fps, int frames, int warmupFrames)
        {
            var go = new GameObject("FootageCapture");
            var c = go.AddComponent<FootageCapture>();
            c._dir = dir; c._fps = fps; c._frames = frames; c._warmup = warmupFrames;
            Directory.CreateDirectory(dir);
            Busy = true;
            c.StartCoroutine(c.Run());
        }

        private IEnumerator Run()
        {
            Time.captureFramerate = _fps;
            try
            {
                for (int i = 0; i < _warmup; i++) yield return null;
                for (int i = 0; i < _frames; i++)
                {
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(_dir, i.ToString("0000") + ".jpg"), tex.EncodeToJPG(93));
                    Destroy(tex);
                }
            }
            finally
            {
                Time.captureFramerate = 0;
                GameManager.AutopilotInterval = 0f;
                Busy = false;
                Debug.Log("[Footage] wrote " + _frames + " frames to " + _dir);
                Destroy(gameObject);
            }
        }
    }
}
#endif
