using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI (displays the controller's numbers; no experiment logic).
     * Purpose: The pendulum's Calc tab (TASK_menus_and_pendulum 4.6):
     *   - WORKED CALCULATION for one row of the table, with the student's own numbers, in the order
     *     the syllabus does it: r = d/2 -> L = ℓ + r -> mean t -> T = t/n -> T², then (once the graph
     *     has a line) g = 4π² / slope. The row is chosen with ◀ ▶ (it follows the newest row).
     *   - Learn mode reveals the lines one per tap ("Show next line"), so the student follows each
     *     step instead of reading only the answer; Perform mode shows them all at once.
     *   - A CALCULATOR button opens the simple calculator (CalculatorUI_Azm) over the tab.
     * Talks to: PendulumController_Azm (rows, analysis, RowAdded/AnalysisChanged), PendulumTexts_Azm,
     * PanelTransition_Azm (calculator), Motion_Azm/FeedbackFX_Azm (reveal feel).
     */
    public class PendulumCalcUI_Azm : MonoBehaviour
    {
        public PendulumController_Azm controller;
        public TMP_Text rowLabel;
        public Button previousRowButton;
        public Button nextRowButton;
        public TMP_Text body;
        public Button revealButton;
        public Button calculatorButton;
        public PanelTransition_Azm calculatorPanel;
        public Button calculatorCloseButton;

        // Set by the HUD: Learn reveals line by line, Perform shows everything.
        public bool RevealStepByStep { get; set; } = true;
        public bool CalculatorOpen => calculatorPanel != null && calculatorPanel.IsVisible;

        private int rowIndex = -1;     // index into controller.Rows
        private int revealed;          // lines shown (Learn mode)
        private const int LineCount = 6;

        private void Awake()
        {
            previousRowButton.onClick.AddListener(() => SelectRow(rowIndex - 1));
            nextRowButton.onClick.AddListener(() => SelectRow(rowIndex + 1));
            revealButton.onClick.AddListener(RevealNext);
            calculatorButton.onClick.AddListener(() => { calculatorPanel.Show(); FeedbackFX_Azm.PanelSlide(); });
            calculatorCloseButton.onClick.AddListener(() => calculatorPanel.Hide());
        }

        private void Start() => calculatorPanel.Hide(false);

        private void OnEnable()
        {
            controller.RowAdded += OnRowAdded;
            controller.AnalysisChanged += Refresh;
            if (rowIndex < 0 && controller.Rows.Count > 0) rowIndex = controller.Rows.Count - 1;
            Refresh();
        }

        private void OnDisable()
        {
            controller.RowAdded -= OnRowAdded;
            controller.AnalysisChanged -= Refresh;
        }

        public void CloseCalculator() { if (CalculatorOpen) calculatorPanel.Hide(); }

        private void OnRowAdded(TrialRow_Azm row)
        {
            // Follow the newest row; a new row starts a fresh worked calculation.
            rowIndex = controller.Rows.Count - 1;
            revealed = 0;
            Refresh();
        }

        private void SelectRow(int index)
        {
            if (controller.Rows.Count == 0) return;
            rowIndex = Mathf.Clamp(index, 0, controller.Rows.Count - 1);
            Refresh();
        }

        private void RevealNext()
        {
            if (revealed >= LineCount) return;
            revealed++;
            FeedbackFX_Azm.Reveal(null);
            Refresh();
        }

        private void Refresh()
        {
            var rows = controller.Rows;
            if (rows.Count == 0 || rowIndex < 0)
            {
                rowLabel.text = "–";
                body.text = PendulumTexts_Azm.CalcNoRows;
                revealButton.gameObject.SetActive(false);
                previousRowButton.interactable = nextRowButton.interactable = false;
                return;
            }

            var row = rows[rowIndex];
            rowLabel.text = PendulumTexts_Azm.CalcRowTitle(row.Number) + (row.Discarded ? " (discarded)" : "");
            previousRowButton.interactable = rowIndex > 0;
            nextRowButton.interactable = rowIndex < rows.Count - 1;

            var a = controller.Analysis;
            var lines = new List<string>
            {
                PendulumTexts_Azm.CalcRadius(row.RadiusCm * 2f, row.RadiusCm),
                PendulumTexts_Azm.CalcEffectiveLength(row.LengthCm, row.RadiusCm, row.EffectiveLengthCm),
                PendulumTexts_Azm.CalcMeanTime(row.Time1, row.Time2, row.MeanTime),
                PendulumTexts_Azm.CalcPeriod(row.MeanTime, row.Oscillations, row.Period),
                PendulumTexts_Azm.CalcPeriodSquared(row.Period, row.PeriodSquared),
                a.hasFit ? PendulumTexts_Azm.CalcG(a.slope, a.g) : PendulumTexts_Azm.CalcNeedFit
            };

            int shown = RevealStepByStep ? revealed : LineCount;
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i < shown) sb.Append(i + 1).Append(". ").Append(lines[i]).Append("\n\n");
                else sb.Append("<color=#7F8AA0>").Append(i + 1).Append(". …</color>\n\n");
            }
            body.text = sb.ToString();
            revealButton.gameObject.SetActive(RevealStepByStep && revealed < LineCount);
        }
    }
}
