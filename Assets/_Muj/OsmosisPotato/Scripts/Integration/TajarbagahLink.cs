using UnityEngine;
using VirtualLab.Azm.Core;

namespace Osmosis
{
    /// INTEGRATION (Tajarbagah, added during the pilot import - see INTEGRATION_GUIDE.md).
    /// Layer: Core (app flow). The only place this practical talks to the Tajarbagah app shell.
    /// Leaving the practical: the persistent sound bank (Sfx) and scene loader are shut down first,
    /// so the background music and the fade overlay never follow the student into the app menu,
    /// then AppManager_Azm returns to the main menu. If the practical runs without the app (e.g. the
    /// scene was opened on its own and has no AppBootstrap), it falls back to its own menu scene.
    public static class TajarbagahLink
    {
        public static void ExitToApp()
        {
            Sfx.Shutdown();
            SceneLoader.Shutdown();
            var app = AppManager_Azm.Instance;
            if (app != null) app.GoToMainMenu();
            else SceneLoader.Load(Scenes.Menu);
        }
    }
}
