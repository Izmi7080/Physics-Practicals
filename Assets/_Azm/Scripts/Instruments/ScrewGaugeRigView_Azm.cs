using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: UI / presentation (moves the 3D screw gauge; no decisions).
     * Purpose: Poses the model (Art/Models/ScrewGauge, see its NOTES - verified in Unity):
     *   Spindle_Azm.localPosition = (-gap, 0, 0)                     gap in metres
     *   Spindle_Azm.localRotation = Euler(gap / pitch × 360°, 0, 0)  one turn per 0.5 mm
     * The thimble and ratchet are children of the spindle and follow. The zero error turns the
     * thimble's scale relative to the spindle: Euler(e / LC × 7.2°, 0, 0) (+ = reads high), so with
     * the gauge closed the circular 0 sits below (+) or above (−) the datum line, as on a real gauge.
     * A ratchet click makes the ratchet twitch on its own; the lock lever turns when locked.
     * Talks to: ScrewGaugeMeasurement_Azm (gap, zero error, lock, Clicked).
     */
    public class ScrewGaugeRigView_Azm : MonoBehaviour
    {
        public ScrewGaugeMeasurement_Azm measurement;
        public Transform spindle;
        public Transform thimble;
        public Transform ratchet;
        public Transform lockLever;
        public float clickWobbleDegrees = 18f;
        public float lockTurnDegrees = 80f;

        private Vector3 spindleRest;
        private Quaternion thimbleRest, ratchetRest, lockRest;
        private float wobble;      // 1 right after a click, decays to 0
        private float lockAmount;

        private void Awake()
        {
            spindleRest = spindle.localPosition;
            thimbleRest = thimble.localRotation;
            ratchetRest = ratchet.localRotation;
            if (lockLever != null) lockRest = lockLever.localRotation;
        }

        private void OnEnable() => measurement.Clicked += OnClicked;
        private void OnDisable() => measurement.Clicked -= OnClicked;
        private void OnClicked() => wobble = 1f;

        private void LateUpdate()
        {
            var cfg = measurement.config;
            float gapMm = measurement.GapMm;
            spindle.localPosition = spindleRest + new Vector3(-gapMm / 1000f, 0f, 0f);
            spindle.localRotation = Quaternion.Euler(gapMm / cfg.pitchMm * 360f, 0f, 0f);

            float zeroDeg = measurement.ZeroErrorMm / cfg.LeastCountMm * (360f / cfg.divisions);
            thimble.localRotation = thimbleRest * Quaternion.Euler(zeroDeg, 0f, 0f);

            wobble = Mathf.MoveTowards(wobble, 0f, Time.unscaledDeltaTime * 8f);
            ratchet.localRotation = ratchetRest * Quaternion.Euler(-Mathf.Sin(wobble * Mathf.PI) * clickWobbleDegrees, 0f, 0f);

            if (lockLever != null)
            {
                lockAmount = Mathf.MoveTowards(lockAmount, measurement.IsLocked ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                lockLever.localRotation = lockRest * Quaternion.Euler(0f, lockAmount * lockTurnDegrees, 0f);
            }
        }
    }
}
