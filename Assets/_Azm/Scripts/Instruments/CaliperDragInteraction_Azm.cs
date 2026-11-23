using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Interaction (turns a touch/mouse drag on the sliding jaw into meaning).
     * Purpose: The student grabs the sliding unit of the caliper and slides it along the beam, like
     * a thumb on the thumb rest. Only movement ALONG the beam counts: the finger's ray is
     * intersected with the caliper's front plane and the point is projected onto the beam axis, so
     * the jaw follows the finger exactly from any camera angle and zoom. The grab offset keeps the
     * jaw from jumping to the finger. Contact with the object is decided by the logic layer
     * (CaliperMeasurement_Azm clamps the opening), not here.
     * It reads NO device: it listens to IPracticalInput_Azm and calls RequestOpeningMm.
     * Talks to: IPracticalInput_Azm; CaliperMeasurement_Azm; CaliperRigView_Azm (axis mapping);
     * the slider's hit collider; a Camera for the ray. Implements IDragOwner_Azm so the camera
     * does not orbit while the jaw is dragged.
     */
    public class CaliperDragInteraction_Azm : MonoBehaviour, IGrabbable_Azm
    {
        [Tooltip("Any component implementing IPracticalInput_Azm.")]
        public MonoBehaviour inputSource;
        public CaliperMeasurement_Azm measurement;
        public CaliperRigView_Azm rig;
        [Tooltip("A generous collider around the sliding unit (the grab area).")]
        public Collider sliderHit;
        public Camera viewCamera;

        public bool IsDragging { get; private set; }

        // ----- onboarding (IGrabbable_Azm, HCI restructure Stage 5) -----
        public bool GrabAvailable => isActiveAndEnabled && measurement != null && measurement.DragEnabled;
        public Vector3 GrabPoint => sliderHit != null ? sliderHit.bounds.center : transform.position;
        // A first drag CLOSES the jaw onto the object: from the slider back towards the fixed jaw
        // (the model root is at the main-scale zero), 2.5 cm.
        public Vector3 GrabHintMotion
        {
            get
            {
                Vector3 towardsZero = rig.caliperRoot.position - rig.slider.position;
                if (towardsZero.sqrMagnitude < 1e-6f) towardsZero = -rig.caliperRoot.right;
                return towardsZero.normalized * 0.025f;
            }
        }
        public string GrabHintId => "caliper.jaw";
        public bool HasOwnAffordance => false;

        private IPracticalInput_Azm input;
        private float grabOffsetMm;

        private void Awake()
        {
            input = inputSource as IPracticalInput_Azm;
            if (input == null) Debug.LogError("[CaliperDragInteraction_Azm] inputSource does not implement IPracticalInput_Azm.", this);
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.PointerDown += OnDown;
            input.PointerDrag += OnDrag;
            input.PointerUp += OnUp;
            input.PointerCancel += OnUp0;
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.PointerDown -= OnDown;
            input.PointerDrag -= OnDrag;
            input.PointerUp -= OnUp;
            input.PointerCancel -= OnUp0;
        }

        // The finger's position as "mm along the beam from the main-scale zero".
        private bool TryGetOpeningMm(Vector2 screenPosition, out float mm)
        {
            mm = 0f;
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(rig.caliperRoot.forward, rig.caliperRoot.position);
            if (!plane.Raycast(ray, out float enter)) return false;
            mm = rig.WorldToOpeningMm(ray.GetPoint(enter));
            return true;
        }

        private void OnDown(Vector2 screenPosition)
        {
            if (!measurement.DragEnabled) return;
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (!sliderHit.Raycast(ray, out RaycastHit _, 100f)) return;
            if (!TryGetOpeningMm(screenPosition, out float fingerMm)) return;
            grabOffsetMm = fingerMm - measurement.OpeningMm;
            IsDragging = true;
        }

        private void OnDrag(Vector2 screenPosition, Vector2 delta)
        {
            if (!IsDragging) return;
            if (!measurement.DragEnabled) { IsDragging = false; return; }
            if (TryGetOpeningMm(screenPosition, out float fingerMm))
                measurement.RequestOpeningMm(fingerMm - grabOffsetMm);
        }

        private void OnUp(Vector2 screenPosition)
        {
            IsDragging = false;
        }

        private void OnUp0()
        {
            IsDragging = false;
        }
    }
}
