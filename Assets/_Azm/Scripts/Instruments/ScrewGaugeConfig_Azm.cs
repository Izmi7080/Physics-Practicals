using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Logic support (data asset - CLAUDE.md: tunable numbers live in ScriptableObject configs).
     * Purpose: The numbers of the screw gauge instrument and of turning it on a touchscreen. The
     * scale values match the 3D model (Art/Models/ScrewGauge, see its NOTES) and the textbook
     * (Exp 1.3): pitch 0.5 mm, 50 circular divisions, so LC = 0.5 mm / 50 = 0.01 mm; range 0-25 mm.
     * Talks to: read by ScrewGaugeMeasurement_Azm, ScrewGaugeMagnifierUI_Azm, the drag interaction.
     */
    [CreateAssetMenu(fileName = "ScrewGaugeConfig_Azm", menuName = "VirtualLab_Azm/Screw Gauge Config")]
    public class ScrewGaugeConfig_Azm : ScriptableObject
    {
        [Header("Instrument (textbook Exp 1.3)")]
        [Tooltip("Pitch: spindle travel for one full turn of the thimble, mm.")]
        public float pitchMm = 0.5f;
        [Tooltip("Divisions on the circular (thimble) scale.")]
        public int divisions = 50;
        [Tooltip("Largest gap, mm (0-25 mm micrometer).")]
        public float rangeMm = 25f;

        [Header("Turning it (touch)")]
        [Tooltip("ASSUMPTION: ratchet speed while held, turns per second (fast enough to close a few mm in a second or two).")]
        public float ratchetTurnsPerSecond = 2f;
        [Tooltip("ASSUMPTION: 'Open' button speed while held, turns per second.")]
        public float openTurnsPerSecond = 2.5f;
        [Tooltip("Thimble drag: 1 = the thimble surface follows the finger; > 1 turns it faster.")]
        public float thimbleDragGain = 1.5f;
        [Tooltip("Seconds between repeated ratchet clicks while it is held at contact.")]
        public float clickInterval = 0.15f;
        [Tooltip("Gap opened when a measurement starts, mm beyond the object's size.")]
        public float startClearanceMm = 1.5f;

        [Header("Checking a reading")]
        [Tooltip("A reading within this many least counts of what the scales show is accepted.")]
        public int toleranceLeastCounts = 1;

        [Header("Magnifier")]
        public float magnifierPixelsPerMm = 80f;
        public float magnifierPixelsPerDivision = 30f;

        public float LeastCountMm => pitchMm / divisions;   // 0.5 / 50 = 0.01 mm
    }
}
