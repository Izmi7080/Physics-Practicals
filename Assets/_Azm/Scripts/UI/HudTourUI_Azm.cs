using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (onboarding; HCI restructure Stage 5).
     * Purpose: The first-run HUD TOUR: three speech bubbles, one after the other, each pointing at
     * one part of the shared practical screen with a soft teal highlight around it:
     *   1. the instruction card (what to do now, Why?, the hint bulb);
     *   2. the view buttons (other views of the apparatus);
     *   3. the drawer strip (table, graph, working, guide).
     * WHY: a Matric student opening the first practical does not know where anything is; three short
     * pointers cost ~10 seconds and remove the "where is my table?" moment. It runs once for the
     * whole app (every practical uses the same shell) - the HUD stores the tutorial flag - and can
     * be skipped at any time. Taps outside the bubble are absorbed so nothing starts by accident.
     * Each bubble is placed on whichever side of its target has room (below, above, left, right)
     * and clamped inside the screen, so it works at 16:9, 20:9 and 4:3.
     * Talks to: Motion_Azm (fade/pop), FeedbackFX_Azm (sound); the caller passes the targets.
     */
    [RequireComponent(typeof(CanvasGroup))]
    public class HudTourUI_Azm : MonoBehaviour
    {
        // All tour wording in one place (no localisation system yet).
        public static readonly string[] Texts =
        {
            "This card always tells you the next step. Tap <b>Why?</b> for the reason, and the bulb when it lights up for a hint.",
            "Switch between views of the apparatus here. You can also drag empty space to look around and pinch to zoom.",
            "Your table, graph, working and the written guide are kept here. Tap a tab to open it."
        };
        public const string NextLabel = "Next";
        public const string DoneLabel = "Got it";

        public RectTransform highlight;
        public RectTransform bubble;
        public RectTransform arrow;
        public TMP_Text stepText;
        public TMP_Text bodyText;
        public Button nextButton;
        public TMP_Text nextLabel;
        public Button skipButton;
        [Tooltip("Gap between the target and the bubble (canvas units).")]
        public float gap = 36f;

        public bool IsOpen { get; private set; }

        private CanvasGroup group;
        private RectTransform root;
        private RectTransform[] targets = new RectTransform[0];
        private int index;
        private Action closed;
        private Image bubbleImage;

        private bool initialised;

        private void Awake() => Init();

        // Show() may run before Awake (the tour starts inactive), so set up exactly once here.
        private void Init()
        {
            if (initialised) return;
            initialised = true;
            group = GetComponent<CanvasGroup>();
            root = (RectTransform)transform;
            nextButton.onClick.AddListener(Next);
            skipButton.onClick.AddListener(Close);
        }

        public void Show(RectTransform[] tourTargets, Action onClosed)
        {
            Init();
            targets = tourTargets;
            closed = onClosed;
            index = 0;
            IsOpen = true;
            gameObject.SetActive(true);
            group.alpha = 0f;
            Motion_Azm.FadeIn(group);
            ShowStep();
        }

        private void Next()
        {
            index++;
            if (index >= targets.Length || index >= Texts.Length) Close();
            else ShowStep();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Motion_Azm.FadeOut(group);
            var callback = closed;
            closed = null;
            callback?.Invoke();
        }

        private void ShowStep()
        {
            stepText.text = $"{index + 1} / {targets.Length}";
            bodyText.text = Texts[index];
            nextLabel.text = index == targets.Length - 1 ? DoneLabel : NextLabel;
            Canvas.ForceUpdateCanvases();
            Place(targets[index]);
            Motion_Azm.PopIn(bubble, 0.92f);
            FeedbackFX_Azm.PanelSlide();
        }

        // Follow the target every frame while open: the screen size can change (rotation between
        // landscape-left/right, split screen) and the target may still be moving into place.
        private void LateUpdate()
        {
            if (IsOpen && index < targets.Length && targets[index] != null) Place(targets[index]);
            // The bubble must be fully opaque (the theme's panel colour is slightly translucent and
            // the keypad showed through). Re-applied each frame because the themed colour returns
            // whenever the bubble is re-enabled.
            if (bubbleImage == null) bubbleImage = bubble.GetComponent<Image>();
            if (bubbleImage != null && bubbleImage.color.a < 1f)
            {
                var c = bubbleImage.color; c.a = 1f; bubbleImage.color = c;
            }
        }

        // Highlight the target and put the bubble on the side with the most room.
        private void Place(RectTransform target)
        {
            Rect area = root.rect;
            Rect t = BoundsIn(target);
            highlight.anchoredPosition = t.center;
            highlight.sizeDelta = t.size + new Vector2(24f, 24f);

            Vector2 size = bubble.rect.size;
            float below = t.yMin - area.yMin, above = area.yMax - t.yMax;
            float left = t.xMin - area.xMin, right = area.xMax - t.xMax;
            Vector2 centre;
            float arrowAngle;   // the arrow is a square rotated 45°, its tip pointing at the target
            if (below >= size.y + gap + 20f) { centre = new Vector2(t.center.x, t.yMin - gap - size.y * 0.5f); arrowAngle = 0f; }
            else if (above >= size.y + gap + 20f) { centre = new Vector2(t.center.x, t.yMax + gap + size.y * 0.5f); arrowAngle = 180f; }
            else if (left >= size.x + gap + 20f) { centre = new Vector2(t.xMin - gap - size.x * 0.5f, t.center.y); arrowAngle = 90f; }
            else { centre = new Vector2(t.xMax + gap + size.x * 0.5f, t.center.y); arrowAngle = -90f; }

            // Keep the bubble on screen.
            float margin = 24f;
            centre.x = Mathf.Clamp(centre.x, area.xMin + margin + size.x * 0.5f, area.xMax - margin - size.x * 0.5f);
            centre.y = Mathf.Clamp(centre.y, area.yMin + margin + size.y * 0.5f, area.yMax - margin - size.y * 0.5f);
            bubble.anchoredPosition = centre;

            // The arrow sits on the bubble's edge facing the target, lined up with the target.
            Vector2 a;
            if (arrowAngle == 0f) a = new Vector2(Mathf.Clamp(t.center.x, centre.x - size.x * 0.4f, centre.x + size.x * 0.4f), centre.y + size.y * 0.5f);
            else if (arrowAngle == 180f) a = new Vector2(Mathf.Clamp(t.center.x, centre.x - size.x * 0.4f, centre.x + size.x * 0.4f), centre.y - size.y * 0.5f);
            else if (arrowAngle == 90f) a = new Vector2(centre.x + size.x * 0.5f, Mathf.Clamp(t.center.y, centre.y - size.y * 0.4f, centre.y + size.y * 0.4f));
            else a = new Vector2(centre.x - size.x * 0.5f, Mathf.Clamp(t.center.y, centre.y - size.y * 0.4f, centre.y + size.y * 0.4f));
            arrow.anchoredPosition = a;
            arrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        // The target's rectangle in this (full-screen) rect's local space.
        private Rect BoundsIn(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = root.InverseTransformPoint(corners[0]);
            Vector2 max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
