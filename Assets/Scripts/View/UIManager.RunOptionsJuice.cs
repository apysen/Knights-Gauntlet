using System.Collections.Generic;
using TMPro;
using KnightsGauntlet.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // "Your next run" (modern style): mode cards in a swipeable carousel, difficulty buttons
    // with threat pips over a heat bar that slides to the chosen level.
    //
    // Each mode card has an art window in the mode's colour with turning rays and the
    // mode's 3D emblem (crossed swords for Battle, a spiked mine for Trap), its name, a
    // tagline and three keyword chips. Settling on a card selects that mode with a punch,
    // a spark burst and a tap of vibration.
    public partial class UIManager
    {
        private sealed class ModeCard
        {
            public GameMode mode; public RectTransform rt, rays, border; public Image glow; public Color accent; public float punch, rayAngle;
        }

        private ModeCarousel _modeCarousel;
        private readonly List<ModeCard> _modeCards = new List<ModeCard>();
        private RectTransform _runFx, _heatTrack, _modeDots;
        private Material _liquid;
        // Liquid gauge state: level (spring), its velocity, the slosh envelope, and the ember timer.
        private float _heatShown = -1f, _heatVel, _slosh, _emberTimer, _runTime, _descFade = 1f;
        // Liquid animation phases, integrated here so their speed can change without a jump.
        private float _flow, _swirl, _rise, _boilShown;
        private static readonly int FillId = Shader.PropertyToID("_Fill"),
            SloshId = Shader.PropertyToID("_Slosh"), BoilId = Shader.PropertyToID("_Boil"), TId = Shader.PropertyToID("_T"),
            DeepId = Shader.PropertyToID("_Deep"), FlowId = Shader.PropertyToID("_Flow"),
            SwirlId = Shader.PropertyToID("_Swirl"), RiseId = Shader.PropertyToID("_Rise"), BrightId = Shader.PropertyToID("_Bright"), AspectId = Shader.PropertyToID("_Aspect");
        // Liquid colours per level (deep, bright): toxic green, molten gold, lava orange, blood red.
        private static readonly Color[] LiquidDeep =
            { new Color32(0x12, 0x7A, 0x42, 0xFF), new Color32(0xA0, 0x6A, 0x0E, 0xFF), new Color32(0xA8, 0x3A, 0x12, 0xFF), new Color32(0x86, 0x0E, 0x14, 0xFF) };
        private static readonly Color[] LiquidBright =
            { new Color32(0x7C, 0xFF, 0xA8, 0xFF), new Color32(0xFF, 0xE0, 0x7A, 0xFF), new Color32(0xFF, 0xB0, 0x6A, 0xFF), new Color32(0xFF, 0x6A, 0x48, 0xFF) };
        private readonly Dictionary<Difficulty, float> _diffPunch = new Dictionary<Difficulty, float>();

        private static readonly Color[] ThreatColors =
        {
            new Color32(0x3C, 0xDC, 0x78, 0xFF), new Color32(0xF0, 0xC0, 0x40, 0xFF),
            new Color32(0xFF, 0x8A, 0x3C, 0xFF), new Color32(0xE8, 0x3A, 0x3A, 0xFF),
        };

        private void BuildModeCarousel(Transform col)
        {
            var go = new GameObject("ModeCarousel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(col, false);
            var le = go.GetComponent<LayoutElement>(); le.preferredWidth = 360f; le.preferredHeight = 316f;
            go.GetComponent<Image>().color = Color.clear; // catches drags between the cards
            _modeCarousel = go.AddComponent<ModeCarousel>();
            _modeCards.Add(BuildModeCard(GameMode.Battle, "BATTLE", "Capture enemies. Keep your streak alive.",
                "CAPTURE · COMBO · TRANSFORM", UIFactory.Hex("#7A1F24"), UIFactory.Hex("#FF8A5C"), "IconModels/ModeBattle"));
            _modeCards.Add(BuildModeCard(GameMode.Trap, "TRAP", "Keep moving. Avoid the obstacles.",
                "DODGE · SURVIVE · COLLECT", UIFactory.Hex("#1D3566"), UIFactory.Hex("#7FD4FF"), "IconModels/ModeTrap"));
            foreach (var c in _modeCards) _modeCarousel.Add(c.rt);
            _modeCarousel.OnSelected = i => SetMode(_modeCards[i].mode);
            _modeCarousel.ExtraScale = i => 1f + .08f * Mathf.Sin(_modeCards[i].punch * Mathf.PI);

            // Page dots underneath, flanked by Previous / Next arrows.
            var nav = _ui.Row(col, 10f);
            var prev = MenuButton(nav, "‹", () => _modeCarousel.Previous(), 40, 34, 26, textOnly: true);
            _modeDots = _ui.Row(nav, 8f);
            var next = MenuButton(nav, "›", () => _modeCarousel.Next(), 40, 34, 26, textOnly: true);
            prev.GetComponentInChildren<TMP_Text>().color = UIFactory.Gold;
            next.GetComponentInChildren<TMP_Text>().color = UIFactory.Gold;
            for (int i = 0; i < _modeCards.Count; i++)
            {
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                dot.transform.SetParent(_modeDots, false);
                var dle = dot.GetComponent<LayoutElement>(); dle.preferredWidth = 8f; dle.preferredHeight = 8f;
                var img = dot.GetComponent<Image>(); img.sprite = UISprites.Circle(); img.raycastTarget = false;
            }
        }

        private ModeCard BuildModeCard(GameMode mode, string title, string tagline, string chips, Color deep, Color accent, string model)
        {
            var card = new GameObject("Card" + title, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)card.transform;
            rt.sizeDelta = new Vector2(230f, 304f);
            var bg = card.GetComponent<Image>(); bg.sprite = _ui.Fill(); bg.type = Image.Type.Sliced; bg.color = UIFactory.Panel;

            // Soft glow behind the card when it is selected.
            var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(rt, false); glowGo.transform.SetAsFirstSibling();
            var grt = (RectTransform)glowGo.transform; UIFactory.Stretch(grt);
            grt.offsetMin = new Vector2(-46f, -46f); grt.offsetMax = new Vector2(46f, 46f);
            var glow = glowGo.GetComponent<Image>(); glow.sprite = UISprites.SoftGlow(); glow.raycastTarget = false;
            glow.color = new Color(accent.r, accent.g, accent.b, 0f);

            // Art window: the mode's colour, turning rays, the 3D emblem.
            // A stencil Mask (not RectMask2D): it clips to the window's real, rotated shape, so
            // the rays stay inside it when the card tilts in the carousel.
            var art = new GameObject("Art", typeof(RectTransform), typeof(Image), typeof(Mask));
            art.transform.SetParent(rt, false);
            var art_rt = (RectTransform)art.transform;
            art_rt.anchorMin = new Vector2(0f, 1f); art_rt.anchorMax = new Vector2(1f, 1f); art_rt.pivot = new Vector2(.5f, 1f);
            art_rt.sizeDelta = new Vector2(-16f, 168f); art_rt.anchoredPosition = new Vector2(0f, -8f);
            var artImg = art.GetComponent<Image>(); artImg.sprite = _ui.Fill(); artImg.type = Image.Type.Sliced; artImg.color = deep; artImg.raycastTarget = false;
            var rays = new GameObject("Rays", typeof(RectTransform), typeof(Image));
            rays.transform.SetParent(art_rt, false);
            var raysRt = (RectTransform)rays.transform; raysRt.sizeDelta = new Vector2(360f, 360f);
            var raysImg = rays.GetComponent<Image>(); raysImg.sprite = GameView.RallyRays(); raysImg.raycastTarget = false;
            raysImg.color = new Color(accent.r, accent.g, accent.b, .35f);
            var emblem = new GameObject("Emblem", typeof(RectTransform));
            emblem.transform.SetParent(art_rt, false);
            ((RectTransform)emblem.transform).sizeDelta = new Vector2(140f, 140f);
            if (!AddModelIcon(emblem.transform, model, 140f))
            {
                var fallback = _ui.Label(emblem.transform, mode == GameMode.Battle ? "♞" : "✖", 80, accent, width: 140);
                fallback.font = UIFactory.ChessFont();
            }

            var name = TextIn(rt, title, 30, UIFactory.Gold, UIFactory.ComicFont(), -186f, 40f);
            name.characterSpacing = 4f;
            TextIn(rt, tagline, 14, UIFactory.Text, UIFactory.BodyFont(), -224f, 38f).textWrappingMode = TextWrappingModes.Normal;
            var chip = TextIn(rt, chips, 11, accent, UIFactory.BodyFont(), -268f, 20f);
            chip.characterSpacing = 1.5f;

            // Gold rim, shown on the selected card.
            var border = new GameObject("Rim", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(rt, false);
            var brt = (RectTransform)border.transform; UIFactory.Stretch(brt);
            var bimg = border.GetComponent<Image>(); bimg.sprite = _ui.Border(); bimg.type = Image.Type.Sliced; bimg.color = UIFactory.Gold; bimg.raycastTarget = false;
            return new ModeCard { mode = mode, rt = rt, rays = raysRt, border = brt, glow = glow, accent = accent };
        }

        private static TMP_Text TextIn(RectTransform parent, string text, float size, Color color, TMP_FontAsset font, float y, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size * UIFactory.FontScale; t.color = color;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(-24f, height); rt.anchoredPosition = new Vector2(0f, y);
            return t;
        }

        // Threat pips along the bottom of a difficulty button: 1 to 4, green to red.
        private void AddThreatPips(Button b, int level)
        {
            var le = b.GetComponent<LayoutElement>(); le.preferredHeight = le.minHeight = 60f;
            var label = b.GetComponentInChildren<TMP_Text>();
            label.rectTransform.offsetMin = new Vector2(12f, 18f);
            var row = new GameObject("Pips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(b.transform, false);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 9f); rt.sizeDelta = new Vector2(80f, 8f);
            var h = row.GetComponent<HorizontalLayoutGroup>(); h.spacing = 5f; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            for (int i = 0; i < 4; i++)
            {
                var pip = new GameObject("Pip", typeof(RectTransform), typeof(Image));
                pip.transform.SetParent(rt, false);
                ((RectTransform)pip.transform).sizeDelta = new Vector2(12f, 6f);
                var img = pip.GetComponent<Image>(); img.sprite = UISprites.RoundedFill(); img.raycastTarget = false;
                img.color = i <= level ? ThreatColors[level] : new Color(1f, 1f, 1f, .12f);
            }
        }

        private void BuildHeatBar(Transform col)
        {
            const float width = 312f, height = 22f;
            _heatTrack = (RectTransform)new GameObject("Heat", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement)).transform;
            _heatTrack.SetParent(col, false);
            var le = _heatTrack.GetComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = height;
            var img = _heatTrack.GetComponent<RawImage>(); img.raycastTarget = false;
            var shader = Resources.Load<Shader>("LiquidBar");
            if (shader != null && shader.isSupported)
            {
                _liquid = new Material(shader) { name = "Difficulty liquid" };
                _liquid.SetFloat(AspectId, width / height);
                img.material = _liquid;
            }
            else img.color = UIFactory.Hex("#26354A"); // no shader: a plain track
        }

        // Selection feedback (only when the screen is open; the saved choice restores quietly).
        private void RunOptionPicked(Vector3 at, Color color)
        {
            if (_runOptions == null || !_runOptions.activeSelf) return;
            SparkBurst(_runFx, at, 16, color, Color.white, .8f);
            Haptics.Play(Haptics.Select, Haptics.SelectAmp);
        }

        private void UpdateRunOptionsJuice()
        {
            if (_runOptions == null || !_runOptions.activeSelf) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _runTime += dt;
            bool reduced = SaveData.IsReducedEffects();

            float pos = _modeCarousel != null ? _modeCarousel.Position : 0f;
            for (int i = 0; i < _modeCards.Count; i++)
            {
                var c = _modeCards[i];
                float near = 1f - Mathf.Clamp01(Mathf.Abs(i - pos));
                c.punch = Mathf.Max(0f, c.punch - dt / .35f);
                // Accumulate the angle at one steady speed. (Scaling elapsed time by a speed that
                // changes mid-swipe made the rays whip round.)
                if (!reduced) c.rayAngle = Mathf.Repeat(c.rayAngle + 12f * dt, 360f);
                if (c.rays != null) c.rays.localRotation = Quaternion.Euler(0f, 0f, -c.rayAngle);
                var col = c.glow.color; col.a = (.22f + (reduced ? 0f : .1f * Mathf.Sin(_runTime * 2.4f))) * near; c.glow.color = col;
                c.border.gameObject.SetActive(near > .5f);
            }
            if (_modeDots != null)
                for (int i = 0; i < _modeDots.childCount; i++)
                {
                    float near = 1f - Mathf.Clamp01(Mathf.Abs(i - pos));
                    var dot = (RectTransform)_modeDots.GetChild(i);
                    dot.GetComponent<LayoutElement>().preferredWidth = Mathf.Lerp(8f, 22f, near);
                    dot.GetComponent<Image>().color = Color.Lerp(new Color(1f, 1f, 1f, .25f), UIFactory.Gold, near);
                }

            // Description fades and lifts in after a mode change.
            _descFade = Mathf.Min(1f, _descFade + dt / .3f);
            if (_modeDescription != null)
            {
                _modeDescription.alpha = _descFade;
                _modeDescription.rectTransform.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, _descFade);
            }

            UpdateLiquidGauge(dt, reduced);
            foreach (var kv in _diffBtns)
            {
                _diffPunch.TryGetValue(kv.Key, out float p);
                p = Mathf.Max(0f, p - dt / .3f); _diffPunch[kv.Key] = p;
                kv.Value.transform.localScale = Vector3.one * (1f + .1f * Mathf.Sin(p * Mathf.PI));
            }
        }

        // The difficulty gauge as a volatile liquid in a glass tube.
        private void UpdateLiquidGauge(float dt, bool reduced)
        {
            int level = (int)_selDiff;
            float target = (level + 1) / 4f;
            if (_heatShown < 0f) _heatShown = target;
            // An underdamped spring: the level overshoots and settles, so a change sloshes.
            _heatVel += (target - _heatShown) * 90f * dt;
            _heatVel *= Mathf.Exp((reduced ? -20f : -7f) * dt);
            _heatShown = Mathf.Clamp(_heatShown + _heatVel * dt, 0f, 1.04f);
            // Slosh eases in and out (following the spring's speed directly snapped the waves).
            float sloshTarget = Mathf.Clamp01(Mathf.Abs(_heatVel) * .7f);
            _slosh = Mathf.MoveTowards(_slosh, sloshTarget, dt * (sloshTarget > _slosh ? 4f : 1.2f));

            if (_liquid == null) return;
            // Colour follows the liquid level smoothly between the four tiers.
            float tier = Mathf.Clamp(_heatShown * 4f - 1f, 0f, 3f);
            int a = Mathf.FloorToInt(tier), b = Mathf.Min(3, a + 1);
            // Boil eases toward its target (it follows the spring, which overshoots), and the
            // phases advance by speed * dt, so changing difficulty never makes the liquid lurch.
            float boilTarget = reduced ? 0f : Mathf.Clamp01((Mathf.Clamp01(_heatShown) - .75f) * 4f);
            _boilShown = Mathf.MoveTowards(_boilShown, boilTarget, dt * 1.5f);
            _flow += dt * (.6f + _boilShown * 1.4f);
            _swirl += dt * (.5f + _boilShown);
            _rise += dt * (1f + _boilShown * 2f);
            float f = tier - a;
            _liquid.SetColor(DeepId, Color.Lerp(LiquidDeep[a], LiquidDeep[b], f));
            _liquid.SetColor(BrightId, Color.Lerp(LiquidBright[a], LiquidBright[b], f));
            _liquid.SetFloat(FillId, Mathf.Clamp01(_heatShown));
            _liquid.SetFloat(SloshId, reduced ? 0f : _slosh);
            float boil = _boilShown;
            _liquid.SetFloat(BoilId, boil);
            _liquid.SetFloat(TId, _runTime);
            _liquid.SetFloat(FlowId, _flow);
            _liquid.SetFloat(SwirlId, _swirl);
            _liquid.SetFloat(RiseId, _rise);

            // Grandmaster: embers spit off the boiling surface.
            if (!reduced && boil > .5f && _runFx != null && _heatTrack != null)
            {
                _emberTimer -= dt;
                if (_emberTimer <= 0f)
                {
                    _emberTimer = Random.Range(.08f, .2f);
                    var r = _heatTrack.rect;
                    Vector3 front = _heatTrack.TransformPoint(new Vector3(r.xMin + r.width * Mathf.Clamp01(_heatShown) - 6f, r.center.y + Random.Range(-6f, 8f), 0f));
                    SparkBurst(_runFx, front, 2, new Color(1f, .45f, .15f), new Color(1f, .9f, .5f), .35f);
                }
            }
        }

        // Hooks from SetMode / SetDiff.
        private void RunOptionsModeChanged(GameMode m, bool userAction)
        {
            if (_modeCarousel == null) return;
            int i = _modeCards.FindIndex(c => c.mode == m);
            if (i < 0) return;
            bool open = _runOptions != null && _runOptions.activeSelf;
            if (_modeCarousel.Index != i) _modeCarousel.SetIndex(i, instant: !open);
            if (!open) return;
            _modeCards[i].punch = 1f;
            _descFade = 0f;
            RunOptionPicked(_modeCards[i].rt.position, _modeCards[i].accent);
        }

        private void RunOptionsDiffChanged(Difficulty d)
        {
            if (_runOptions == null || !_runOptions.activeSelf) return;
            _diffPunch[d] = 1f;
            if (_diffBtns.TryGetValue(d, out var b)) RunOptionPicked(b.transform.position, ThreatColors[(int)d]);
        }
    }
}
