using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: UI / presentation (moves the 3D caliper; no decisions).
     * Purpose: Puts the model's sliding unit where the logic says the jaw is, and shifts the vernier
     * plate by the instrument's zero error.
     * The model (Art/Models/VernierCaliper, see its NOTES) is built so that Slider_Azm at local
     * position 0 means "jaws closed" and the jaw opening equals the slider's travel exactly. After
     * the FBX import, Blender's +X slide axis became Unity's LOCAL -X, so an opening of d metres is
     * Slider_Azm.localPosition.x = -d. The depth rod is a child of the slider and follows it.
     * VernierPlate_Azm (child of the slider) is moved by the zero error e the same way (local x = -e),
     * so with the jaws closed its 0 line sits e away from the main-scale 0, as on a real caliper.
     * Talks to: CaliperMeasurement_Azm (reads OpeningMm and ZeroErrorMm).
     */
    public class CaliperRigView_Azm : MonoBehaviour
    {
        public CaliperMeasurement_Azm measurement;
        [Tooltip("The model's root (VernierCaliper_Azm). Its origin is the main-scale zero.")]
        public Transform caliperRoot;
        [Tooltip("The model's Slider_Azm.")]
        public Transform slider;
        [Tooltip("Optional: the model's VernierPlate_Azm, shifted by the zero error.")]
        public Transform vernierPlate;

        private Vector3 sliderRest;
        private Vector3 plateRest;

        private void Awake()
        {
            sliderRest = slider.localPosition;
            if (vernierPlate != null) plateRest = vernierPlate.localPosition;
        }

        private void LateUpdate()
        {
            // Unity local -X is the opening direction (see the summary).
            slider.localPosition = sliderRest + new Vector3(-measurement.OpeningMm / 1000f, 0f, 0f);
            if (vernierPlate != null)
                vernierPlate.localPosition = plateRest + new Vector3(-measurement.ZeroErrorMm / 1000f, 0f, 0f);
        }

        // Converts a world point to "mm along the opening direction from the main-scale zero".
        public float WorldToOpeningMm(Vector3 worldPoint)
        {
            return -caliperRoot.InverseTransformPoint(worldPoint).x * 1000f;
        }
    }
}
