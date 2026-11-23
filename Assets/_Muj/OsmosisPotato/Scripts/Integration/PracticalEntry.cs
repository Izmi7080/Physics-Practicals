using UnityEngine;

namespace Osmosis
{
    /// INTEGRATION (Tajarbagah). Layer: Core (scene setup).
    /// In the stand-alone project the sound bank (SFX prefab) lived in Osmosis_Menu and the music was
    /// started by its PLAY button. In Tajarbagah the practical starts at Osmosis_Guidance (the app's
    /// menu replaces Osmosis_Menu), so this component, placed in Guidance and Lab:
    ///  - creates the sound bank if it does not exist yet (Sfx itself refuses duplicates);
    ///  - in Guidance, starts the background music, exactly like the old PLAY button did
    ///    (MenuActions.StartLab still stops it when the lab begins).
    public class PracticalEntry : MonoBehaviour
    {
        [SerializeField] GameObject sfxPrefab;
        [SerializeField] bool startMusic;

        void Awake()
        {
            if (!Sfx.Exists && sfxPrefab != null) Instantiate(sfxPrefab);
        }

        void Start()
        {
            if (startMusic && !Sfx.IsPlaying(Sfx.Music)) Sfx.Play(Sfx.Music);
        }
    }
}
