using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    [Serializable]
    public class AssetCreditEntry_Azm
    {
        public string assetName;
        public string author;
        [Tooltip("e.g. \"CC BY 4.0\" or \"SIL OFL 1.1\".")]
        public string license;
        public string source;
        [TextArea(1, 3)] public string note;
    }

    /*
     * Layer: Core (data asset).
     * Purpose: The list of third-party assets (models, fonts, sounds) and their attribution, shown
     * on the About panel. It is data so a licence can be added later by editing this asset only.
     * Talks to: read by the About panel (Scripts/Menu).
     */
    [CreateAssetMenu(fileName = "AssetCredits_Azm", menuName = "VirtualLab_Azm/Asset Credits")]
    public class AssetCredits_Azm : ScriptableObject
    {
        public List<AssetCreditEntry_Azm> entries = new List<AssetCreditEntry_Azm>();
    }
}
