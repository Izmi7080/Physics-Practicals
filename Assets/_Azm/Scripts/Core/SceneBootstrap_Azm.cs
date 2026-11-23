using UnityEngine;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core.
     * Purpose: Lets any scene be played on its own in the editor. The two app-level managers are
     * normally created once by Splash_Azm; if a scene is started directly (skipping the splash)
     * they would not exist, so every scene carries one of these, which creates whichever manager
     * is missing. When the managers already exist it does nothing, so there are never duplicates.
     * It also hands the catalog and theme to AppManager_Azm (first scene to provide them wins).
     * Runs very early (negative execution order) so the managers exist before any other script's
     * Awake/Start asks for them.
     * Talks to: creates SettingsManager_Azm and AppManager_Azm; calls AppManager_Azm.Configure.
     */
    [DefaultExecutionOrder(-1000)]
    public class SceneBootstrap_Azm : MonoBehaviour
    {
        public PracticalCatalog_Azm catalog;
        public UITheme_Azm theme;

        private void Awake()
        {
            if (SettingsManager_Azm.Instance == null)
                new GameObject("SettingsManager_Azm").AddComponent<SettingsManager_Azm>();

            if (AppManager_Azm.Instance == null)
                new GameObject("AppManager_Azm").AddComponent<AppManager_Azm>();

            AppManager_Azm.Instance.Configure(catalog, theme);
        }
    }
}
