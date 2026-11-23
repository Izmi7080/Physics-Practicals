using UnityEngine;
using UnityEngine.InputSystem;

namespace Osmosis
{
    /// INTEGRATION (Tajarbagah). Layer: Interaction.
    /// Android back button (arrives as Escape): opens this scene's own "exit?" popup (the one the
    /// Home button opens), and closes it when pressed again. It never quits the app; leaving still
    /// goes through the popup's YES, so a student cannot lose a lab run by an accidental press.
    public class BackButton : MonoBehaviour
    {
        [SerializeField] PopupPanel exitPopup;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || exitPopup == null) return;
            if (exitPopup.gameObject.activeSelf) exitPopup.Close();
            else exitPopup.Open();
        }
    }
}
