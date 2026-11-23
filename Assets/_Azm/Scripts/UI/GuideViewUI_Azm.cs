using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI.
     * Purpose: The Guide tab: renders a PracticalGuide_Azm as one scrolling, formatted text -
     * Aim, Description, Apparatus, What you measure, Procedure, Formulas, Precautions (each with
     * its reason) and Observations. The procedure step the student is on is highlighted (amber,
     * bold, with an arrow) so the written procedure and the simulation stay connected.
     * Shared by every practical; it knows nothing about a particular experiment.
     * Talks to: PracticalGuide_Azm (content), UITheme_Azm (highlight colour); the practical's HUD
     * calls SetCurrentStep and listens to the replay button.
     */
    public class GuideViewUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public PracticalGuide_Azm guide;
        public TMP_Text body;
        [Tooltip("Optional: 'Replay briefing' button under the text.")]
        public Button replayBriefingButton;
        [Tooltip("Optional: a second replay button (the pendulum's oscillation demonstration).")]
        public Button replayDemoButton;

        private int currentStep = -1;

        private void OnEnable()
        {
            Render();
        }

        // Index into guide.procedure (-1 = none highlighted).
        public void SetCurrentStep(int procedureIndex)
        {
            if (currentStep == procedureIndex) return;
            currentStep = procedureIndex;
            if (isActiveAndEnabled) Render();
        }

        private static void Section(StringBuilder sb, string title)
        {
            sb.Append("\n<b><size=115%>").Append(title).Append("</size></b>\n");
        }

        public void Render()
        {
            if (guide == null || body == null) return;
            string accent = theme != null ? ColorUtility.ToHtmlStringRGB(theme.GetColor(ThemeColor_Azm.Accent)) : "FFC040";
            string soft = theme != null ? ColorUtility.ToHtmlStringRGB(theme.GetColor(ThemeColor_Azm.TextSecondary)) : "B8C7E0";
            var sb = new StringBuilder();

            sb.Append("<b><size=130%>").Append(guide.title).Append("</size></b>\n");
            if (!string.IsNullOrEmpty(guide.aim)) { Section(sb, "Aim"); sb.Append(guide.aim).Append('\n'); }
            if (!string.IsNullOrEmpty(guide.description)) { Section(sb, "Description"); sb.Append(guide.description).Append('\n'); }
            if (!string.IsNullOrEmpty(guide.hypothesis)) { Section(sb, "Hypothesis"); sb.Append(guide.hypothesis).Append('\n'); }
            if (guide.apparatus.Length > 0)
            {
                Section(sb, "Apparatus");
                foreach (var a in guide.apparatus) sb.Append("• ").Append(a).Append('\n');
            }
            if (!string.IsNullOrEmpty(guide.whatYouMeasure)) { Section(sb, "What you will measure"); sb.Append(guide.whatYouMeasure).Append('\n'); }
            if (guide.procedure.Length > 0)
            {
                Section(sb, "Procedure");
                for (int i = 0; i < guide.procedure.Length; i++)
                {
                    if (i == currentStep)
                        sb.Append("<color=#").Append(accent).Append("><b>▶ ").Append(i + 1).Append(". ").Append(guide.procedure[i]).Append("</b></color>\n");
                    else
                        sb.Append(i + 1).Append(". ").Append(guide.procedure[i]).Append('\n');
                }
            }
            if (guide.formulas.Length > 0)
            {
                Section(sb, "Formulas");
                foreach (var f in guide.formulas) sb.Append("• ").Append(f).Append('\n');
            }
            if (guide.precautions.Length > 0)
            {
                Section(sb, "Precautions");
                foreach (var p in guide.precautions)
                {
                    sb.Append("• <b>").Append(p.text).Append("</b>\n");
                    if (!string.IsNullOrEmpty(p.why)) sb.Append("   <color=#").Append(soft).Append(">Why: ").Append(p.why).Append("</color>\n");
                }
            }
            if (!string.IsNullOrEmpty(guide.observationTable)) { Section(sb, "Observations"); sb.Append(guide.observationTable).Append('\n'); }

            body.text = sb.ToString();
        }
    }
}
