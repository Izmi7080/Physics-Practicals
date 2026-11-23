using UnityEngine;

namespace VirtualLab.Azm.Menu
{
    /*
     * Layer: Presentation (menu background).
     * Purpose: The slow, gentle camera drift around the lab bench behind the main menu. The angle
     * follows a sine wave, so the camera swings smoothly out and back and the loop is seamless by
     * construction (no jump at the end of a cycle). Purely cosmetic.
     * Talks to: moves its own Transform to orbit the target; nothing else.
     */
    public class MenuCameraDrift_Azm : MonoBehaviour
    {
        [Tooltip("What the camera looks at and orbits (the pendulum on the bench).")]
        public Transform target;
        public Vector3 lookOffset = new Vector3(0f, 0.3f, 0f);
        public float radius = 2.8f;
        [Tooltip("Camera height above the look-at point.")]
        public float heightAboveTarget = 0.35f;
        [Tooltip("Middle of the swing, in degrees around the target (0 = camera on the +Z side of the target).")]
        public float centreAngleDegrees = 0f;
        public float sweepDegrees = 22f;
        public float periodSeconds = 28f;

        private void LateUpdate()
        {
            if (target == null) return;

            float angle = (centreAngleDegrees + sweepDegrees * Mathf.Sin(2f * Mathf.PI * Time.time / periodSeconds)) * Mathf.Deg2Rad;
            Vector3 centre = target.position + lookOffset;
            transform.position = centre + new Vector3(Mathf.Sin(angle) * radius, heightAboveTarget, Mathf.Cos(angle) * radius);
            transform.LookAt(centre);
        }
    }
}
