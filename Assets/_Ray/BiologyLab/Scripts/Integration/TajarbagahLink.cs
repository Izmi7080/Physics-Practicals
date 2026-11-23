using UnityEngine;
using UnityEngine.InputSystem;
using VirtualLab.Azm.Core;

namespace BiologyLab
{
    /// INTEGRATION (Tajarbagah). Layer: Core (app flow).
    /// The way out of Rayyan's frog practical into the app's main menu (AppManager_Azm).
    /// His stand-alone build quit the application; inside the app, Quit returns to the menu instead,
    /// so the student is never dropped out of Tajarbagah. Outside the app (scene opened on its own in
    /// the Editor) the original behaviour is kept.
    /// Also reads the Android back button (arrives as Escape) for BiologyLabBootstrap.
    internal static class TajarbagahLink
    {
        public static bool InApp => AppManager_Azm.Instance != null;

        /// Returns to the app menu. False when not running inside the app shell.
        public static bool ExitToApp()
        {
            if (!InApp) return false;
            Time.timeScale = 1f;
            AppManager_Azm.Instance.GoToMainMenu();
            return true;
        }

        public static bool BackPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
    }
}
