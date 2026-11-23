using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Menu
{
    /*
     * Layer: UI.
     * Purpose: The settings controls - master volume, SFX volume, hints on/off, vibration on/off,
     * reduce motion, large text, graphics quality (Low / High) and "reset tutorials". The last
     * three accessibility/feel options came with the HCI restructure. Changes apply immediately and are saved by
     * SettingsManager_Azm. The same panel is meant to be reused by the in-practical pause menu
     * (Stage 4), so it only talks to SettingsManager_Azm and knows nothing about the main menu.
     * Talks to: reads/writes SettingsManager_Azm; shows confirmation through an optional toast.
     */
    public class SettingsPanelUI_Azm : MonoBehaviour
    {
        public Slider masterSlider;
        public Slider sfxSlider;
        public Toggle hintsToggle;
        [Tooltip("Optional (HCI restructure).")]
        public Toggle hapticsToggle;
        public Toggle reduceMotionToggle;
        public Toggle largeTextToggle;
        public Button lowQualityButton;
        public Button highQualityButton;
        public Button resetTutorialsButton;
        public ToastUI_Azm toast;

        private void Awake()
        {
            masterSlider.onValueChanged.AddListener(v => { if (Settings != null) Settings.MasterVolume = v; });
            sfxSlider.onValueChanged.AddListener(v => { if (Settings != null) Settings.SfxVolume = v; });
            hintsToggle.onValueChanged.AddListener(v => { if (Settings != null) Settings.HintsEnabled = v; });
            if (hapticsToggle != null)
                hapticsToggle.onValueChanged.AddListener(v =>
                {
                    if (Settings == null) return;
                    Settings.HapticsEnabled = v;
                    if (v) Haptics_Azm.Play(HapticKind_Azm.Medium);   // let the student feel what they turned on
                });
            if (reduceMotionToggle != null) reduceMotionToggle.onValueChanged.AddListener(v => { if (Settings != null) Settings.ReduceMotion = v; });
            if (largeTextToggle != null) largeTextToggle.onValueChanged.AddListener(v => { if (Settings != null) Settings.LargeText = v; });
            lowQualityButton.onClick.AddListener(() => SetQuality(GraphicsQuality_Azm.Low));
            highQualityButton.onClick.AddListener(() => SetQuality(GraphicsQuality_Azm.High));
            resetTutorialsButton.onClick.AddListener(OnResetTutorials);
        }

        private static SettingsManager_Azm Settings => SettingsManager_Azm.Instance;

        // Re-read the saved values every time the panel opens, without re-triggering the listeners.
        private void OnEnable()
        {
            if (Settings == null) return;
            masterSlider.SetValueWithoutNotify(Settings.MasterVolume);
            sfxSlider.SetValueWithoutNotify(Settings.SfxVolume);
            hintsToggle.SetIsOnWithoutNotify(Settings.HintsEnabled);
            if (hapticsToggle != null) hapticsToggle.SetIsOnWithoutNotify(Settings.HapticsEnabled);
            if (reduceMotionToggle != null) reduceMotionToggle.SetIsOnWithoutNotify(Settings.ReduceMotion);
            if (largeTextToggle != null) largeTextToggle.SetIsOnWithoutNotify(Settings.LargeText);
            RefreshQualityButtons();
        }

        private void SetQuality(GraphicsQuality_Azm quality)
        {
            if (Settings == null) return;
            Settings.GraphicsQuality = quality;
            RefreshQualityButtons();
        }

        private void RefreshQualityButtons()
        {
            bool low = Settings.GraphicsQuality == GraphicsQuality_Azm.Low;
            Highlight(lowQualityButton, low);
            Highlight(highQualityButton, !low);
        }

        private void OnResetTutorials()
        {
            if (Settings == null) return;
            Settings.ResetTutorials();
            if (toast != null) toast.Show("Tutorials reset - hints will show again.");
        }

        private static void Highlight(Button button, bool selected)
        {
            var image = button.GetComponent<ThemedImage_Azm>();
            var label = button.GetComponentInChildren<ThemedText_Azm>();
            if (image != null) image.SetRole(selected ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
            if (label != null) label.SetRole(selected ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
        }
    }
}
