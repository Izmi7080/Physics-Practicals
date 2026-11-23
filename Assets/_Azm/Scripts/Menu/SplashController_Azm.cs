using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Menu
{
    /*
     * Layer: UI (app start-up).
     * Purpose: Plays the splash sequence (about 2.5-3 s): university logo -> app title with a
     * tagline -> "Final Year Project" credit line, all with soft fades. The main menu scene is
     * pre-loaded in the background during the sequence, so there is no extra wait at the end.
     * Tap to skip is only allowed after the first launch (tracked with PlayerPrefs through
     * SettingsManager_Azm).
     * Note: if Unity's own splash is enabled in Player Settings it plays BEFORE this one; that
     * setting is left alone.
     * Talks to: SettingsManager_Azm (first-launch flag), AppManager_Azm (preload + load menu).
     */
    public class SplashController_Azm : MonoBehaviour
    {
        [Header("Sequence parts")]
        public CanvasGroup logoGroup;
        public CanvasGroup titleGroup;
        public CanvasGroup creditGroup;

        [Header("University logo (placeholder slot)")]
        [Tooltip("Drop the real university logo sprite here. While empty, a placeholder frame is shown.")]
        public Sprite universityLogo;
        public Image logoImage;
        public GameObject logoPlaceholder;

        [Tooltip("Full-screen invisible button, enabled only after the first launch.")]
        public Button skipButton;

        private bool skipRequested;

        private IEnumerator Start()
        {
            logoGroup.alpha = 0f;
            titleGroup.alpha = 0f;
            creditGroup.alpha = 0f;

            bool hasLogo = universityLogo != null;
            logoImage.gameObject.SetActive(hasLogo);
            logoPlaceholder.SetActive(!hasLogo);
            if (hasLogo) logoImage.sprite = universityLogo;

            var settings = SettingsManager_Azm.Instance;
            var app = AppManager_Azm.Instance;

            // Skippable only if the app has been launched before; mark it launched right away so
            // the NEXT launch can be skipped even if this one is interrupted.
            bool canSkip = settings != null && settings.HasLaunchedBefore;
            settings?.MarkLaunched();
            skipButton.gameObject.SetActive(canSkip);
            skipButton.onClick.AddListener(() => skipRequested = true);

            // Start loading the main menu now, behind the splash.
            app?.PreloadScene(AppManager_Azm.MainMenuSceneName);

            // Logo (juice, Stage 6: it grows in gently as it fades in; the title rises the same way)
            Motion_Azm.PopIn(logoGroup.transform, 0.85f);
            yield return Fade(logoGroup, 1f, 0.35f);
            yield return Wait(0.45f);
            yield return Fade(logoGroup, 0f, 0.25f);

            // Title + tagline, then the credit line under it
            Motion_Azm.PopIn(titleGroup.transform, 0.9f);
            yield return Fade(titleGroup, 1f, 0.35f);
            yield return Fade(creditGroup, 1f, 0.3f);
            yield return Wait(0.6f);

            // Everything out, then hand over to the menu (AppManager fades to black and reveals it)
            yield return Fade(titleGroup, 0f, 0.25f);
            creditGroup.alpha = 0f;

            if (app != null) app.LoadScene(AppManager_Azm.MainMenuSceneName);
            else Debug.LogWarning("[SplashController_Azm] No AppManager_Azm - cannot load the main menu.");
        }

        private IEnumerator Fade(CanvasGroup group, float target, float seconds)
        {
            float start = group.alpha;
            float t = 0f;
            while (t < seconds && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            group.alpha = target;
        }

        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
