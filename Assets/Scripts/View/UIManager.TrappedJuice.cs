using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // "Was I trapped?" life (modern style). The answer is the payoff, so it lands hard:
    //   - "Yep" / "Nope" slams down with a flash, sparks in its colour, a jolt and a buzz
    //   - then the board tells the story in order: traps pop up, your last moves draw
    //     themselves as a blue line, their move badges pop, and the way out (if there was
    //     one) lights up step by step, each step with a small green burst
    //   - once revealed, a green pulse keeps running along the escape route, and the
    //     knight bobs where it was caught
    //   - the text legend becomes colour-keyed chips, motes drift behind, Back squashes
    public partial class UIManager
    {
        private sealed class TrapPathFx { public Image cell; public TMP_Text glyph; public bool popped; }

        private readonly List<RectTransform> _trapXs = new List<RectTransform>();
        private readonly List<(RectTransform rt, float len)> _trapLines = new List<(RectTransform, float)>();
        private readonly List<RectTransform> _trapBadges = new List<RectTransform>();
        private readonly List<TrapPathFx> _trapPath = new List<TrapPathFx>();
        private RectTransform _trapKnight;
        private RectTransform _trapFx;
        private Color _trapVerdictColor = Color.white;
        private float _trapT = 999f;
        private bool _trapSlammed;

        private static Color TrapPathBright => new Color(1f, .86f, .42f, 1f);

        // Called at the end of BuildTrapped.
        private void SetupTrappedJuice(GameObject screen, RectTransform col, TMP_Text legend, Button back)
        {
            AddShopMotes(screen.transform, 14);
            back.gameObject.AddComponent<PressSquash>();

            // The one-line legend becomes three colour-keyed chips.
            legend.gameObject.SetActive(false);
            var row = _ui.Row(col, 22f);
            row.name = "Legend";
            row.SetSiblingIndex(legend.transform.GetSiblingIndex());
            LegendChip(row, "Way out", sw =>
            {
                sw.color = PathCell;
                TargetRing(sw.rectTransform);
                var n = _ui.Label(sw.transform, "1", 15, PathText); UIFactory.Stretch(n.rectTransform);
                n.font = UIFactory.ComicFont(); n.alignment = TextAlignmentOptions.Center;
            });
            LegendChip(row, "Your last moves", sw =>
            {
                sw.color = Color.clear;
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(sw.transform, false);
                var brt = (RectTransform)bar.transform; brt.sizeDelta = new Vector2(30f, 6f);
                bar.GetComponent<Image>().color = new Color(0.435f, 0.827f, 1f, 0.9f);
            });
            LegendChip(row, "Trap", sw =>
            {
                var tex = PieceIconCache.Get(_theme, PieceType.Trap, false);
                if (tex != null)
                {
                    sw.color = Color.clear;
                    var tok = new GameObject("Token", typeof(RectTransform), typeof(RawImage));
                    tok.transform.SetParent(sw.transform, false);
                    UIFactory.Stretch((RectTransform)tok.transform);
                    var ri = tok.GetComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
                    return;
                }
                sw.color = TrapCell;
                var x = _ui.Label(sw.transform, "✕", 17, TrapGlyph); UIFactory.Stretch(x.rectTransform);
                if (_theme != null && _theme.glyphFont != null) x.font = _theme.glyphFont;
                x.alignment = TextAlignmentOptions.Center;
            });

            _trapFx = ShopFxLayer(screen.transform);
        }

        private void LegendChip(RectTransform row, string label, System.Action<Image> dressSwatch)
        {
            var chip = _ui.Row(row, 8f);
            var sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            sw.transform.SetParent(chip, false);
            var le = sw.GetComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = le.minWidth = le.minHeight = 30f;
            var img = sw.GetComponent<Image>(); img.raycastTarget = false;
            dressSwatch(img);
            var t = _ui.Label(chip, label, 17, UIFactory.Muted);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var tle = t.GetComponent<LayoutElement>();
            if (tle == null) tle = t.gameObject.AddComponent<LayoutElement>();
            tle.preferredWidth = t.GetPreferredValues(label).x + 4f;
        }

        private void ClearTrappedFx()
        {
            _trapXs.Clear(); _trapLines.Clear(); _trapBadges.Clear(); _trapPath.Clear(); _trapKnight = null;
        }

        // From ShowTrapped, after Populate and FitTrapped.
        private void TrappedOpened()
        {
            bool reduced = SaveData.IsReducedEffects();
            _trapT = reduced ? 999f : 0f;
            _trapSlammed = reduced;
            ApplyTrappedReveal(0f); // hide the pieces before the first frame draws
        }

        private void UpdateTrappedJuice()
        {
            if (_trapped == null || !_trapped.activeSelf) return;
            ApplyTrappedReveal(Mathf.Min(Time.unscaledDeltaTime, .05f));
        }

        private void ApplyTrappedReveal(float dt)
        {
            _trapT += dt;
            float t = _trapT;
            bool reduced = SaveData.IsReducedEffects();

            // Verdict slam.
            float vu = Mathf.Clamp01(t / .3f);
            var vrt = _trappedVerdict.rectTransform;
            float breathe = vu >= 1f && !reduced ? .02f * Mathf.Sin(t * 2.4f) : 0f;
            vrt.localScale = Vector3.one * (Mathf.LerpUnclamped(2.8f, 1f, EaseOutBackUI(vu)) + breathe);
            vrt.localRotation = Quaternion.Euler(0f, 0f, (1f - vu) * -12f);
            _trappedVerdict.alpha = Mathf.Clamp01(t / .08f);
            if (!_trapSlammed && t >= .22f)
            {
                _trapSlammed = true;
                if (_trapFx != null)
                {
                    var flash = _trapVerdictColor; flash.a = .8f;
                    FlashAt(_trapFx, vrt.position, flash, 380f);
                    SparkBurst(_trapFx, vrt.position, 28, _trapVerdictColor, Color.white, 1.1f);
                }
                Haptics.Play(Haptics.Settle, Haptics.SettleAmp);
            }
            // A short jolt of the whole panel right as it lands.
            float jolt = t >= .22f && t < .5f && !reduced ? (1f - (t - .22f) / .28f) : 0f;
            if (_trappedPanel != null)
                _trappedPanel.anchoredPosition = jolt > 0f
                    ? new Vector2(Mathf.Sin(t * 95f) * 9f * jolt, Mathf.Cos(t * 80f) * 5f * jolt) : Vector2.zero;
            _trappedSub.alpha = Mathf.Clamp01((t - .3f) / .25f);

            // The board, in story order.
            const float r0 = .5f;
            for (int i = 0; i < _trapXs.Count; i++)
                if (_trapXs[i] != null)
                    _trapXs[i].localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, EaseOutBackUI(Mathf.Clamp01((t - r0 - i * .02f) / .22f)));

            float l0 = r0 + .15f;
            for (int i = 0; i < _trapLines.Count; i++)
            {
                var (rt, len) = _trapLines[i];
                if (rt == null) continue;
                float k = Mathf.Clamp01((t - l0 - i * .07f) / .1f);
                rt.sizeDelta = new Vector2(len * k, 5f);
            }

            float b0 = l0 + _trapLines.Count * .07f;
            for (int i = 0; i < _trapBadges.Count; i++)
                if (_trapBadges[i] != null)
                    _trapBadges[i].localScale = Vector3.one * Mathf.Max(0f, Mathf.LerpUnclamped(0f, 1f, EaseOutBackUI(Mathf.Clamp01((t - b0 - i * .03f) / .2f))));

            float p0 = b0 + .1f;
            for (int i = 0; i < _trapPath.Count; i++)
            {
                var p = _trapPath[i];
                if (p.cell == null) continue;
                float k = Mathf.Clamp01((t - p0 - i * .12f) / .24f);
                var c = PathCell; c.a *= k; p.cell.color = c;
                p.cell.rectTransform.localScale = Vector3.one * Mathf.Max(0f, Mathf.LerpUnclamped(.2f, 1f, EaseOutBackUI(k))) * (k > 0f ? 1f : 0f);
                p.glyph.rectTransform.localScale = Vector3.one * Mathf.Max(0f, Mathf.LerpUnclamped(0f, 1f, EaseOutBackUI(k)));
                if (!p.popped && k > 0f)
                {
                    p.popped = true;
                    if (!reduced && dt > 0f && _trapFx != null && CellVisible(p.cell.rectTransform))
                    {
                        SparkBurst(_trapFx, p.cell.rectTransform.TransformPoint(p.cell.rectTransform.rect.center), 7,
                            UIFactory.Gold, Color.white, .45f);
                        Haptics.Play(Haptics.Tick, Haptics.TickAmp);
                    }
                }
            }

            // Afterwards: a pulse keeps running along the way out.
            float end = p0 + _trapPath.Count * .12f + .3f;
            if (t > end && !reduced && _trapPath.Count > 0)
            {
                float phase = Mathf.Repeat((t - end) * 7f, _trapPath.Count + 6f);
                for (int i = 0; i < _trapPath.Count; i++)
                {
                    var p = _trapPath[i];
                    if (p.cell == null) continue;
                    float k = Mathf.Max(0f, 1f - Mathf.Abs(phase - i) * .8f);
                    p.cell.color = Color.Lerp(PathCell, TrapPathBright, k);
                    p.glyph.rectTransform.localScale = Vector3.one * (1f + .28f * k);
                }
            }

            if (_trapKnight != null)
                _trapKnight.localScale = Vector3.one * (reduced ? 1f : 1f + .08f * Mathf.Sin(t * 3.2f));
        }

        // Only burst for steps the scroll view actually shows.
        private bool CellVisible(RectTransform cell)
        {
            if (_trappedScroll == null || _trappedScroll.viewport == null) return true;
            Vector3 world = cell.TransformPoint(cell.rect.center);
            return RectTransformUtility.RectangleContainsScreenPoint(_trappedScroll.viewport,
                RectTransformUtility.WorldToScreenPoint(null, world));
        }
    }
}
