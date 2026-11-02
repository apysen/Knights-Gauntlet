using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    // Customization screen life.
    //   1. A showcase stage at the top: the previewed skin's 3D knight (or the VFX's
    //      loop) over turning rays and a glow in the item's colour. It breathes, and a
    //      tap makes it rear up with sparks, like the menu knight.
    //   2. Tapping a tile tries it on: the showcase swaps to that item with a punch,
    //      and a gold selector frame glides to the tile.
    //   3. The equipped tile has a gold rim and a slow pulsing glow.
    //   4. Tiles pop in one after another when the screen or a tab opens; the tab
    //      bar glides under the active tab; gold motes drift behind.
    //   5. The item popup springs open from its tile. Equip and Buy burst sparks and
    //      stamp "EQUIPPED!" / "UNLOCKED!" on the showcase; not enough shillings
    //      wobbles the popup.
    public partial class UIManager
    {
        private RectTransform _customShowcase, _customShowSlot, _customShowRays, _customShowGlowRt, _customTabBar,
            _customFx, _customSelector, _customSelectorTarget, _customEquippedGlow;
        private Image _customShowGlow;
        private TMP_Text _customShowName, _customShowInfo;
        private CanvasGroup _customPopupCg;
        private Color _customShowColor = Color.white;
        private float _customTime, _customPunch, _customRayAngle, _customRayBoost, _customPopupT = 1f;
        private string _customShownId;

        private bool CustomJuiceOn => _customShowcase != null;

        // Called from BuildCustomization after the title (modern style only).
        private void BuildCustomShowcase(Transform col)
        {
            _customShowcase = _customShowSlot = _customShowRays = _customShowGlowRt = _customTabBar = null;
            _customFx = _customSelector = _customSelectorTarget = _customEquippedGlow = null;
            _customShownId = null;

            var go = new GameObject("Showcase", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(col, false);
            var le = go.GetComponent<LayoutElement>(); le.preferredWidth = 380f; le.preferredHeight = 210f;
            _customShowcase = (RectTransform)go.transform;

            var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(_customShowcase, false);
            _customShowGlowRt = (RectTransform)glow.transform;
            _customShowGlowRt.sizeDelta = new Vector2(260f, 220f); _customShowGlowRt.anchoredPosition = new Vector2(0f, 18f);
            _customShowGlow = glow.GetComponent<Image>(); _customShowGlow.sprite = UISprites.SoftGlow(); _customShowGlow.raycastTarget = false;

            var rays = new GameObject("Rays", typeof(RectTransform), typeof(Image));
            rays.transform.SetParent(_customShowcase, false);
            _customShowRays = (RectTransform)rays.transform;
            _customShowRays.sizeDelta = new Vector2(250f, 250f); _customShowRays.anchoredPosition = new Vector2(0f, 18f);
            var raysImg = rays.GetComponent<Image>(); raysImg.sprite = GameView.RallyRays(); raysImg.raycastTarget = false;
            raysImg.color = new Color(1f, .85f, .45f, .22f);

            // A soft pedestal shadow under the knight.
            var shadow = new GameObject("Pedestal", typeof(RectTransform), typeof(Image));
            shadow.transform.SetParent(_customShowcase, false);
            var srt = (RectTransform)shadow.transform;
            srt.sizeDelta = new Vector2(150f, 26f); srt.anchoredPosition = new Vector2(0f, -52f);
            var simg = shadow.GetComponent<Image>(); simg.sprite = UISprites.SoftGlow(); simg.raycastTarget = false;
            simg.color = new Color(0f, 0f, 0f, .55f);

            // The tap target / item slot. Its pivot sits at the knight's feet so the
            // breathing and rearing scale up from the ground.
            var slot = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(_customShowcase, false);
            _customShowSlot = (RectTransform)slot.transform;
            _customShowSlot.pivot = new Vector2(.5f, 0f);
            _customShowSlot.sizeDelta = new Vector2(136f, 136f); _customShowSlot.anchoredPosition = new Vector2(0f, -56f);
            slot.GetComponent<Image>().color = Color.clear;
            var btn = slot.GetComponent<Button>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OnShowcaseTapped);

            _customShowName = TextIn(_customShowcase, "", 20, UIFactory.Gold, UIFactory.ComicFont(), -175f, 28f);
            _customShowName.characterSpacing = 2f;
            _customShowInfo = TextIn(_customShowcase, "", 12, UIFactory.Muted, UIFactory.BodyFont(), -197f, 20f);
        }

        // Called at the end of BuildCustomization.
        private void FinishCustomJuice(Transform screen, Transform tabs)
        {
            if (!CustomJuiceOn) return;
            AddShopMotes(screen, 14);
            // AddShopMotes puts them first; keep the dismiss catcher below the content but above the motes.
            if (_skinPopupCatcher != null) _skinPopupCatcher.transform.SetSiblingIndex(1);
            _customTabBar = CreateTabBar(tabs);
            foreach (var b in tabs.GetComponentsInChildren<Button>()) b.gameObject.AddComponent<PressSquash>();

            var sel = new GameObject("Selector", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            sel.transform.SetParent(screen, false);
            sel.GetComponent<LayoutElement>().ignoreLayout = true;
            _customSelector = (RectTransform)sel.transform;
            _customSelector.sizeDelta = new Vector2(76f, 76f);
            var simg = sel.GetComponent<Image>(); simg.sprite = _ui.Border(); simg.type = Image.Type.Sliced; simg.raycastTarget = false;
            simg.color = UIFactory.Gold;
            sel.SetActive(false);

            _customFx = ShopFxLayer(screen);
            // A solid card with a gold rim, so the tiles underneath don't show through.
            var popImg = _skinPopup.GetComponent<Image>();
            var solid = new GameObject("Solid", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            solid.transform.SetParent(_skinPopup.transform, false); solid.transform.SetAsFirstSibling();
            solid.GetComponent<LayoutElement>().ignoreLayout = true;
            UIFactory.Stretch((RectTransform)solid.transform);
            var solidImg = solid.GetComponent<Image>(); solidImg.sprite = UISprites.RoundedFill(); solidImg.type = Image.Type.Sliced;
            solidImg.color = UIFactory.Hex("#161B2C"); solidImg.raycastTarget = false;
            if (popImg != null) popImg.color = new Color(0f, 0f, 0f, 0f);
            var rim = new GameObject("Rim", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rim.transform.SetParent(solid.transform, false);
            rim.GetComponent<LayoutElement>().ignoreLayout = true;
            UIFactory.Stretch((RectTransform)rim.transform);
            var rimImg = rim.GetComponent<Image>(); rimImg.sprite = UISprites.RoundedBorder(); rimImg.type = Image.Type.Sliced;
            rimImg.color = new Color(UIFactory.Gold.r, UIFactory.Gold.g, UIFactory.Gold.b, .8f); rimImg.raycastTarget = false;
            _customPopupCg = _skinPopup.GetComponent<CanvasGroup>();
            if (_customPopupCg == null) _customPopupCg = _skinPopup.AddComponent<CanvasGroup>();
        }

        // A gold bar under a row of tabs that glides to the active one (see GlideTabBar).
        private RectTransform CreateTabBar(Transform tabs)
        {
            var bar = new GameObject("TabIndicator", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            bar.transform.SetParent(tabs, false);
            bar.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(60f, 4f);
            var img = bar.GetComponent<Image>(); img.color = UIFactory.Gold; img.raycastTarget = false;
            return rt;
        }

        private static void GlideTabBar(RectTransform bar, Button tab, float dt)
        {
            if (bar == null || tab == null) return;
            var trt = (RectTransform)tab.transform;
            var target = new Vector2(trt.anchoredPosition.x, -4f);
            float w = trt.rect.width - 16f;
            float k = 1f - Mathf.Exp(-16f * dt);
            bar.anchoredPosition = bar.anchoredPosition.y == 0f ? target : Vector2.Lerp(bar.anchoredPosition, target, k);
            bar.sizeDelta = new Vector2(Mathf.Lerp(bar.sizeDelta.x, w, k), 4f);
        }

        // ── Grid hooks ───────────────────────────────────────────────────────
        // Per tile: press squash, and the equipped tile's rim and glow.
        private void DecorateCustomTile(Button btn, bool equipped)
        {
            if (!CustomJuiceOn) return;
            btn.gameObject.AddComponent<PressSquash>();
            if (!equipped) return;
            UIFactory.StyleToggle(btn, true);
            var g = new GameObject("EquipGlow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            g.transform.SetParent(btn.transform, false); g.transform.SetAsFirstSibling();
            g.GetComponent<LayoutElement>().ignoreLayout = true;
            _customEquippedGlow = (RectTransform)g.transform;
            UIFactory.Stretch(_customEquippedGlow);
            _customEquippedGlow.offsetMin = new Vector2(-6f, -6f); _customEquippedGlow.offsetMax = new Vector2(6f, 6f);
            var img = g.GetComponent<Image>(); img.sprite = UISprites.SoftGlow(); img.raycastTarget = false;
            img.color = new Color(1f, .82f, .4f, .35f);
            // The selector starts on the equipped tile.
            _customSelectorTarget = (RectTransform)btn.transform;
        }

        // After a grid rebuild: show the equipped item, and pop the tiles in if asked.
        private void CustomGridBuilt(bool popIn)
        {
            if (!CustomJuiceOn) return;
            if (_customTab == 0) { var s = Skins.Find(SaveData.GetSkin()); if (s != null) ShowcaseSkin(s, false); }
            else foreach (var v in Vfx.Grid()) if (v.Id == SaveData.GetVfx()) { ShowcaseVfx(v, false); break; }
            if (_customSelector != null)
            {
                _customSelector.gameObject.SetActive(_customSelectorTarget != null);
                if (_customSelectorTarget != null) SnapSelector();
            }
            if (!popIn) return;
            PopInAfter(_customShowcase, 0f);
            float delay = .08f;
            foreach (Transform row in _customGrid) { PopInChildren(row, .035f, delay); delay += row.childCount * .035f; }
            if (_customSelector != null) _customSelector.gameObject.SetActive(false); // reappears once the tiles land
            _customSelectorDelay = delay + .2f;
        }
        private float _customSelectorDelay;

        private void SnapSelector()
        {
            if (_customSelector == null || _customSelectorTarget == null) return;
            Canvas.ForceUpdateCanvases();
            _customSelector.position = _customSelectorTarget.position;
        }

        // A tile was tapped (before its popup opens): try the item on.
        private void CustomTileTapped(RectTransform tile, SkinInfo skin, VfxInfo vfx)
        {
            if (!CustomJuiceOn) return;
            _customSelectorTarget = tile;
            if (_customSelector != null && !_customSelector.gameObject.activeSelf)
            {
                _customSelector.gameObject.SetActive(true);
                _customSelector.position = tile.position;
            }
            if (skin != null) ShowcaseSkin(skin, true); else if (vfx != null) ShowcaseVfx(vfx, true);
            Haptics.Play(Haptics.Select, Haptics.SelectAmp);
            _customPopupT = 0f;
            if (_skinPopup != null)
            {
                // Keep the popup on screen above edge tiles.
                var prt = (RectTransform)_skinPopup.transform;
                var canvasRt = prt.parent as RectTransform;
                if (canvasRt != null)
                {
                    float half = canvasRt.rect.width * .5f - prt.rect.width * .5f - 10f;
                    var lp = prt.localPosition; lp.x = Mathf.Clamp(lp.x, -half, half); prt.localPosition = lp;
                }
                prt.localScale = Vector3.one * .6f;
            }
            if (_customPopupCg != null) _customPopupCg.alpha = 0f;
            if (_skinPopupInfo != null) _skinPopupInfo.color = UIFactory.Muted;
        }

        private void ShowcaseSkin(SkinInfo s, bool fx)
        {
            bool owned = Skins.IsOwned(s.Id);
            bool mystery = s.Source == SkinSource.Mastery && !owned;
            string key = "skin:" + s.Id + (owned ? ":o" : "");
            if (key == _customShownId && !fx) { ShowcaseText(s.Name, mystery, owned, SaveData.GetSkin() == s.Id, SkinRequirement(s)); return; }
            bool changed = key != _customShownId;
            _customShownId = key;
            if (changed)
            {
                ClearShowcaseSlot();
                if (mystery || !AddSkinKnight(_customShowSlot, s.Id, 136f, owned ? 1f : .4f, 220f, 1f, .58f))
                    ShowcaseGlyph(mystery ? "?" : "♞", mystery ? UIFactory.Muted : s.Color);
                Center(_customShowSlot);
            }
            _customShowColor = mystery ? new Color(.5f, .5f, .6f) : Color.Lerp(Skins.GlowOf(s.Id), new Color(1f, .82f, .4f), .35f);
            ShowcaseText(s.Name, mystery, owned, SaveData.GetSkin() == s.Id, SkinRequirement(s));
            if (fx && changed) ShowcasePop(.6f);
        }

        private void ShowcaseVfx(VfxInfo v, bool fx)
        {
            bool owned = Vfx.IsOwned(v.Id);
            string key = "vfx:" + v.Id + (owned ? ":o" : "");
            bool changed = key != _customShownId;
            _customShownId = key;
            if (changed)
            {
                ClearShowcaseSlot();
                if (v.Id == Vfx.NoneId || !AddVfxLoop(_customShowSlot, v, 128f)) ShowcaseGlyph("-", UIFactory.Muted);
                else if (!owned) _customShowSlot.GetChild(0).GetComponent<Image>().color = new Color(1f, 1f, 1f, .4f);
                Center(_customShowSlot);
            }
            _customShowColor = new Color(.72f, .55f, 1f);
            ShowcaseText(v.Name, false, owned, SaveData.GetVfx() == v.Id, v.Price.ToString("N0") + " shillings");
            if (fx && changed) ShowcasePop(.6f);
        }

        // Children of the slot sit centred on its middle, not on its feet pivot.
        private static void Center(RectTransform slot)
        {
            foreach (Transform c in slot)
            {
                var rt = (RectTransform)c;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        private void ShowcaseText(string name, bool mystery, bool owned, bool equipped, string requirement)
        {
            _customShowName.text = mystery ? "???" : name.ToUpperInvariant();
            _customShowInfo.text = equipped ? "EQUIPPED" : owned ? "Owned · tap Equip to wear it" : requirement;
            _customShowInfo.color = equipped ? UIFactory.Gold : owned ? UIFactory.Text : UIFactory.Muted;
        }

        private void ClearShowcaseSlot()
        {
            for (int i = _customShowSlot.childCount - 1; i >= 0; i--) Destroy(_customShowSlot.GetChild(i).gameObject);
        }

        private void ShowcaseGlyph(string glyph, Color color)
        {
            var t = _ui.Label(_customShowSlot, glyph, 96, color, FontStyles.Normal, 136);
            if (glyph == "♞") t.font = _theme != null && _theme.glyphFont != null ? _theme.glyphFont : UIFactory.ChessFont();
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            var le = t.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = true;
            t.rectTransform.sizeDelta = new Vector2(136f, 136f);
        }

        private void ShowcasePop(float power)
        {
            _customPunch = 1f;
            _customRayBoost = Mathf.Max(_customRayBoost, power);
            if (_customFx == null) return;
            Vector3 at = _customShowSlot.TransformPoint(new Vector3(0f, 68f, 0f));
            SparkBurst(_customFx, at, Mathf.RoundToInt(10 * power) + 4, _customShowColor, Color.white, .6f + .4f * power);
        }

        private void OnShowcaseTapped()
        {
            if (!CustomJuiceOn) return;
            ShowcasePop(.8f);
            if (_customFx != null) FlashAt(_customFx, _customShowSlot.TransformPoint(new Vector3(0f, 68f, 0f)), new Color(_customShowColor.r, _customShowColor.g, _customShowColor.b, .55f), 170f);
            Haptics.Play(Haptics.Purchase, Haptics.PurchaseAmp);
            if (_customTab == 0) SfxManager.Play(Sfx.KnightTap);
        }

        // Equip / buy outcomes (called after the grid rebuilds).
        private void CustomEquippedFx(bool bought)
        {
            if (!CustomJuiceOn || _customFx == null) return;
            Vector3 at = _customShowSlot.TransformPoint(new Vector3(0f, 68f, 0f));
            _customPunch = 1f;
            _customRayBoost = 1f;
            FlashAt(_customFx, at, new Color(1f, .85f, .45f, .9f), bought ? 240f : 180f);
            SparkBurst(_customFx, at, bought ? 34 : 20, UIFactory.Gold, _customShowColor, bought ? 1.3f : .9f);
            StampAt(_customFx, at + Vector3.up * 10f, bought ? "UNLOCKED!" : "EQUIPPED!", UIFactory.Gold, bought ? 40f : 34f);
            if (bought) SfxManager.Play(Sfx.CoinLand);
            else if (_customTab == 0) SfxManager.Play(Sfx.KnightTap);
            Haptics.Play(bought ? Haptics.Jackpot : Haptics.Purchase, bought ? Haptics.JackpotAmp : Haptics.PurchaseAmp, strong: bought);
        }

        private void CustomDenyFx()
        {
            if (_skinPopup != null) WobbleRow((RectTransform)_skinPopup.transform);
            if (_skinPopupInfo != null) _skinPopupInfo.color = UIFactory.Error;
            Haptics.Play(Haptics.Deny, Haptics.DenyAmp);
        }

        // ── Per frame ────────────────────────────────────────────────────────
        private void UpdateCustomJuice()
        {
            if (_custom == null || !_custom.activeSelf || !CustomJuiceOn) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _customTime += dt;
            bool reduced = SaveData.IsReducedEffects();

            GlideTabBar(_customTabBar, _customTab == 0 ? _tabSkins : _tabVfx, dt);

            // Showcase: rays turn (faster after a pop), the item breathes and rears up.
            _customRayBoost = Mathf.Max(0f, _customRayBoost - dt * .8f);
            if (!reduced) _customRayAngle = Mathf.Repeat(_customRayAngle + (10f + 220f * _customRayBoost * _customRayBoost) * dt, 360f);
            if (_customShowRays != null) _customShowRays.localRotation = Quaternion.Euler(0f, 0f, -_customRayAngle);
            _customPunch = Mathf.Max(0f, _customPunch - dt / .45f);
            if (_customShowSlot != null)
            {
                float breath = reduced ? 0f : Mathf.Sin(_customTime * 1.6f);
                float p = Mathf.Sin(_customPunch * Mathf.PI);
                _customShowSlot.localScale = new Vector3(1f + .012f * breath - .06f * p, 1f + .022f * breath + .12f * p, 1f);
                _customShowSlot.localRotation = Quaternion.Euler(0f, 0f, 8f * p * Mathf.Sin(_customPunch * 14f));
            }
            if (_customShowGlow != null)
            {
                var target = new Color(_customShowColor.r, _customShowColor.g, _customShowColor.b,
                    .30f + (reduced ? 0f : .08f * Mathf.Sin(_customTime * 2f)) + .25f * Mathf.Sin(_customPunch * Mathf.PI));
                _customShowGlow.color = Color.Lerp(_customShowGlow.color, target, 1f - Mathf.Exp(-8f * dt));
            }

            // Equipped tile glow pulse.
            if (_customEquippedGlow != null)
                _customEquippedGlow.GetComponent<Image>().color = new Color(1f, .82f, .4f, .28f + (reduced ? 0f : .16f * Mathf.Sin(_customTime * 2.6f)));

            // Selector glides to the tapped tile and breathes.
            if (_customSelector != null && _customSelectorTarget != null)
            {
                if (_customSelectorDelay > 0f)
                {
                    _customSelectorDelay -= dt;
                    if (_customSelectorDelay <= 0f) { _customSelector.gameObject.SetActive(true); _customSelector.position = _customSelectorTarget.position; }
                }
                else
                {
                    _customSelector.position = Vector3.Lerp(_customSelector.position, _customSelectorTarget.position, 1f - Mathf.Exp(-18f * dt));
                    float k = reduced ? 0f : .5f + .5f * Mathf.Sin(_customTime * 4f);
                    _customSelector.localScale = Vector3.one * (1f + .04f * k);
                    _customSelector.GetComponent<Image>().color = new Color(UIFactory.Gold.r, UIFactory.Gold.g, UIFactory.Gold.b, .6f + .4f * k);
                }
            }

            // The popup springs open from its tile.
            if (_skinPopup != null && _skinPopup.activeSelf && _customPopupT < 1f)
            {
                _customPopupT = reduced ? 1f : Mathf.Min(1f, _customPopupT + dt / .24f);
                _skinPopup.transform.localScale = Vector3.one * Mathf.LerpUnclamped(.6f, 1f, EaseOutBackUI(_customPopupT));
                if (_customPopupCg != null) _customPopupCg.alpha = Mathf.Clamp01(_customPopupT * 3f);
            }
        }
    }
}
