using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Logic support (data asset - CLAUDE.md: tunable numbers live in ScriptableObject configs).
     * Purpose: The numbers of the vernier caliper instrument and of the measuring interaction. One
     * asset is shared by every practical that uses the caliper, so the instrument is defined once.
     * The scale values match the 3D model (Assets/_Azm/Art/Models/VernierCaliper, see its NOTES):
     * 1 mm main divisions, 10 vernier divisions spanning 9 mm, main scale 0-150 mm.
     * Talks to: read by CaliperMeasurement_Azm, CaliperMagnifierUI_Azm and CaliperReadingPadUI_Azm.
     */
    [CreateAssetMenu(fileName = "CaliperConfig_Azm", menuName = "VirtualLab_Azm/Caliper Config")]
    public class CaliperConfig_Azm : ScriptableObject
    {
        [Header("Instrument (school vernier caliper)")]
        [Tooltip("One main-scale division, mm.")]
        public float mainDivisionMm = 1f;
        [Tooltip("Vernier divisions (10 divisions = 9 mm, so the least count is 1 mm / 10 = 0.1 mm).")]
        public int vernierDivisions = 10;
        [Tooltip("Highest main-scale mark, mm (the model's scale runs 0-150 mm).")]
        public float mainScaleMaxMm = 150f;
        [Tooltip("Largest jaw opening the slider can reach, mm.")]
        public float maxOpeningMm = 150f;

        [Header("Measuring interaction")]
        [Tooltip("Jaw opening when a measurement starts, mm - open enough that the object clearly fits.")]
        public float startOpeningMm = 34f;
        [Tooltip("A reading within this many least counts of the true value is accepted (task: within one least count).")]
        public int toleranceLeastCounts = 1;

        [Header("Magnifier")]
        [Tooltip("Scale of the code-drawn magnifier, reference pixels (1920x1080) per mm.")]
        public float magnifierPixelsPerMm = 44f;

        public float LeastCountMm => VernierScale_Azm.LeastCount(mainDivisionMm, vernierDivisions);
        public float VernierSpacingMm => VernierScale_Azm.VernierSpacing(mainDivisionMm, vernierDivisions);
    }
}
