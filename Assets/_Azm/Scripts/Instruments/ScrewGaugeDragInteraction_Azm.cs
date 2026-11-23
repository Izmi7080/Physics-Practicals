using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Interaction (turns a touch/mouse drag on the thimble into meaning).
     * Purpose: The student turns the thimble by dragging up or down on it, as if rolling it with a
     * finger: the near surface follows the finger, so dragging DOWN opens the gauge (the front
     * surface moves down as the gap grows) and dragging UP closes it. The finger's movement is
     * converted to an angle using the thimble's radius ON SCREEN, so it feels the same at any zoom.
     * thimbleDragGain (config) can make it turn faster than the finger. The logic layer decides
     * whether it may turn (lock, contact).
     * It reads NO device: it listens to IPracticalInput_Azm and calls TurnThimbleDeg.
     * Talks to: IPracticalInput_Azm; ScrewGaugeMeasurement_Azm; the thimble's grab collider; a
     * Camera. Implements IDragOwner_Azm so the camera does not orbit while the thimble is turned.
     */
    public class ScrewGaugeDragInteraction_Azm : MonoBehaviour, IGrabbable_Azm
    {
        [Tooltip("Any component implementing IPracticalInput_Azm.")]
        public MonoBehaviour inputSource;
        public ScrewGaugeMeasurement_Azm measurement;
        [Tooltip("The thimble (its world position is on the axis).")]
        public Transform thimble;
        [Tooltip("A generous collider around the thimble and ratchet (the grab area).")]
        public Collider thimbleHit;
        public Camera viewCamera;
        [Tooltip("Thimble radius in metres (16 mm diameter).")]
        public float thimbleRadius = 0.008f;

        public bool IsDragging { get; private set; }

        // ----- onboarding (IGrabbable_Azm, HCI restructure Stage 5) -----
        public bool GrabAvailable => isActiveAndEnabled && measurement != null && measurement.IsMeasuring;
        public Vector3 GrabPoint => thimbleHit != null ? thimbleHit.bounds.center : thimble.position;
        // Dragging UP on the thimble closes the gauge (see OnDrag), the usual first move: 2 cm up.
        public Vector3 GrabHintMotion => Vector3.up * 0.02f;
        public string GrabHintId => "screwgauge.thimble";
        public bool HasOwnAffordance => false;

        private IPracticalInput_Azm input;

        private void Awake()
        {
            input = inputSource as IPracticalInput_Azm;
            if (input == null) Debug.LogError("[ScrewGaugeDragInteraction_Azm] inputSource does not implement IPracticalInput_Azm.", this);
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.PointerDown += OnDown;
            input.PointerDrag += OnDrag;
            input.PointerUp += OnUp;
            input.PointerCancel += OnCancel;
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.PointerDown -= OnDown;
            input.PointerDrag -= OnDrag;
            input.PointerUp -= OnUp;
            input.PointerCancel -= OnCancel;
        }

        private void OnDown(Vector2 screenPosition)
        {
            if (!measurement.IsMeasuring) return;
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            IsDragging = thimbleHit.Raycast(ray, out RaycastHit _, 100f);
        }

        private void OnDrag(Vector2 screenPosition, Vector2 delta)
        {
            if (!IsDragging) return;
            // The thimble's radius in pixels: one radius of finger travel = one radian of turn.
            Vector3 c = viewCamera.WorldToScreenPoint(thimble.position);
            Vector3 top = viewCamera.WorldToScreenPoint(thimble.position + Vector3.up * thimbleRadius);
            float radiusPx = Mathf.Max(8f, Vector2.Distance(c, top));
            float degrees = -delta.y / radiusPx * Mathf.Rad2Deg * measurement.config.thimbleDragGain;
            measurement.TurnThimbleDeg(degrees);
        }

        private void OnUp(Vector2 screenPosition) => IsDragging = false;
        private void OnCancel() => IsDragging = false;
    }
}
