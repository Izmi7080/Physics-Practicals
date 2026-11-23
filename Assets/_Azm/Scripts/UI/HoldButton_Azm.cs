using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: A button that acts while it is HELD (press and hold to turn the ratchet, for example).
     * IsHeld is true from finger-down until finger-up or the finger leaves the button; the owner reads
     * it every frame and sends its intent to the logic layer. Works with the Button's visual press
     * state on the same object.
     * Talks to: read by a HUD (e.g. ScrewGaugeHUD_Azm).
     */
    public class HoldButton_Azm : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => IsHeld = true;
        public void OnPointerUp(PointerEventData eventData) => IsHeld = false;
        public void OnPointerExit(PointerEventData eventData) => IsHeld = false;

        private void OnDisable() => IsHeld = false;

        // For scripted tests.
        public void SetHeld(bool held) => IsHeld = held;
    }
}
