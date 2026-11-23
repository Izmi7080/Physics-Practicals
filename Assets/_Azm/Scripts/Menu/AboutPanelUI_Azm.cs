using System.Text;
using TMPro;
using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.Menu
{
    /*
     * Layer: UI.
     * Purpose: The About panel: app name, team member names (placeholder fields the team fills in
     * from the Inspector), supervisor, university, and the "Asset credits" list read from an
     * AssetCredits_Azm data asset so model/font attributions can be added later without touching
     * code.
     * Talks to: reads AssetCredits_Azm; writes TMP labels.
     */
    public class AboutPanelUI_Azm : MonoBehaviour
    {
        public TMP_Text infoText;
        public TMP_Text creditsText;
        public AssetCredits_Azm credits;

        [Header("Text (edit in the Inspector)")]
        public string appName = "Virtual Science Lab";
        public string[] teamMembers = { "Team member 1", "Team member 2", "Team member 3", "Team member 4", "Team member 5" };
        public string supervisor = "Sarfraz Raza";
        public string university = "Information Technology University";

        private void OnEnable()
        {
            var info = new StringBuilder();
            info.AppendLine(appName);
            info.AppendLine("Final Year Project - " + university);
            info.AppendLine();
            info.AppendLine("Team");
            foreach (string member in teamMembers)
                info.AppendLine(member);
            info.AppendLine();
            info.AppendLine("Supervisor: " + supervisor);
            infoText.text = info.ToString();

            var list = new StringBuilder();
            if (credits == null || credits.entries.Count == 0)
            {
                list.Append("No third-party assets credited yet.");
            }
            else
            {
                foreach (var entry in credits.entries)
                {
                    list.Append("- ").Append(entry.assetName);
                    if (!string.IsNullOrEmpty(entry.author)) list.Append(" by ").Append(entry.author);
                    if (!string.IsNullOrEmpty(entry.license)) list.Append(" (").Append(entry.license).Append(")");
                    list.AppendLine();
                }
            }
            creditsText.text = list.ToString();
        }
    }
}
