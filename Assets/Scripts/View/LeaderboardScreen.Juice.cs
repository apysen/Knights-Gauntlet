using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Scores screen life.
    //   1. The title carries the 3D trophy from the menu nav; it hops when the screen opens.
    //   2. Gold motes drift behind; a gold bar glides under the active source tab.
    //   3. Rows slide in from the right one after another, and the top scores count up.
    //   4. The top three sit on gold / silver / bronze bands; their crowns bob, and the
    //      leader's row has a shimmer and a flash when it lands.
    //   5. Your own row is picked out with a pulsing gold band and a YOU tag.
    //   6. "Loading" animates its dots while the online board is fetched.
    public partial class LeaderboardScreen
    {
        private sealed class RowAnim
        {
            public RectTransform body; public CanvasGroup cg; public float delay;
            public TMP_Text score; public int target, shown = -1; public bool countUp;
            public RectTransform crown; public Image band, flash; public float bandAlpha; public bool you, landed;
        }
        private sealed class Mote { public RectTransform rt; public Image img; public float phase, speed; }

        private readonly List<RowAnim> _rowAnims = new List<RowAnim>();
        private readonly List<Mote> _motes = new List<Mote>();
        private RectTransform _srcBar, _titleIcon, _glint, _glintRow;
        private float _time, _rowsTime, _titleHop;
        private bool _youFound;

        private bool Juicy => _theme != null;

        // ── Build hooks ──────────────────────────────────────────────────────
        private void BuildTitle(Transform col)
        {
            _titleIcon = null;
            // The title uses the button font (UIFactory.BodyFont), like the rest of this screen.
            if (!Juicy) { _ui.Label(col, "Leaderboard", 30, UIFactory.Gold, FontStyles.Normal).font = UIFactory.BodyFont(); return; }
            var row = _ui.Row(col, 6f);
            var slot = new GameObject("TrophySlot", typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(row, false);
            var le = slot.GetComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 48f;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage), typeof(PowerupModelIcon));
            icon.transform.SetParent(slot.transform, false);
            ((RectTransform)icon.transform).sizeDelta = new Vector2(48f, 48f);
            if (icon.GetComponent<PowerupModelIcon>().InitializeModel("IconModels/NavScores")) _titleIcon = (RectTransform)slot.transform;
            else Destroy(slot);
            var title = _ui.Label(row, "Leaderboard", 30, UIFactory.Gold, FontStyles.Normal);
            title.font = UIFactory.BodyFont(); // before measuring its width below
            // Fit the label to its text so the trophy sits right beside it; nudge the row
            // left by half the trophy so the words stay centred on screen.
            var tle = title.GetComponent<LayoutElement>();
            if (tle == null) tle = title.gameObject.AddComponent<LayoutElement>();
            tle.preferredWidth = title.GetPreferredValues(title.text, 1000f, 100f).x + 6f;
            var pad = row.GetComponent<HorizontalLayoutGroup>();
            if (pad != null) pad.padding = new RectOffset(0, 54, 0, 0);
        }

        private void BuildJuice(RectTransform screen, Transform srcRow)
        {
            _srcBar = null; _motes.Clear(); _rowAnims.Clear();
            if (!Juicy) return;
            // Motes behind everything.
            var holder = new GameObject("Motes", typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(screen, false);
            holder.GetComponent<LayoutElement>().ignoreLayout = true;
            var hrt = (RectTransform)holder.transform; UIFactory.Stretch(hrt); hrt.SetAsFirstSibling();
            for (int i = 0; i < 14; i++)
            {
                var go = new GameObject("Mote", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(hrt, false);
                var img = go.GetComponent<Image>(); img.sprite = UISprites.SoftGlow(); img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                float s = Random.Range(10f, 26f);
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = new Vector2(Random.Range(-260f, 260f), Random.Range(-480f, 480f));
                _motes.Add(new Mote { rt = rt, img = img, phase = Random.value * 6f, speed = Random.Range(10f, 26f) });
            }

            var bar = new GameObject("TabIndicator", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            bar.transform.SetParent(srcRow, false);
            bar.GetComponent<LayoutElement>().ignoreLayout = true;
            _srcBar = (RectTransform)bar.transform;
            _srcBar.anchorMin = _srcBar.anchorMax = Vector2.zero; _srcBar.pivot = new Vector2(.5f, .5f);
            _srcBar.sizeDelta = new Vector2(60f, 4f);
            var bimg = bar.GetComponent<Image>(); bimg.color = UIFactory.Gold; bimg.raycastTarget = false;

            foreach (var b in screen.GetComponentsInChildren<Button>(true)) b.gameObject.AddComponent<PressSquash>();
        }

        private void OpenJuice() { _titleHop = 1f; }

        private void ClearJuiceRows()
        {
            _rowAnims.Clear(); _rowsTime = 0f; _youFound = false;
            _glint = _glintRow = null;
        }

        // Wraps a finished row's content in a body that can slide, and adds the bands.
        private void DecorateRow(GameObject row, int rank, ScoreRow data, TMP_Text score, TMP_Text crown)
        {
            if (!Juicy) return;
            var rowRt = (RectTransform)row.transform;
            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasGroup));
            var body = (RectTransform)bodyGo.transform;
            body.SetParent(rowRt, false); UIFactory.Stretch(body);
            for (int i = rowRt.childCount - 1; i >= 0; i--)
            {
                var c = rowRt.GetChild(i);
                if (c != body) c.SetParent(body, false);
            }
            // SetParent in reverse order flipped the siblings; restore their order.
            for (int i = 0, n = body.childCount; i < n; i++) body.GetChild(n - 1).SetSiblingIndex(i);

            // Top three: a unique 3D crown (gold royal crown, silver coronet, bronze
            // circlet) in place of the glyph, which stays as the fallback.
            if (crown != null)
            {
                string model = rank == 1 ? "IconModels/CrownGold" : rank == 2 ? "IconModels/CrownSilver" : "IconModels/CrownBronze";
                var icon = new GameObject("Crown3D", typeof(RectTransform), typeof(RawImage), typeof(PowerupModelIcon));
                icon.transform.SetParent(crown.rectTransform, false);
                var irt = (RectTransform)icon.transform;
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(.5f, .5f);
                irt.sizeDelta = new Vector2(40f, 40f); irt.anchoredPosition = Vector2.zero;
                if (icon.GetComponent<PowerupModelIcon>().InitializeModel(model)) crown.text = "";
                else Destroy(icon);
            }

            string me = SaveData.GetName();
            bool you = !_youFound && !string.IsNullOrEmpty(me) && data.name == me;
            if (you) _youFound = true;

            Image band = null; float bandAlpha = 0f;
            Color bandColor = Color.clear;
            if (you) { bandColor = UIFactory.Gold; bandAlpha = .12f; }
            else if (rank <= 3) { bandColor = rank == 1 ? Gold : rank == 2 ? Silver : Bronze; bandAlpha = rank == 1 ? .13f : .07f; }
            if (bandAlpha > 0f)
            {
                var bg = new GameObject("Band", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(body, false); bg.transform.SetAsFirstSibling();
                var brt = (RectTransform)bg.transform; UIFactory.Stretch(brt);
                brt.offsetMin = new Vector2(3f, 2f); brt.offsetMax = new Vector2(-3f, -2f);
                band = bg.GetComponent<Image>(); band.sprite = _ui.Fill(); band.type = Image.Type.Sliced; band.raycastTarget = false;
                band.color = new Color(bandColor.r, bandColor.g, bandColor.b, bandAlpha);
                // An accent stripe on the left edge.
                var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
                stripe.transform.SetParent(bg.transform, false);
                var srt = (RectTransform)stripe.transform;
                srt.anchorMin = new Vector2(0f, .15f); srt.anchorMax = new Vector2(0f, .85f); srt.pivot = new Vector2(0f, .5f);
                srt.sizeDelta = new Vector2(4f, 0f); srt.anchoredPosition = Vector2.zero;
                var simg = stripe.GetComponent<Image>(); simg.sprite = UISprites.RoundedFill(); simg.raycastTarget = false;
                simg.color = bandColor;
            }
            if (you)
            {
                var tag = new GameObject("You", typeof(RectTransform), typeof(TextMeshProUGUI));
                tag.transform.SetParent(body, false);
                var t = tag.GetComponent<TextMeshProUGUI>();
                t.font = UIFactory.BodyFont();
                t.text = "YOU"; t.fontSize = 10f; t.color = UIFactory.Gold; t.characterSpacing = 3f;
                t.alignment = TextAlignmentOptions.Left; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
                var trt = t.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 0f); trt.pivot = new Vector2(0f, 0f);
                trt.sizeDelta = new Vector2(60f, 14f); trt.anchoredPosition = new Vector2(NameLeft, 3f);
            }

            Image flash = null;
            if (rank == 1)
            {
                // Shimmer that sweeps the leader's row now and then.
                // Clipped to the gold band's own shape, so the sweep follows its rounded ends.
                var clip = UIFactory.ShapeClip(body, band);
                if (band != null)
                {
                    var brt = band.rectTransform;
                    clip.anchorMin = brt.anchorMin; clip.anchorMax = brt.anchorMax;
                    clip.offsetMin = brt.offsetMin; clip.offsetMax = brt.offsetMax;
                }
                var g = new GameObject("Glint", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(clip, false);
                _glint = (RectTransform)g.transform; _glintRow = rowRt;
                _glint.sizeDelta = new Vector2(40f, RowH * 2.2f); _glint.localRotation = Quaternion.Euler(0f, 0f, -20f);
                var gi = g.GetComponent<Image>(); gi.sprite = UISprites.SoftGlow(); gi.raycastTarget = false; gi.color = new Color(1f, 1f, 1f, .35f);
                g.SetActive(false);
                // Flash when it lands.
                var f = new GameObject("Flash", typeof(RectTransform), typeof(Image));
                f.transform.SetParent(body, false);
                var frt = (RectTransform)f.transform;
                frt.anchorMin = frt.anchorMax = new Vector2(0f, .5f); frt.anchoredPosition = new Vector2(LeftPad + 15f, 0f);
                frt.sizeDelta = new Vector2(70f, 70f);
                flash = f.GetComponent<Image>(); flash.sprite = UISprites.SoftGlow(); flash.raycastTarget = false;
                flash.color = new Color(1f, .85f, .4f, 0f);
            }

            int index = _rowAnims.Count;
            var anim = new RowAnim
            {
                body = body, cg = bodyGo.GetComponent<CanvasGroup>(), delay = Mathf.Min(index, 12) * .045f,
                score = score, target = data.score, countUp = index < 10,
                crown = crown != null ? crown.rectTransform : null, band = band, bandAlpha = bandAlpha, you = you, flash = flash,
            };
            if (anim.countUp && !SaveData.IsReducedEffects()) score.text = "0";
            if (!SaveData.IsReducedEffects()) { anim.cg.alpha = 0f; body.anchoredPosition = new Vector2(70f, 0f); }
            _rowAnims.Add(anim);
        }

        // ── Per frame ────────────────────────────────────────────────────────
        private void Update()
        {
            if (_root == null || !_root.activeSelf || !Juicy) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _time += dt; _rowsTime += dt;
            bool reduced = SaveData.IsReducedEffects();

            // Source tab bar glides to the active tab.
            var tab = _source == "personal" ? _personalTab : _source == "daily" ? _dailyTab : _onlineTab;
            if (_srcBar != null && tab != null)
            {
                var trt = (RectTransform)tab.transform;
                var target = new Vector2(trt.anchoredPosition.x, -4f);
                float k = 1f - Mathf.Exp(-16f * dt);
                _srcBar.anchoredPosition = _srcBar.anchoredPosition.y == 0f ? target : Vector2.Lerp(_srcBar.anchoredPosition, target, k);
                _srcBar.sizeDelta = new Vector2(Mathf.Lerp(_srcBar.sizeDelta.x, trt.rect.width - 16f, k), 4f);
            }

            // Title trophy: hops on open, then bobs gently.
            _titleHop = Mathf.Max(0f, _titleHop - dt / .55f);
            if (_titleIcon != null)
            {
                float hop = Mathf.Sin(_titleHop * Mathf.PI);
                float bob = reduced ? 0f : Mathf.Sin(_time * 1.8f) * 2f;
                _titleIcon.localScale = new Vector3(1f - .08f * hop, 1f + .18f * hop, 1f);
                _titleIcon.localRotation = Quaternion.Euler(0f, 0f, 10f * hop * Mathf.Sin(_titleHop * 16f) + (reduced ? 0f : Mathf.Sin(_time * 1.3f) * 3f));
                _titleIcon.GetChild(0).localPosition = new Vector3(0f, bob + 10f * hop, 0f);
            }

            // Status line: animated dots while loading.
            if (_status != null && _status.text.StartsWith("Loading"))
                _status.text = "Loading" + new string('.', 1 + (int)(_time * 3f) % 3);

            foreach (var a in _rowAnims)
            {
                if (a.body == null) continue;
                float u = Mathf.Clamp01((_rowsTime - a.delay) / .38f);
                if (!reduced)
                {
                    float e = 1f - Mathf.Pow(1f - u, 3f);
                    a.body.anchoredPosition = new Vector2(70f * (1f - e), 0f);
                    a.cg.alpha = Mathf.Clamp01(u * 2f);
                }
                if (a.countUp && a.score != null)
                {
                    float c = reduced ? 1f : Mathf.Clamp01((_rowsTime - a.delay - .1f) / .6f);
                    int v = Mathf.RoundToInt(a.target * (1f - Mathf.Pow(1f - c, 3f)));
                    if (v != a.shown) { a.shown = v; a.score.text = UIFactory.Number(v); }
                }
                if (!a.landed && u >= 1f)
                {
                    a.landed = true;
                    if (a.flash != null && !reduced) a.flash.color = new Color(1f, .85f, .4f, .9f);
                }
                if (a.flash != null)
                {
                    var fc = a.flash.color; fc.a = Mathf.Max(0f, fc.a - dt * 1.6f); a.flash.color = fc;
                    a.flash.rectTransform.localScale = Vector3.one * (1f + (1f - fc.a / .9f) * 1.2f);
                }
                if (a.crown != null && !reduced)
                {
                    a.crown.localRotation = Quaternion.Euler(0f, 0f, 7f * Mathf.Sin(_time * 2.2f + a.delay * 9f));
                    var p = a.crown.anchoredPosition; p.y = 2f * Mathf.Sin(_time * 2.6f + a.delay * 9f); a.crown.anchoredPosition = p;
                }
                if (a.band != null && a.you)
                {
                    var bc = a.band.color; bc.a = a.bandAlpha + (reduced ? 0f : .05f * Mathf.Sin(_time * 3f)); a.band.color = bc;
                }
            }

            // The leader's shimmer: one sweep every 3.4 s.
            if (_glint != null && _glintRow != null)
            {
                float g = Mathf.Repeat(_time / 3.4f, 1f) / .25f;
                bool on = !reduced && g < 1f && _rowsTime > .6f;
                _glint.gameObject.SetActive(on);
                float w = _glintRow.rect.width * .5f + 30f;
                if (on) _glint.anchoredPosition = new Vector2(Mathf.Lerp(-w, w, g), 0f);
            }

            foreach (var m in _motes)
            {
                if (m.rt == null) continue;
                var parent = (RectTransform)m.rt.parent;
                float h = parent.rect.height, w = parent.rect.width;
                var p = m.rt.anchoredPosition;
                p.y += m.speed * dt;
                p.x += Mathf.Sin(_time * .7f + m.phase) * 6f * dt;
                if (p.y > h * .5f + 20f) { p.y = -h * .5f - 20f; p.x = Random.Range(-w * .5f, w * .5f); }
                m.rt.anchoredPosition = p;
                m.img.color = new Color(1f, .82f, .4f, reduced ? 0f : .10f + .10f * Mathf.Sin(_time * 1.7f + m.phase * 3f));
            }
        }
    }
}
