using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KnightsGauntlet.EditorTools
{
    // Store screenshot capture. Enter Play mode, get to a nice moment, press F9.
    // The game is frozen (timeScale 0), the Game view is switched to each store size in turn,
    // the UI gets a few frames to re-layout (so iPad gets the real tablet layout with curtains),
    // and a PNG is written per size. Then the original Game view size and timeScale come back.
    //
    // Output: <repo parent>/Store Art/<yyyy-MM-dd>/<size folder>/<nn>.png
    public static class StoreScreenshots
    {
        struct Size { public string Folder; public int W, H; public Size(string f, int w, int h) { Folder = f; W = w; H = h; } }

        static readonly Size[] Sizes =
        {
            new Size("iPhone 6.9in (1320x2868)", 1320, 2868),   // App Store, required; Apple scales it down for smaller iPhones
            new Size("iPad 13in (2064x2752)",    2064, 2752),   // App Store, required (app runs on iPad); also fine for Play tablets
            new Size("Android phone (1080x1920)",1080, 1920),   // Google Play: max 2:1 aspect, so the tall iPhone shots don't qualify
            new Size("iPhone Duo unfolded (2180x2460)", 2180, 2460), // not a required store size; for marketing / if ASC asks for one
        };

        // Not store sizes: a layout check for foldables (iPhone Duo). The Duo's inner panel is
        // reported as anywhere from ~8:9 (2180x2460) to ~3:4; 3:4 is already covered by the iPad
        // shot, so check the near-square case, a true square worst case, and fold/unfold
        // transitions (phone -> inner -> phone) as live resizes.
        static readonly Size[] FoldableSizes =
        {
            new Size("1 inner (2180x2460)",  2180, 2460),
            new Size("2 folded (1170x2532)", 1170, 2532),
            new Size("3 square (2048x2048)", 2048, 2048),
            new Size("4 unfolded again (2180x2460)", 2180, 2460),
        };

        static Size[] _active = Sizes;

        const int SettleFrames = 8;         // frames to let LayoutHud/safe-area/curtains react to a new size
        const double SettleSeconds = 0.35;   // plus a little wall-clock time

        static Queue<Size> _queue;
        static Size _current;
        static int _framesLeft;
        static double _readyAt;
        static bool _waitingForFile;
        static string _pendingPath;
        static int _restoreIndex;
        static float _restoreTimeScale;
        static string _outRoot;
        static int _shotNumber;
        static EditorWindow _restoreWindow, _restoreMain;

        [MenuItem("Tools/Store Screenshots/Capture All Sizes _F9")]
        static void CaptureAll() => Capture(Sizes, DateTime.Now.ToString("yyyy-MM-dd"));

        [MenuItem("Tools/Store Screenshots/Foldable Check (not for the store)")]
        static void CaptureFoldable() => Capture(FoldableSizes, Path.Combine("Foldable check", DateTime.Now.ToString("yyyy-MM-dd")));

        static void Capture(Size[] set, string subfolder)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Screenshots] Enter Play mode first."); return; }
            if (_queue != null) { Debug.LogWarning("[Screenshots] Capture already running."); return; }

            _active = set;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string repoParent = Directory.GetParent(projectRoot).FullName;
            _outRoot = Path.Combine(repoParent, "Store Art", subfolder);
            _shotNumber = NextShotNumber(_outRoot);

            _restoreTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            // If the Device Simulator is the active play view, the Game view isn't rendering.
            // Bring the Game tab forward for the capture and hand back to the Simulator after.
            _restoreWindow = EditorWindow.focusedWindow;
            var gv = GameView();
            gv.Show();
            gv.Focus();
            // Focus alone doesn't make the Game view the "main" play view; while the Simulator
            // stays main, the game reads Screen size from it and layouts can mix two sizes.
            _restoreMain = GetMainPlayView();
            SetMainPlayView(gv);
            _restoreIndex = GameViewSelectedIndex();
            _queue = new Queue<Size>(set);
            StartNext();
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Store Screenshots/Open Output Folder")]
        static void OpenFolder()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(Directory.GetParent(projectRoot).FullName, "Store Art");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        static void StartNext()
        {
            _current = _queue.Dequeue();
            SetGameViewSize(_current.W, _current.H, "Store " + _current.W + "x" + _current.H);
            _framesLeft = SettleFrames;
            _readyAt = EditorApplication.timeSinceStartup + SettleSeconds;
            _waitingForFile = false;
            RepaintGameView();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            RepaintGameView();

            if (_waitingForFile)
            {
                if (!File.Exists(_pendingPath)) return;
                if (_queue.Count > 0) StartNext(); else Finish(true);
                return;
            }

            if (_framesLeft-- > 0 || EditorApplication.timeSinceStartup < _readyAt) return;

            string dir = Path.Combine(_outRoot, _current.Folder);
            Directory.CreateDirectory(dir);
            _pendingPath = Path.Combine(dir, _shotNumber.ToString("00") + ".png");
            ScreenCapture.CaptureScreenshot(_pendingPath);
            _waitingForFile = true;
        }

        static void Finish(bool ok)
        {
            EditorApplication.update -= Tick;
            _queue = null;
            // Screen.width inside editor callbacks reports the Simulator, so verify the files instead.
            if (ok)
                foreach (var s in _active)
                {
                    string f = Path.Combine(_outRoot, s.Folder, _shotNumber.ToString("00") + ".png");
                    var tex = new Texture2D(2, 2);
                    if (File.Exists(f) && tex.LoadImage(File.ReadAllBytes(f)) && (tex.width != s.W || tex.height != s.H))
                        Debug.LogWarning($"[Screenshots] {f} came out {tex.width}x{tex.height}, expected {s.W}x{s.H}.");
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            if (EditorApplication.isPlaying) Time.timeScale = _restoreTimeScale;
            if (_restoreIndex >= 0) GameViewSelect(_restoreIndex);
            if (_restoreMain != null) SetMainPlayView(_restoreMain);
            if (_restoreWindow != null && _restoreWindow.GetType() != GameViewType) _restoreWindow.Focus();
            _restoreWindow = null; _restoreMain = null;
            if (ok) Debug.Log($"[Screenshots] Shot {_shotNumber:00} saved for {_active.Length} sizes in {_outRoot}");
            else Debug.LogWarning("[Screenshots] Capture cancelled (left Play mode).");
        }

        static int NextShotNumber(string root)
        {
            int max = 0;
            foreach (var s in _active)
            {
                string dir = Path.Combine(root, s.Folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.png"))
                    if (int.TryParse(Path.GetFileNameWithoutExtension(f), out int n)) max = Math.Max(max, n);
            }
            return max + 1;
        }

        // ---- Game view size plumbing (internal Unity API, via reflection) ----

        static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        static readonly Type SizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        static readonly Type SizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        static readonly Type SizeKindType = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static EditorWindow GameView() => EditorWindow.GetWindow(GameViewType, false, null, false);

        static readonly Type PlayModeViewType = Type.GetType("UnityEditor.PlayModeView,UnityEditor");

        static EditorWindow GetMainPlayView() =>
            PlayModeViewType?.GetMethod("GetMainPlayModeView", All)?.Invoke(null, null) as EditorWindow;

        static void SetMainPlayView(EditorWindow view)
        {
            var f = PlayModeViewType?.GetField("s_LastFocused", All);
            if (f != null && view != null) f.SetValue(null, view);
        }

        static void RepaintGameView()
        {
            var gv = GameView();
            if (gv != null) gv.Repaint();
        }

        static object CurrentGroup()
        {
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(SizesType);
            var instance = singleton.GetProperty("instance", All).GetValue(null);
            var groupType = SizesType.GetProperty("currentGroupType", All).GetValue(instance);
            return SizesType.GetMethod("GetGroup", All).Invoke(instance, new[] { groupType });
        }

        static int GameViewSelectedIndex()
        {
            var p = GameViewType.GetProperty("selectedSizeIndex", All);
            return p != null ? (int)p.GetValue(GameView()) : -1;
        }

        static void GameViewSelect(int index)
        {
            var gv = GameView();
            var p = GameViewType.GetProperty("selectedSizeIndex", All);
            if (p != null) p.SetValue(gv, index);
            var m = GameViewType.GetMethod("SizeSelectionCallback", All);
            if (m != null) m.Invoke(gv, new object[] { index, null });
            gv.Repaint();
        }

        static void SetGameViewSize(int w, int h, string label)
        {
            var group = CurrentGroup();
            var getTotal = group.GetType().GetMethod("GetTotalCount", All);
            var getSize = group.GetType().GetMethod("GetGameViewSize", All);
            int total = (int)getTotal.Invoke(group, null);
            int found = -1;
            for (int i = 0; i < total; i++)
            {
                var s = getSize.Invoke(group, new object[] { i });
                int sw = (int)SizeType.GetProperty("width", All).GetValue(s);
                int sh = (int)SizeType.GetProperty("height", All).GetValue(s);
                string kind = SizeType.GetProperty("sizeType", All).GetValue(s).ToString();
                if (sw == w && sh == h && kind == "FixedResolution") { found = i; break; }
            }
            if (found < 0)
            {
                var fixedKind = Enum.Parse(SizeKindType, "FixedResolution");
                var ctor = SizeType.GetConstructor(new[] { SizeKindType, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new[] { fixedKind, w, h, (object)label });
                group.GetType().GetMethod("AddCustomSize", All).Invoke(group, new[] { size });
                found = (int)getTotal.Invoke(group, null) - 1;
            }
            GameViewSelect(found);
        }
    }
}
