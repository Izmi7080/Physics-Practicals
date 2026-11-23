using System.Collections;
using UnityEngine;

namespace VirtualLab.Azm.UI
{
    public enum SlideDirection_Azm
    {
        None,
        FromRight,
        FromLeft,
        FromBottom,
        FromTop
    }

    /*
     * Layer: UI.
     * Purpose: Shows and hides a panel with a short fade plus a small slide (0.2-0.3 s, taken from
     * the theme's transitionSeconds). Every menu panel uses this so all transitions feel the same.
     * Hidden panels are fully deactivated, so they cost nothing and cannot catch taps.
     * Uses unscaled time so it still animates when the simulation is paused.
     * Talks to: moves this panel's RectTransform and fades its CanvasGroup; it is driven by
     * whichever controller owns the panel (e.g. MainMenuController_Azm).
     */
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class PanelTransition_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public SlideDirection_Azm slideFrom = SlideDirection_Azm.FromRight;
        [Tooltip("How far (canvas units) the panel travels while sliding in/out.")]
        public float slideDistance = 80f;

        public bool IsVisible { get; private set; }

        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 restPosition;
        private bool restCaptured;
        private Coroutine running;

        private void Capture()
        {
            if (restCaptured) return;
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            restCaptured = true;
        }

        public void Show(bool animate = true)
        {
            Capture();
            IsVisible = true;
            gameObject.SetActive(true);
            if (running != null) StopCoroutine(running);

            if (!animate || !isActiveAndEnabled)
            {
                Apply(1f);
                return;
            }
            running = StartCoroutine(Animate(0f, 1f, false));
        }

        public void Hide(bool animate = true)
        {
            Capture();
            IsVisible = false;
            if (running != null) StopCoroutine(running);

            if (!animate || !gameObject.activeInHierarchy)
            {
                Apply(0f);
                gameObject.SetActive(false);
                return;
            }
            running = StartCoroutine(Animate(1f, 0f, true));
        }

        private IEnumerator Animate(float from, float to, bool deactivateAtEnd)
        {
            float duration = theme != null ? Mathf.Max(0.01f, theme.transitionSeconds) : 0.25f;
            group.interactable = false;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                // Smooth ease in/out so the motion doesn't feel mechanical.
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                Apply(Mathf.Lerp(from, to, eased));
                yield return null;
            }
            Apply(to);
            running = null;
            if (deactivateAtEnd) gameObject.SetActive(false);
        }

        // visibility 0 = hidden (offset + transparent), 1 = shown (rest position, opaque).
        private void Apply(float visibility)
        {
            group.alpha = visibility;
            group.blocksRaycasts = visibility > 0.01f;
            group.interactable = visibility >= 0.99f;
            rect.anchoredPosition = restPosition + (SlideOffset() * (1f - visibility));
        }

        private Vector2 SlideOffset()
        {
            switch (slideFrom)
            {
                case SlideDirection_Azm.FromRight: return new Vector2(slideDistance, 0f);
                case SlideDirection_Azm.FromLeft: return new Vector2(-slideDistance, 0f);
                case SlideDirection_Azm.FromBottom: return new Vector2(0f, -slideDistance);
                case SlideDirection_Azm.FromTop: return new Vector2(0f, slideDistance);
                default: return Vector2.zero;
            }
        }
    }
}
