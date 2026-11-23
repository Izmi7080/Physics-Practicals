using UnityEngine;

namespace VirtualLab.Azm.Cylinder
{
    /*
     * Layer: UI / presentation (moves the 3D cylinder; no decisions).
     * Purpose: Places the solid cylinder for the current step - resting on the tray while the
     * least count and zero error are found (the jaws must close on nothing), standing ACROSS the
     * outside jaws for the diameter readings (end-on to the camera, so the circle being measured is
     * visible), and LENGTHWISE between the jaws for the length readings. It glides between the
     * poses (about 0.5 s) so the change is easy to follow.
     * The poses are empty Transforms placed by the scene builder in the caliper's frame.
     * Talks to: CylinderController_Azm (StepChanged, CurrentStepId).
     */
    public class CylinderRigView_Azm : MonoBehaviour
    {
        public CylinderController_Azm controller;
        public Transform cylinder;
        public Transform restPose;
        public Transform acrossPose;
        public Transform lengthPose;
        public float glideSeconds = 0.5f;

        private Transform target;
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private float t = 1f;

        private void OnEnable() => controller.StepChanged += OnStepChanged;
        private void OnDisable() => controller.StepChanged -= OnStepChanged;

        private void Start()
        {
            target = PoseFor(controller.CurrentStepId);
            cylinder.SetPositionAndRotation(target.position, target.rotation);
        }

        private Transform PoseFor(CylinderStepId_Azm id)
        {
            switch (id)
            {
                case CylinderStepId_Azm.Diameter: return acrossPose;
                case CylinderStepId_Azm.Length: return lengthPose;
                default: return restPose;
            }
        }

        private void OnStepChanged(int index)
        {
            var next = PoseFor(controller.CurrentStepId);
            if (next == target) return;
            target = next;
            fromPosition = cylinder.position;
            fromRotation = cylinder.rotation;
            t = 0f;
        }

        private void LateUpdate()
        {
            if (t >= 1f || target == null) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / glideSeconds);
            float e = Mathf.SmoothStep(0f, 1f, t);
            // Lift it over the jaws on the way (an arc), then set it down.
            Vector3 p = Vector3.Lerp(fromPosition, target.position, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.05f);
            cylinder.SetPositionAndRotation(p, Quaternion.Slerp(fromRotation, target.rotation, e));
        }
    }
}
