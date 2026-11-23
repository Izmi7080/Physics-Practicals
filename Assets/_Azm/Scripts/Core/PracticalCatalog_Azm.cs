using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core (data asset).
     * Purpose: The list of every practical in the app. The menu builds its cards from this asset
     * at runtime, so the menu never names a specific practical.
     * Talks to: holds PracticalInfo_Azm assets; read by the menu scripts.
     */
    [CreateAssetMenu(fileName = "PracticalCatalog_Azm", menuName = "VirtualLab_Azm/Practical Catalog")]
    public class PracticalCatalog_Azm : ScriptableObject
    {
        public List<PracticalInfo_Azm> practicals = new List<PracticalInfo_Azm>();

        // Returns the practicals for one subject, in the order they appear in the list.
        public List<PracticalInfo_Azm> GetForSubject(SubjectType_Azm subject)
        {
            var result = new List<PracticalInfo_Azm>();
            foreach (var practical in practicals)
            {
                if (practical != null && practical.subject == subject)
                    result.Add(practical);
            }
            return result;
        }
    }
}
