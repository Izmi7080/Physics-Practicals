using UnityEngine;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    /*
     * Layer: UI / presentation (moves the 3D strip; no decisions).
     * Purpose: Places the metal strip (or wire) for the current step - lying on the tray while the
     * pitch and the zero error are found (the gauge must close on nothing), and standing between the
     * anvil and the spindle for each thickness reading. Each reading uses a different place on the
     * strip (textbook: "take two more readings at different positions"), shown by shifting it a few
     * millimetres. It glides between poses so the change is easy to follow.
     * Talks to: ScrewGaugeController_Azm (StepChanged, CurrentStepId, ReadingNumber).
     */
    public class ScrewGaugeObjectView_Azm : MonoBehaviour
    {
        public ScrewGaugeController_Azm controller;
        public Transform item;
        public Transform restPose;
        public Transform gaugePose;
        [Tooltip("Shift (metres, along the pose's up) between reading places.")]
        public float placeShift = 0.004f;
        public float glideSeconds = 0.5f;

        private Vector3 fromPos, toPos;
        private Quaternion fromRot, toRot;
        private float t = 1f;

        private void OnEnable() => controller.StepChanged += OnStep;
        private void OnDisable() => controller.StepChanged -= OnStep;

        private void Start()
        {
            Target(out toPos, out toRot);
            item.SetPositionAndRotation(toPos, toRot);
        }

        private void Target(out Vector3 pos, out Quaternion rot)
        {
            if (controller.CurrentStepId == SGStepId_Azm.Thickness)
            {
                pos = gaugePose.position + gaugePose.up * ((controller.ReadingNumber - 2) * placeShift);
                rot = gaugePose.rotation;
            }
            else
            {
                pos = restPose.position;
                rot = restPose.rotation;
            }
        }

        private void OnStep(int index)
        {
            Target(out Vector3 p, out Quaternion r);
            if ((p - toPos).sqrMagnitude < 1e-10f && Quaternion.Angle(r, toRot) < 0.01f) return;
            fromPos = item.position;
            fromRot = item.rotation;
            toPos = p;
            toRot = r;
            t = 0f;
        }

        private void LateUpdate()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / glideSeconds);
            float e = Mathf.SmoothStep(0f, 1f, t);
            Vector3 p = Vector3.Lerp(fromPos, toPos, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.04f);
            item.SetPositionAndRotation(p, Quaternion.Slerp(fromRot, toRot, e));
        }
    }
}
