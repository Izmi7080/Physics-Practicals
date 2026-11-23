using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (feedback presets; no practical logic).
     * Purpose: ONE call = sound + haptic + motion, so every practical gives the same feedback for the
     * same kind of event and the "feel" can be tuned in one file. For example
     * FeedbackFX_Azm.Correct(field) plays the confirm chime, a Confirm vibration and punches the
     * field. Any argument may be null (e.g. a sound and vibration with no visual).
     * THE JUICE RULE (TASK_hci_and_juice): these presets follow what the student DID; they must
     * never be used to reveal a measurement (for example, no tick when the vernier lines coincide).
     * Talks to: UISound_Azm.Current (the scene's sound player), Haptics_Azm, Motion_Azm.
     */
    public static class FeedbackFX_Azm
    {
        private static void Sound(SoundId_Azm id, float volume = 1f)
        {
            if (UISound_Azm.Current != null) UISound_Azm.Current.Play(id, volume);
        }

        // A button or key press.
        public static void Tap(Transform target)
        {
            Sound(SoundId_Azm.Click);
            Haptics_Azm.Play(HapticKind_Azm.Light);
        }

        // A correct reading / answer.
        public static void Correct(Transform target)
        {
            Sound(SoundId_Azm.Confirm);
            Haptics_Azm.Play(HapticKind_Azm.Confirm);
            if (target != null) Motion_Azm.Punch(target, 0.1f);
        }

        // A wrong reading: a gentle shake and a soft sound - corrective, not punishing.
        public static void Wrong(RectTransform target)
        {
            Sound(SoundId_Azm.Wrong);
            Haptics_Azm.Play(HapticKind_Azm.Reject);
            if (target != null) Motion_Azm.Shake(target);
        }

        // A warning (not ready yet, use the ratchet...): sound only, light vibration.
        public static void Warning(RectTransform target)
        {
            Sound(SoundId_Azm.Wrong, 0.6f);
            Haptics_Azm.Play(HapticKind_Azm.Light);
            if (target != null) Motion_Azm.Shake(target, 8f);
        }

        // A step finished (the progress bar segment fills).
        public static void StepDone(Transform target)
        {
            Sound(SoundId_Azm.Chime);
            Haptics_Azm.Play(HapticKind_Azm.Medium);
            if (target != null) Motion_Azm.Punch(target, 0.25f);
        }

        // A value appearing (a chip, a table row, a revealed calculation line).
        public static void Reveal(Transform target)
        {
            Sound(SoundId_Azm.Pop, 0.8f);
            if (target != null) Motion_Azm.PopIn(target);
        }

        // One unit of travel on an instrument (1 mm of jaw, 5 divisions of thimble) - follows the
        // student's hand, never the reading.
        public static void Tick()
        {
            Sound(SoundId_Azm.Tick, 0.6f);
            Haptics_Azm.Play(HapticKind_Azm.Tick);
        }

        // Jaws / spindle touching the object.
        public static void Contact()
        {
            Sound(SoundId_Azm.Tock);
            Haptics_Azm.Play(HapticKind_Azm.Medium);
        }

        public static void Ratchet()
        {
            Sound(SoundId_Azm.Ratchet);
            Haptics_Azm.Play(HapticKind_Azm.Heavy);
        }

        public static void Lock()
        {
            Sound(SoundId_Azm.Lock);
            Haptics_Azm.Play(HapticKind_Azm.Medium);
        }

        public static void PanelSlide() => Sound(SoundId_Azm.Whoosh, 0.5f);

        // The final result of a practical.
        public static void Success(Transform target)
        {
            Sound(SoundId_Azm.Success);
            Haptics_Azm.Play(HapticKind_Azm.Heavy);
            if (target != null) Motion_Azm.PopIn(target, 0.7f);
        }
    }
}
