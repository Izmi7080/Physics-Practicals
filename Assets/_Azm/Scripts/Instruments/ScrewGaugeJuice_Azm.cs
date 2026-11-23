using UnityEngine;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: UI / presentation (feel only; HCI restructure Stage 6). No measuring logic.
     * Purpose: Makes the screw gauge feel mechanical:
     *   - a soft TICK every 5 thimble divisions (0.05 mm with a 0.01 mm gauge) WHILE the student
     *     turns the thimble - it follows the hand, never the reading;
     *   - when the spindle is closed onto the object with the THIMBLE instead of the ratchet, the
     *     object is visibly squeezed (a short squash) and flashes red: "too hard - use the ratchet".
     *     WHY: the syllabus precaution (use the ratchet so the object is not pressed too hard) is
     *     easier to remember when the student SEES the strip being pinched. The controller already
     *     shows the written warning; this is only the physical cue.
     * (The ratchet click and the lock are played by ScrewGaugeHUD_Azm.)
     * Talks to: ScrewGaugeMeasurement_Azm (GapChanged, ContactByThimble), ScrewGaugeDragInteraction_Azm,
     * Motion_Azm / FeedbackFX_Azm.
     */
    public class ScrewGaugeJuice_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public ScrewGaugeMeasurement_Azm measurement;
        public ScrewGaugeDragInteraction_Azm drag;
        [Tooltip("The measured object (strip or wire) that gets squeezed.")]
        public Transform squeezedObject;
        [Tooltip("ASSUMPTION (feel): one tick per this many thimble divisions.")]
        public int divisionsPerTick = 5;
        public float minTickInterval = 0.035f;

        private int lastStep = int.MinValue;
        private float lastTickTime;

        private void OnEnable()
        {
            if (measurement == null) return;
            measurement.GapChanged += OnGapChanged;
            measurement.ContactByThimble += OnContactByThimble;
        }

        private void OnDisable()
        {
            if (measurement == null) return;
            measurement.GapChanged -= OnGapChanged;
            measurement.ContactByThimble -= OnContactByThimble;
        }

        private void OnGapChanged()
        {
            float stepMm = measurement.LeastCountMm * Mathf.Max(1, divisionsPerTick);
            int step = Mathf.FloorToInt(measurement.GapMm / stepMm);
            bool crossed = lastStep != int.MinValue && step != lastStep;
            lastStep = step;
            if (!crossed || drag == null || !drag.IsDragging) return;
            if (Time.unscaledTime - lastTickTime < minTickInterval) return;
            lastTickTime = Time.unscaledTime;
            FeedbackFX_Azm.Tick();
        }

        private void OnContactByThimble()
        {
            if (squeezedObject == null) return;
            Motion_Azm.Squash(squeezedObject, 0.14f, 0.32f);
            Color red = theme != null ? theme.GetColor(ThemeColor_Azm.Invalid) : new Color(0.90f, 0.30f, 0.30f);
            foreach (var r in squeezedObject.GetComponentsInChildren<Renderer>())
                Motion_Azm.FlashRenderer(r, red, Motion_Azm.Slow + 0.2f);
        }
    }
}
