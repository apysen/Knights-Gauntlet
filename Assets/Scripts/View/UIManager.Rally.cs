using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KnightsGauntlet.View
{
    // The War Horn "rally": the screen-space half of the powerup's animation (the board
    // half is GameView.Rally). Both read the same clock through GameManager.RallyTime.
    //
    //   0.00  gold flare flashes in from the screen edges
    //   0.06  a dark title ribbon snaps open across the screen
    //   0.10  "RALLY!" slams down onto it, oversized, then settles
    //   0.26  the multiplier appears under the ribbon and counts up, bumping on each step
    //   1.05  it lands on the peak: the number punches, flashes white, the card shakes,
    //         and the edges flare again
    //   1.38  the card lifts and fades away
    public partial class UIManager
    {
        private GameObject _rallyCanvas;
        private Image _rallyVignette, _rallyGlint;
        private RectTransform _rallyGroup, _rallyRibbon, _rallyGlintRt;
        private CanvasGroup _rallyGroupAlpha;
        private TMP_Text _rallyTitle, _rallySubtitle, _rallyCount;
        private int _rallyCountShown = -1;
        private float _rallyBump, _rallyLastT = -1f;
        private bool _rallySlamSeen;
        private static Sprite _rallyEdgeSprite;
        private readonly Image[] _rallyRims = new Image[2];

        // The same card also announces the other powerups (no count, no slam, shorter),
        // on its own unscaled clock. A War Horn rally takes the card over if both overlap.
        private const float CardDuration = 1.3f, CardExit = .95f;
        private float _cardT = -1f;
        private int _overlayMode; // 0 none, 1 War Horn rally, 2 powerup card
        private string _cardTitle, _cardSubtitle;
        private Color _cardAccent, _cardFlare;

        public void PlayPowerupCard(PowerupId id)
        {
            switch (id)
            {
                case PowerupId.Chronokinesis:
                    _cardTitle = "TIME BENDS"; _cardSubtitle = "THE WORLD SLOWS FOR " + Mathf.RoundToInt(Powerups.Chronokinesis.Duration) + " SECONDS";
                    _cardAccent = UIFactory.Hex("#8FEAFF"); _cardFlare = new Color(.45f, .9f, 1f); break;
                case PowerupId.DivineIntervention:
                    _cardTitle = "BLESSED!"; _cardSubtitle = "THE GREEN SQUARE IS YOUR BEST MOVE";
                    _cardAccent = UIFactory.Hex("#FFF1C4"); _cardFlare = new Color(1f, .95f, .75f); break;
                case PowerupId.DivinePath:
                    _cardTitle = "PATH REVEALED"; _cardSubtitle = "FOLLOW THE GREEN STEPS";
                    _cardAccent = UIFactory.Hex("#F2C46B"); _cardFlare = new Color(.95f, .75f, .4f); break;
                case PowerupId.Berserker:
                    _cardTitle = "FURY!"; _cardSubtitle = "EVERY CAPTURE BUILDS 5X THE MULTIPLIER";
                    _cardAccent = UIFactory.Hex("#FF8A4A"); _cardFlare = new Color(1f, .3f, .1f); break;
                default: return; // the War Horn runs its own rally
            }
            _cardT = 0f; _overlayMode = 0; // forces the card to re-dress on the next update
        }

        private const float RallyGroupY = 150f; // above centre, clear of the knight's usual rows

        private void UpdateRallyOverlay()
        {
            float rallyT = _gm != null ? _gm.RallyTime : -1f;
            bool rally = rallyT >= 0f;
            if (rally) _cardT = -1f;
            else if (_cardT >= 0f) { _cardT += Time.unscaledDeltaTime; if (_cardT > CardDuration) _cardT = -1f; }
            float t = rally ? rallyT : _cardT;
            if (t < 0f)
            {
                if (_rallyCanvas != null && _rallyCanvas.activeSelf) _rallyCanvas.SetActive(false);
                _rallyLastT = -1f; _overlayMode = 0;
                return;
            }
            if (_rallyCanvas == null) BuildRallyOverlay();
            if (!_rallyCanvas.activeSelf) _rallyCanvas.SetActive(true);
            int mode = rally ? 1 : 2;
            if (_rallyLastT < 0f || t < _rallyLastT || mode != _overlayMode)
            {
                _rallyCountShown = -1; _rallySlamSeen = false; _rallyBump = 0f; _overlayMode = mode;
                DressCard(rally);
            }
            _rallyLastT = t;
            bool reduced = SaveData.IsReducedEffects();
            float dt = Time.unscaledDeltaTime;

            // ── Count + slam detection (rally only) ──────────────────────────
            int shown = rally ? _gm.RallyShown : 0;
            bool slam = rally && t >= GameView.RallyCountEnd;
            float sinceSlam = slam ? t - GameView.RallyCountEnd : 99f;
            if (rally && shown != _rallyCountShown)
            {
                _rallyCountShown = shown;
                _rallyCount.text = "X" + shown;
                _rallyBump = 1f;
            }
            if (slam && !_rallySlamSeen) { _rallySlamSeen = true; _rallySubtitle.text = "PEAK STREAK RESTORED"; }
            _rallyBump = Mathf.Max(0f, _rallyBump - dt / .12f);

            // ── Edge flare ───────────────────────────────────────────────────
            float flare = 0f;
            if (!reduced)
            {
                flare = t < .06f ? t / .06f * .85f : .85f * Mathf.Pow(Mathf.Clamp01(1f - (t - .06f) / .55f), 2f);
                if (sinceSlam < .55f) flare = Mathf.Max(flare, .95f * Mathf.Pow(1f - sinceSlam / .55f, 2f));
            }
            _rallyVignette.enabled = flare > .001f;
            Color flareColor = !rally ? _cardFlare : slam ? new Color(1f, .9f, .6f) : new Color(1f, .74f, .22f);
            _rallyVignette.color = WithAlphaUI(flareColor, flare);

            // ── Title card ───────────────────────────────────────────────────
            float open = EaseOutBackUI(Mathf.Clamp01((t - .06f) / .22f));
            _rallyRibbon.localScale = new Vector3(reduced ? 1f : open, 1f, 1f);

            float u = Mathf.Clamp01((t - .10f) / .24f);
            float titleScale = reduced ? 1f : Mathf.LerpUnclamped(2.6f, 1f, EaseOutBackUI(u));
            if (sinceSlam < .3f && !reduced) titleScale += .12f * (1f - sinceSlam / .3f);
            _rallyTitle.rectTransform.localScale = Vector3.one * titleScale;
            _rallyTitle.alpha = Mathf.Clamp01(u * 4f);
            _rallySubtitle.alpha = Mathf.Clamp01((t - .32f) / .2f);

            // A glint sweeps across the ribbon once it is open, and again on the slam.
            float sweep = Mathf.InverseLerp(.30f, .62f, t);
            if (slam) sweep = Mathf.InverseLerp(0f, .3f, sinceSlam);
            bool glint = !reduced && sweep > 0f && sweep < 1f;
            _rallyGlint.enabled = glint;
            if (glint) _rallyGlintRt.anchoredPosition = new Vector2(Mathf.Lerp(-340f, 340f, sweep), 0f);

            // ── The multiplier ───────────────────────────────────────────────
            float countIn = Mathf.Clamp01((t - .26f) / .16f);
            float countScale = (reduced ? 1f : Mathf.Lerp(.4f, 1f, EaseOutBackUI(countIn))) + .22f * _rallyBump;
            if (sinceSlam < .4f && !reduced) countScale += .9f * Mathf.Pow(1f - sinceSlam / .4f, 2f);
            _rallyCount.rectTransform.localScale = Vector3.one * countScale;
            if (!rally) countIn = 0f;
            _rallyCount.alpha = countIn;
            _rallyCount.color = WithAlphaUI(sinceSlam < .35f ? Color.Lerp(Color.white, UIFactory.Gold, sinceSlam / .35f) : UIFactory.Gold, countIn);

            // ── Card shake on the slam, then lift and fade out ───────────────
            float exit = rally ? Mathf.Clamp01((t - 1.38f) / .37f) : Mathf.Clamp01((t - CardExit) / (CardDuration - CardExit));
            Vector2 shake = Vector2.zero;
            if (sinceSlam < .3f && !reduced) shake = Random.insideUnitCircle * 12f * (1f - sinceSlam / .3f);
            _rallyGroup.anchoredPosition = new Vector2(0f, RallyGroupY + 40f * exit * exit) + shake;
            _rallyGroup.localScale = Vector3.one * (1f + .12f * exit);
            _rallyGroupAlpha.alpha = 1f - exit;
        }

        // Text and colours for the current mode: the War Horn's gold, or the powerup's own.
        private void DressCard(bool rally)
        {
            Color accent = rally ? UIFactory.Gold : _cardAccent;
            _rallyTitle.text = rally ? "RALLY!" : _cardTitle;
            _rallySubtitle.text = rally ? "YOUR STREAK RIDES AGAIN" : _cardSubtitle;
            _rallyTitle.color = accent;
            foreach (var rim in _rallyRims) if (rim != null) rim.color = accent;
            _rallyCount.text = "";
        }

        private void BuildRallyOverlay()
        {
            var canvas = _ui.CreateCanvas("WarHornRally", 95, new Vector2(HudRefW, HudRefH));
            _rallyCanvas = canvas.gameObject;
            // Purely visual: never swallow taps meant for the board.
            Destroy(_rallyCanvas.GetComponent<GraphicRaycaster>());

            var vignette = new GameObject("EdgeFlare", typeof(RectTransform), typeof(Image));
            vignette.transform.SetParent(_rallyCanvas.transform, false);
            _rallyVignette = vignette.GetComponent<Image>();
            _rallyVignette.sprite = RallyEdgeSprite(); _rallyVignette.raycastTarget = false;
            UIFactory.Stretch(_rallyVignette.rectTransform);

            _rallyGroup = (RectTransform)new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup)).transform;
            _rallyGroup.SetParent(_rallyCanvas.transform, false);
            _rallyGroup.anchorMin = _rallyGroup.anchorMax = _rallyGroup.pivot = new Vector2(.5f, .5f);
            _rallyGroup.sizeDelta = new Vector2(560, 320);
            _rallyGroupAlpha = _rallyGroup.GetComponent<CanvasGroup>();
            _rallyGroupAlpha.blocksRaycasts = false; _rallyGroupAlpha.interactable = false;

            // Ribbon: dark navy band with a gold rim, masking the glint.
            _rallyRibbon = (RectTransform)new GameObject("Ribbon", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).transform;
            _rallyRibbon.SetParent(_rallyGroup, false);
            _rallyRibbon.anchorMin = _rallyRibbon.anchorMax = _rallyRibbon.pivot = new Vector2(.5f, .5f);
            _rallyRibbon.sizeDelta = new Vector2(540, 132); _rallyRibbon.anchoredPosition = new Vector2(0, 40);
            var ribbonImage = _rallyRibbon.GetComponent<Image>();
            ribbonImage.sprite = null; ribbonImage.color = new Color(.03f, .05f, .11f, .88f); ribbonImage.raycastTarget = false;
            for (int edge = 0; edge < 2; edge++)
            {
                var rim = new GameObject(edge == 0 ? "RimTop" : "RimBottom", typeof(RectTransform), typeof(Image));
                rim.transform.SetParent(_rallyRibbon, false);
                var rt = (RectTransform)rim.transform;
                rt.anchorMin = new Vector2(0, edge == 0 ? 1 : 0); rt.anchorMax = new Vector2(1, edge == 0 ? 1 : 0);
                rt.pivot = new Vector2(.5f, edge == 0 ? 1 : 0); rt.sizeDelta = new Vector2(0, 4); rt.anchoredPosition = Vector2.zero;
                var img = rim.GetComponent<Image>(); img.sprite = null; img.color = UIFactory.Gold; img.raycastTarget = false;
                _rallyRims[edge] = img;
            }
            var glintGo = new GameObject("Glint", typeof(RectTransform), typeof(Image));
            glintGo.transform.SetParent(_rallyRibbon, false);
            _rallyGlintRt = (RectTransform)glintGo.transform;
            _rallyGlintRt.anchorMin = _rallyGlintRt.anchorMax = _rallyGlintRt.pivot = new Vector2(.5f, .5f);
            _rallyGlintRt.sizeDelta = new Vector2(90, 220); _rallyGlintRt.localRotation = Quaternion.Euler(0, 0, -18f);
            _rallyGlint = glintGo.GetComponent<Image>();
            _rallyGlint.sprite = UISprites.SoftGlow(); _rallyGlint.color = new Color(1f, .95f, .8f, .55f); _rallyGlint.raycastTarget = false;

            TMP_FontAsset display = UIFactory.ComicFont();
            _rallyTitle = RallyText(_rallyRibbon, "Title", "RALLY!", 78, UIFactory.Gold, display, new Vector2(0, 14), new Vector2(520, 96));
            _rallySubtitle = RallyText(_rallyRibbon, "Subtitle", "YOUR STREAK RIDES AGAIN", 20, UIFactory.Text,
                UIFactory.BodyFont(), new Vector2(0, -44), new Vector2(520, 28));
            _rallyCount = RallyText(_rallyGroup, "Multiplier", "", 104, UIFactory.Gold, display, new Vector2(0, -82), new Vector2(520, 120));
            _rallyCanvas.SetActive(false);
        }

        private static TMP_Text RallyText(Transform parent, string name, string value, float size, Color color,
                                          TMP_FontAsset font, Vector2 position, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .4f;
            text.raycastTarget = false;
            if (size >= 60f)
            {
                // A heavy dark outline so the big lettering pops off any board colour.
                text.outlineWidth = .22f;
                text.outlineColor = new Color32(0x1A, 0x0E, 0x04, 0xFF);
            }
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = box; rt.anchoredPosition = position;
            return text;
        }

        // Transparent in the middle, glowing toward the screen edges (stretched to fit).
        private static Sprite RallyEdgeSprite()
        {
            if (_rallyEdgeSprite != null) return _rallyEdgeSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / n * 2f - 1f), dy = Mathf.Abs((y + .5f) / n * 2f - 1f);
                    // Rounded-rectangle distance to the edge, so the corners glow evenly.
                    float d = Mathf.Pow(Mathf.Pow(dx, 6f) + Mathf.Pow(dy, 6f), 1f / 6f);
                    float a = Mathf.Clamp01((d - .62f) / .38f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px); tex.Apply();
            _rallyEdgeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            return _rallyEdgeSprite;
        }

        private static Color WithAlphaUI(Color c, float a) { c.a = a; return c; }

        private static float EaseOutBackUI(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }
    }
}
