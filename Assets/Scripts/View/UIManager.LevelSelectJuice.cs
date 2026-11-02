using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Adventure level select life (modern style), built from the shop/settings toolkit:
    //   - gold motes drifting behind, and a swaying 3D gold crown beside the title
    //   - the world label punches and the tiles pop in one after another on open and on
    //     every page flip (ordered from the side you flipped toward)
    //   - earned stars pop in after their tile, with a small spark on each
    //   - the next level to play breathes and sits on a pulsing gold glow
    //   - three-star tiles get a shimmer that sweeps across now and then
    //   - every button squashes when pressed
    //   - tapping a locked tile, or Next at a locked world, wobbles it with a buzz
    public partial class UIManager
    {
        private RectTransform _lsFx, _lsTitleRt;
        private RectTransform _lsNextGlow;
        private float _lsTime, _lsPunch;
        private readonly List<(RectTransform rt, float delay)> _lsStarPops = new List<(RectTransform, float)>();
        private float _lsStarClock;

        // Called at the end of BuildLevelSelect.
        private void SetupLevelSelectJuice(Transform screen, TMP_Text title)
        {
            if (LegacyStyle || screen == null) return;
            AddShopMotes(screen, 16);

            if (title != null)
            {
                float w = title.GetPreferredValues(title.text, 1000f, 100f).x;
                var holder = new GameObject("Crown", typeof(RectTransform));
                holder.transform.SetParent(title.rectTransform, false);
                var hrt = (RectTransform)holder.transform;
                hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(.5f, .5f);
                hrt.sizeDelta = new Vector2(38f, 38f); hrt.anchoredPosition = new Vector2(-(w * .5f + 28f), 2f);
                AddModelIcon(holder.transform, "IconModels/CrownGold", 38f); // gentle sway, like the other icons
            }

            foreach (var b in screen.GetComponentsInChildren<Button>(true))
                if (b.GetComponent<PressSquash>() == null) b.gameObject.AddComponent<PressSquash>();

            _lsTitleRt = _levelPageLabel != null ? _levelPageLabel.rectTransform : null;
            _lsFx = ShopFxLayer(screen);
        }

        // From RenderLevelSelect, after the tiles are built. dir: -1 flipped back, +1
        // forward, 0 opened the screen.
        private void LevelSelectRendered(int dir, LevelData nextToPlay)
        {
            if (LegacyStyle || _levelRow == null) return;
            _breathers.RemoveAll(b => b.rt == null);
            _lsStarPops.Clear();
            _lsStarClock = 0f;
            _lsNextGlow = null;
            _lsPunch = 1f;

            // Collect the tile cells in reading order, then pop them in from the flip side.
            var cells = new List<RectTransform>();
            for (int r = 0; r < _levelRow.childCount; r++)
            {
                var row = _levelRow.GetChild(r);
                for (int c = 0; c < row.childCount; c++) cells.Add((RectTransform)row.GetChild(c));
            }
            bool reduced = SaveData.IsReducedEffects();
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                int col = i % 3;
                int order = dir < 0 ? (2 - col) + (i / 3) : i; // flipping back sweeps right to left
                float delay = .05f + order * .055f;
                PopInAfter(cell, delay);

                var tile = cell.GetComponentInChildren<Button>(true);
                var stars = cell.childCount > 1 ? cell.GetChild(1) as RectTransform : null;
                if (tile == null) continue;
                var tileRt = (RectTransform)tile.transform;
                if (tile.GetComponent<PressSquash>() == null) tile.gameObject.AddComponent<PressSquash>();

                // Earned stars pop in after their tile lands.
                int earned = 0;
                if (stars != null)
                    for (int s = 0; s < stars.childCount; s++)
                    {
                        var star = (RectTransform)stars.GetChild(s);
                        var img = star.GetComponent<Graphic>(); // RawImage (3D star) or Image
                        bool got = img != null && img.color.r > .5f; // unearned stars are drawn dim
                        if (!got) continue;
                        earned++;
                        if (!reduced) { star.localScale = Vector3.zero; _lsStarPops.Add((star, delay + .22f + s * .09f)); }
                    }

                if (earned >= 3) AddTileGlint(tile, i);
            }

            // The next level to play: a breathing tile on a pulsing glow.
            if (nextToPlay != null)
                foreach (var cell in cells)
                {
                    if (cell.name != "Lvl_" + nextToPlay.Id) continue;
                    var tile = cell.GetComponentInChildren<Button>(true);
                    if (tile == null) break;
                    var glow = new GameObject("NextGlow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    glow.GetComponent<LayoutElement>().ignoreLayout = true;
                    glow.transform.SetParent(cell, false);
                    glow.transform.SetAsFirstSibling();
                    var grt = (RectTransform)glow.transform;
                    grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(.5f, .5f);
                    grt.sizeDelta = new Vector2(150f, 150f);
                    var gi = glow.GetComponent<Image>(); gi.sprite = UISprites.SoftGlow(); gi.raycastTarget = false;
                    gi.color = new Color(1f, .82f, .4f, 0f);
                    _lsNextGlow = grt;
                    _breathers.Add((cell, 0f)); // the cell, so it never fights the tile's press squash
                    break;
                }
        }

        // A shimmer sweeping across a three-star tile, rippling across the grid.
        private void AddTileGlint(Button tile, int index)
        {
            if (tile.GetComponent<RectMask2D>() == null) tile.gameObject.AddComponent<RectMask2D>();
            var g = new GameObject("Glint", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(tile.transform, false);
            var grt = (RectTransform)g.transform;
            grt.sizeDelta = new Vector2(30f, 84f * 2.2f); grt.localRotation = Quaternion.Euler(0, 0, -20f);
            var img = g.GetComponent<Image>(); img.sprite = UISprites.SoftGlow(); img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, .55f);
            g.SetActive(false);
            _glints.Add(new Glint { rt = grt, img = img, phase = index * .07f, range = 70f });
        }

        // Locked tile or locked world: shake it and buzz.
        private void LevelSelectDenied(RectTransform rt)
        {
            if (LegacyStyle) return;
            WobbleRow(rt);
            Haptics.Play(Haptics.Deny, Haptics.DenyAmp);
        }

        private void UpdateLevelSelectJuice()
        {
            if (_levelSelect == null || !_levelSelect.activeSelf || LegacyStyle) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _lsTime += dt;
            bool reduced = SaveData.IsReducedEffects();

            // World label punch on open / flip.
            _lsPunch = Mathf.Max(0f, _lsPunch - dt / .3f);
            if (_lsTitleRt != null) _lsTitleRt.localScale = Vector3.one * (1f + (reduced ? 0f : .16f * Mathf.Sin(_lsPunch * Mathf.PI)));

            // Stars pop in with a spark each.
            _lsStarClock += dt;
            for (int i = _lsStarPops.Count - 1; i >= 0; i--)
            {
                var (rt, delay) = _lsStarPops[i];
                if (rt == null) { _lsStarPops.RemoveAt(i); continue; }
                float u = Mathf.Clamp01((_lsStarClock - delay) / .28f);
                if (u <= 0f) continue;
                if (rt.localScale.x == 0f && _lsFx != null)
                    SparkBurst(_lsFx, rt.position, 4, UIFactory.Gold, Color.white, .35f);
                rt.localScale = Vector3.one * Mathf.Max(.001f, Mathf.LerpUnclamped(0f, 1f, EaseOutBackUI(u)));
                if (u >= 1f) { rt.localScale = Vector3.one; _lsStarPops.RemoveAt(i); }
            }

            // The next-level glow pulses.
            if (_lsNextGlow != null)
            {
                float p = .5f + .5f * Mathf.Sin(_lsTime * 2.6f);
                var img = _lsNextGlow.GetComponent<Image>();
                img.color = new Color(1f, .82f, .4f, reduced ? .25f : .22f + .28f * p);
                _lsNextGlow.localScale = Vector3.one * (reduced ? 1f : .92f + .12f * p);
            }
        }
    }
}
