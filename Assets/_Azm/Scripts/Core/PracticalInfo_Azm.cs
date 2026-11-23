using UnityEngine;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core (data asset).
     * Purpose: One entry in the practical catalog - everything the menu needs to show a card and
     * to launch the practical. Adding a teammate's practical later means creating one of these
     * assets, adding it to PracticalCatalog_Azm, and adding their scene to Build Settings. No menu
     * code changes are needed.
     * Talks to: listed by PracticalCatalog_Azm; read by the menu scripts (Scripts/Menu) and passed
     * to AppManager_Azm.StartPractical.
     */
    [CreateAssetMenu(fileName = "PracticalInfo_Azm", menuName = "VirtualLab_Azm/Practical Info")]
    public class PracticalInfo_Azm : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, e.g. \"physics.simple_pendulum\". Never shown to the student.")]
        public string id;
        public string displayName;
        public SubjectType_Azm subject;

        [Header("Card content")]
        [TextArea(2, 3)] public string shortDescription;
        public Sprite thumbnail;
        [Tooltip("Rough time a student needs, in minutes.")]
        public int estimatedMinutes = 15;
        [Tooltip("Short apparatus list shown on the detail panel.")]
        public string[] apparatus;

        [Header("Launch")]
        [Tooltip("Exact scene name. The scene must be in Build Settings or the card shows as unavailable.")]
        public string sceneName;

        [Header("Availability")]
        [Tooltip("Locked practicals are shown on the menu as 'Coming soon' - never hidden.")]
        public bool isLocked = true;
        public bool learnModeAvailable = true;
        public bool performModeAvailable = false;
    }
}
