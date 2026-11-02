using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Main menu life (modern style). Ambient loops are slow and quiet, since players
    // sit on this screen; the bursts are saved for taps.
    //   1. The knight breathes, a sunburst turns behind its crest, and a tap makes it rear
    //      up with a spark burst.
    //   2. Play has a heartbeat pulse, a glow and a shimmer, and squashes when pressed.
    //   3. A faint chessboard drifts diagonally behind everything, with gold motes rising.
    //   4. On first launch and when coming back from a run, the crest, 3D title, buttons
    //      and nav pop in one after another (the title's letters drop in).
    //   5. The 3D title (MenuTitle3D) waves and shines on its own.
    //   6. Daily Challenge glows with a pulsing dot while attempts are left; the wallet
    //      counts up (with sparks) when you return richer.
    //   7. Shop / Customize / Scores carry 3D icons and bounce when pressed.
    public partial class UIManager
    {
        private RectTransform _menuRays, _menuKnightArt, _menuPlayRt, _menuPlayGlow, _menuDailyDot, _menuDailyGlow, _menuFx, _menuWalletRt;
        private RawImage _menuChecker;
        private MenuTitle3D _menuTitle3D;
        private PressSquash _menuPlaySquash;
        private BalanceView _menuBalance;
        private float _menuTime, _knightPunch, _menuLastBalance = -1f;
        private bool _menuEntrancePending = true;
        private static Texture2D _checkerTex;

        // The 3D title, or the old text title when the model is missing or legacy style is on.
        private void BuildMenuTitle(Transform col)
        {
            if (!LegacyStyle)
            {
                var go = new GameObject("Title3D", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
                go.transform.SetParent(col, false);
                var le = go.GetComponent<LayoutElement>(); le.preferredWidth = 390f; le.preferredHeight = 171f;
                var title = go.AddComponent<MenuTitle3D>();
                if (title.Initialize()) { _menuTitle3D = title; return; }
                Destroy(go);
            }
            _ui.Label(col, "KNIGHT'S GAUNTLET", 38, UIFactory.Gold, FontStyles.Normal, 390);
        }

        // Called at the end of the modern BuildMenu.
        private void SetupMenuJuice(Transform screen, Button play, Button[] squashy, Button[] nav, RectTransform wallet, TMP_Text walletText)
        {
            // 3. Drifting chessboard and motes, behind everything.
            var checker = new GameObject("Chessboard", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            checker.transform.SetParent(screen, false);
            checker.GetComponent<LayoutElement>().ignoreLayout = true;
            UIFactory.Stretch((RectTransform)checker.transform);
            _menuChecker = checker.GetComponent<RawImage>();
            _menuChecker.texture = CheckerTexture(); _menuChecker.raycastTarget = false;
            _menuChecker.color = new Color(UIFactory.Gold.r, UIFactory.Gold.g, UIFactory.Gold.b, .035f);
            AddShopMotes(screen, 16);
            checker.transform.SetAsFirstSibling();

            // 1. Sunburst behind the crest, and the knight as a tap target.
            var hero = _menuHero;
            var rays = new GameObject("Rays", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rays.transform.SetParent(hero, false);
            rays.transform.SetAsFirstSibling();
            rays.GetComponent<LayoutElement>().ignoreLayout = true;
            _menuRays = (RectTransform)rays.transform;
            _menuRays.sizeDelta = new Vector2(330f, 330f);
            var raysImg = rays.GetComponent<Image>(); raysImg.sprite = GameView.RallyRays(); raysImg.raycastTarget = false;
            raysImg.color = new Color(1f, .8f, .35f, .30f);
            _menuKnightArt = (RectTransform)hero.Find("Knight");
            var hit = hero.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var heroBtn = hero.gameObject.AddComponent<Button>(); heroBtn.transition = Selectable.Transition.None;
            heroBtn.onClick.AddListener(OnKnightTapped);

            // 2. Play: glow, shimmer, heartbeat (scale driven here, squash from PressSquash).
            _menuPlayRt = (RectTransform)play.transform;
            var glow = new GameObject("PlayGlow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            glow.transform.SetParent(_menuPlayRt.parent, false);
            glow.transform.SetSiblingIndex(_menuPlayRt.GetSiblingIndex());
            glow.GetComponent<LayoutElement>().ignoreLayout = true;
            _menuPlayGlow = (RectTransform)glow.transform;
            var glowImg = glow.GetComponent<Image>(); glowImg.sprite = UISprites.SoftGlow(); glowImg.raycastTarget = false;
            AddGlint(play, 68f);
            _menuPlaySquash = play.gameObject.AddComponent<PressSquash>();
            _menuPlaySquash.External = true;
            foreach (var b in squashy) if (b != null) b.gameObject.AddComponent<PressSquash>();

            // 6. Daily: a pulsing dot at the label's end and a soft glow behind the button.
            if (_dailyBtn != null)
            {
                var dailyRt = (RectTransform)_dailyBtn.transform;
                var dg = new GameObject("DailyGlow", typeof(RectTransform), typeof(Image));
                dg.transform.SetParent(dailyRt, false); dg.transform.SetAsFirstSibling();
                _menuDailyGlow = (RectTransform)dg.transform;
                UIFactory.Stretch(_menuDailyGlow); _menuDailyGlow.offsetMin = new Vector2(-30f, -16f); _menuDailyGlow.offsetMax = new Vector2(30f, 16f);
                var dgi = dg.GetComponent<Image>(); dgi.sprite = UISprites.SoftGlow(); dgi.raycastTarget = false;
                var dot = new GameObject("NewDot", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(dailyRt, false);
                _menuDailyDot = (RectTransform)dot.transform;
                _menuDailyDot.anchorMin = _menuDailyDot.anchorMax = new Vector2(1f, .5f);
                _menuDailyDot.sizeDelta = new Vector2(10f, 10f); _menuDailyDot.anchoredPosition = new Vector2(-22f, 0f);
                var di = dot.GetComponent<Image>(); di.sprite = UISprites.Circle(); di.color = UIFactory.Gold; di.raycastTarget = false;
            }

            // 7. Nav icons above the labels.
            string[] icons = { "IconModels/NavShop", "IconModels/NavCustomize", "IconModels/NavScores" };
            for (int i = 0; i < nav.Length && i < icons.Length; i++)
            {
                var b = nav[i];
                if (b == null) continue;
                b.gameObject.AddComponent<PressSquash>();
                var le = b.GetComponent<LayoutElement>(); le.preferredHeight = le.minHeight = 70f;
                var label = b.GetComponentInChildren<TMP_Text>();
                label.rectTransform.offsetMin = new Vector2(4f, 2f); label.rectTransform.offsetMax = new Vector2(-4f, -40f);
                label.fontSizeMax = 14f * UIFactory.FontScale;
                var holder = new GameObject("IconSlot", typeof(RectTransform));
                holder.transform.SetParent(b.transform, false);
                var hrt = (RectTransform)holder.transform;
                hrt.anchorMin = hrt.anchorMax = new Vector2(.5f, 1f); hrt.pivot = new Vector2(.5f, 1f);
                hrt.sizeDelta = new Vector2(40f, 40f); hrt.anchoredPosition = new Vector2(0f, -2f);
                AddModelIcon(holder.transform, icons[i], 40f);
            }

            // 6. Wallet: tweened count, sparks when it grows.
            _menuWalletRt = wallet;
            _menuBalance = TrackBalance(walletText, wallet);
            _menuFx = ShopFxLayer(screen);
        }

        private void OnKnightTapped()
        {
            if (_menuReveal != null) return;
            _knightPunch = 1f;
            if (_menuFx != null && _menuHero != null)
            {
                SparkBurst(_menuFx, _menuHero.position, 18, UIFactory.Gold, Color.white, .9f);
                FlashAt(_menuFx, _menuHero.position, new Color(1f, .85f, .45f, .6f), 190f);
            }
            _spinBoost = Mathf.Max(_spinBoost, .6f);
            Haptics.Play(Haptics.Purchase, Haptics.PurchaseAmp);
            OnKnightTapAudio();
        }

        // Audio hook points for the menu (implemented by the audio pass).
        private void OnKnightTapAudio() => SfxManager.Play(Sfx.KnightTap);
        private void OnMenuEntranceAudio()
        {
            // A soft swell under the pop-ins; the tinks follow the title letters landing
            // (15 letters, 45 ms apart), so they only play when the letters actually drop.
            SfxManager.Play(Sfx.MenuEntrance);
            if (_menuTitle3D != null && !SaveData.IsReducedEffects()) SfxManager.Play(Sfx.MenuTitleTinks);
        }

        // Before the menu is re-shown: remember whether we are coming back from a run.
        private void NoteMenuEntrance()
        {
            bool fromRun = (_hud != null && _hud.activeSelf) || (_gameOver != null && _gameOver.activeSelf) ||
                           (_pause != null && _pause.activeSelf);
            if (fromRun) _menuEntrancePending = true;
        }

        // After the menu is shown: play the entrance if one is due.
        private void PlayMenuEntranceIfDue()
        {
            if (LegacyStyle || _menuColumn == null) return;
            if (_menuRays != null) _menuRays.gameObject.SetActive(true);
            if (_menuKnightArt != null) { _menuKnightArt.localScale = Vector3.one; _menuKnightArt.localRotation = Quaternion.identity; }
            if (!_menuEntrancePending) return;
            _menuEntrancePending = false;
            PopInChildren(_menuColumn, .06f);
            if (_menuNavRt != null) PopInChildren(_menuNavRt, .05f);
            if (_menuTitle3D != null) _menuTitle3D.PlayEntrance();
            OnMenuEntranceAudio();
        }

        // Play pressed: freeze the ambient motion so the circle reveal owns the knight.
        private void PrepareMenuForReveal()
        {
            if (_menuRays != null) _menuRays.gameObject.SetActive(false);
            if (_menuKnightArt != null) { _menuKnightArt.localScale = Vector3.one; _menuKnightArt.localRotation = Quaternion.identity; }
            if (_menuPlayRt != null) _menuPlayRt.localScale = Vector3.one;
        }

        private void UpdateMenuJuice()
        {
            if (_menu == null || !_menu.activeSelf || LegacyStyle) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _menuTime += dt;
            if (_menuReveal != null) return; // the Play reveal is animating the knight
            bool reduced = SaveData.IsReducedEffects();

            // 3. The chessboard drifts diagonally.
            if (_menuChecker != null)
            {
                var r = ((RectTransform)_menuChecker.transform).rect;
                float cells = Mathf.Max(1f, r.width / 120f);
                float drift = reduced ? 0f : _menuTime * .025f;
                _menuChecker.uvRect = new Rect(drift, drift, cells / 2f, cells / 2f * r.height / Mathf.Max(1f, r.width));
            }

            // 1. Rays turn; the knight breathes, and rears up when tapped.
            if (_menuRays != null) _menuRays.localRotation = Quaternion.Euler(0f, 0f, -(_menuTime * 9f + _spinBoost * _spinBoost * 200f));
            _knightPunch = Mathf.Max(0f, _knightPunch - dt / .45f);
            if (_menuKnightArt != null)
            {
                float breath = reduced ? 0f : Mathf.Sin(_menuTime * 1.6f);
                float p = Mathf.Sin(_knightPunch * Mathf.PI);
                _menuKnightArt.localScale = new Vector3(1f + .012f * breath - .05f * p, 1f + .022f * breath + .14f * p, 1f);
                _menuKnightArt.localRotation = Quaternion.Euler(0f, 0f, 9f * p * Mathf.Sin(_knightPunch * 14f));
            }

            // 2. Play's heartbeat: two quick beats, then rest (every 1.8 s).
            if (_menuPlayRt != null)
            {
                float t = Mathf.Repeat(_menuTime, 1.8f);
                float beat = reduced ? 0f : Mathf.Max(Pulse(t, .0f), .6f * Pulse(t, .22f));
                float squash = _menuPlaySquash != null ? _menuPlaySquash.Factor : 1f;
                _menuPlayRt.localScale = Vector3.one * (1f + .035f * beat) * squash;
                if (_menuPlayGlow != null)
                {
                    _menuPlayGlow.position = _menuPlayRt.position;
                    _menuPlayGlow.sizeDelta = _menuPlayRt.rect.size + new Vector2(90f, 70f) * (1f + .25f * beat);
                    _menuPlayGlow.GetComponent<Image>().color = new Color(1f, .8f, .35f, .16f + .14f * beat);
                }
            }

            // 6. Daily dot pulses while attempts remain.
            bool dailyOpen = SaveData.DailyAttemptsLeft() > 0;
            if (_menuDailyDot != null)
            {
                _menuDailyDot.gameObject.SetActive(dailyOpen);
                _menuDailyGlow.gameObject.SetActive(dailyOpen);
                if (dailyOpen)
                {
                    float k = .5f + .5f * Mathf.Sin(_menuTime * 3.4f);
                    _menuDailyDot.localScale = Vector3.one * (1f + (reduced ? 0f : .35f * k));
                    _menuDailyGlow.GetComponent<Image>().color = new Color(1f, .8f, .35f, .06f + (reduced ? 0f : .08f * k));
                }
            }

            // 6. Wallet: sparks when the balance has grown since we last saw it.
            int balance = _gm.Shillings;
            if (_menuLastBalance >= 0f && balance > _menuLastBalance && _menuFx != null && _menuWalletRt != null)
            {
                SparkBurst(_menuFx, _menuWalletRt.position, 14, UIFactory.Gold, Color.white, .6f);
                SfxManager.Play(Sfx.CoinLand);
            }
            _menuLastBalance = balance;
        }

        private static float Pulse(float t, float at)
        {
            float u = (t - at) / .18f;
            return u < 0f || u > 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        // A shimmer that sweeps across a button now and then (shared with the shop).
        private void AddGlint(Button btn, float height)
        {
            var clip = UIFactory.ShapeClip(btn.transform, btn.targetGraphic as Image);
            var glintGo = new GameObject("Glint", typeof(RectTransform), typeof(Image));
            glintGo.transform.SetParent(clip, false);
            var grt = (RectTransform)glintGo.transform;
            grt.sizeDelta = new Vector2(44f, height * 2.2f); grt.localRotation = Quaternion.Euler(0, 0, -20f);
            var gimg = glintGo.GetComponent<Image>(); gimg.sprite = UISprites.SoftGlow(); gimg.raycastTarget = false;
            gimg.color = new Color(1f, 1f, 1f, .6f);
            glintGo.SetActive(false);
            _glints.Add(new Glint { rt = grt, img = gimg, phase = .5f, range = 190f });
        }

        private static Texture2D CheckerTexture()
        {
            if (_checkerTex != null) return _checkerTex;
            const int n = 64;
            _checkerTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color(1f, 1f, 1f, ((x < n / 2) ^ (y < n / 2)) ? 1f : 0f);
            _checkerTex.SetPixels(px); _checkerTex.Apply();
            return _checkerTex;
        }
    }

    // A press squash for a button: shrinks on touch, springs back with a bounce on release.
    // With External set, it only computes Factor and the owner applies it (the Play button
    // combines it with its heartbeat).
    public sealed class PressSquash : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool External;
        public float Factor { get; private set; } = 1f;
        private bool _down;
        private float _vel;

        public void OnPointerDown(PointerEventData e) { _down = true; }
        public void OnPointerUp(PointerEventData e) { _down = false; _vel = 4f; }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float target = _down ? .9f : 1f;
            // A damped spring so release overshoots a little.
            _vel += (target - Factor) * 260f * dt;
            _vel *= Mathf.Exp(-16f * dt);
            Factor += _vel * dt;
            // At rest it leaves the scale alone, so entrance pop-ins can animate it.
            bool resting = !_down && Mathf.Abs(Factor - 1f) < .002f && Mathf.Abs(_vel) < .01f;
            if (resting) { if (Factor != 1f) { Factor = 1f; _vel = 0f; if (!External) transform.localScale = Vector3.one; } return; }
            if (!External) transform.localScale = Vector3.one * Factor;
        }

        private void OnDisable() { Factor = 1f; _vel = 0f; _down = false; if (!External) transform.localScale = Vector3.one; }
    }
}
