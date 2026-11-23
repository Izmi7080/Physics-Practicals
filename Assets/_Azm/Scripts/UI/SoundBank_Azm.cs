using System;
using UnityEngine;

namespace VirtualLab.Azm.UI
{
    // Every sound the app can play, by meaning (never by file name), so a HUD asks for "Confirm" and
    // the bank decides which clip that is. Append new values at the end (assets store the number).
    public enum SoundId_Azm
    {
        Click,      // any button press
        Confirm,    // a correct reading / answer (soft rising chime)
        Wrong,      // a wrong reading (soft low "hmm", never harsh)
        Tick,       // one millimetre / one division / one tap count
        Tock,       // the jaws or spindle touching the object (wooden "tock")
        Ratchet,    // the screw gauge ratchet slipping ("trrr-click")
        Chime,      // a step completed
        Whoosh,     // a panel or the drawer sliding
        Success,    // the final result
        Pop,        // a value landing in the table / a chip appearing
        Lock        // a lever or lock snapping
    }

    /*
     * Layer: UI (data asset).
     * Purpose: Maps each SoundId_Azm to an audio clip with its own volume and a small random pitch
     * range (repeated ticks and clicks sound natural instead of machine-gun identical). The clips
     * live in Assets/_Azm/Art/Audio. They were synthesised by Editor/SoundBankGenerator_Azm (our own
     * work, no licence needed) and can be replaced by recorded sounds without touching any code.
     * Talks to: read by UISound_Azm.
     */
    [CreateAssetMenu(fileName = "SoundBank_Azm", menuName = "VirtualLab_Azm/Sound Bank")]
    public class SoundBank_Azm : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SoundId_Azm id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.5f;
            [Tooltip("Random pitch in [1 - pitchVariation, 1 + pitchVariation].")]
            [Range(0f, 0.3f)] public float pitchVariation = 0.04f;
        }

        public Entry[] entries = new Entry[0];

        public Entry Get(SoundId_Azm id)
        {
            foreach (var e in entries)
                if (e != null && e.id == id) return e;
            return null;
        }
    }
}
