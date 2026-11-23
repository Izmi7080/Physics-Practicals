using UnityEngine;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: UI / presentation (feel only; HCI restructure Stage 6). No measuring logic.
     * Purpose: Makes the vernier caliper feel like a real instrument in the hand:
     *   - a soft TICK (sound + tiny vibration) for every whole millimetre the sliding jaw travels
     *     WHILE the student drags it - it follows the hand, like the detent of a real slide;
     *   - a TOCK + a small camera nudge the moment the jaws touch the object.
     * THE JUICE RULE: nothing here depends on the reading being right. There is deliberately no
     * sound when the vernier lines coincide, so the student still has to READ the scale.
     * Talks to: CaliperMeasurement_Azm (OpeningChanged, TouchingChanged), CaliperDragInteraction_Azm
     * (is the student dragging?), PracticalCameraRig_Azm (Nudge), FeedbackFX_Azm.
     */
    public class CaliperJuice_Azm : MonoBehaviour
    {
        public CaliperMeasurement_Azm measurement;
        public CaliperDragInteraction_Azm drag;
        public PracticalCameraRig_Azm cameraRig;
        [Tooltip("Minimum seconds between two ticks (a fast drag does not become a buzz).")]
        public float minTickInterval = 0.035f;

        private int lastWholeMm = int.MinValue;
        private float lastTickTime;

        private void OnEnable()
        {
            if (measurement == null) return;
            measurement.OpeningChanged += OnOpeningChanged;
            measurement.TouchingChanged += OnTouchingChanged;
        }

        private void OnDisable()
        {
            if (measurement == null) return;
            measurement.OpeningChanged -= OnOpeningChanged;
            measurement.TouchingChanged -= OnTouchingChanged;
        }

        private void OnOpeningChanged()
        {
            int mm = Mathf.FloorToInt(measurement.OpeningMm);
            bool crossed = lastWholeMm != int.MinValue && mm != lastWholeMm;
            lastWholeMm = mm;
            // Only for the student's own drag (not for "Show me" or a reset).
            if (!crossed || drag == null || !drag.IsDragging) return;
            if (Time.unscaledTime - lastTickTime < minTickInterval) return;
            lastTickTime = Time.unscaledTime;
            FeedbackFX_Azm.Tick();
        }

        private void OnTouchingChanged(bool touching)
        {
            if (!touching || !measurement.IsMeasuring) return;
            FeedbackFX_Azm.Contact();
            if (cameraRig != null) cameraRig.Nudge();
        }
    }
}
