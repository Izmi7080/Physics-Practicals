using UnityEngine;

namespace VirtualLab.Azm.Menu
{
    /*
     * Layer: Presentation (menu background).
     * Purpose: Gives the simplified pendulum stand on the menu bench a slow decorative sway so the
     * scene reads as a physics lab. It is NOT the experiment's motion: nothing measures it, so the
     * "no measured motion without scripted physics" rule does not apply - the real swing is
     * scripted from the pendulum equation in the practical scene (Stage 2).
     * Talks to: rotates the pivot Transform; nothing else.
     */
    public class MenuPendulumSway_Azm : MonoBehaviour
    {
        public Transform pivot;
        public float amplitudeDegrees = 9f;
        public float periodSeconds = 2.6f;

        private void LateUpdate()
        {
            if (pivot == null) return;
            float angle = amplitudeDegrees * Mathf.Sin(2f * Mathf.PI * Time.time / periodSeconds);
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
