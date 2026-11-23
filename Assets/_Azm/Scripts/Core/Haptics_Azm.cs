using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using CandyCoded.HapticFeedback.Android;
#endif

namespace VirtualLab.Azm.Core
{
    // The kinds of vibration the app uses. Each maps to an Android system haptic constant, so the
    // phone plays its own short, tuned effect instead of a crude 400 ms buzz (Handheld.Vibrate).
    public enum HapticKind_Azm
    {
        Tick,       // CLOCK_TICK - one millimetre of jaw travel, one division of the thimble, a tap count
        Light,      // CONTEXT_CLICK - a button press
        Medium,     // VIRTUAL_KEY - a panel opening, a step completed
        Heavy,      // LONG_PRESS - the ratchet slipping, a big result
        Confirm,    // CONFIRM (Android 11+, falls back to Medium) - a correct reading
        Reject      // REJECT (Android 11+, falls back to Medium) - a wrong reading
    }

    /*
     * Layer: Core (device output, no practical logic).
     * Purpose: The only place the app vibrates the phone. Wraps the CandyCoded HapticFeedback package
     * (MIT, `xyz.candycoded.hapticfeedback`), which calls Android's View.performHapticFeedback - the
     * same short system effects the keyboard uses, and they respect the phone's own haptics setting.
     *   - Respects the app's Haptics setting (SettingsManager_Azm.HapticsEnabled).
     *   - Rate-limited (minIntervalSeconds) so a fast drag can never become a continuous buzz.
     *   - Does nothing in the Editor or on other platforms (CandyCoded only ships Android/iOS code).
     * The package is used only here, so it can be swapped for another plugin in one file.
     * Talks to: SettingsManager_Azm (setting); called by FeedbackFX_Azm and instrument interactions.
     */
    public static class Haptics_Azm
    {
        // ASSUMPTION (feel, to tune on a phone): 40 ms is the shortest gap at which separate ticks
        // still feel separate rather than a buzz.
        public const float MinIntervalSeconds = 0.04f;

        private static float lastTime = -1f;

        public static bool Enabled =>
            SettingsManager_Azm.Instance == null || SettingsManager_Azm.Instance.HapticsEnabled;

        public static void Play(HapticKind_Azm kind)
        {
            if (!Enabled) return;
            float now = Time.unscaledTime;
            // Heavier effects always play; ticks are dropped if they come too fast.
            if (kind == HapticKind_Azm.Tick && now - lastTime < MinIntervalSeconds) return;
            lastTime = now;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                HapticFeedback.PerformHapticFeedback(ToAndroid(kind));
            }
            catch (System.Exception e)
            {
                // A device without the plugin's Java class must never crash a practical.
                Debug.LogWarning("[Haptics_Azm] Haptic feedback failed: " + e.Message);
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static HapticFeedbackConstants ToAndroid(HapticKind_Azm kind)
        {
            // CONFIRM / REJECT exist from Android 11 (API 30); older phones get VIRTUAL_KEY.
            bool api30 = AndroidApiLevel >= 30;
            switch (kind)
            {
                case HapticKind_Azm.Tick: return HapticFeedbackConstants.CLOCK_TICK;
                case HapticKind_Azm.Light: return HapticFeedbackConstants.CONTEXT_CLICK;
                case HapticKind_Azm.Heavy: return HapticFeedbackConstants.LONG_PRESS;
                case HapticKind_Azm.Confirm: return api30 ? HapticFeedbackConstants.CONFIRM : HapticFeedbackConstants.VIRTUAL_KEY;
                case HapticKind_Azm.Reject: return api30 ? HapticFeedbackConstants.REJECT : HapticFeedbackConstants.VIRTUAL_KEY;
                default: return HapticFeedbackConstants.VIRTUAL_KEY;
            }
        }

        private static int apiLevel = -1;
        private static int AndroidApiLevel
        {
            get
            {
                if (apiLevel >= 0) return apiLevel;
                try
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        apiLevel = version.GetStatic<int>("SDK_INT");
                }
                catch { apiLevel = 0; }
                return apiLevel;
            }
        }
#endif
    }
}
