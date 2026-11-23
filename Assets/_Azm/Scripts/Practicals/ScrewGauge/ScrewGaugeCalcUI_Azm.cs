using System.Text;
using TMPro;
using UnityEngine;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    /*
     * Layer: UI (displays the controller's numbers).
     * Purpose: The Calc tab - the worked calculation with the student's own readings: each corrected
     * thickness t = Y + ZC, then the mean t = (t1 + t2 + t3) / 3. Learn mode reveals one line per tap
     * ("Show next line") so the student follows each step; Perform mode shows them all at once.
     * Talks to: ScrewGaugeController_Azm (Readings, MeanThicknessMm, CalcLinesChanged, StepChanged).
     */
    public class ScrewGaugeCalcUI_Azm : MonoBehaviour
    {
        public ScrewGaugeController_Azm controller;
        public TMP_Text body;

        private void OnEnable()
        {
            controller.CalcLinesChanged += OnChange;
            controller.ReadingsChanged += Refresh;
            controller.StepChanged += OnChange;
            Refresh();
        }

        private void OnDisable()
        {
            controller.CalcLinesChanged -= OnChange;
            controller.ReadingsChanged -= Refresh;
            controller.StepChanged -= OnChange;
        }

        private void OnChange(int value) => Refresh();

        private void Refresh()
        {
            var c = controller;
            var id = c.CurrentStepId;
            if (id != SGStepId_Azm.Calculate && id != SGStepId_Azm.Results)
            {
                body.text = "<b>Worked calculation</b>\n\nThe calculation appears here once all three readings are recorded.";
                return;
            }
            var lines = new System.Collections.Generic.List<string>();
            foreach (var r in c.Readings) lines.Add(ScrewGaugePracticalTexts_Azm.CorrectedLine(r));
            if (c.Readings.Count >= 3)
                lines.Add(ScrewGaugePracticalTexts_Azm.MeanLine(c.Readings[0].CorrectedMm, c.Readings[1].CorrectedMm, c.Readings[2].CorrectedMm, c.MeanThicknessMm));

            var sb = new StringBuilder("<b>Worked calculation</b>  <size=80%>(zero correction applied)</size>\n\n");
            int shown = id == SGStepId_Azm.Results ? lines.Count : c.CalcLinesRevealed;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i < shown) sb.Append(i + 1).Append(". ").Append(lines[i]).Append("\n\n");
                else sb.Append("<color=#7F8AA0>").Append(i + 1).Append(". …</color>\n\n");
            }
            body.text = sb.ToString();
        }
    }
}
