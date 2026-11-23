using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: A small message pill that fades in, stays long enough to read and fades out; tapping
     * it dismisses it early. Used for "Coming soon" on locked cards and for the feedback / hint
     * messages in the pendulum scene. Showing a new message replaces the current one. Longer
     * messages stay longer (reading time grows with length), and an optional tint colours the pill
     * (e.g. red for an error).
     * Talks to: fades its own CanvasGroup and writes its TMP label; called by menu/practical
     * controllers via Show(message).
     */
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastUI_Azm : MonoBehaviour, IPointerClickHandler
    {
        public TMP_Text label;
        [Tooltip("Optional: the pill background, so it can be tinted per message.")]
        public Image background;
        public float fadeSeconds = 0.2f;
        public float visibleSeconds = 2.5f;
        [Tooltip("Extra seconds of reading time per character.")]
        public float secondsPerCharacter = 0.045f;
        public float maxVisibleSeconds = 9f;

        private CanvasGroup group;
        private Coroutine running;
        private Color defaultBackground;
        private bool defaultCaptured;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public void Show(string message)
        {
            Show(message, null);
        }

        public void Show(string message, Color? tint)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (background != null)
            {
                if (!defaultCaptured) { defaultBackground = background.color; defaultCaptured = true; }
                background.color = tint ?? defaultBackground;
            }

            label.text = message;
            gameObject.SetActive(true);
            if (running != null) StopCoroutine(running);
            float hold = Mathf.Min(maxVisibleSeconds, visibleSeconds + message.Length * secondsPerCharacter);
            running = StartCoroutine(Run(hold));
        }

        public void Dismiss()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData) => Dismiss();

        private IEnumerator Run(float hold)
        {
            group.blocksRaycasts = true; // so a tap can dismiss it
            yield return Fade(group.alpha, 1f);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f);
            Dismiss();
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeSeconds));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
