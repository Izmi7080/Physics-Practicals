using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    // The kind of a feedback message, shared by every practical's HUD (each practical's controller
    // keeps its own message enum; the HUD maps it to this). Named Feedback... rather than
    // Message... because the pendulum already has VirtualLab.Azm.Pendulum.MessageKind_Azm.
    public enum FeedbackKind_Azm { Info, Success, Warning, Error, Hint }

    /*
     * Layer: UI.
     * Purpose: A feedback card INSIDE a panel (the reading keypad, the least-count question). The
     * baseline evaluation found that error and success messages shown as a toast over the 3D view
     * covered the very instrument the student had to read (findings 1 and 2). Feedback about an
     * answer now appears where the answer was given: tinted by kind (green / amber / red, plus an
     * icon, so colour is never the only cue), with a gentle shake for errors and a pop for success.
     * It stays until the student types again or the next message replaces it.
     * Talks to: UITheme_Azm (colours), Motion_Azm; driven by CaliperReadingPadUI_Azm and the HUD.
     */
    [RequireComponent(typeof(CanvasGroup))]
    public class PanelFeedback_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public Image background;
        public TMP_Text label;
        [Tooltip("Optional icon (check / info / lightbulb) beside the text.")]
        public Image icon;
        public ThemedIcon_Azm iconTheme;

        public bool IsShowing => gameObject.activeSelf && group != null && group.alpha > 0.01f;

        private CanvasGroup group;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            gameObject.SetActive(false);
        }

        public void Show(string text, FeedbackKind_Azm kind)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            bool wasShowing = IsShowing;
            // Activate FIRST: ThemedImage_Azm re-applies its theme colour in OnEnable, which would
            // otherwise undo the tint set below.
            gameObject.SetActive(true);
            label.text = text;

            Color strong = theme.GetColor(ThemeColor_Azm.PanelStrong);
            Color tint;
            ThemeIcon_Azm symbol;
            switch (kind)
            {
                case FeedbackKind_Azm.Success: tint = theme.GetColor(ThemeColor_Azm.Valid); symbol = ThemeIcon_Azm.Check; break;
                case FeedbackKind_Azm.Error: tint = theme.GetColor(ThemeColor_Azm.Invalid); symbol = ThemeIcon_Azm.Close; break;
                case FeedbackKind_Azm.Warning: tint = theme.GetColor(ThemeColor_Azm.Accent); symbol = ThemeIcon_Azm.Info; break;
                case FeedbackKind_Azm.Hint: tint = theme.GetColor(ThemeColor_Azm.Accent); symbol = ThemeIcon_Azm.Lightbulb; break;
                default: tint = theme.GetColor(ThemeColor_Azm.Secondary); symbol = ThemeIcon_Azm.Info; break;
            }
            background.color = Color.Lerp(strong, tint, 0.5f);
            if (iconTheme != null) { iconTheme.icon = symbol; iconTheme.Apply(); }

            group.blocksRaycasts = false;
            Motion_Azm.FadeIn(group, Motion_Azm.Fast);
            if (kind == FeedbackKind_Azm.Error || kind == FeedbackKind_Azm.Warning)
                Motion_Azm.Shake((RectTransform)transform, kind == FeedbackKind_Azm.Error ? 14f : 8f);
            else if (!wasShowing)
                Motion_Azm.PopIn(transform, 0.92f);
        }

        public void Hide()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (!gameObject.activeSelf) return;
            Motion_Azm.FadeOut(group, true, Motion_Azm.Fast);
        }
    }
}
