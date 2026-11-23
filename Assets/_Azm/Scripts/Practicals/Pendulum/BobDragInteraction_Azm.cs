using System.Collections.Generic;
using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: Interaction (turns a touch/mouse drag on the bob into meaning).
     * Purpose: Lets the student grab the bob and move it like a real object:
     *   - while setting the length, dragging up/down moves the bob and the thread length follows
     *     (the bob stays under the finger);
     *   - while setting the angle, dragging sideways swings the bob to that angle; lifting the
     *     finger releases it.
     * It works on the swing plane (the vertical plane the pendulum moves in): the finger's ray is
     * intersected with that plane, so the mapping is exact from every camera view and zoom.
     * At release it also measures how fast the bob was still moving (degrees per second over the
     * last ~0.1 s) and passes that on, so the logic layer can tell a gentle release from a push.
     * It reads NO device: it listens to IPracticalInput_Azm, and only calls intent methods on the
     * controller (SetLengthCm, SetDisplacementDeg, ReleaseBob...). Decisions stay in the logic layer.
     * Talks to: IPracticalInput_Azm events; PendulumController_Azm (intent + DragMode);
     * PendulumRigView_Azm (pivot anchor and the bob's hit collider); a Camera for the ray.
     */
    public class BobDragInteraction_Azm : MonoBehaviour, IGrabbable_Azm
    {
        [Tooltip("Any component implementing IPracticalInput_Azm.")]
        public MonoBehaviour inputSource;
        public PendulumController_Azm controller;
        public PendulumRigView_Azm rig;
        public Camera viewCamera;

        public bool IsDragging { get; private set; }

        // ----- onboarding (IGrabbable_Azm, HCI restructure Stage 5) -----
        public bool GrabAvailable => isActiveAndEnabled && controller.DragMode != DragMode_Azm.None;
        public Vector3 GrabPoint => rig.bob.position;
        // Setting the length: drag DOWN 8 cm. Releasing: pull SIDEWAYS ~10° (12 cm at 70 cm length).
        public Vector3 GrabHintMotion =>
            controller.DragMode == DragMode_Azm.Length ? Vector3.down * 0.08f : rig.anchor.right * 0.12f;
        public string GrabHintId => controller.DragMode == DragMode_Azm.Length ? "pendulum.length" : "pendulum.angle";
        public bool HasOwnAffordance => true;   // the rig draws its own grab ring around the bob

        private IPracticalInput_Azm input;
        private float grabOffsetY;      // finger-to-bob-centre offset when length dragging starts
        private struct Sample { public float time; public float angle; }
        private readonly List<Sample> history = new List<Sample>();

        private void Awake()
        {
            input = inputSource as IPracticalInput_Azm;
            if (input == null) Debug.LogError("[BobDragInteraction_Azm] inputSource does not implement IPracticalInput_Azm.", this);
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

        // The finger's position on the swing plane, in the (non-rotating) anchor's local space:
        // x to the right, y up, origin at the pivot.
        private bool TryGetLocalPoint(Vector2 screenPosition, out Vector2 local)
        {
            local = Vector2.zero;
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(rig.anchor.forward, rig.anchor.position);
            if (!plane.Raycast(ray, out float enter)) return false;
            Vector3 p = rig.anchor.InverseTransformPoint(ray.GetPoint(enter));
            local = new Vector2(p.x, p.y);
            return true;
        }

        private void OnDown(Vector2 screenPosition)
        {
            if (controller.DragMode == DragMode_Azm.None) return;

            // Only the bob's (generous) hit sphere counts; anything in front of it is ignored.
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (!rig.bobHit.Raycast(ray, out RaycastHit hit, 100f)) return;

            IsDragging = true;
            history.Clear();
            controller.SetBobHeld(true);

            if (controller.DragMode == DragMode_Azm.Length && TryGetLocalPoint(screenPosition, out Vector2 local))
            {
                float bobCentreY = -(controller.LengthCm + controller.TrueRadiusCm) / 100f;
                grabOffsetY = local.y - bobCentreY;   // keeps the bob from jumping to the finger
            }
        }

        private void OnDrag(Vector2 screenPosition, Vector2 delta)
        {
            if (!IsDragging) return;
            if (!TryGetLocalPoint(screenPosition, out Vector2 local)) return;

            if (controller.DragMode == DragMode_Azm.Length)
            {
                // The bob's centre is l + r below the pivot, so l = (depth of the bob centre) - r.
                float depthM = -(local.y - grabOffsetY);
                controller.SetLengthCm(depthM * 100f - controller.TrueRadiusCm);
            }
            else if (controller.DragMode == DragMode_Azm.Angle)
            {
                // Angle from straight down; positive = to the right.
                if (local.magnitude < 0.02f) return;
                float angle = Mathf.Atan2(local.x, -local.y) * Mathf.Rad2Deg;
                controller.SetDisplacementDeg(angle);
                // realtimeSinceStartup (not Time.unscaledTime) so samples taken within one frame still get distinct times.
                history.Add(new Sample { time = Time.realtimeSinceStartup, angle = angle });
                if (history.Count > 12) history.RemoveAt(0);
            }
        }

        private void OnUp(Vector2 screenPosition)
        {
            if (!IsDragging) return;
            IsDragging = false;

            if (controller.DragMode == DragMode_Azm.Angle) controller.ReleaseBob(CurrentSpeedDegPerSec());
            else controller.SetBobHeld(false);
        }

        private void OnCancel()
        {
            if (!IsDragging) return;
            IsDragging = false;
            controller.CancelDrag();
        }

        // How fast the bob was still moving when the finger lifted. A finger that stopped before
        // lifting has no recent samples, so the speed is ~0 (a gentle release from rest).
        private float CurrentSpeedDegPerSec()
        {
            if (history.Count < 2) return 0f;
            Sample last = history[history.Count - 1];
            if (Time.realtimeSinceStartup - last.time > 0.06f) return 0f;

            // Use the oldest sample within the last 0.12 s.
            Sample first = last;
            for (int i = history.Count - 2; i >= 0; i--)
            {
                if (last.time - history[i].time > 0.12f) break;
                first = history[i];
            }
            float dt = last.time - first.time;
            return dt > 0.005f ? Mathf.Abs(last.angle - first.angle) / dt : 0f;
        }
    }
}
