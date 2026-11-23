using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    /*
     * Layer: UI (displays the controller's readings; edits nothing).
     * Purpose: The Data tab - the textbook observation table of Exp 1.3: a constants line (LC, zero
     * error, zero correction), then for each reading
     *   No. | M (mm) | n | X = n × LC (mm) | Y = M + X (mm) | t = Y ± ZC (mm)
     * and the mean thickness under it. Values in mm to 2 decimal places (LC = 0.01 mm), the mean to 3.
     * Talks to: ScrewGaugeController_Azm (Readings, MeanThicknessMm, ReadingsChanged).
     */
    public class ScrewGaugeTableUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public ScrewGaugeController_Azm controller;
        public TMP_Text headerText;
        public RectTransform headerRow;
        public RectTransform rowContainer;
        public TMP_Text meanText;
        public TMP_Text emptyLabel;
        public float fontSize = 24f;
        public float rowHeight = 60f;

        private static readonly string[] Headers = { "No.", "M (mm)", "n", "X = n×LC (mm)", "Y = M+X (mm)", "t = Y±ZC (mm)" };
        private static readonly float[] Widths = { 70f, 100f, 70f, 140f, 140f, 160f };
        private readonly List<GameObject> rows = new List<GameObject>();
        private bool headerBuilt;

        private void OnEnable()
        {
            controller.ReadingsChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => controller.ReadingsChanged -= Refresh;

        private void Cell(Transform parent, string text, float width, ThemeColor_Azm role, FontStyles style)
        {
            var label = UIFactory_Azm.NewText(parent, "Cell", theme, text, fontSize, role, TextAlignmentOptions.Center, style);
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = rowHeight;
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
            headerText.text = ScrewGaugePracticalTexts_Azm.DataHeader(controller.LeastCountKnown, controller.LeastCountMm,
                controller.ZeroErrorKnown, controller.RecordedZeroErrorMm);

            foreach (var row in rows) Destroy(row);
            rows.Clear();
            foreach (var r in controller.Readings)
            {
                var rowRect = UIFactory_Azm.NewImage(rowContainer, r.Label, theme, ThemeColor_Azm.Secondary).rectTransform;
                var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 2f;
                layout.padding = new RectOffset(4, 4, 0, 0);
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var element = rowRect.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = element.minHeight = rowHeight;
                string[] cells =
                {
                    r.Number.ToString(),
                    ScrewGaugePracticalTexts_Azm.F(r.MainScaleMm, 1),
                    r.Division.ToString(),
                    ScrewGaugePracticalTexts_Azm.F(r.FractionMm, 2),
                    ScrewGaugePracticalTexts_Azm.F(r.ObservedMm, 2),
                    $"{r.Label} = {ScrewGaugePracticalTexts_Azm.F(r.CorrectedMm, 2)}"
                };
                for (int i = 0; i < cells.Length; i++)
                    Cell(rowRect, cells[i], Widths[i], i == cells.Length - 1 ? ThemeColor_Azm.Valid : ThemeColor_Azm.TextPrimary,
                        i == cells.Length - 1 ? FontStyles.Bold : FontStyles.Normal);
                rows.Add(rowRect.gameObject);
            }
            LandNewRow(rows.Count > 0 ? rows[rows.Count - 1] : null, controller.Readings.Count);
            emptyLabel.gameObject.SetActive(controller.Readings.Count == 0);
            int needed = controller.config.readings;
            meanText.text = controller.Readings.Count == needed
                ? $"Mean thickness t = (t₁ + t₂ + t₃) / 3 = {ScrewGaugePracticalTexts_Azm.F(controller.MeanThicknessMm, 3)} mm"
                : $"Mean thickness t: {controller.Readings.Count} of {needed} readings";
        }
    }
}
