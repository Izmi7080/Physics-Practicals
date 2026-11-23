using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: The instruction card at the top of every practical (replaces the old "Step X of Y -
     * ..." pill). Designed for a Matric student reading in a second language:
     *   - a small "STEP 3 OF 6" label and ONE short action line (the step's instruction);
     *   - a segmented PROGRESS BAR along its bottom edge: done steps green with a tick feel, the
     *     current step teal, later steps dim - so the student always sees how far they are;
     *   - a "Why?" chip: the reason for the step is one tap away instead of being crammed into the
     *     instruction (finding 7: too much text competing);
     *   - a DETAIL panel that drops down under the card for the "Why?" text and for Learn-mode HINTS.
     *     Hints used to be appended to the feedback toast over the 3D view (finding 1); now they
     *     belong to the instruction they help with, collapse by themselves after reading time, and
     *     can be re-opened with the lightbulb chip.
     * Talks to: UITheme_Azm, Motion_Azm, FeedbackFX_Azm; driven by PracticalHUDBase_Azm.
     */
    public class InstructionCardUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public TMP_Text stepLabel;
        public TMP_Text instructionText;
        public CanvasGroup textGroup;
        [Tooltip("Holds one segment per step (HorizontalLayoutGroup).")]
        public RectTransform progressRoot;
        public Button whyButton;
        public Button hintButton;

        [Header("Drop-down detail")]
        public RectTransform detailPanel;
        public CanvasGroup detailGroup;
        public Image detailBackground;
        public TMP_Text detailTitle;
        public TMP_Text detailText;
        public Button detailCloseArea;
        [Tooltip("The detail never reaches past this rect's right edge (the left column), so an open drawer or keypad cannot cover the hint text.")]
        public RectTransform clampTo;

        [Tooltip("Seconds a hint stays open: base + per character (capped).")]
        public float hintBaseSeconds = 3.5f;
        public float hintSecondsPerCharacter = 0.05f;
        public float hintMaxSeconds = 12f;

        private readonly List<Image> segments = new List<Image>();
        private int lastIndex = -1;
        private string whyText = "";
        private string lastHint = "";
        private bool detailOpen;
        private Tween autoClose;

        private void Awake()
        {
            whyButton.onClick.AddListener(ToggleWhy);
            hintButton.onClick.AddListener(() => { if (detailOpen) CloseDetail(); else OpenDetail(true, lastHint, false); });
            if (detailCloseArea != null) detailCloseArea.onClick.AddListener(CloseDetail);
            hintButton.gameObject.SetActive(false);
            detailGroup.alpha = 0f;
            detailPanel.gameObject.SetActive(false);
        }

        // Keep the drop-down detail inside the left column (it follows the column as panels open).
        private readonly Vector3[] corners = new Vector3[4];
        private void LateUpdate()
        {
            if (clampTo == null || !detailPanel.gameObject.activeSelf) return;
            clampTo.GetWorldCorners(corners);
            float columnRight = corners[2].x;
            ((RectTransform)transform).GetWorldCorners(corners);
            float cardRight = corners[2].x;
            float scale = transform.lossyScale.x > 0f ? transform.lossyScale.x : 1f;
            float inset = Mathf.Max(0f, (cardRight - columnRight) / scale);
            detailPanel.offsetMax = new Vector2(-inset, detailPanel.offsetMax.y);
        }

        // ----- steps -----

        public void SetStep(int index, int count, string instruction, string why)
        {
            BuildSegments(count);
            bool advanced = lastIndex >= 0 && index > lastIndex;
            bool sameStep = index == lastIndex;
            lastIndex = index;

            stepLabel.text = $"STEP {index + 1} OF {count}";
            instructionText.text = instruction;
            whyText = why ?? "";
            whyButton.gameObject.SetActive(!string.IsNullOrEmpty(whyText));

            // A new step: the old hint no longer applies.
            if (!sameStep)
            {
                lastHint = "";
                hintButton.gameObject.SetActive(false);
                CloseDetail();
            }

            for (int i = 0; i < segments.Count; i++)
            {
                ThemeColor_Azm role = i < index ? ThemeColor_Azm.Valid : (i == index ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
                var color = theme.GetColor(role);
                if (i > index) color.a = 0.6f;
                segments[i].color = color;
            }

            // Motion: the text slides in; the segment just completed gives a little "done" punch.
            if (textGroup != null && !sameStep)
            {
                textGroup.alpha = 0f;
                Motion_Azm.FadeIn(textGroup, Motion_Azm.Normal);
            }
            if (advanced && index - 1 < segments.Count)
                FeedbackFX_Azm.StepDone(segments[index - 1].transform);
        }

        private void BuildSegments(int count)
        {
            if (segments.Count == count) return;
            foreach (var s in segments) if (s != null) Destroy(s.gameObject);
            segments.Clear();
            for (int i = 0; i < count; i++)
            {
                var seg = UIFactory_Azm.NewImage(progressRoot, "Segment" + (i + 1), theme, ThemeColor_Azm.Secondary);
                Destroy(seg.GetComponent<ThemedImage_Azm>());   // colour is set per state above
                var layout = seg.gameObject.AddComponent<LayoutElement>();
                layout.flexibleWidth = 1f;
                layout.preferredHeight = progressRoot.rect.height;
                seg.raycastTarget = false;
                segments.Add(seg);
            }
        }

        // ----- why / hint -----

        public void ToggleWhy()
        {
            if (detailOpen && detailTitle.text == "Why?") { CloseDetail(); return; }
            OpenDetail(false, whyText, false);
        }

        // Learn-mode hint: opens now, closes by itself after reading time, re-openable from the bulb.
        public void ShowHint(string text)
        {
            lastHint = text;
            hintButton.gameObject.SetActive(true);
            Motion_Azm.PopIn(hintButton.transform);
            OpenDetail(true, text, true);
        }

        private void OpenDetail(bool hint, string text, bool autoCollapse)
        {
            if (string.IsNullOrEmpty(text)) return;
            bool wasOpen = detailOpen;
            // Activate FIRST: ThemedImage_Azm / ThemedText_Azm re-apply theme colours in OnEnable,
            // which would otherwise undo the tint and title colour set below.
            detailPanel.gameObject.SetActive(true);
            detailTitle.text = hint ? "Hint" : "Why?";
            detailTitle.GetComponent<ThemedText_Azm>()?.SetRole(hint ? ThemeColor_Azm.Accent : ThemeColor_Azm.Primary);
            detailText.text = text;
            Color strong = theme.GetColor(ThemeColor_Azm.PanelStrong);
            detailBackground.color = hint ? Color.Lerp(strong, theme.GetColor(ThemeColor_Azm.Accent), 0.18f) : strong;

            detailOpen = true;
            Motion_Azm.FadeIn(detailGroup, Motion_Azm.Fast);
            if (!wasOpen) Motion_Azm.PopIn(detailPanel, 0.95f);
            if (hint) FeedbackFX_Azm.Reveal(null);

            autoClose?.Kill();
            if (autoCollapse)
            {
                float seconds = Mathf.Min(hintMaxSeconds, hintBaseSeconds + text.Length * hintSecondsPerCharacter);
                autoClose = Motion_Azm.Delay(this, seconds, CloseDetail);
            }
        }

        public void CloseDetail()
        {
            autoClose?.Kill();
            autoClose = null;
            if (!detailOpen) return;
            detailOpen = false;
            Motion_Azm.FadeOut(detailGroup, true, Motion_Azm.Fast);
        }

        public bool DetailOpen => detailOpen;

        // How far the open detail hangs below the card (canvas units; 0 when closed). The screen
        // regions move the scene window down by this much, so a hint never covers the instrument.
        public float VisibleDetailHeight => detailOpen && detailPanel != null ? detailPanel.rect.height + 8f : 0f;
    }
}
