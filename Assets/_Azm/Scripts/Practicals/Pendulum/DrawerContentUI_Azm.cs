using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI (content of the drawer's Data and Graph tabs).
     * Purpose: Keeps the observation table and the T^2-against-L graph in step with the controller.
     *   Data tab:  the constants header ("n = 20 · r = 1.00 cm (d = 2.00 cm)"), the compact table,
     *              and an Expand button that shows the full syllabus table (with n and r).
     *   Graph tab: the plot with the best-fit line, the slope and intercept, g and its % error, a line
     *              saying whether the fit passes close to the origin, and an Expand button.
     * A discard (x) asks for confirmation before anything changes; a warning icon on a suspect row
     * explains why it is suspect.
     * UNITS: the graph is DRAWN in cm (like the table) but the slope shown, and g, come from the
     * analysis done in metres (PendulumAnalysis_Azm) - the plot only re-scales the line for display.
     * Talks to: PendulumController_Azm (rows, analysis); TableView_Azm; GraphView_Azm; ConfirmDialog_Azm;
     * ToastUI_Azm.
     */
    public class DrawerContentUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public PendulumController_Azm controller;
        public ConfirmDialog_Azm confirm;
        public ToastUI_Azm toast;

        [Header("Data tab")]
        public TMP_Text dataHeaderText;
        public TableView_Azm compactTable;
        public Button dataExpandButton;
        public PanelTransition_Azm dataExpandOverlay;
        public TableView_Azm fullTable;
        public TMP_Text fullTableHeaderText;
        public Button dataExpandCloseButton;

        [Header("Graph tab")]
        public GraphView_Azm graph;
        public TMP_Text graphStatsText;
        public Button graphExpandButton;
        public PanelTransition_Azm graphExpandOverlay;
        public GraphView_Azm bigGraph;
        public TMP_Text bigGraphStatsText;
        public Button graphExpandCloseButton;

        public bool IsExpandedOpen =>
            (dataExpandOverlay != null && dataExpandOverlay.IsVisible) || (graphExpandOverlay != null && graphExpandOverlay.IsVisible);

        private void Awake()
        {
            dataExpandButton.onClick.AddListener(() => dataExpandOverlay.Show());
            dataExpandCloseButton.onClick.AddListener(() => dataExpandOverlay.Hide());
            graphExpandButton.onClick.AddListener(() => graphExpandOverlay.Show());
            graphExpandCloseButton.onClick.AddListener(() => graphExpandOverlay.Hide());
        }

        private void OnEnable()
        {
            controller.AnalysisChanged += Refresh;
            compactTable.DiscardRequested += AskDiscard;
            fullTable.DiscardRequested += AskDiscard;
            compactTable.SuspectTapped += ExplainSuspect;
            fullTable.SuspectTapped += ExplainSuspect;
        }

        private void OnDisable()
        {
            controller.AnalysisChanged -= Refresh;
            compactTable.DiscardRequested -= AskDiscard;
            fullTable.DiscardRequested -= AskDiscard;
            compactTable.SuspectTapped -= ExplainSuspect;
            fullTable.SuspectTapped -= ExplainSuspect;
        }

        private void Start()
        {
            dataExpandOverlay.Hide(false);
            graphExpandOverlay.Hide(false);
            Refresh();
        }

        public void CloseExpanded()
        {
            dataExpandOverlay.Hide();
            graphExpandOverlay.Hide();
        }

        private void AskDiscard(int rowNumber)
        {
            confirm.Ask(PendulumTexts_Azm.DiscardQuestion(rowNumber), "Discard", () => controller.DiscardRow(rowNumber));
        }

        private void ExplainSuspect(TrialRow_Azm row)
        {
            Color tint = Color.Lerp(theme.GetColor(ThemeColor_Azm.PanelStrong), theme.GetColor(ThemeColor_Azm.Accent), 0.45f);
            toast.Show(row.SuspectReason, tint);
        }

        private void Refresh()
        {
            var cfg = controller.config;
            // d is the student's caliper reading (or the given value in the fallback mode).
            string header = controller.HasDiameter
                ? $"n = {cfg.oscillationCount} · r = {F(controller.RadiusCm, 2)} cm (d = {F(controller.DiameterCm, 2)} cm)"
                : $"n = {cfg.oscillationCount} · d not measured yet";
            dataHeaderText.text = header;
            fullTableHeaderText.text = header;

            var a = controller.Analysis;

            // Display units: x in cm, y in s^2. A line y = m x + c with x in metres becomes
            // y = (m / 100) x + c with x in centimetres.
            var cmPoints = new List<Vector2>();
            foreach (var p in a.points) cmPoints.Add(new Vector2(p.x * 100f, p.y));
            graph.SetData(cmPoints, a.hasFit, a.slope / 100f, a.intercept);
            bigGraph.SetData(cmPoints, a.hasFit, a.slope / 100f, a.intercept);

            string stats = BuildStats(a);
            graphStatsText.text = stats;
            bigGraphStatsText.text = stats;
        }

        private static string F(float value, int decimals) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);

        private string BuildStats(PendulumAnalysisResult_Azm a)
        {
            if (!a.hasFit)
                return a.validCount < 2
                    ? "Record at least 2 valid rows to see the best-fit line."
                    : "The lengths are too similar to fit a line - use clearly different lengths.";

            return $"slope = {F(a.slope, 3)} s²/m    intercept = {F(a.intercept, 3)} s²\n" +
                   $"g = 4π² / slope = {F(a.g, 2)} m/s²    error = {F(a.percentError, 1)} %\n" +
                   (a.nearOrigin ? PendulumTexts_Azm.OriginGood : PendulumTexts_Azm.OriginOff(a.intercept));
        }
    }
}
