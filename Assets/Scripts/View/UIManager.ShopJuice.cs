using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Feedback for the Shop and Treasury (Buy Shillings) screens. A small toolkit of
    // UI animations, all driven from UpdateShopJuice on unscaled time:
    //   - balances that tween to the real value and punch when it changes
    //   - cards that pop in with a stagger when a screen or tab opens
    //   - a shimmer that sweeps affordable price buttons
    //   - coins that fly along an arc (spending: balance -> item; buying: card -> balance)
    //   - flashes, spark bursts, rising stamp text and a wobble for "can't afford"
    //   - slow gold motes drifting behind the content
    // Every effect lives on an overlay layer on top of its canvas, so rebuilding a list
    // mid-animation never cuts one off.
    public partial class UIManager
    {
        private sealed class BalanceView
        {
            public TMP_Text text; public RectTransform punchRt;
            public float shown = -1f, punch; public int held, lastTarget = -1, lastShown = -1;
        }
        private sealed class CardPop { public RectTransform rt; public CanvasGroup cg; public float t, delay; }
        private sealed class Glint { public RectTransform rt; public Image img; public float phase, range = 90f; }
        private sealed class Flyer
        {
            public RectTransform rt; public Graphic g; public RectTransform layer; public bool active;
            public Vector2 a, c, b; public float t, delay, dur, size, spin; public BalanceView land; public int value;
        }
        private sealed class Spark
        {
            public RectTransform rt; public Image img; public RectTransform layer; public bool active;
            public Vector2 vel; public float life, maxLife, size, gravity; public Color color;
        }
        private sealed class Stamp
        {
            public TMP_Text text; public RectTransform layer; public bool active; public float t, dur; public Vector2 at;
        }
        private sealed class Wobble { public RectTransform rt; public float t; }
        private sealed class Mote { public RectTransform rt; public Image img; public float phase, speed; }

        private readonly List<BalanceView> _balances = new List<BalanceView>();
        private readonly List<CardPop> _popIns = new List<CardPop>();
        private readonly List<Glint> _glints = new List<Glint>();
        private readonly List<Flyer> _flyers = new List<Flyer>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly List<Stamp> _stamps = new List<Stamp>();
        private readonly List<Wobble> _wobbles = new List<Wobble>();
        private readonly List<Mote> _motes = new List<Mote>();
        private readonly List<(Image img, float t)> _flashes = new List<(Image, float)>();
        private readonly List<(RectTransform rt, float phase)> _breathers = new List<(RectTransform, float)>();
        private readonly List<RectTransform> _spinners = new List<RectTransform>();
        private float _spinBoost, _shopTime, _lastCoinClink = -1f;
        private Texture _shopCoinTex, _treasuryCoinTex;

        // ── Per frame ────────────────────────────────────────────────────────
        private void UpdateShopJuice()
        {
            bool marketOn = _market != null && _market.activeSelf;
            bool treasuryOn = _shillingsShop != null && _shillingsShop.activeSelf;
            bool menuOn = _menu != null && _menu.activeSelf && !LegacyStyle;
            bool runOn = _runOptions != null && _runOptions.activeSelf && !LegacyStyle;
            bool customOn = _custom != null && _custom.activeSelf && !LegacyStyle;
            bool settingsOn = _settings != null && _settings.activeSelf && !LegacyStyle;
            bool levelsOn = _levelSelect != null && _levelSelect.activeSelf && !LegacyStyle;
            if (!marketOn && !treasuryOn && !menuOn && !runOn && !customOn && !settingsOn && !levelsOn) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _shopTime += dt;
            bool reduced = SaveData.IsReducedEffects();

            int real = _gm.Shillings;
            foreach (var b in _balances)
            {
                if (b.text == null || !b.text.gameObject.activeInHierarchy) continue;
                int target = Mathf.Max(0, real - b.held);
                if (b.shown < 0f) b.shown = target;
                if (b.lastTarget >= 0 && target != b.lastTarget) b.punch = 1f;
                b.lastTarget = target;
                b.shown = Mathf.Abs(target - b.shown) < 1f ? target : Mathf.Lerp(b.shown, target, 1f - Mathf.Exp(-9f * dt));
                int shownInt = Mathf.RoundToInt(b.shown);
                if (shownInt != b.lastShown) { b.lastShown = shownInt; b.text.text = shownInt.ToString("N0"); }
                b.punch = Mathf.Max(0f, b.punch - dt / .25f);
                if (b.punchRt != null) b.punchRt.localScale = Vector3.one * (1f + .18f * Mathf.Sin(b.punch * Mathf.PI));
            }

            for (int i = _popIns.Count - 1; i >= 0; i--)
            {
                var p = _popIns[i];
                if (p.rt == null) { _popIns.RemoveAt(i); continue; }
                p.t += dt;
                float u = Mathf.Clamp01((p.t - p.delay) / .32f);
                p.rt.localScale = Vector3.one * Mathf.LerpUnclamped(.82f, 1f, EaseOutBackUI(u));
                if (p.cg != null) p.cg.alpha = Mathf.Clamp01(u * 2.5f);
                if (u >= 1f) { p.rt.localScale = Vector3.one; if (p.cg != null) p.cg.alpha = 1f; _popIns.RemoveAt(i); }
            }

            for (int i = _glints.Count - 1; i >= 0; i--)
            {
                var g = _glints[i];
                if (g.rt == null) { _glints.RemoveAt(i); continue; }
                // One sweep every 3.2 s, each button offset so they ripple down the list.
                float u = Mathf.Repeat(_shopTime * .3125f + g.phase, 1f) / .22f;
                bool on = !reduced && u < 1f && g.img.enabled;
                g.rt.gameObject.SetActive(on);
                if (on) g.rt.anchoredPosition = new Vector2(Mathf.Lerp(-g.range, g.range, u), 0f);
            }

            foreach (var f in _flyers) if (f.active) AnimateFlyer(f, dt);
            foreach (var s in _sparks) if (s.active) AnimateSpark(s, dt);
            foreach (var s in _stamps) if (s.active) AnimateStamp(s, dt);

            for (int i = _wobbles.Count - 1; i >= 0; i--)
            {
                var w = _wobbles[i];
                w.t += dt;
                if (w.rt == null || w.t >= .42f) { if (w.rt != null) w.rt.localRotation = Quaternion.identity; _wobbles.RemoveAt(i); continue; }
                w.rt.localRotation = Quaternion.Euler(0f, 0f, 4.5f * Mathf.Sin(w.t * 46f) * (1f - w.t / .42f));
            }

            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var (img, t) = _flashes[i];
                t += dt;
                if (img == null || t >= .45f) { if (img != null) img.gameObject.SetActive(false); _flashes.RemoveAt(i); continue; }
                float u = t / .45f;
                img.rectTransform.localScale = Vector3.one * Mathf.Lerp(.6f, 2.4f, 1f - (1f - u) * (1f - u));
                var c = img.color; c.a = (1f - u) * (1f - u) * .9f; img.color = c;
                _flashes[i] = (img, t);
            }

            foreach (var (rt, phase) in _breathers)
                if (rt != null && !_popIns.Exists(p => p.rt == rt))
                    rt.localScale = Vector3.one * (1f + (reduced ? 0f : .018f * Mathf.Sin(_shopTime * 2.1f + phase)));

            // The tab bar glides under the selected tab.
            if (marketOn) GlideTabBar(_tabIndicator, _shopTab == 0 ? _tabShopSkins : _shopTab == 1 ? _tabShopVfx : _tabPowerups, dt);

            _spinBoost = Mathf.Max(0f, _spinBoost - dt * .7f);
            foreach (var rt in _spinners)
                if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, -(_shopTime * 14f + _spinBoost * _spinBoost * 260f));

            foreach (var m in _motes)
            {
                if (m.rt == null || !m.rt.gameObject.activeInHierarchy) continue;
                var parent = (RectTransform)m.rt.parent;
                float h = parent.rect.height, w = parent.rect.width;
                var p = m.rt.anchoredPosition;
                p.y += m.speed * dt;
                p.x += Mathf.Sin(_shopTime * .7f + m.phase) * 6f * dt;
                if (p.y > h * .5f + 20f) { p.y = -h * .5f - 20f; p.x = Random.Range(-w * .5f, w * .5f); }
                m.rt.anchoredPosition = p;
                m.img.color = new Color(1f, .82f, .4f, reduced ? 0f : .10f + .10f * Mathf.Sin(_shopTime * 1.7f + m.phase * 3f));
            }
        }

        // ── Building blocks ──────────────────────────────────────────────────
        // A top-most layer on a screen for flying coins, sparks and stamps.
        private RectTransform ShopFxLayer(Transform screen)
        {
            var go = new GameObject("ShopFx", typeof(RectTransform));
            go.transform.SetParent(screen, false);
            var rt = (RectTransform)go.transform;
            UIFactory.Stretch(rt);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return rt;
        }

        // Slow gold motes drifting up behind a screen's content.
        private void AddShopMotes(Transform screen, int count)
        {
            var holder = new GameObject("Motes", typeof(RectTransform));
            holder.transform.SetParent(screen, false);
            var hrt = (RectTransform)holder.transform; UIFactory.Stretch(hrt);
            holder.AddComponent<LayoutElement>().ignoreLayout = true;
            hrt.SetAsFirstSibling();
            for (int i = 0; i < count; i++)
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
        }

        // The 3D shilling as a UI coin. Returns its texture so other coins can share the render.
        private RawImage AddCoin(Transform parent, float size, bool swing = false)
        {
            var go = new GameObject("Coin", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = size;
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<RawImage>(); img.raycastTarget = false;
            if (LegacyStyle) { img.texture = UISprites.Circle().texture; img.color = UIFactory.Gold; return img; }
            var coin = go.AddComponent<SpinningCoinIcon>();
            if (swing) coin.EnableSwing();
            return img;
        }

        // A static copy of a shared coin render (the price tags, the treasure piles, the flyers).
        private RawImage CoinCopy(Transform parent, Texture tex, float size)
        {
            var go = new GameObject("CoinCopy", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<RawImage>(); img.raycastTarget = false;
            img.texture = tex != null ? tex : UISprites.Circle().texture;
            if (tex == null || LegacyStyle) img.color = UIFactory.Gold;
            return img;
        }

        private BalanceView TrackBalance(TMP_Text text, RectTransform punchRt)
        {
            var b = new BalanceView { text = text, punchRt = punchRt };
            _balances.Add(b);
            return b;
        }

        // Pop the children of a list in, one after another.
        private void PopInChildren(Transform list, float stagger = .045f, float startDelay = 0f)
        {
            if (list == null || SaveData.IsReducedEffects()) return;
            int n = 0;
            for (int i = 0; i < list.childCount; i++)
            {
                var rt = list.GetChild(i) as RectTransform;
                if (rt == null || !rt.gameObject.activeSelf) continue;
                PopInAfter(rt, startDelay + n++ * stagger);
            }
        }

        // Pop one element in after a delay.
        private void PopInAfter(RectTransform rt, float delay)
        {
            if (rt == null || SaveData.IsReducedEffects()) return;
            // Not `??`: a missing Unity component is a fake null that `??` lets through.
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; rt.localScale = Vector3.one * .82f;
            _popIns.RemoveAll(p => p.rt == rt);
            _popIns.Add(new CardPop { rt = rt, cg = cg, delay = delay });
        }

        // Turn a layout Row into a card: panel backing, padding.
        // Cosmetic rows are 64 tall (48 swatch + card padding) and 354 wide.
        private const float ShopRowHeight = 64f, ShopRowWidth = 354f;
        private const float ShopVisibleRows = 5.5f; // the half row peeking out says "scroll me"

        // A fixed-height scrolling list for a long shop catalog, so rows stay full size
        // instead of UIScreenFit shrinking the whole shop to fit every item. Returns the
        // content column; `view` is the frame to show or hide with its tab.
        private RectTransform ShopScrollList(Transform parent, float spacing, out GameObject view)
        {
            const float barGap = 10f, barWidth = 8f;
            var holder = new GameObject("ShopList", typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(parent, false);
            var le = holder.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = ShopRowWidth + barGap + barWidth;
            le.preferredHeight = le.minHeight = ShopVisibleRows * (ShopRowHeight + spacing) - spacing;
            view = holder;

            var viewport = new GameObject("ShopListViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
            viewport.transform.SetParent(holder.transform, false);
            var viewRt = (RectTransform)viewport.transform;
            UIFactory.Stretch(viewRt);
            viewRt.offsetMax = new Vector2(-(barGap + barWidth), 0f);
            // A transparent graphic receives drags between the rows.
            viewport.GetComponent<Image>().color = Color.clear;

            var content = _ui.Column(viewRt, spacing);
            content.name = "ShopListContent";
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewRt; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic; scroll.elasticity = .12f;
            scroll.scrollSensitivity = 28f;

            var sbGo = new GameObject("ShopListScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(holder.transform, false);
            var rt = (RectTransform)sbGo.transform;
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, .5f);
            rt.sizeDelta = new Vector2(barWidth, 0f); rt.anchoredPosition = Vector2.zero;
            sbGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, .06f);
            var area = new GameObject("SlidingArea", typeof(RectTransform));
            area.transform.SetParent(rt, false); UIFactory.Stretch((RectTransform)area.transform);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(area.transform, false);
            var hrt = (RectTransform)handle.transform; UIFactory.Stretch(hrt);
            var sb = sbGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = hrt; sb.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = sb;
            // Always shown (when the list overflows) so players know there is more below.
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            sbGo.AddComponent<JuicyScrollbar>().Setup(UIFactory.Gold, new Color(1f, 1f, 1f, .07f), 4f, 8f);
            return content;
        }

        private static void ResetShopScroll(RectTransform content)
        {
            if (content == null) return;
            var scroll = content.GetComponentInParent<ScrollRect>(true);
            if (scroll == null) return;
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void MakeCard(RectTransform row, bool highlight = false)
        {
            var img = row.gameObject.AddComponent<Image>();
            img.sprite = _ui.Fill(); img.type = Image.Type.Sliced; img.raycastTarget = false;
            img.color = highlight ? Color.Lerp(UIFactory.Panel, UIFactory.Gold, .18f) : UIFactory.Panel;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null) h.padding = new RectOffset(10, 10, 8, 8);
            var v = row.GetComponent<VerticalLayoutGroup>();
            if (v != null) v.padding = new RectOffset(10, 10, 12, 12);
        }

        // A price button with a coin glyph. Affordable ones are gold and shimmer;
        // unaffordable ones are dimmed but still tappable (to explain why).
        private Button PriceButton(Transform parent, int price, System.Action buy, float width, float height, float font)
        {
            var btn = _ui.Button(parent, price.ToString("N0"), buy, UIFactory.Gold, UIFactory.Bg, width, height, font);
            var label = btn.GetComponentInChildren<TMP_Text>();
            label.rectTransform.offsetMin += new Vector2(18f, 0f);
            var coin = CoinCopy(btn.transform, _shopCoinTex, height * .5f);
            var crt = coin.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, .5f); crt.pivot = new Vector2(0f, .5f);
            crt.anchoredPosition = new Vector2(10f, 0f);
            btn.gameObject.AddComponent<RectMask2D>();
            var glintGo = new GameObject("Glint", typeof(RectTransform), typeof(Image));
            glintGo.transform.SetParent(btn.transform, false);
            var grt = (RectTransform)glintGo.transform;
            grt.sizeDelta = new Vector2(34f, height * 2.2f); grt.localRotation = Quaternion.Euler(0, 0, -20f);
            var gimg = glintGo.GetComponent<Image>(); gimg.sprite = UISprites.SoftGlow(); gimg.raycastTarget = false;
            gimg.color = new Color(1f, 1f, 1f, .55f);
            glintGo.SetActive(false);
            _glints.Add(new Glint { rt = grt, img = gimg, phase = _glints.Count * .07f });
            StylePrice(btn, price);
            return btn;
        }

        private void StylePrice(Button btn, int price)
        {
            if (btn == null) return;
            bool can = _gm.Shillings >= price;
            if (btn.targetGraphic is Image img) img.color = can ? UIFactory.Gold : Color.Lerp(UIFactory.Panel, Color.black, .25f);
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = can ? UIFactory.Bg : UIFactory.Muted;
            var glint = btn.transform.Find("Glint");
            if (glint != null) glint.GetComponent<Image>().enabled = can;
        }

        // ── Effects ──────────────────────────────────────────────────────────
        private Vector2 LayerPoint(RectTransform layer, Vector3 world) => layer.InverseTransformPoint(world);

        // The button the player just pressed (Unity selects it on click).
        private static RectTransform PressedButton()
        {
            var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return go != null ? go.transform as RectTransform : null;
        }

        private void FlyCoins(RectTransform layer, Texture tex, Vector3 fromWorld, Vector3 toWorld, int count,
                              float burst, BalanceView land = null, int totalValue = 0)
        {
            if (layer == null) return;
            bool reduced = SaveData.IsReducedEffects();
            if (reduced) count = Mathf.Min(count, 3);
            Vector2 a = LayerPoint(layer, fromWorld), b = LayerPoint(layer, toWorld);
            int left = totalValue;
            for (int i = 0; i < count; i++)
            {
                var f = GetFlyer(layer, tex);
                f.a = a + Random.insideUnitCircle * 14f;
                f.b = b + Random.insideUnitCircle * 6f;
                Vector2 outward = Random.insideUnitCircle.normalized * Random.Range(.5f, 1f) * burst;
                f.c = Vector2.Lerp(f.a, f.b, .3f) + outward + Vector2.up * burst * .6f;
                f.t = 0f; f.delay = i * (burst > 150f ? .028f : .04f); f.dur = Random.Range(.5f, .7f);
                f.size = Random.Range(26f, 36f); f.spin = Random.Range(-1f, 1f) * 360f;
                f.land = land;
                f.value = i == count - 1 ? left : totalValue / count;
                left -= f.value;
                f.active = true;
                f.rt.gameObject.SetActive(true);
                f.rt.localScale = Vector3.zero;
            }
        }

        private void AnimateFlyer(Flyer f, float dt)
        {
            f.t += dt;
            float u = (f.t - f.delay) / f.dur;
            if (u < 0f) return;
            if (u >= 1f || f.rt == null)
            {
                f.active = false;
                if (f.rt != null) f.rt.gameObject.SetActive(false);
                if (f.land != null) { f.land.held = Mathf.Max(0, f.land.held - f.value); f.land.punch = 1f; }
                if (Time.unscaledTime - _lastCoinClink > .07f) { _lastCoinClink = Time.unscaledTime; SfxManager.Play(Sfx.CoinLand); }
                return;
            }
            // Quadratic Bezier: bursts out toward the control point, then homes in, speeding up.
            float e = u * u * (3f - 2f * u);
            Vector2 p = (1 - e) * (1 - e) * f.a + 2 * (1 - e) * e * f.c + e * e * f.b;
            f.rt.anchoredPosition = p;
            float s = Mathf.Min(1f, u * 6f) * Mathf.Lerp(1f, .7f, u);
            f.rt.localScale = new Vector3(s * Mathf.Abs(Mathf.Cos(u * 3f + f.spin * .01f)) * .6f + s * .4f, s, 1f);
            f.rt.sizeDelta = new Vector2(f.size, f.size);
        }

        private Flyer GetFlyer(RectTransform layer, Texture tex)
        {
            foreach (var f in _flyers) if (!f.active && f.layer == layer) { ((RawImage)f.g).texture = tex; return f; }
            var img = CoinCopy(layer, tex, 30f);
            img.gameObject.name = "FlyingCoin";
            var fl = new Flyer { rt = img.rectTransform, g = img, layer = layer };
            _flyers.Add(fl);
            return fl;
        }

        private void SparkBurst(RectTransform layer, Vector3 world, int count, Color a, Color b, float power = 1f)
        {
            if (layer == null || SaveData.IsReducedEffects()) return;
            Vector2 at = LayerPoint(layer, world);
            for (int i = 0; i < count; i++)
            {
                Spark s = null;
                foreach (var x in _sparks) if (!x.active && x.layer == layer) { s = x; break; }
                if (s == null)
                {
                    var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(layer, false);
                    var img = go.GetComponent<Image>(); img.sprite = UISprites.SoftGlow(); img.raycastTarget = false;
                    s = new Spark { rt = (RectTransform)go.transform, img = img, layer = layer };
                    _sparks.Add(s);
                }
                float ang = Random.value * Mathf.PI * 2f;
                s.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(160f, 520f) * power + Vector2.up * 120f;
                s.maxLife = s.life = Random.Range(.4f, .8f);
                s.size = Random.Range(12f, 26f);
                s.gravity = -520f;
                s.color = Color.Lerp(a, b, Random.value);
                s.rt.anchoredPosition = at;
                s.rt.SetAsLastSibling();
                s.active = true; s.rt.gameObject.SetActive(true);
            }
        }

        private void AnimateSpark(Spark s, float dt)
        {
            s.life -= dt;
            if (s.life <= 0f) { s.active = false; s.rt.gameObject.SetActive(false); return; }
            s.vel *= Mathf.Exp(-2.4f * dt);
            s.vel.y += s.gravity * dt;
            s.rt.anchoredPosition += s.vel * dt;
            float k = s.life / s.maxLife;
            s.rt.sizeDelta = Vector2.one * s.size * (.4f + .6f * k);
            var c = s.color; c.a = Mathf.Clamp01(k * 1.5f); s.img.color = c;
        }

        // A soft flash that blooms at a point (purchases).
        private void FlashAt(RectTransform layer, Vector3 world, Color color, float size)
        {
            if (layer == null) return;
            var go = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(layer, false);
            var img = go.GetComponent<Image>(); img.sprite = UISprites.SoftGlow(); img.raycastTarget = false;
            img.color = color;
            img.rectTransform.anchoredPosition = LayerPoint(layer, world);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            _flashes.Add((img, 0f));
            // Flashes are cheap one-offs; clean up the finished ones so they don't pile up.
            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                var child = layer.GetChild(i);
                if (child.name == "Flash" && !child.gameObject.activeSelf) Destroy(child.gameObject);
            }
        }

        // Rising, popping text ("UNLOCKED!", "+1", "+25,000").
        private void StampAt(RectTransform layer, Vector3 world, string text, Color color, float size, float dur = .9f)
        {
            if (layer == null) return;
            Stamp s = null;
            foreach (var x in _stamps) if (!x.active && x.layer == layer) { s = x; break; }
            if (s == null)
            {
                var go = new GameObject("Stamp", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(layer, false);
                var t = go.GetComponent<TextMeshProUGUI>();
                t.font = LegacyStyle ? UIFactory.PixelFont() : UIFactory.ComicFont();
                t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
                t.raycastTarget = false; t.outlineWidth = .2f; t.outlineColor = new Color32(0x1A, 0x0E, 0x04, 0xFF);
                t.rectTransform.sizeDelta = new Vector2(420f, 80f);
                s = new Stamp { text = t, layer = layer };
                _stamps.Add(s);
            }
            s.text.text = text; s.text.fontSize = size; s.text.color = color;
            s.at = LayerPoint(layer, world); s.t = 0f; s.dur = dur; s.active = true;
            s.text.rectTransform.SetAsLastSibling();
            s.text.gameObject.SetActive(true);
            AnimateStamp(s, 0f);
        }

        private void AnimateStamp(Stamp s, float dt)
        {
            s.t += dt;
            float u = s.t / s.dur;
            if (u >= 1f) { s.active = false; s.text.gameObject.SetActive(false); return; }
            float pop = Mathf.LerpUnclamped(2.2f, 1f, EaseOutBackUI(Mathf.Clamp01(u / .22f)));
            s.text.rectTransform.localScale = Vector3.one * pop;
            s.text.rectTransform.anchoredPosition = s.at + Vector2.up * (30f + 70f * u * u);
            s.text.alpha = Mathf.Clamp01((1f - u) / .3f);
        }

        private void WobbleRow(RectTransform rt)
        {
            if (rt == null || SaveData.IsReducedEffects()) return;
            _wobbles.RemoveAll(w => w.rt == rt);
            _wobbles.Add(new Wobble { rt = rt });
        }

        // Card a pressed button lives in (the row/column that holds it).
        private static RectTransform CardOf(RectTransform button)
            => button != null ? button.parent as RectTransform : null;

        // ── Shop feedback ────────────────────────────────────────────────────
        // A purchase: coins pour from the balance into the item, which flashes and stamps.
        private void ShopPurchaseFx(RectTransform button, string stamp)
        {
            if (_marketFx == null || button == null) return;
            Vector3 target = button.position;
            Vector3 from = _marketBalanceCoin != null ? _marketBalanceCoin.position : target;
            FlyCoins(_marketFx, _shopCoinTex, from, target, 7, 90f);
            FlashAt(_marketFx, target, new Color(1f, .85f, .4f, .9f), 160f);
            SparkBurst(_marketFx, target, 22, UIFactory.Gold, Color.white);
            StampAt(_marketFx, target, stamp, UIFactory.Gold, 34f);
            Haptics.Play(Haptics.Purchase, Haptics.PurchaseAmp);
        }

        // Can't afford it: the card wobbles, the price flashes red, and the + button nudges.
        private void ShopDenyFx(RectTransform button, int price)
        {
            WobbleRow(CardOf(button));
            if (button != null && button.GetComponent<Button>() is Button b && b.targetGraphic is Image img)
            {
                img.color = UIFactory.Error;
                StartCoroutine(RestorePrice(b, price));
            }
            if (_marketBuyShillings != null) WobbleRow(_marketBuyShillings);
            int need = price - _gm.Shillings;
            _marketMsg.color = UIFactory.Error;
            _marketMsg.text = "You need " + need.ToString("N0") + " more shillings. Tap + to get more.";
            Haptics.Play(Haptics.Deny, Haptics.DenyAmp);
        }

        // ── Treasury feedback ────────────────────────────────────────────────
        // Shillings bought: the card bursts, a shower of coins arcs up and pours into the
        // counter (which holds the new amount back and counts up as they land), the rays
        // spin up, and a big "+25,000" rises.
        private void TreasuryCelebrate(RectTransform card, int amount)
        {
            if (_treasuryFx == null) return;
            Vector3 from = card.position;
            Vector3 to = _shillingsShopBalance != null ? _shillingsShopBalance.rectTransform.position : from;
            int coins = Mathf.Clamp(8 + amount / 5000, 8, 30);
            if (_treasuryBalance != null) _treasuryBalance.held += amount;
            FlyCoins(_treasuryFx, _treasuryCoinTex, from, to, coins, 260f, _treasuryBalance, amount);
            FlashAt(_treasuryFx, from, new Color(1f, .85f, .4f, .95f), 220f);
            FlashAt(_treasuryFx, to, new Color(1f, .95f, .7f, .7f), 260f);
            SparkBurst(_treasuryFx, from, 34, UIFactory.Gold, Color.white, 1.3f);
            StampAt(_treasuryFx, from, "+" + amount.ToString("N0"), UIFactory.Gold, 44f, 1.3f);
            WobbleRow(card);
            _spinBoost = 1f;
            Haptics.Play(Haptics.Jackpot, Haptics.JackpotAmp, strong: true);
        }

        private System.Collections.IEnumerator RestorePrice(Button b, int price)
        {
            yield return new WaitForSecondsRealtime(.35f);
            StylePrice(b, price);
        }
    }
}
