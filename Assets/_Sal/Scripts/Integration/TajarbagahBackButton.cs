using UnityEngine;
using UnityEngine.InputSystem;

/// INTEGRATION (Tajarbagah, added during Stage 4 import - see INTEGRATION_GUIDE.md).
/// Layer: Interaction. The Android back button (arrives as Escape) in the Chemistry and Physics 14.1
/// scenes, which had no back handling. It uses each practical's own "All practicals" exit, which saves
/// progress first (NaphthalenePracticalSession.ReturnToLauncher, OhmBench.ReturnToMenu), so a back
/// press never loses work and never quits the app. Physics 8.1 handles Escape itself.
public sealed class TajarbagahBackButton : MonoBehaviour
{
    NaphthalenePracticalSession chemistry;
    VirtualLab.Physics141.OhmBench ohm;

    void Start()
    {
        chemistry = FindFirstObjectByType<NaphthalenePracticalSession>();
        ohm = FindFirstObjectByType<VirtualLab.Physics141.OhmBench>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (chemistry != null) chemistry.ReturnToLauncher();
        else if (ohm != null) ohm.ReturnToMenu();
    }
}
