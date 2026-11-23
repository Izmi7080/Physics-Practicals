using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Cylinder
{
    /*
     * Layer: UI (displays the controller's readings; edits nothing).
     * Purpose: The Data tab - the textbook observation table of Exp 1.2 (also used for Exp 1.1's
     * diameter): a constants line (LC, zero error, zero correction), then for each reading
     *   Quantity | No. | M (cm) | n | X = n × LC (cm) | Y = M + X (cm) | Corrected = Y + ZC (cm)
     * with the rows of L and D, and the means under the table. Values are in cm to 2 decimal places,
     * matching the least count 0.01 cm; the means get 3.
     * Talks to: CylinderController_Azm (Readings, means, ReadingsChanged); UIFactory_Azm / UITheme_Azm.
     */
    public class CylinderTableUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public CylinderController_Azm controller;
        public TMP_Text headerText;
        public RectTransform headerRow;
        public RectTransform rowContainer;
        public TMP_Text meansText;
        public TMP_Text emptyLabel;
        public float fontSize = 24f;
        public float rowHeight = 60f;

        private static readonly string[] Headers = { "Qty", "No.", "M (cm)", "n", "X = n×LC (cm)", "Y = M+X (cm)", "Corrected (cm)" };
        private static readonly float[] Widths = { 70f, 60f, 90f, 50f, 130f, 130f, 150f };
        private readonly List<GameObject> rows = new List<GameObject>();
        private bool headerBuilt;

        private void OnEnable()
        {
            controller.ReadingsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            controller.ReadingsChanged -= Refresh;
        }

        private TMP_Text Cell(Transform parent, string text, float width, ThemeColor_Azm role, FontStyles style)
        {
            var label = UIFactory_Azm.NewText(parent, "Cell", theme, text, fontSize, role, TextAlignmentOptions.Center, style);
            label.textWrappingMode = TextWrappingModes.Normal;
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = rowHeight;
            return label;
        }

        // Juice (HCI restructure Stage 6): the reading the student just recorded lands in the table -
        // it pops in and flashes green once. Only on a NEW reading (not on every rebuild).
        private int shownReadings;
        private void LandNewRow(GameObject row, int readingCount)
        {
            bool added = readingCount == shownReadings + 1;
            shownReadings = readingCount;
            if (!added || row == null || !row.activeInHierarchy) return;
            FeedbackFX_Azm.Reveal(row.transform);
            var image = row.GetComponent<UnityEngine.UI.Image>();
            if (image != null) Motion_Azm.Flash(image, Color.Lerp(image.color, theme.GetColor(ThemeColor_Azm.Valid), 0.6f), Motion_Azm.Slow + 0.2f);
        }

        private void Refresh()
        {
            if (!headerBuilt)
            {
                headerBuilt = true;
                for (int i = 0; i < Headers.Length; i++) Cell(headerRow, Headers[i], Widths[i], ThemeColor_Azm.Accent, FontStyles.Bold);
            }

            headerText.text = CylinderTexts_Azm.DataHeader(controller.LeastCountKnown, controller.LeastCountMm,
                controller.ZeroErrorKnown, controller.RecordedZeroErrorMm);

            foreach (var row in rows) Destroy(row);
            rows.Clear();

            // Length rows first, then diameter, as in the textbook table.
            var ordered = new List<CylinderReading_Azm>();
            foreach (var r in controller.Readings) if (r.Quantity == CylinderQuantity_Azm.Length) ordered.Add(r);
            foreach (var r in controller.Readings) if (r.Quantity == CylinderQuantity_Azm.Diameter) ordered.Add(r);

            GameObject newestRow = null;
            var newest = controller.Readings.Count > 0 ? controller.Readings[controller.Readings.Count - 1] : null;
            foreach (var r in ordered)
            {
                var rowRect = UIFactory_Azm.NewImage(rowContainer, r.Label, theme, ThemeColor_Azm.Secondary).rectTransform;
                var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 2f;
                layout.padding = new RectOffset(4, 4, 0, 0);
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var element = rowRect.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = rowHeight;
                element.minHeight = rowHeight;

                string[] cells =
                {
                    r.Quantity == CylinderQuantity_Azm.Length ? "L" : "D",
                    r.Number.ToString(),
                    CylinderTexts_Azm.F(r.MainScaleCm, 1),
                    r.VernierDivision.ToString(),
                    CylinderTexts_Azm.F(r.FractionCm, 2),
                    CylinderTexts_Azm.F(r.ObservedCm, 2),
                    $"{r.Label} = {CylinderTexts_Azm.F(r.CorrectedCm, 2)}"
                };
                for (int i = 0; i < cells.Length; i++)
                    Cell(rowRect, cells[i], Widths[i], i == cells.Length - 1 ? ThemeColor_Azm.Valid : ThemeColor_Azm.TextPrimary,
                        i == cells.Length - 1 ? FontStyles.Bold : FontStyles.Normal);
                rows.Add(rowRect.gameObject);
                if (r == newest) newestRow = rowRect.gameObject;
            }
            LandNewRow(newestRow, controller.Readings.Count);

            emptyLabel.gameObject.SetActive(ordered.Count == 0);

            int needed = controller.config.readingsPerQuantity;
            int lCount = 0, dCount = 0;
            foreach (var r in controller.Readings) { if (r.Quantity == CylinderQuantity_Azm.Length) lCount++; else dCount++; }
            string meanL = lCount == needed ? $"Mean length L = {CylinderTexts_Azm.F(controller.MeanLengthCm, 3)} cm" : $"Mean length L: {lCount} of {needed} readings";
            string meanD = dCount == needed ? $"Mean diameter D = {CylinderTexts_Azm.F(controller.MeanDiameterCm, 3)} cm" : $"Mean diameter D: {dCount} of {needed} readings";
            meansText.text = meanL + "\n" + meanD;
        }
    }
}
