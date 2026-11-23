using System.Text;
using TMPro;
using UnityEngine;

namespace VirtualLab.Azm.Cylinder
{
    /*
     * Layer: UI (displays the controller's numbers).
     * Purpose: The Calc tab - the worked calculation with the student's OWN numbers, one formula per
     * line, in the order the textbook does it:
     *   mean D = (D₁ + D₂ + D₃)/3 -> r = D/2 -> A = πr² (Exp 1.1) -> mean L -> V = πr²L (Exp 1.2).
     * In Learn mode the lines appear one at a time as the student taps "Show next line" (so they
     * follow each step instead of just reading the answer); in Perform mode all appear at once (the
     * HUD asks the controller to reveal them). Before the readings are complete it says so.
     * Talks to: CylinderController_Azm (values, CalcLinesChanged, ReadingsChanged), CylinderTexts_Azm.
     */
    public class CylinderCalcUI_Azm : MonoBehaviour
    {
        public CylinderController_Azm controller;
        public TMP_Text body;

        private void OnEnable()
        {
            controller.CalcLinesChanged += OnLines;
            controller.ReadingsChanged += Refresh;
            controller.StepChanged += OnLines;
            Refresh();
        }

        private void OnDisable()
        {
            controller.CalcLinesChanged -= OnLines;
            controller.ReadingsChanged -= Refresh;
            controller.StepChanged -= OnLines;
        }

        private void OnLines(int count) => Refresh();

        private void Refresh()
        {
            var c = controller;
            var id = c.CurrentStepId;
            if (id != CylinderStepId_Azm.Calculate && id != CylinderStepId_Azm.Results)
            {
                body.text = "<b>Worked calculation</b>\n\nThe calculation appears here once all six readings (three of D, three of L) are recorded.";
                return;
            }

            var d = CylinderQuantity_Azm.Diameter;
            var l = CylinderQuantity_Azm.Length;
            string[] lines =
            {
                CylinderTexts_Azm.MeanLine("D", c.CorrectedValue(d, 1), c.CorrectedValue(d, 2), c.CorrectedValue(d, 3), c.MeanDiameterCm),
                CylinderTexts_Azm.RadiusLine(c.MeanDiameterCm, c.RadiusCm),
                CylinderTexts_Azm.AreaLine(c.RadiusCm, c.AreaCm2),
                CylinderTexts_Azm.MeanLine("L", c.CorrectedValue(l, 1), c.CorrectedValue(l, 2), c.CorrectedValue(l, 3), c.MeanLengthCm),
                CylinderTexts_Azm.VolumeLine(c.RadiusCm, c.MeanLengthCm, c.VolumeCm3)
            };

            var sb = new StringBuilder("<b>Worked calculation</b>  <size=80%>(corrected values)</size>\n\n");
            int shown = id == CylinderStepId_Azm.Results ? lines.Length : c.CalcLinesRevealed;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i < shown) sb.Append(i + 1).Append(". ").Append(lines[i]).Append("\n\n");
                else { sb.Append("<color=#7F8AA0>").Append(i + 1).Append(". …</color>\n\n"); }
            }
            body.text = sb.ToString();
        }
    }
}
