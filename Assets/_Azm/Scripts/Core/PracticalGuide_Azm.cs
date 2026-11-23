using System;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    [Serializable]
    public class GuidePrecaution_Azm
    {
        [TextArea(1, 2)] public string text;
        [Tooltip("Why it matters - shown under the precaution.")]
        [TextArea(1, 3)] public string why;
    }

    /*
     * Layer: Core (data asset - CLAUDE.md: teaching content lives in data, not in scripts).
     * Purpose: The written guide of one practical: aim, hypothesis/description, apparatus, what is
     * measured, procedure, formulas, precautions (each with its reason) and the observation table
     * layout. The Guide tab and the Learn-mode briefing cards are filled from it, so a teacher can
     * correct the wording by editing this asset only.
     * Talks to: read by GuideViewUI_Azm and the practical's HUD (briefing cards).
     */
    [CreateAssetMenu(fileName = "PracticalGuide_Azm", menuName = "VirtualLab_Azm/Practical Guide")]
    public class PracticalGuide_Azm : ScriptableObject
    {
        public string title;
        [TextArea(2, 4)] public string aim;
        [TextArea(2, 6)] public string description;
        [Tooltip("Optional (e.g. the pendulum: T² ∝ L). Shown in the Guide tab and as a briefing card.")]
        [TextArea(2, 6)] public string hypothesis;
        public string[] apparatus = new string[0];
        [TextArea(2, 6)] public string whatYouMeasure;
        [Tooltip("Procedure steps in order. The Guide tab highlights the one the student is on.")]
        [TextArea(1, 3)] public string[] procedure = new string[0];
        [TextArea(1, 2)] public string[] formulas = new string[0];
        public GuidePrecaution_Azm[] precautions = new GuidePrecaution_Azm[0];
        [TextArea(2, 6)] public string observationTable;
    }
}
