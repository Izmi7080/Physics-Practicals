using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (button "feel"; no practical logic).
     * Purpose: Gives every button the same physical feel: it squashes slightly while pressed and
     * springs back when released; a completed tap plays the click sound and a light vibration
     * (FeedbackFX_Azm.Tap). Tapping a DISABLED button is not silently ignored: the button gives a
     * small shake and raises DisabledTapped, so the HUD can say why it is disabled (e.g. "Turn the
     * thimble one full turn first"). Scene builders add this to every button they create.
     * Talks to: the sibling Button; Motion_Azm and FeedbackFX_Azm.
     */
    [RequireComponent(typeof(Button))]
    public class ButtonFeel_Azm : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("Scale while pressed (1 = no squash). Big round buttons look better with less.")]
        [Range(0.85f, 1f)] public float pressScale = 0.94f;
        [Tooltip("Play the click sound and light haptic on a tap.")]
        public bool clickFeedback = true;

        // Raised when the student taps this button while it is not interactable.
        public event Action<ButtonFeel_Azm> DisabledTapped;

        private Button button;
        private bool pressed;

        private void Awake() => button = GetComponent<Button>();

        private void OnDisable()
        {
            pressed = false;
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!button.IsInteractable()) return;
            pressed = true;
            Motion_Azm.Press(transform, pressScale);
        }

        public void OnPointerUp(PointerEventData eventData) => ReleaseIfPressed();
        public void OnPointerExit(PointerEventData eventData) => ReleaseIfPressed();

        private void ReleaseIfPressed()
        {
            if (!pressed) return;
            pressed = false;
            Motion_Azm.Release(transform);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!button.IsInteractable())
            {
                Motion_Azm.Shake((RectTransform)transform, 8f);
                DisabledTapped?.Invoke(this);
                return;
            }
            if (clickFeedback) FeedbackFX_Azm.Tap(transform);
        }
    }
}
