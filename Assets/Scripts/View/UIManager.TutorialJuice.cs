using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // The interactive tutorial's lesson card (modern style). The old card was one line of
    // 14pt text tucked under the top HUD and easy to miss, so this one is built to be seen:
    //   - a big framed card with a gold accent bar and a breathing glow behind it
    //   - "LESSON 2 OF 7" plus a row of progress pips that fill as lessons are cleared
    //   - each lesson drops in with an overshoot and types its text out, key words in gold
    //   - action lessons carry a pulsing "YOUR MOVE" prompt; info lessons a glinting Continue
    //   - clearing a lesson stamps praise ("NICE!") with sparks, a flash and a light buzz
    public partial class UIManager
    {
        private RectTransform _tutGlow, _tutFx, _tutAccent, _tutPipRow;
        private TMP_Text _tutChip, _tutHint;
        private CanvasGroup _tutCardCg;
        private readonly List<Image> _tutPips = new List<Image>();
        private float[] _tutPipPunch = new float[0];
        private float _tutT, _tutTime, _tutCardBaseY, _tutCardH = 120f;
        private int _tutStepShown = -1, _tutTotal;
        private bool _tutAction;

        private const float TutPad = 20f, TutTopRow = 40f, TutCaptionSize = 22f;
        private static readonly Color TutCardColor = new Color(.045f, .075f, .15f, .97f);
        private static readonly string[] TutPraise = { "NICE!", "GREAT!", "PERFECT!", "WELL PLAYED!", "SUPERB!" };

        // Key game words get the gold treatment so a skim still lands the lesson.
        private static readonly Regex TutKeywords = new Regex(
            @"\b(Trap mode|Battle mode|captur(?:e|es|ed|ing)|combos?|multiplier|transform(?:s|ed|ing)?|tap|knight|nightrider|L-shape|gaps?|traps?|red outlines?|\d+ seconds?|misses|free miss|backward)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static string HighlightKeywords(string s) =>
            string.IsNullOrEmpty(s) ? s : TutKeywords.Replace(s, m => "<color=#FFD36B>" + m.Value + "</color>");

        private void BuildModernTutorial()
        {
            _tutorial = _ui.CreateCanvas("Tutorial", 110, new Vector2(HudRefW, HudRefH)).gameObject;

            // Glow first so it sits behind the card.
            var glowGo = new GameObject("LessonGlow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(_tutorial.transform, false);
            _tutGlow = (RectTransform)glowGo.transform;
            _tutGlow.anchorMin = _tutGlow.anchorMax = _tutGlow.pivot = new Vector2(.5f, 1);
            var gImg = glowGo.GetComponent<Image>(); gImg.sprite = UISprites.SoftGlow(); gImg.raycastTarget = false;
            gImg.color = new Color(1f, .8f, .35f, 0f);

            _tutorialCard = HudSurface(_tutorial.transform, "LessonCard", TutCardColor);
            _tutorialCard.anchorMin = _tutorialCard.anchorMax = _tutorialCard.pivot = new Vector2(.5f, 1);
            var outline = _tutorialCard.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.87f, .73f, .41f, .85f); outline.effectDistance = new Vector2(2f, -2f);
            _tutCardCg = _tutorialCard.gameObject.AddComponent<CanvasGroup>();
            _tutCardCg.blocksRaycasts = false; _tutCardCg.interactable = false; // board taps go straight through

            _tutAccent = HudSurface(_tutorialCard, "Accent", UIFactory.Gold);
            _tutAccent.anchorMin = new Vector2(0, 1); _tutAccent.anchorMax = new Vector2(1, 1); _tutAccent.pivot = new Vector2(.5f, 1);
            _tutAccent.sizeDelta = new Vector2(0, 5); _tutAccent.anchoredPosition = Vector2.zero;

            _tutChip = HudText(_tutorialCard, "Chip", "LESSON 1", 15, UIFactory.Gold, TutPad, 12, 320, 24);
            _tutChip.characterSpacing = 6f; _tutChip.enableAutoSizing = false;

            _tutPipRow = HudRect(_tutorialCard, "Pips", 0, 16, 10, 16);

            _tutCaption = HudText(_tutorialCard, "Lesson", "", TutCaptionSize, UIFactory.Text, TutPad, TutTopRow, 500, 80);
            _tutCaption.enableAutoSizing = false; _tutCaption.fontSize = TutCaptionSize;
            _tutCaption.textWrappingMode = TextWrappingModes.Normal; _tutCaption.alignment = TextAlignmentOptions.TopLeft;
            _tutCaption.richText = true; _tutCaption.lineSpacing = 4f;

            _tutHint = HudText(_tutorialCard, "Hint", "YOUR MOVE  •  TAP THE GLOWING SQUARE", 15, UIFactory.Gold, TutPad, 0, 500, 24);
            _tutHint.characterSpacing = 3f; _tutHint.enableAutoSizing = false;

            _tutorialActions = HudRect(_tutorial.transform, "LessonActions", 0, 0, 500, 52);
            _tutorialActions.anchorMin = _tutorialActions.anchorMax = _tutorialActions.pivot = new Vector2(.5f, 0);
            var skip = MenuButton(_tutorialActions, "Skip tutorial", () => _gm.TutorialSkip(), 150, 52, 16, textOnly: true);
            skip.name = "SkipTutorial"; skip.gameObject.AddComponent<PressSquash>();
            var cont = MenuButton(_tutorialActions, "Continue", () => _gm.TutorialContinue(), 180, 52, 19, primary: true);
            cont.name = "ContinueTutorial"; _tutContinueBtn = cont.gameObject;
            cont.gameObject.AddComponent<PressSquash>();
            AddGlint(cont, 52f);

            _tutFx = ShopFxLayer(_tutorial.transform); // last child: sparks and stamps draw on top
            LayoutModernTutorial(); _tutorial.SetActive(false);
        }

        private void LayoutModernTutorial()
        {
            if (_tutorialCard == null) return;
            float center = HudCenterX();
            float w = _hudContentWidth > 0 ? _hudContentWidth : 560f;
            float textW = w - TutPad * 2f;
            _tutCaption.rectTransform.sizeDelta = new Vector2(textW, 80);
            float capH = Mathf.Max(30f, _tutCaption.GetPreferredValues(_tutCaption.text, textW, 0f).y);
            HudPlace(_tutCaption.rectTransform, TutPad, TutTopRow, textW, capH);
            HudPlace(_tutHint.rectTransform, TutPad, TutTopRow + capH + 10f, textW, 24);
            _tutCardH = TutTopRow + capH + (_tutAction ? 46f : 20f);
            _tutorialCard.sizeDelta = new Vector2(w, _tutCardH);
            _tutCardBaseY = -TopOffsetCanvas() - 62;
            _tutorialCard.anchoredPosition = new Vector2(center, _tutCardBaseY);
            LayoutTutorialPips(w);

            _tutorialActions.sizeDelta = new Vector2(w, 52);
            _tutorialActions.anchoredPosition = new Vector2(center, BottomBarCanvas() - 52);
            HudPlace((RectTransform)_tutorialActions.Find("SkipTutorial"), 0, 0, 150, 52);
            HudPlace((RectTransform)_tutContinueBtn.transform, w - 180, 0, 180, 52);
        }

        private void LayoutTutorialPips(float cardW)
        {
            if (_tutPipRow == null) return;
            const float size = 12f, gap = 8f;
            float rowW = _tutPips.Count * size + Mathf.Max(0, _tutPips.Count - 1) * gap;
            HudPlace(_tutPipRow, cardW - TutPad - rowW, 18, rowW, size);
            for (int i = 0; i < _tutPips.Count; i++)
            {
                var rt = _tutPips[i].rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(-rowW * .5f + size * .5f + i * (size + gap), 0f);
            }
        }

        private void EnsureTutorialPips(int total)
        {
            if (_tutPips.Count == total) return;
            foreach (var p in _tutPips) if (p != null) Destroy(p.gameObject);
            _tutPips.Clear();
            for (int i = 0; i < total; i++)
            {
                var go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_tutPipRow, false);
                var img = go.GetComponent<Image>(); img.sprite = UISprites.Circle(); img.raycastTarget = false;
                _tutPips.Add(img);
            }
            _tutPipPunch = new float[total];
        }

        public void ShowTutorial(string caption, bool continueBtn, int step = 0, int total = 1)
        {
            if (LegacyStyle || _tutorialCard == null)
            {
                if (_tutCaption != null) _tutCaption.text = caption;
                if (_tutContinueBtn != null) _tutContinueBtn.SetActive(continueBtn);
                if (_tutorial != null) _tutorial.SetActive(true);
                return;
            }
            bool final = step >= total - 1;
            _tutAction = !continueBtn;
            _tutCaption.text = HighlightKeywords(caption);
            _tutChip.text = final && continueBtn ? "FINAL LESSON" : "LESSON " + (step + 1) + " OF " + total;
            _tutHint.gameObject.SetActive(_tutAction);
            _tutContinueBtn.SetActive(continueBtn);
            if (continueBtn) _tutContinueBtn.GetComponentInChildren<TMP_Text>().text = final ? "Let's go!" : "Continue";
            EnsureTutorialPips(total);
            _tutTotal = total;
            LayoutModernTutorial();

            if (step != _tutStepShown)
            {
                _tutT = 0f;
                if (_tutStepShown >= 0) SfxManager.Play(Sfx.PageTurn);
                if (continueBtn) PopInAfter((RectTransform)_tutContinueBtn.transform, .35f);
            }
            _tutStepShown = step;
            _tutCaption.maxVisibleCharacters = SaveData.IsReducedEffects() ? 99999 : 0;
            _tutorial.SetActive(true);
            UpdateTutorialJuice();
        }

        public void HideTutorial()
        {
            if (_tutorial != null) _tutorial.SetActive(false);
            _tutStepShown = -1;
            ResetTutorialFocus();
        }

        // From GameManager the moment the player lands on the lesson's target square.
        public void TutorialStepCleared(int step)
        {
            if (LegacyStyle || _tutFx == null || _tutorialCard == null) return;
            if (step >= 0 && step < _tutPips.Count)
            {
                _tutPipPunch[step] = 1f;
                SparkBurst(_tutFx, _tutPips[step].rectTransform.position, 14, UIFactory.Gold, Color.white, .6f);
            }
            // Praise just below the card, over the top of the board.
            Vector3 at = _tutorialCard.TransformPoint(new Vector3(0f, -_tutCardH - 70f, 0f));
            FlashAt(_tutFx, at, new Color(1f, .85f, .4f, .7f), 280f);
            SparkBurst(_tutFx, at, 22, UIFactory.Gold, Color.white, .9f);
            StampAt(_tutFx, at, TutPraise[Mathf.Abs(step) % TutPraise.Length], UIFactory.Gold, 54f, 1.05f);
            Haptics.Play(Haptics.Settle, Haptics.SettleAmp);
        }

        private void UpdateTutorialJuice()
        {
            if (_tutorial == null || !_tutorial.activeSelf || LegacyStyle || _tutorialCard == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _tutT += dt; _tutTime += dt;
            UpdateTutorialFocus(dt);
            bool reduced = SaveData.IsReducedEffects();

            // Entrance: drops in from above with an overshoot.
            float u = reduced ? 1f : Mathf.Clamp01(_tutT / .42f);
            float e = EaseOutBackUI(u);
            _tutorialCard.localScale = Vector3.one * Mathf.LerpUnclamped(.86f, 1f, e);
            var pos = _tutorialCard.anchoredPosition;
            _tutorialCard.anchoredPosition = new Vector2(pos.x, _tutCardBaseY + (1f - Mathf.Clamp01(e)) * 34f);
            _tutCardCg.alpha = reduced ? 1f : Mathf.Clamp01(_tutT / .14f);

            // Typewriter (fast, so it never feels like waiting).
            if (!reduced) _tutCaption.maxVisibleCharacters = Mathf.Max(0, (int)((_tutT - .12f) * 110f));

            // Breathing glow behind the card, flaring on arrival.
            float flare = reduced ? 0f : (1f - u) * .45f;
            float breathe = reduced ? .15f : .18f + .1f * Mathf.Sin(_tutTime * 2.6f);
            _tutGlow.anchoredPosition = _tutorialCard.anchoredPosition + new Vector2(0f, 60f);
            _tutGlow.sizeDelta = _tutorialCard.sizeDelta * _tutorialCard.localScale.x + new Vector2(120f, 120f);
            _tutGlow.GetComponent<Image>().color = new Color(1f, .8f, .35f, breathe + flare);

            // Accent bar shimmers between gold and pale gold.
            _tutAccent.GetComponent<Image>().color = Color.Lerp(UIFactory.Gold, new Color(1f, .95f, .75f),
                reduced ? 0f : .5f + .5f * Mathf.Sin(_tutTime * 3.1f));

            if (_tutAction) _tutHint.alpha = reduced ? 1f : .55f + .45f * Mathf.Sin(_tutTime * 4.2f);

            // Pips: done = gold, current = pulsing white, ahead = dim.
            for (int i = 0; i < _tutPips.Count; i++)
            {
                _tutPipPunch[i] = Mathf.Max(0f, _tutPipPunch[i] - dt / .35f);
                bool done = i < _tutStepShown, cur = i == _tutStepShown;
                float s = 1f + .7f * Mathf.Sin(_tutPipPunch[i] * Mathf.PI);
                if (cur && !reduced) s += .22f * (.5f + .5f * Mathf.Sin(_tutTime * 5f));
                _tutPips[i].rectTransform.localScale = Vector3.one * s;
                _tutPips[i].color = done ? UIFactory.Gold : cur ? Color.white : new Color(1f, 1f, 1f, .18f);
            }

            // The final lesson's button keeps breathing to invite the tap.
            if (_tutContinueBtn.activeSelf && _tutStepShown >= _tutTotal - 1 && !reduced && !_popIns.Exists(p => p.rt == (RectTransform)_tutContinueBtn.transform))
            {
                var sq = _tutContinueBtn.GetComponent<PressSquash>();
                if (sq == null || Mathf.Approximately(sq.Factor, 1f))
                    _tutContinueBtn.transform.localScale = Vector3.one * (1f + .04f * Mathf.Sin(_tutTime * 4f));
            }
        }

        // ── HUD spotlight ────────────────────────────────────────────────────
        // "The dots under your piece icon" means nothing until you've seen them, so the
        // lesson shows them: a big copy of the HUD piece icon pops up over the knight on a
        // dimmed board with a label, demonstrates itself (the capture dots pulse in turn, or
        // the transform ring drains and refills), then arcs up and shrinks into its real
        // spot on the HUD, which flashes and punches as it lands.
        public enum HudFocus { None, CapturePips, TransformTimer }

        private RectTransform _focusClone;
        private Image _focusDim, _focusRadial;
        private TMP_Text _focusLabel;
        private readonly List<Image> _focusPips = new List<Image>();
        private HudFocus _focusKind;
        private Vector3 _focusFrom;
        private float _focusT, _focusPunch;
        private bool _focusOn, _focusRefilled;

        private const float FocusDelay = .45f, FocusIn = .4f, FocusHold = 2.1f, FocusFly = .55f, FocusScale = 2.8f;

        public bool TutorialFocusBusy => _focusOn;

        public void TutorialFocusHud(HudFocus kind, Vector2 pieceScreen)
        {
            EndTutorialFocus();
            if (kind == HudFocus.None || LegacyStyle || _tutorial == null || _tutFx == null
                || _battleForm == null || !_battleForm.gameObject.activeInHierarchy) return;
            _focusKind = kind; _focusOn = true; _focusRefilled = false;
            _focusT = SaveData.IsReducedEffects() ? FocusDelay + FocusIn : 0f;

            var dimGo = new GameObject("FocusDim", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(_tutorial.transform, false); dimGo.transform.SetAsFirstSibling();
            var drt = (RectTransform)dimGo.transform; UIFactory.Stretch(drt);
            drt.offsetMin = new Vector2(-2000f, -2000f); drt.offsetMax = new Vector2(2000f, 2000f);
            _focusDim = dimGo.GetComponent<Image>(); _focusDim.raycastTarget = false; _focusDim.color = Color.clear;

            var clone = Instantiate(_battleForm.gameObject, _tutorial.transform);
            clone.name = "FocusClone"; clone.SetActive(true);
            _focusClone = (RectTransform)clone.transform;
            _focusClone.SetSiblingIndex(_tutFx.GetSiblingIndex());
            _focusClone.anchorMin = _focusClone.anchorMax = _focusClone.pivot = new Vector2(.5f, .5f);
            _focusClone.sizeDelta = _battleForm.rect.size;
            _focusClone.localScale = Vector3.zero;
            foreach (var g in clone.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            var timer = _focusClone.Find("FormTimer");
            _focusRadial = timer != null ? timer.GetComponent<Image>() : null;
            _focusPips.Clear();
            for (int i = 0; i < GameConfig.TransformTrigger; i++)
            {
                var p = _focusClone.Find("CapturePip" + i);
                if (p != null) _focusPips.Add(p.GetComponent<Image>());
            }

            // Pop up right over the knight (covering it), kept clear of the screen edges.
            float half = _battleForm.rect.width * FocusScale * .5f * HudScale();
            var safe = Screen.safeArea;
            _focusFrom = new Vector3(
                Mathf.Clamp(pieceScreen.x, safe.xMin + half + 20f, safe.xMax - half - 20f),
                Mathf.Clamp(pieceScreen.y, safe.yMin + half * 3f, safe.yMax - half * 4f), 0f);
            _focusClone.position = _focusFrom;

            string title = kind == HudFocus.CapturePips ? "CAPTURE DOTS" : "TRANSFORM TIMER";
            string sub = kind == HudFocus.CapturePips ? "3 captures in a row = transform" : "every capture refills it";
            _focusLabel = _ui.Label(_tutorial.transform, title + "\n<size=70%><color=#F6EDD6>" + sub + "</color></size>",
                22, UIFactory.Gold, width: 420);
            _focusLabel.font = UIFactory.BodyFont(); _focusLabel.alignment = TextAlignmentOptions.Center;
            _focusLabel.characterSpacing = 4f; _focusLabel.raycastTarget = false; _focusLabel.richText = true;
            _focusLabel.outlineWidth = .22f; _focusLabel.outlineColor = new Color32(0, 0, 0, 230); // readable over the board
            var lrt = _focusLabel.rectTransform;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(.5f, .5f); lrt.sizeDelta = new Vector2(420f, 70f);
            lrt.SetSiblingIndex(_tutFx.GetSiblingIndex());
            _focusLabel.alpha = 0f;
            UpdateTutorialFocus(0f);
        }

        // Spotlight gone and the real HUD icon back at rest (screen changes, tutorial end).
        private void ResetTutorialFocus()
        {
            EndTutorialFocus();
            _focusPunch = 0f;
            if (_battleForm != null) _battleForm.localScale = Vector3.one;
        }

        private void EndTutorialFocus()
        {
            if (_focusClone != null) Destroy(_focusClone.gameObject);
            if (_focusDim != null) Destroy(_focusDim.gameObject);
            if (_focusLabel != null) Destroy(_focusLabel.gameObject);
            _focusClone = null; _focusDim = null; _focusLabel = null; _focusRadial = null;
            _focusPips.Clear();
            _focusOn = false;
        }

        private void UpdateTutorialFocus(float dt)
        {
            // The real HUD icon's landing punch outlives the clone.
            if (_focusPunch > 0f && _battleForm != null)
            {
                _focusPunch = Mathf.Max(0f, _focusPunch - dt / .4f);
                _battleForm.localScale = Vector3.one * (1f + .45f * Mathf.Sin(_focusPunch * Mathf.PI));
            }
            if (!_focusOn || _focusClone == null) return;
            _focusT += dt;
            float t = _focusT - FocusDelay;
            if (t < 0f) return;

            float flyStart = FocusIn + FocusHold;
            Vector3 target = _battleForm.TransformPoint(_battleForm.rect.center);
            _focusDim.color = new Color(0f, 0f, 0f, .62f * Mathf.Clamp01(t / .25f) * (1f - Mathf.Clamp01((t - flyStart) / FocusFly)));

            if (t < flyStart)
            {
                float u = Mathf.Clamp01(t / FocusIn);
                float bob = Mathf.Sin(t * 3f) * 6f * HudScale() * u;
                _focusClone.position = _focusFrom + new Vector3(0f, bob, 0f);
                _focusClone.localScale = Vector3.one * Mathf.Max(0f, Mathf.LerpUnclamped(0f, FocusScale, EaseOutBackUI(u)));
                _focusClone.localRotation = Quaternion.Euler(0f, 0f, (1f - u) * 25f);
                if (t - dt < .12f && t >= .12f) Haptics.Play(Haptics.Settle, Haptics.SettleAmp);
                DemoFocus(t - FocusIn);
            }
            else
            {
                float u = Mathf.Clamp01((t - flyStart) / FocusFly);
                float e = u * u * (3f - 2f * u);
                // Arc up and across into the HUD slot.
                Vector3 ctrl = Vector3.Lerp(_focusFrom, target, .5f) + new Vector3(0f, Screen.height * .08f, 0f);
                _focusClone.position = (1 - e) * (1 - e) * _focusFrom + 2 * (1 - e) * e * ctrl + e * e * target;
                _focusClone.localScale = Vector3.one * Mathf.Lerp(FocusScale, 1f, e);
                _focusClone.localRotation = Quaternion.identity;
                if (_focusRadial != null) _focusRadial.fillAmount = Mathf.Lerp(_focusRadial.fillAmount, _battleFormRadial.fillAmount, e);
                if (u >= 1f)
                {
                    FlashAt(_tutFx, target, new Color(1f, .85f, .4f, .9f), 150f);
                    SparkBurst(_tutFx, target, 14, UIFactory.Gold, Color.white, .55f);
                    Haptics.Play(Haptics.Tick, Haptics.TickAmp);
                    _focusPunch = 1f;
                    EndTutorialFocus();
                    return;
                }
            }

            // The label sits under the big icon and fades as it flies off.
            _focusLabel.alpha = Mathf.Clamp01((t - .15f) / .25f) * (1f - Mathf.Clamp01((t - flyStart) / (FocusFly * .4f)));
            float below = _battleForm.rect.height * FocusScale * .5f * HudScale() + 44f * HudScale();
            _focusLabel.rectTransform.position = _focusFrom - new Vector3(0f, below, 0f);
        }

        // During the hold the big icon shows what it does.
        private void DemoFocus(float h)
        {
            if (h < 0f) return;
            if (_focusKind == HudFocus.CapturePips)
            {
                // Each dot pulses in turn.
                for (int i = 0; i < _focusPips.Count; i++)
                {
                    float k = Mathf.Max(0f, 1f - Mathf.Abs(Mathf.Repeat(h * 2.2f, _focusPips.Count + 1f) - i) * 1.4f);
                    _focusPips[i].rectTransform.localScale = Vector3.one * (1f + 1.1f * k);
                }
            }
            else if (_focusRadial != null)
            {
                // The ring drains, then a capture snaps it back to full.
                const float drainEnd = 1.1f;
                if (h < drainEnd) _focusRadial.fillAmount = 1f - .55f * (h / drainEnd);
                else
                {
                    if (!_focusRefilled)
                    {
                        _focusRefilled = true;
                        SparkBurst(_tutFx, _focusClone.position, 16, UIFactory.Gold, Color.white, .6f);
                        StampAt(_tutFx, _focusClone.position, "REFILL!", UIFactory.Gold, 34f, .8f);
                    }
                    _focusRadial.fillAmount = Mathf.Lerp(_focusRadial.fillAmount, 1f, .35f);
                }
            }
        }
    }
}
