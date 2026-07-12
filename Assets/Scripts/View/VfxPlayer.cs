using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Plays a one-shot sprite-frame animation on a world-space SpriteRenderer, then
    // destroys itself. Frames are loaded once from Resources and cached. Used for
    // the equipped movement VFX (played at the knight's feet on each move) and for
    // the customization preview.
    public class VfxPlayer : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        public static Sprite[] Frames(VfxInfo v)
        {
            if (v == null) return null;
            if (_cache.TryGetValue(v.Id, out var cached)) return cached;
            var frames = Load(v);
            _cache[v.Id] = frames; // cache even null/empty so we don't re-hit Resources
            return frames;
        }

        private static Sprite[] Load(VfxInfo v)
        {
            var list = new List<Sprite>();
            if (v.Sheet)
            {
                // One sliced sheet → its sub-sprites are the frames.
                var sprites = Resources.LoadAll<Sprite>(v.ResourcesPath);
                if (sprites != null && sprites.Length > 0)
                {
                    var sorted = new List<Sprite>(sprites);
                    sorted.Sort((a, b) => NaturalCompare(a.name, b.name));
                    list.AddRange(sorted);
                }
            }
            else
            {
                // Folder of per-frame textures → build a sprite per texture. This
                // ignores each PNG's import slicing, so it works regardless of it.
                var texs = Resources.LoadAll<Texture2D>(v.ResourcesPath);
                if (texs != null && texs.Length > 0)
                {
                    var sorted = new List<Texture2D>(texs);
                    sorted.Sort((a, b) => NaturalCompare(a.name, b.name));
                    foreach (var t in sorted)
                        list.Add(Sprite.Create(t, new Rect(0, 0, t.width, t.height),
                                               new Vector2(0.5f, 0.5f), t.height));
                }
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        // Order "Frame2" before "Frame10" by comparing any trailing integer.
        public static int NaturalCompare(string a, string b)
        {
            int na = TrailingInt(a), nb = TrailingInt(b);
            if (na >= 0 && nb >= 0 && na != nb) return na.CompareTo(nb);
            return string.CompareOrdinal(a, b);
        }
        private static int TrailingInt(string s)
        {
            int i = s.Length;
            while (i > 0 && char.IsDigit(s[i - 1])) i--;
            if (i == s.Length) return -1;
            return int.TryParse(s.Substring(i), out var n) ? n : -1;
        }

        // Spawn a one-shot VFX parented to `parent` (so it follows the knight and
        // doesn't drift as the board scrolls), at `localPos` in the parent's local
        // space, sized to `localSize` (parent-local units), on `sortingOrder`.
        public static void Play(VfxInfo v, Transform parent, Vector3 localPos, float localSize, int sortingOrder)
        {
            var frames = Frames(v);
            if (frames == null || frames.Length == 0) return;
            var go = new GameObject("Vfx_" + v.Id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = sortingOrder;
            var size = frames[0].bounds.size;
            float m = Mathf.Max(size.x, size.y, 0.0001f);
            go.transform.localScale = Vector3.one * (localSize * Mathf.Max(0.01f, v.Scale) / m);
            go.AddComponent<VfxPlayer>().Init(sr, frames, v.Fps);
        }

        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps, _t;
        private int _shown;

        private void Init(SpriteRenderer sr, Sprite[] frames, float fps)
        {
            _sr = sr; _frames = frames; _fps = Mathf.Max(1f, fps); _shown = 0; _t = 0f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            int idx = (int)(_t * _fps);
            if (idx >= _frames.Length) { Destroy(gameObject); return; }
            if (idx != _shown) { _shown = idx; _sr.sprite = _frames[idx]; }
        }
    }

    // Loops a VFX's frames on a UI Image, used for the customization previews.
    // Uses unscaled time so it animates even over a paused game.
    public class VfxUiPreview : MonoBehaviour
    {
        private Image _img;
        private Sprite[] _frames;
        private float _fps, _t;
        private int _shown = -1;

        public void Play(Sprite[] frames, float fps)
        {
            _img = GetComponent<Image>();
            _frames = frames;
            _fps = Mathf.Max(1f, fps);
        }

        private void Update()
        {
            if (_img == null || _frames == null || _frames.Length == 0) return;
            _t += Time.unscaledDeltaTime;
            int idx = (int)(_t * _fps) % _frames.Length;
            if (idx != _shown) { _shown = idx; _img.sprite = _frames[idx]; }
        }
    }
}
