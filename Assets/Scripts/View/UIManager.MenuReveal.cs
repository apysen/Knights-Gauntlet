using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnightsGauntlet.View
{
    public partial class UIManager
    {
        private RectTransform _menuHero;
        private MenuKnightPreview _menuKnight;
        private Coroutine _menuReveal;

        private IEnumerator RevealFromMenu()
        {
            var shader = Resources.Load<Shader>("MenuCircleReveal");
            if (_menuHero == null || shader == null || !shader.isSupported)
            {
                StartWithTutorial(_selMode, _selDiff);
                yield return null;
                _menuReveal = null;
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            var parent = _menuHero.parent;
            int sibling = _menuHero.GetSiblingIndex();
            int menuOrder = _menuCanvas.sortingOrder;
            Vector3 oldScale = _menuHero.localScale;
            Vector2 oldSize = _menuHero.sizeDelta;
            var corners = new Vector3[4];
            _menuHero.GetWorldCorners(corners);
            Vector2 startScreen = (corners[0] + corners[2]) * .5f;
            float startPixels = Vector3.Distance(corners[0], corners[1]);
            var menuGroup = RevealGroup(_menu);
            var screenImage = _menu.transform.Find("Screen").GetComponent<Image>();
            var paper = _menuHero.Find("Paper").gameObject;
            var inset = _menuHero.Find("CrestInset").gameObject;
            var portrait = _menuKnight.GetComponent<RawImage>();
            var hidden = new List<CanvasGroup>();
            var overlay = _ui.CreateCanvas("MenuToGameReveal", 1000);
            // CanvasScaler normally updates on the next frame. Match the already
            // laid-out menu now so reparenting cannot render one oversized frame.
            overlay.scaleFactor = _menuCanvas.scaleFactor;
            Canvas.ForceUpdateCanvases();
            var overlayRt = (RectTransform)overlay.transform;
            var placeholder = new GameObject("KnightLayoutSpace", typeof(RectTransform), typeof(LayoutElement));
            placeholder.transform.SetParent(parent, false);
            placeholder.transform.SetSiblingIndex(sibling);
            var space = placeholder.GetComponent<LayoutElement>();
            space.preferredWidth = 158; space.preferredHeight = 158;
            var material = new Material(shader) { name = "Menu circle reveal" };
            var cover = new GameObject("ExpandingCircle", typeof(RectTransform), typeof(RawImage));
            cover.transform.SetParent(overlay.transform, false);
            UIFactory.Stretch((RectTransform)cover.transform);
            cover.GetComponent<RawImage>().material = material;
            material.SetColor("_Outside", UIFactory.Bg);
            material.SetColor("_Inside", UIFactory.Panel);
            material.SetColor("_Ring", UIFactory.Gold);
            material.SetFloat("_RingAlpha", 1);
            material.SetFloat("_RingWidth", 2 * overlay.scaleFactor);
            material.SetFloat("_Reveal", 0);
            SetRevealCircle(material, startScreen, startPixels * .5f);

            try
            {
                _gm.MenuTransitionActive = true;
                menuGroup.interactable = false; menuGroup.blocksRaycasts = false;
                _menuHero.SetParent(overlay.transform, false);
                _menuHero.localScale = Vector3.one;
                _menuHero.anchorMin = _menuHero.anchorMax = _menuHero.pivot = Vector2.one * .5f;
                _menuHero.sizeDelta = Vector2.one * startPixels / overlay.scaleFactor;
                SetHeroScreenPosition(overlayRt, startScreen);
                paper.SetActive(false); inset.SetActive(false);
                StartWithTutorial(_selMode, _selDiff);
                // StartWithTutorial also selects the correct HUD/tutorial. Keep it beneath the reveal.
                _gm.SetMenuPlayerVisible(false);
                foreach (var canvas in _ui.Canvases)
                {
                    if (canvas == null || canvas == overlay || canvas == _menuCanvas || !canvas.gameObject.activeSelf) continue;
                    var group = RevealGroup(canvas.gameObject);
                    group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
                    hidden.Add(group);
                }
                _menu.SetActive(true);
                _menuCanvas.sortingOrder = 1001;
                screenImage.enabled = false;
                // Allow the production view to place the newly prepared knight before measuring it.
                yield return null;
                var target = _gm.MenuPlayerRect;
                float targetPixels = Mathf.Max(target.width, target.height) / MenuKnightPreview.Fill;
                bool reduced = SaveData.IsReducedEffects();
                float moveDuration = reduced ? .22f : .62f;
                for (float elapsed = 0; elapsed < moveDuration; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.SmoothStep(0, 1, elapsed / moveDuration);
                    _menuKnight.SetBoardView(t);
                    // Re-read the target so a resize/safe-area change cannot move the handoff.
                    target = _gm.MenuPlayerRect;
                    targetPixels = Mathf.Max(target.width, target.height) / MenuKnightPreview.Fill;
                    Vector2 center = Vector2.Lerp(startScreen, target.center, t);
                    float diameter = Mathf.Lerp(startPixels, targetPixels, t);
                    SetHeroScreenPosition(overlayRt, center);
                    _menuHero.sizeDelta = Vector2.one * diameter / overlay.scaleFactor;
                    menuGroup.alpha = 1 - t;
                    material.SetFloat("_Reveal", t);
                    SetRevealCircle(material, center, diameter * .5f);
                    yield return null;
                }
                SetHeroScreenPosition(overlayRt, target.center);
                _menuKnight.SetBoardView(1);
                _gm.SetMenuPlayerVisible(true);
                _menuHero.sizeDelta = Vector2.one * targetPixels / overlay.scaleFactor;
                material.SetFloat("_Reveal", 1);
                _menu.SetActive(false);
                // Exact same model/skin/camera angle underneath; dissolve the preview at the handoff.
                float radius = targetPixels * .5f;
                float fullRadius = Mathf.Max(
                    Vector2.Distance(target.center, Vector2.zero),
                    Vector2.Distance(target.center, new Vector2(Screen.width, 0)),
                    Vector2.Distance(target.center, new Vector2(0, Screen.height)),
                    Vector2.Distance(target.center, new Vector2(Screen.width, Screen.height))) + 8;
                float expandDuration = reduced ? .18f : .48f;
                for (float elapsed = 0; elapsed < expandDuration; elapsed += Time.unscaledDeltaTime)
                {
                    float progress = elapsed / expandDuration;
                    float t = Mathf.SmoothStep(0, 1, progress);
                    portrait.color = new Color(1, 1, 1, 1 - Mathf.Clamp01(progress * 5));
                    SetRevealCircle(material, target.center, Mathf.Lerp(radius, fullRadius, t));
                    material.SetFloat("_RingAlpha", 1 - t);
                    foreach (var group in hidden) if (group != null) group.alpha = Mathf.Clamp01((progress - .45f) / .55f);
                    yield return null;
                }
            }
            finally
            {
                _gm.MenuTransitionActive = false;
                _gm.SetMenuPlayerVisible(true);
                _menuCanvas.sortingOrder = menuOrder;
                menuGroup.alpha = 1; menuGroup.interactable = true; menuGroup.blocksRaycasts = true;
                screenImage.enabled = true;
                portrait.color = Color.white;
                _menuKnight.SetBoardView(0);
                paper.SetActive(true); inset.SetActive(true);
                _menuHero.SetParent(parent, false);
                _menuHero.SetSiblingIndex(sibling);
                placeholder.SetActive(false); Destroy(placeholder);
                _menuHero.localScale = oldScale; _menuHero.sizeDelta = oldSize;
                foreach (var group in hidden)
                    if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
                overlay.gameObject.SetActive(false);
                _ui.Canvases.Remove(overlay);
                Destroy(overlay.gameObject); Destroy(material);
                _menuReveal = null;
                _forceLayout = true;
            }
        }

        private void SetHeroScreenPosition(RectTransform canvas, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var position);
            _menuHero.anchoredPosition = position;
        }

        private static CanvasGroup RevealGroup(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            return group;
        }

        private static void SetRevealCircle(Material material, Vector2 center, float radius)
        {
            material.SetVector("_Center", new Vector4(center.x, center.y, 0, 0));
            material.SetVector("_ScreenSize", new Vector4(Screen.width, Screen.height, 0, 0));
            material.SetFloat("_Radius", radius);
        }
    }
}
