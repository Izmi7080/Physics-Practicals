using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    public enum GraphicsQuality_Azm
    {
        Low,
        High
    }

    /*
     * Layer: Core (app-level singleton, persists across scenes - one of only two allowed by
     * CLAUDE.md; the other is AppManager_Azm).
     * Purpose: Loads, applies and saves the student's settings with PlayerPrefs: master volume,
     * SFX volume, hints on/off, graphics quality (Low/High), haptics (vibration) on/off, reduce
     * motion (accessibility: animations become instant) and large text (reading text grows by
     * UITheme_Azm.largeTextScale), plus the "first-time" flags used by the splash skip and by the
     * Learn-mode tutorials/hints ("reset tutorials" clears them).
     * Talks to: read/written by the Settings panel (Scripts/Menu) and by practical scenes
     * (hints setting, SFX volume). Raises SettingChanged so listeners can react immediately.
     * Everything is local PlayerPrefs - no backend, per CLAUDE.md.
     *
     * Graphics quality maps onto the project's two Quality levels by NAME ("Mobile" = Low,
     * "PC" = High); if a name is missing it falls back to the first/last level.
     */
    public class SettingsManager_Azm : MonoBehaviour
    {
        public static SettingsManager_Azm Instance { get; private set; }

        // Keys double as the string passed to SettingChanged so listeners can filter.
        public const string MasterVolumeKey = "Azm.Settings.MasterVolume";
        public const string SfxVolumeKey = "Azm.Settings.SfxVolume";
        public const string HintsEnabledKey = "Azm.Settings.HintsEnabled";
        public const string GraphicsQualityKey = "Azm.Settings.GraphicsQuality";
        public const string HapticsEnabledKey = "Azm.Settings.HapticsEnabled";
        public const string ReduceMotionKey = "Azm.Settings.ReduceMotion";
        public const string LargeTextKey = "Azm.Settings.LargeText";

        private const string HasLaunchedBeforeKey = "Azm.HasLaunchedBefore";
        private const string TutorialFlagPrefix = "Azm.Tutorial.";
        // PlayerPrefs cannot list its keys, so the tutorial flags we set are indexed here so
        // ResetTutorials can clear exactly those.
        private const string TutorialIndexKey = "Azm.Tutorial.Index";

        public event Action<string> SettingChanged;

        private float masterVolume;
        private float sfxVolume;
        private bool hintsEnabled;
        private GraphicsQuality_Azm graphicsQuality;
        private bool hapticsEnabled;
        private bool reduceMotion;
        private bool largeText;

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
                AudioListener.volume = masterVolume;
                Changed(MasterVolumeKey);
            }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
                Changed(SfxVolumeKey);
            }
        }

        public bool HintsEnabled
        {
            get => hintsEnabled;
            set
            {
                hintsEnabled = value;
                PlayerPrefs.SetInt(HintsEnabledKey, value ? 1 : 0);
                Changed(HintsEnabledKey);
            }
        }

        public GraphicsQuality_Azm GraphicsQuality
        {
            get => graphicsQuality;
            set
            {
                graphicsQuality = value;
                PlayerPrefs.SetInt(GraphicsQualityKey, (int)value);
                ApplyGraphicsQuality();
                Changed(GraphicsQualityKey);
            }
        }

        // Short vibrations on taps, ticks and results (Haptics_Azm). On by default.
        public bool HapticsEnabled
        {
            get => hapticsEnabled;
            set
            {
                hapticsEnabled = value;
                PlayerPrefs.SetInt(HapticsEnabledKey, value ? 1 : 0);
                Changed(HapticsEnabledKey);
            }
        }

        // Accessibility: pop/shake/slide animations become instant (Motion_Azm). Off by default.
        public bool ReduceMotion
        {
            get => reduceMotion;
            set
            {
                reduceMotion = value;
                PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0);
                Changed(ReduceMotionKey);
            }
        }

        // Accessibility: reading text (instructions, hints, guide, briefing) is drawn larger. Off by default.
        public bool LargeText
        {
            get => largeText;
            set
            {
                largeText = value;
                PlayerPrefs.SetInt(LargeTextKey, value ? 1 : 0);
                Changed(LargeTextKey);
            }
        }

        // True once the app has been launched at least once; the splash is only skippable after
        // the first launch.
        public bool HasLaunchedBefore => PlayerPrefs.GetInt(HasLaunchedBeforeKey, 0) == 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[SettingsManager_Azm] Duplicate on '{name}' - keeping the existing one on '{Instance.name}'.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
            AudioListener.volume = masterVolume;
            ApplyGraphicsQuality();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Load()
        {
            masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            hintsEnabled = PlayerPrefs.GetInt(HintsEnabledKey, 1) == 1;
            hapticsEnabled = PlayerPrefs.GetInt(HapticsEnabledKey, 1) == 1;
            reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            largeText = PlayerPrefs.GetInt(LargeTextKey, 0) == 1;

            // Default to the lighter level on a real phone, the fuller one in the editor/desktop.
            int defaultQuality = Application.isMobilePlatform ? (int)GraphicsQuality_Azm.Low : (int)GraphicsQuality_Azm.High;
            graphicsQuality = (GraphicsQuality_Azm)PlayerPrefs.GetInt(GraphicsQualityKey, defaultQuality);
        }

        private void ApplyGraphicsQuality()
        {
            string[] names = QualitySettings.names;
            string wanted = graphicsQuality == GraphicsQuality_Azm.Low ? "Mobile" : "PC";
            int index = Array.IndexOf(names, wanted);
            if (index < 0) index = graphicsQuality == GraphicsQuality_Azm.Low ? 0 : names.Length - 1;
            QualitySettings.SetQualityLevel(index, true);
        }

        private void Changed(string key)
        {
            PlayerPrefs.Save();
            SettingChanged?.Invoke(key);
        }

        public void MarkLaunched()
        {
            PlayerPrefs.SetInt(HasLaunchedBeforeKey, 1);
            PlayerPrefs.Save();
        }

        // --- First-time flags (briefing shown, hint shown, ...) ---

        public bool HasSeenTutorial(string id)
        {
            return PlayerPrefs.GetInt(TutorialFlagPrefix + id, 0) == 1;
        }

        public void MarkTutorialSeen(string id)
        {
            PlayerPrefs.SetInt(TutorialFlagPrefix + id, 1);

            var index = new List<string>(PlayerPrefs.GetString(TutorialIndexKey, string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (!index.Contains(id))
            {
                index.Add(id);
                PlayerPrefs.SetString(TutorialIndexKey, string.Join(",", index));
            }
            PlayerPrefs.Save();
        }

        // The "reset tutorials" action on the Settings panel: clears every first-time flag so the
        // briefing and hints fire again. Does NOT clear HasLaunchedBefore (that only controls
        // whether the splash can be skipped).
        public void ResetTutorials()
        {
            string[] ids = PlayerPrefs.GetString(TutorialIndexKey, string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string id in ids)
                PlayerPrefs.DeleteKey(TutorialFlagPrefix + id);
            PlayerPrefs.DeleteKey(TutorialIndexKey);
            PlayerPrefs.Save();
        }
    }
}
