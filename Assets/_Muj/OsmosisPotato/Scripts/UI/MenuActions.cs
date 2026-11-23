using UnityEngine;

namespace Osmosis
{
    /// Port of MainMenuHandler + ExitMenuHandler + ExperimentStarter: the scene-level button
    /// targets, wired from button OnClick in the Inspector. One component, any scene.
    public class MenuActions : MonoBehaviour
    {
        // Main menu
        public void Play()
        {
            if (!Sfx.IsPlaying(Sfx.Music)) Sfx.Play(Sfx.Music);
            SceneLoader.Load(Scenes.Guidance);
        }

        // INTEGRATION (Tajarbagah): never quit the app from a practical - return to the app menu.
        public void Quit() => TajarbagahLink.ExitToApp();

        // Exit dialog "Yes", Result window "Quit"
        // INTEGRATION (Tajarbagah): "Home -> YES" and the result's "Quit" leave the practical to the app menu
        // (Osmosis_Menu was the stand-alone title screen; the app menu replaces it).
        public void ToMenu() => TajarbagahLink.ExitToApp();

        // Guidance "Got it": reference stops the music once the lab starts
        public void StartLab()
        {
            Sfx.Stop(Sfx.Music);
            SceneLoader.Load(Scenes.Lab);
        }

        // Result window "Again": restarts the lab (reference bug sent this to the Menu)
        public void Again() => SceneLoader.Reload();
    }
}
