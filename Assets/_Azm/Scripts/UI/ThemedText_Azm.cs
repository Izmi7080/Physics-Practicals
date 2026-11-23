using TMPro;
using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: Makes a TextMeshPro label take its colour and font from the UITheme_Azm by ROLE
     * (TextPrimary, TextSecondary, OnPrimary, Accent...), so text style is never set per label.
     * LARGE TEXT (accessibility setting): labels marked scalesWithLargeText (reading text such as
     * instructions, hints, the guide and the briefing - not buttons or table cells, whose boxes are
     * fixed) grow by theme.largeTextScale while the setting is on. The size the builder gave the
     * label is remembered in baseFontSize, so turning the setting off restores it exactly.
     * Talks to: reads UITheme_Azm and SettingsManager_Azm.LargeText; writes the sibling TMP_Text.
     */
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    public class ThemedText_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public ThemeColor_Azm role = ThemeColor_Azm.TextPrimary;
        [Tooltip("Reading text that grows when the Large text setting is on.")]
        public bool scalesWithLargeText;
        [Tooltip("The size set by the builder (captured automatically the first time).")]
        public float baseFontSize;

        private void Awake() => Apply();

        private void OnEnable()
        {
            Apply();
            if (Application.isPlaying && SettingsManager_Azm.Instance != null)
                SettingsManager_Azm.Instance.SettingChanged += OnSettingChanged;
        }

        private void OnDisable()
        {
            if (SettingsManager_Azm.Instance != null)
                SettingsManager_Azm.Instance.SettingChanged -= OnSettingChanged;
        }

        private void OnValidate() => Apply();

        private void OnSettingChanged(string key)
        {
            if (key == SettingsManager_Azm.LargeTextKey) Apply();
        }

        public void Apply()
        {
            if (theme == null) return;
            var text = GetComponent<TMP_Text>();
            text.color = theme.GetColor(role);
            if (theme.font != null) text.font = theme.font;

            if (!scalesWithLargeText) return;
            if (baseFontSize <= 0f) baseFontSize = text.fontSize;
            bool large = Application.isPlaying && SettingsManager_Azm.Instance != null && SettingsManager_Azm.Instance.LargeText;
            text.fontSize = baseFontSize * (large ? theme.largeTextScale : 1f);
        }

        public void SetRole(ThemeColor_Azm newRole)
        {
            role = newRole;
            Apply();
        }
    }
}
