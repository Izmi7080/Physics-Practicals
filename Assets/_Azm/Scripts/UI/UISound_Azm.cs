using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (feedback sounds).
     * Purpose: Plays the app's short feedback sounds by meaning (SoundId_Azm) from a SoundBank_Azm
     * asset, with a small random pitch so repeated ticks sound natural. If no bank is assigned it
     * falls back to tiny sine bursts generated in code (the original Stage-4 sounds), so a scene
     * never goes silent. Volume follows the SFX setting (SettingsManager_Azm); the master volume is
     * applied by the AudioListener.
     * SCENE-LOCAL SINGLETON (CLAUDE.md rules): one per practical scene, exposed as Current so the
     * static FeedbackFX_Azm can find it; never DontDestroyOnLoad; Current is cleared in OnDestroy.
     * Talks to: SettingsManager_Azm (SFX volume); called by FeedbackFX_Azm and the HUDs.
     */
    [RequireComponent(typeof(AudioSource))]
    public class UISound_Azm : MonoBehaviour
    {
        public static UISound_Azm Current { get; private set; }

        public SoundBank_Azm bank;
        [Range(0f, 1f)] public float clickVolume = 0.35f;
        [Range(0f, 1f)] public float feedbackVolume = 0.45f;

        private AudioSource source;
        private AudioClip click, correct, wrong;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning($"[UISound_Azm] Second instance on '{name}' - '{Current.name}' already plays sounds in this scene. Removing this one.", this);
                Destroy(this);
                return;
            }
            Current = this;

            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            const int rate = 44100;
            click = Tone("Azm_Click", new[] { 1800f }, 0.025f, rate);
            correct = Tone("Azm_Correct", new[] { 880f, 1320f }, 0.09f, rate);
            wrong = Tone("Azm_Wrong", new[] { 220f, 196f }, 0.12f, rate);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // Fallback clip: a sequence of short notes with a fast decay.
        private static AudioClip Tone(string name, float[] notes, float noteSeconds, int sampleRate)
        {
            int perNote = Mathf.RoundToInt(noteSeconds * sampleRate);
            var data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)sampleRate;
                    float envelope = Mathf.Exp(-t * 40f) * Mathf.Clamp01(i / 60f);
                    data[n * perNote + i] = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * envelope;
                }
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sfx => SettingsManager_Azm.Instance != null ? SettingsManager_Azm.Instance.SfxVolume : 1f;

        public void Play(SoundId_Azm id, float volumeScale = 1f)
        {
            if (source == null) return;
            var entry = bank != null ? bank.Get(id) : null;
            if (entry != null && entry.clip != null)
            {
                source.pitch = 1f + Random.Range(-entry.pitchVariation, entry.pitchVariation);
                source.PlayOneShot(entry.clip, entry.volume * volumeScale * Sfx);
                return;
            }

            // No bank / no clip for this id: the procedural fallback.
            source.pitch = 1f;
            switch (id)
            {
                case SoundId_Azm.Confirm:
                case SoundId_Azm.Chime:
                case SoundId_Azm.Success: source.PlayOneShot(correct, feedbackVolume * volumeScale * Sfx); break;
                case SoundId_Azm.Wrong: source.PlayOneShot(wrong, feedbackVolume * volumeScale * Sfx); break;
                default: source.PlayOneShot(click, clickVolume * volumeScale * Sfx); break;
            }
        }

        // Older API, kept so existing callers keep working.
        public void PlayClick() => Play(SoundId_Azm.Click);
        public void PlayRatchet() => Play(SoundId_Azm.Ratchet);
        public void PlayCorrect() => Play(SoundId_Azm.Confirm);
        public void PlayWrong() => Play(SoundId_Azm.Wrong);

        // Adds the click to every button under root that has no ButtonFeel_Azm (which plays its own).
        public void HookButtons(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.transition != Selectable.Transition.None && button.GetComponent<ButtonFeel_Azm>() == null)
                    button.onClick.AddListener(PlayClick);
        }
    }
}
