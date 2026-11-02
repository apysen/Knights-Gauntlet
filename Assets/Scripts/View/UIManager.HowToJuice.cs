using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // How to Play life (modern style):
    //   - gold motes behind, a soft glow breathing behind the framed diagram
    //   - a "LESSON 1 OF 6" kicker over the title, key words in the body picked out in gold
    //   - pages turn with a card flip (squash out, swap, spring back) and can be swiped
    //   - each diagram's parts pop in one after another
    //   - page dots replace "1 / 6"; the active one stretches into a gold pill
    //   - on the last page "Got it" breathes and glints; reaching it bursts a few sparks
    public partial class UIManager
    {
        private RectTransform _howtoFx, _howtoGlow, _howtoDots, _howtoGotIt;
        private TMP_Text _howtoKicker;
        private readonly List<Image> _howtoDotImgs = new List<Image>();
        private float _howtoTime, _howtoFlipT = -1f;
        private int _howtoFlipDir;
        private bool _howtoFlipSwapped;

        // Called at the end of BuildHowTo.
        private void SetupHowToJuice(GameObject screen, RectTransform col, Button gotIt)
        {
            if (LegacyStyle) return;
            AddShopMotes(screen.transform, 16);

            var glowGo = new GameObject("ArtGlow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(screen.transform, false);
            glowGo.transform.SetSiblingIndex(1); // above the motes, below the column
            _howtoGlow = (RectTransform)glowGo.transform;
            var gi = glowGo.GetComponent<Image>(); gi.sprite = UISprites.SoftGlow(); gi.raycastTarget = false;

            var artOutline = _howtoArt.gameObject.AddComponent<Outline>();
            artOutline.effectColor = new Color(.87f, .73f, .41f, .55f); artOutline.effectDistance = new Vector2(2f, -2f);

            _howtoKicker = _ui.Label(col, "", 13, UIFactory.Gold, FontStyles.Normal);
            _howtoKicker.characterSpacing = 8f;
            _howtoKicker.transform.SetSiblingIndex(_howtoTitle.transform.GetSiblingIndex());
            _howtoBody.fontSize = 16f * UIFactory.FontScale; _howtoBody.richText = true; _howtoBody.lineSpacing = 6f;

            // Dots live inside the old pager label's slot.
            _howtoPager.text = "";
            var pagerLe = _howtoPager.GetComponent<LayoutElement>();
            if (pagerLe == null) pagerLe = _howtoPager.gameObject.AddComponent<LayoutElement>();
            pagerLe.preferredWidth = 130f;
            var dotsGo = new GameObject("Dots", typeof(RectTransform));
            dotsGo.transform.SetParent(_howtoPager.transform, false);
            _howtoDots = (RectTransform)dotsGo.transform;
            _howtoDots.anchorMin = _howtoDots.anchorMax = _howtoDots.pivot = new Vector2(.5f, .5f);

            foreach (var b in col.GetComponentsInChildren<Button>(true))
                if (b != gotIt && b.GetComponent<PressSquash>() == null) b.gameObject.AddComponent<PressSquash>();
            AddGlint(gotIt, 52f);
            _howtoGotIt = (RectTransform)gotIt.transform;

            screen.AddComponent<SwipePager>().OnSwipe = d =>
            {
                if (!_howto.activeSelf || _howtoFlipT >= 0f) return;
                SfxManager.Play(Sfx.PageTurn);
                FlipHowTo(d);
            };
            _howtoFx = ShopFxLayer(screen.transform);
        }

        private void EnsureHowToDots(int n)
        {
            if (_howtoDots == null || _howtoDotImgs.Count == n) return;
            foreach (var d in _howtoDotImgs) if (d != null) Destroy(d.gameObject);
            _howtoDotImgs.Clear();
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_howtoDots, false);
                var img = go.GetComponent<Image>(); img.sprite = UISprites.RoundedFill(); img.type = Image.Type.Sliced;
                img.raycastTarget = false;
                var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(10f, 10f);
                _howtoDotImgs.Add(img);
            }
        }

        // From ShowHowTo / ShowHowToSubset after the first render.
        private void HowToOpened()
        {
            if (LegacyStyle || _howtoKicker == null) return;
            _howtoFlipT = -1f;
            var col = _howtoTitle.transform.parent;
            PopInChildren(col, .06f);
            PopArtParts(.25f);
        }

        // From RenderHowTo: per-page dressing that the plain screen doesn't have.
        private void DressHowToPage(int page)
        {
            if (LegacyStyle || _howtoKicker == null) return;
            _howtoKicker.text = "LESSON " + (_howtoIdx + 1) + " OF " + _howtoPages.Length;
            _howtoBody.text = HighlightKeywords(GameContent.HowTo[page].body);
            EnsureHowToDots(_howtoPages.Length);
            _howtoPager.text = "";
        }

        // Pop the new diagram's pieces in (HowToArt rebuilds them as one wrapper child).
        private void PopArtParts(float delay)
        {
            if (_howtoArt == null || _howtoArt.childCount == 0) return;
            var box = _howtoArt.GetChild(_howtoArt.childCount - 1); // the old one is still pending Destroy
            PopInChildren(box, .09f, delay);
        }

        // Card-flip page turn. Returns true when it took over the turn.
        private bool BeginHowToFlip(int d)
        {
            if (LegacyStyle || _howtoKicker == null || SaveData.IsReducedEffects()) return false;
            int next = Mathf.Clamp(_howtoIdx + d, 0, _howtoPages.Length - 1);
            if (next == _howtoIdx) { WobbleRow(_howtoArt); return true; }
            _howtoFlipDir = d; _howtoFlipT = 0f; _howtoFlipSwapped = false;
            return true;
        }

        private void UpdateHowToJuice()
        {
            if (_howto == null || !_howto.activeSelf || LegacyStyle || _howtoKicker == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _howtoTime += dt;
            bool reduced = SaveData.IsReducedEffects();

            // Flip: squash the page edge-on, swap at the midpoint, spring back open.
            float sx = 1f;
            if (_howtoFlipT >= 0f)
            {
                _howtoFlipT += dt;
                const float half = .13f;
                if (_howtoFlipT < half) sx = 1f - _howtoFlipT / half;
                else
                {
                    if (!_howtoFlipSwapped)
                    {
                        _howtoFlipSwapped = true;
                        _howtoIdx = Mathf.Clamp(_howtoIdx + _howtoFlipDir, 0, _howtoPages.Length - 1);
                        RenderHowTo();
                        PopArtParts(.04f);
                        if (_howtoIdx == _howtoPages.Length - 1 && _howtoFx != null && _howtoGotIt != null)
                            SparkBurst(_howtoFx, _howtoGotIt.position, 16, UIFactory.Gold, Color.white, .7f);
                    }
                    float u = Mathf.Clamp01((_howtoFlipT - half) / .3f);
                    sx = Mathf.LerpUnclamped(0f, 1f, EaseOutBackUI(u));
                    if (u >= 1f) { _howtoFlipT = -1f; sx = 1f; }
                }
            }
            float tilt = _howtoFlipT >= 0f ? (1f - sx) * 6f * -_howtoFlipDir : 0f;
            foreach (var t in new Transform[] { _howtoArt, _howtoTitle.transform, _howtoBody.transform, _howtoKicker.transform })
            {
                if (_popIns.Exists(p => p.rt == (RectTransform)t)) continue;
                t.localScale = new Vector3(Mathf.Max(.001f, sx), 1f, 1f);
                t.localRotation = Quaternion.Euler(0f, 0f, t == _howtoArt ? tilt : 0f);
            }

            // Glow behind the diagram.
            if (_howtoGlow != null)
            {
                _howtoGlow.position = _howtoArt.position;
                var lossy = _howtoArt.lossyScale.x / Mathf.Max(.0001f, _howtoGlow.lossyScale.x);
                _howtoGlow.sizeDelta = (_howtoArt.rect.size + new Vector2(150f, 150f)) * lossy;
                float a = reduced ? .14f : .16f + .08f * Mathf.Sin(_howtoTime * 2.2f);
                _howtoGlow.GetComponent<Image>().color = new Color(1f, .8f, .35f, a);
            }

            // Dots glide: the current one stretches into a gold pill.
            for (int i = 0; i < _howtoDotImgs.Count; i++)
            {
                var img = _howtoDotImgs[i];
                var rt = img.rectTransform;
                bool on = i == _howtoIdx;
                float w = Mathf.Lerp(rt.sizeDelta.x, on ? 26f : 10f, 1f - Mathf.Exp(-14f * dt));
                rt.sizeDelta = new Vector2(w, 10f);
                img.color = Color.Lerp(img.color, on ? UIFactory.Gold : new Color(1f, 1f, 1f, .22f), 1f - Mathf.Exp(-14f * dt));
            }
            float x = 0f, total = 0f;
            foreach (var d in _howtoDotImgs) total += d.rectTransform.sizeDelta.x;
            total += Mathf.Max(0, _howtoDotImgs.Count - 1) * 8f;
            foreach (var d in _howtoDotImgs)
            {
                float w = d.rectTransform.sizeDelta.x;
                d.rectTransform.anchoredPosition = new Vector2(-total * .5f + x + w * .5f, 0f);
                x += w + 8f;
            }

            // The finishing button breathes on the last page.
            if (_howtoGotIt != null && !_popIns.Exists(p => p.rt == _howtoGotIt))
            {
                bool last = _howtoIdx == _howtoPages.Length - 1;
                _howtoGotIt.localScale = Vector3.one * (last && !reduced ? 1f + .045f * Mathf.Sin(_howtoTime * 4f) : 1f);
            }
        }
    }

    // Horizontal swipe anywhere on a screen: left = next, right = previous.
    public sealed class SwipePager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public System.Action<int> OnSwipe;
        private Vector2 _start;
        public void OnBeginDrag(PointerEventData e) { _start = e.position; }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e)
        {
            Vector2 d = e.position - _start;
            float min = Screen.width * .12f;
            if (Mathf.Abs(d.x) > min && Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.3f) OnSwipe?.Invoke(d.x < 0 ? 1 : -1);
        }
    }
}
