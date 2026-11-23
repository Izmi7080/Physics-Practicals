using System.Globalization;
using TMPro;
using UnityEngine;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI / presentation (one of the few world-space UI elements the spec allows).
     * Purpose: The small label next to the bob: "ℓ = 64.3 cm" while the length is being set, and
     * "θ = 11°" while the angle is being set (red beyond the 15 degree limit, green within it).
     * It sits beside the bob, always faces the camera and scales with the 3D view, so it reads as
     * part of the apparatus.
     * Talks to: reads PendulumController_Azm; moves its own Transform; writes a TMP label.
     */
    public class WorldReadout_Azm : MonoBehaviour
    {
        public PendulumController_Azm controller;
        public Transform bob;
        public Camera viewCamera;
        public TMP_Text label;
        public ThemedText_Azm labelTheme;
        public CanvasGroup group;
        [Tooltip("Distance to the right of the bob, in metres.")]
        public float sideOffset = 0.18f;

        private void LateUpdate()
        {
            string text = null;
            ThemeColor_Azm role = ThemeColor_Azm.TextPrimary;

            switch (controller.CurrentStepId)
            {
                case StepId_Azm.SetLength:
                    text = "ℓ = " + controller.LengthCm.ToString("F1", CultureInfo.InvariantCulture) + " cm";
                    break;
                case StepId_Azm.Release:
                    if (!controller.IsSwinging && (controller.BobHeld || Mathf.Abs(controller.DisplacementDeg) > 0.5f))
                    {
                        float deg = Mathf.Abs(controller.DisplacementDeg);
                        text = "θ = " + deg.ToString("F0", CultureInfo.InvariantCulture) + "°";
                        role = deg > controller.config.maxReleaseAngleDeg ? ThemeColor_Azm.Invalid : ThemeColor_Azm.Valid;
                    }
                    break;
            }

            group.alpha = text != null ? 1f : 0f;
            if (text == null) return;

            label.text = text;
            labelTheme.SetRole(role);
            transform.rotation = viewCamera.transform.rotation;
            transform.position = bob.position + viewCamera.transform.right * sideOffset;
        }
    }
}
