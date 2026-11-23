using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Interaction (the only place the menu shell reads a device).
     * Purpose: Turns the Android back button into a BackPressed event. The Input System delivers
     * Android's back button as the Escape key, so the same code works with the Escape key in the
     * editor. Menus and the pause menu subscribe; nothing else reads the keyboard.
     * Scene-local (one per scene, no DontDestroyOnLoad).
     * Talks to: raises BackPressed for MainMenuController_Azm (and later the pause menu).
     */
    public class BackButtonInput_Azm : MonoBehaviour
    {
        public event Action BackPressed;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                BackPressed?.Invoke();
        }
    }
}
