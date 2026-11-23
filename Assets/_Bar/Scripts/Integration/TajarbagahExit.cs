using UnityEngine;
using UnityEngine.InputSystem;
using VirtualLab.Azm.Core;

/// INTEGRATION (Tajarbagah). Layer: Core (app flow).
/// The single way out of Bareera's practicals into the app: the app's main menu (AppManager_Azm).
/// Her stand-alone menus (StartScene, SubjectSelectScene, PracticalSelectScene) are not part of the
/// app, so every "back / end / quit" in her code comes here instead. The component part is the
/// Android back button (arrives as Escape): it ends the practical the same way her End button does.
public sealed class TajarbagahExit : MonoBehaviour
{
    public static bool InApp => AppManager_Azm.Instance != null;

    public static void ToAppMenu()
    {
        Time.timeScale = 1f;
        if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.GoToMainMenu();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { ReflectionPractical.ResetForNextVisit(); ToAppMenu(); }
    }
}
