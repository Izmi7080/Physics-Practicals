using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: UI (screen-space inset; displays logic state, decides nothing).
     * Purpose: The READING MAGNIFIER: a large, crisp drawing of the main scale and the vernier scale
     * around the vernier, drawn by code from the jaw's ACTUAL position every frame (TASK Stage 3:
     * "do not rely on the model's texture for the reading"). Whatever the 3D model or the phone's
     * resolution, what the student reads here is exactly what the instrument shows.
     *   - Main scale (top strip): a mark every 1 mm, longer every 5 mm and 10 mm, cm numbers on the
     *     10 mm marks, from 0 to the scale's end (150 mm). Marks start at the shared edge.
     *   - Vernier (bottom strip, the sliding plate): 11 lines spaced 0.9 mm (10 divisions = 9 mm),
     *     its 0 line at the jaw opening, lines 0 / 5 / 10 longer and labelled.
     * The view is centred on the vernier, so the main mark just before the vernier 0 (M) and the
     * coinciding line (n) are always in view. "Show me" (Learn mode) highlights M and the pair of
     * lines that coincide.
     * The marks are a pool of plain UI images re-positioned each frame (no allocation while dragging).
     * HCI restructure: the highlighted lines also get TEXT TAGS ("M" beside the main-scale mark,
     * "n" beside the coinciding vernier line), so "Show me" never relies on colour alone.
     * Talks to: CaliperMeasurement_Azm (opening, show-me), CaliperConfig_Azm, UITheme_Azm.
     */
    public class CaliperMagnifierUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public CaliperMeasurement_Azm measurement;
        [Tooltip("The masked area the scales are drawn in (pivot centre). The shared edge of the two scales is its horizontal centre line.")]
        public RectTransform scaleArea;

        [Header("Drawing (reference pixels)")]
        public float markWidth = 4f;
        public float highlightWidth = 7f;
        public float mainShort = 34f, mainMedium = 48f, mainLong = 64f;
        public float vernierShort = 40f, vernierLong = 58f;
        public float numberSize = 30f;
        [Tooltip("Gap between the end of a long mark and the centre of its number.")]
        public float numberGap = 24f;

        private const int MainPool = 40;
        private readonly List<Image> mainMarks = new List<Image>();
        private readonly List<TMP_Text> mainNumbers = new List<TMP_Text>();
        private readonly List<Image> vernierMarks = new List<Image>();
        private readonly List<TMP_Text> vernierLabels = new List<TMP_Text>();
        private RectTransform vernierPlate;
        private TMP_Text tagM, tagN;
        private bool built;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (built) return;
            built = true;

            // Two strips: the beam (main scale) above the edge, the sliding vernier plate below it.
            var beam = UIFactory_Azm.NewImage(scaleArea, "MainScaleStrip", theme, ThemeColor_Azm.Secondary, false);
            beam.raycastTarget = false;
            beam.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            beam.rectTransform.anchorMax = new Vector2(1f, 1f);
            beam.rectTransform.offsetMin = Vector2.zero;
            beam.rectTransform.offsetMax = Vector2.zero;

            var plate = UIFactory_Azm.NewImage(scaleArea, "VernierPlate", theme, ThemeColor_Azm.PanelStrong, false);
            plate.raycastTarget = false;
            vernierPlate = plate.rectTransform;
            vernierPlate.anchorMin = vernierPlate.anchorMax = new Vector2(0.5f, 0.5f);
            vernierPlate.pivot = new Vector2(0f, 1f);

            for (int i = 0; i < MainPool; i++)
                mainMarks.Add(NewMark("MainMark" + i));
            for (int i = 0; i < 4; i++)
                mainNumbers.Add(NewLabel("MainNumber" + i));

            int lines = measurement.config.vernierDivisions + 1;
            for (int i = 0; i < lines; i++)
                vernierMarks.Add(NewMark("VernierLine" + i));
            for (int i = 0; i < 3; i++)
                vernierLabels.Add(NewLabel("VernierLabel" + i));

            tagM = NewTag("TagM", "M", ThemeColor_Azm.Valid);
            tagN = NewTag("TagN", "n", ThemeColor_Azm.Accent);
        }

        // Juice (Stage 6, Learn mode): the vernier's ZERO line pulses for a few seconds when the
        // magnifier first opens - "read M where this line points". It only draws attention to a
        // line that is always there; it never shows the reading.
        public void PulseVernierZero(float seconds = 3f)
        {
            if (!built || vernierMarks.Count == 0) return;
            var zero = vernierMarks[0];
            Color normal = zero.color;
            zero.color = theme.GetColor(ThemeColor_Azm.Accent);
            Motion_Azm.Pulse(zero.transform, 1.35f, 0.7f);
            Motion_Azm.Delay(this, seconds, () =>
            {
                if (zero == null) return;
                Motion_Azm.StopPulse(zero.transform);
                zero.color = normal;
            });
        }

        // A small bold letter placed beside a highlighted line (left-aligned at its anchor point).
        private TMP_Text NewTag(string name, string text, ThemeColor_Azm role)
        {
            var tag = UIFactory_Azm.NewText(scaleArea, name, theme, text, numberSize * 0.9f, role, TextAlignmentOptions.MidlineLeft, FontStyles.Bold | FontStyles.Italic);
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.overflowMode = TextOverflowModes.Overflow;
            var rect = tag.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(60f, 40f);
            tag.gameObject.SetActive(false);
            return tag;
        }

        private Image NewMark(string name)
        {
            var image = UIFactory_Azm.NewImage(scaleArea, name, theme, ThemeColor_Azm.TextPrimary, false);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return image;
        }

        private TMP_Text NewLabel(string name)
        {
            var label = UIFactory_Azm.NewText(scaleArea, name, theme, "", numberSize, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(90f, 44f);
            return label;
        }

        private static void Place(Image mark, float x, float yBottom, float width, float height)
        {
            var rect = mark.rectTransform;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, yBottom);
        }

        private void LateUpdate()
        {
            if (measurement == null || !measurement.IsMeasuring) return;
            Draw();
        }

        private void Draw()
        {
            var cfg = measurement.config;
            float ppm = cfg.magnifierPixelsPerMm;
            float spacing = cfg.VernierSpacingMm;
            int divisions = cfg.vernierDivisions;
            // The vernier's 0 sits where the SCALES read: the opening plus any zero error.
            float opening = measurement.ScaleReadingMm;

            // Centre the view on the middle of the vernier so M and n are always visible.
            float centreMm = opening + spacing * divisions * 0.5f;
            float halfWidthMm = scaleArea.rect.width * 0.5f / ppm;

            bool highlight = measurement.ShowMeActive;
            int readM = 0, readN = -1, pairMark = int.MinValue;
            if (highlight)
            {
                measurement.CurrentScaleReading(out readM, out readN);
                // The main mark that vernier line n lines up with (general: also for a negative
                // zero error, where line n sits on main mark n - 1).
                pairMark = Mathf.RoundToInt((opening + readN * spacing) / cfg.mainDivisionMm);
            }

            // ----- main scale: marks rise from the shared edge (y = 0) into the top strip -----
            int first = Mathf.CeilToInt((centreMm - halfWidthMm) / cfg.mainDivisionMm);
            int markIndex = 0, numberIndex = 0;
            tagM.gameObject.SetActive(false);
            tagN.gameObject.SetActive(false);
            for (int k = first; markIndex < mainMarks.Count; k++)
            {
                float mm = k * cfg.mainDivisionMm;
                float x = (mm - centreMm) * ppm;
                if (x > halfWidthMm * ppm + 10f) break;
                if (mm < 0f || mm > cfg.mainScaleMaxMm + 1e-3f) continue;

                bool cm = k % 10 == 0, half = k % 5 == 0;
                float h = cm ? mainLong : (half ? mainMedium : mainShort);
                var mark = mainMarks[markIndex++];
                mark.gameObject.SetActive(true);

                bool isM = highlight && k == readM;
                bool isPair = highlight && readN >= 0 && k == pairMark;
                SetRole(mark, isPair ? ThemeColor_Azm.Accent : (isM ? ThemeColor_Azm.Valid : ThemeColor_Azm.TextPrimary));
                Place(mark, x, 0f, (isM || isPair) ? highlightWidth : markWidth, h);
                if (isM)
                {
                    tagM.gameObject.SetActive(true);
                    tagM.rectTransform.anchoredPosition = new Vector2(x + 8f, h + 4f);
                }

                if (cm && numberIndex < mainNumbers.Count)
                {
                    var label = mainNumbers[numberIndex++];
                    label.gameObject.SetActive(true);
                    label.text = (k / 10).ToString();
                    label.rectTransform.anchoredPosition = new Vector2(x, mainLong + numberGap);
                }
            }
            for (int i = markIndex; i < mainMarks.Count; i++) mainMarks[i].gameObject.SetActive(false);
            for (int i = numberIndex; i < mainNumbers.Count; i++) mainNumbers[i].gameObject.SetActive(false);

            // ----- vernier: the plate and its lines rise from below up to the shared edge -----
            float zeroX = (opening - centreMm) * ppm;
            float plateLeft = zeroX - 1.9f * ppm;
            vernierPlate.anchoredPosition = new Vector2(plateLeft, 0f);
            vernierPlate.sizeDelta = new Vector2((spacing * divisions + 3.8f) * ppm, scaleArea.rect.height * 0.5f);

            int labelIndex = 0;
            for (int n = 0; n < vernierMarks.Count; n++)
            {
                float x = zeroX + n * spacing * ppm;
                bool labelled = n == 0 || n == divisions || n * 2 == divisions;
                float h = labelled ? vernierLong : vernierShort;
                var mark = vernierMarks[n];
                bool isPair = highlight && n == readN;
                bool isZero = highlight && n == 0;
                SetRole(mark, isPair ? ThemeColor_Azm.Accent : (isZero ? ThemeColor_Azm.Valid : ThemeColor_Azm.TextPrimary));
                Place(mark, x, -h, (isPair || isZero) ? highlightWidth : markWidth, h);
                if (isPair)
                {
                    tagN.gameObject.SetActive(true);
                    tagN.rectTransform.anchoredPosition = new Vector2(x + 10f, -h * 0.45f);
                }

                if (labelled && labelIndex < vernierLabels.Count)
                {
                    var label = vernierLabels[labelIndex++];
                    label.text = n.ToString();
                    label.rectTransform.anchoredPosition = new Vector2(x, -vernierLong - numberGap);
                }
            }
        }

        private static void SetRole(Image image, ThemeColor_Azm role)
        {
            var themed = image.GetComponent<ThemedImage_Azm>();
            if (themed != null && themed.role != role) themed.SetRole(role);
        }
    }
}
