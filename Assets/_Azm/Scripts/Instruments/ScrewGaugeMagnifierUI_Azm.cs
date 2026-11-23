using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: UI (screen-space inset; displays logic state, decides nothing).
     * Purpose: The READING MAGNIFIER for the screw gauge, drawn by code from the gauge's actual state
     * every frame (the reading never depends on the model's texture or the phone's resolution):
     *   - left, the SLEEVE as the student sees it next to the thimble: the datum line, a mark for
     *     every mm ABOVE it (numbered every 5 mm) and a mark for every half mm BELOW it, all ending
     *     at the thimble edge (the right side of this area) - so M is the last mark visible;
     *   - right, the CIRCULAR SCALE on the thimble: its divisions move past the datum line as the
     *     thimble turns (higher numbers come down from above as the gap opens); n is the division
     *     on the datum line (the line drawn across from the sleeve).
     * Both are drawn at the reading the scales show (gap + zero error). "Show me" highlights M (green)
     * and n (amber). Marks are pooled UI images re-positioned each frame (no allocation).
     * HCI restructure (baseline finding 12: the magnifier was hard to make sense of): small captions
     * name the "datum line" and the "thimble edge", and the highlighted lines get TEXT TAGS ("M",
     * "n") so "Show me" never relies on colour alone.
     * Talks to: ScrewGaugeMeasurement_Azm, ScrewGaugeConfig_Azm, UITheme_Azm.
     */
    public class ScrewGaugeMagnifierUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public ScrewGaugeMeasurement_Azm measurement;
        [Tooltip("Masked area for the sleeve (pivot centre). Its right edge is the thimble edge.")]
        public RectTransform sleeveArea;
        [Tooltip("Masked area for the circular scale (pivot centre), right of the sleeve area.")]
        public RectTransform thimbleArea;

        [Header("Drawing (reference pixels)")]
        public float markWidth = 4f;
        public float highlightWidth = 7f;
        public float mmMark = 46f, fiveMmMark = 62f, halfMark = 40f;
        public float shortTick = 40f, longTick = 64f;
        public float numberSize = 30f;

        private readonly List<Image> sleeveMarks = new List<Image>();
        private readonly List<TMP_Text> sleeveNumbers = new List<TMP_Text>();
        private readonly List<Image> ticks = new List<Image>();
        private readonly List<TMP_Text> tickNumbers = new List<TMP_Text>();
        private TMP_Text tagM, tagN;
        private bool built;

        private void Awake() => Build();

        private void Build()
        {
            if (built) return;
            built = true;
            var datum = UIFactory_Azm.NewImage(sleeveArea, "DatumLine", theme, ThemeColor_Azm.TextPrimary, false);
            datum.raycastTarget = false;
            datum.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            datum.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            datum.rectTransform.sizeDelta = new Vector2(0f, markWidth);
            for (int i = 0; i < 26; i++) sleeveMarks.Add(NewMark(sleeveArea, "SleeveMark" + i));
            for (int i = 0; i < 3; i++) sleeveNumbers.Add(NewLabel(sleeveArea, "SleeveNumber" + i));
            for (int i = 0; i < 14; i++) ticks.Add(NewMark(thimbleArea, "Tick" + i));
            for (int i = 0; i < 4; i++) tickNumbers.Add(NewLabel(thimbleArea, "TickNumber" + i));

            // Captions that name what the student is looking at.
            Caption(sleeveArea, "DatumCaption", "datum line", new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(12f, -6f), TextAlignmentOptions.TopLeft);
            Caption(sleeveArea, "EdgeCaption", "thimble edge ▶", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 6f), TextAlignmentOptions.BottomRight);

            tagM = NewTag(sleeveArea, "TagM", "M", ThemeColor_Azm.Valid);
            tagN = NewTag(thimbleArea, "TagN", "n", ThemeColor_Azm.Accent);
        }

        private void Caption(RectTransform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, TextAlignmentOptions align)
        {
            var label = UIFactory_Azm.NewText(parent, name, theme, text, 22f, ThemeColor_Azm.TextSecondary, align, FontStyles.Italic);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            UIFactory_Azm.Pin(label.rectTransform, anchor, pivot, new Vector2(240f, 30f), pos);
        }

        private TMP_Text NewTag(RectTransform parent, string name, string text, ThemeColor_Azm role)
        {
            var tag = UIFactory_Azm.NewText(parent, name, theme, text, numberSize * 0.9f, role, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.overflowMode = TextOverflowModes.Overflow;
            tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tag.rectTransform.sizeDelta = new Vector2(50f, 40f);
            tag.gameObject.SetActive(false);
            return tag;
        }

        private Image NewMark(RectTransform parent, string name)
        {
            var image = UIFactory_Azm.NewImage(parent, name, theme, ThemeColor_Azm.TextPrimary, false);
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            return image;
        }

        private TMP_Text NewLabel(RectTransform parent, string name)
        {
            var label = UIFactory_Azm.NewText(parent, name, theme, "", numberSize, ThemeColor_Azm.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(80f, 40f);
            return label;
        }

        private static void Place(Image mark, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = mark.rectTransform;
            r.pivot = pivot;
            r.sizeDelta = size;
            r.anchoredPosition = pos;
        }

        private static void SetRole(Image image, ThemeColor_Azm role)
        {
            var themed = image.GetComponent<ThemedImage_Azm>();
            if (themed != null && themed.role != role) themed.SetRole(role);
        }

        private void LateUpdate()
        {
            if (measurement == null || !measurement.IsMeasuring) return;
            var cfg = measurement.config;
            float scale = measurement.ScaleReadingMm;
            bool highlight = measurement.ShowMeActive;
            float readM = -1f; int readN = -1;
            if (highlight) measurement.CurrentScaleReading(out readM, out readN);

            // ----- sleeve: marks up to the thimble edge (the area's right edge) -----
            float ppm = cfg.magnifierPixelsPerMm;
            float halfW = sleeveArea.rect.width * 0.5f;
            float edgeX = halfW;
            int lastHalf = Mathf.FloorToInt(scale / 0.5f + 1e-4f);
            int firstHalf = Mathf.Max(0, lastHalf - Mathf.CeilToInt(sleeveArea.rect.width / ppm / 0.5f) - 1);
            int mi = 0, ni = 0;
            tagM.gameObject.SetActive(false);
            tagN.gameObject.SetActive(false);
            for (int h = firstHalf; h <= lastHalf && mi < sleeveMarks.Count; h++)
            {
                float s = h * 0.5f;
                if (s > cfg.rangeMm + 1e-3f) break;
                float x = edgeX - (scale - s) * ppm;
                bool whole = h % 2 == 0;
                int mm = h / 2;
                bool five = whole && mm % 5 == 0;
                var mark = sleeveMarks[mi++];
                mark.gameObject.SetActive(true);
                bool isM = highlight && Mathf.Abs(s - readM) < 1e-4f;
                SetRole(mark, isM ? ThemeColor_Azm.Valid : ThemeColor_Azm.TextPrimary);
                float w = isM ? highlightWidth : markWidth;
                if (whole) Place(mark, new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(w, five ? fiveMmMark : mmMark));
                else Place(mark, new Vector2(0.5f, 1f), new Vector2(x, 0f), new Vector2(w, halfMark));
                if (isM)
                {
                    // Left of the mark: the thimble edge is on its right.
                    tagM.gameObject.SetActive(true);
                    tagM.rectTransform.anchoredPosition = new Vector2(x - 24f, whole ? mmMark * 0.5f : -halfMark * 0.5f);
                }
                if (five && ni < sleeveNumbers.Count)
                {
                    var label = sleeveNumbers[ni++];
                    label.gameObject.SetActive(true);
                    label.text = mm.ToString();
                    label.rectTransform.anchoredPosition = new Vector2(x, fiveMmMark + 24f);
                }
            }
            for (int i = mi; i < sleeveMarks.Count; i++) sleeveMarks[i].gameObject.SetActive(false);
            for (int i = ni; i < sleeveNumbers.Count; i++) sleeveNumbers[i].gameObject.SetActive(false);

            // ----- circular scale: division value at the datum line, continuous -----
            float lc = cfg.LeastCountMm;
            float within = ((scale % cfg.pitchMm) + cfg.pitchMm) % cfg.pitchMm;
            float nAtDatum = within / lc;                         // 0 .. 50
            float dpx = cfg.magnifierPixelsPerDivision;
            float left = -thimbleArea.rect.width * 0.5f;
            int range = Mathf.CeilToInt(thimbleArea.rect.height * 0.5f / dpx) + 1;
            int centre = Mathf.RoundToInt(nAtDatum);
            int ti = 0, li = 0;
            for (int k = centre - range; k <= centre + range && ti < ticks.Count; k++)
            {
                int label = ((k % cfg.divisions) + cfg.divisions) % cfg.divisions;
                float y = (k - nAtDatum) * dpx;                  // higher numbers above the datum line
                bool five = label % 5 == 0;
                var tick = ticks[ti++];
                tick.gameObject.SetActive(true);
                bool isN = highlight && label == readN && Mathf.Abs(y) < dpx * 0.5f;
                SetRole(tick, isN ? ThemeColor_Azm.Accent : ThemeColor_Azm.TextPrimary);
                Place(tick, new Vector2(0f, 0.5f), new Vector2(left, y), new Vector2(five ? longTick : shortTick, isN ? highlightWidth : markWidth));
                if (isN)
                {
                    tagN.gameObject.SetActive(true);
                    tagN.rectTransform.anchoredPosition = new Vector2(left + longTick + 110f, y);
                }
                if (five && li < tickNumbers.Count)
                {
                    var t = tickNumbers[li++];
                    t.gameObject.SetActive(true);
                    t.text = label.ToString();
                    t.rectTransform.anchoredPosition = new Vector2(left + longTick + 38f, y);
                }
            }
            for (int i = ti; i < ticks.Count; i++) ticks[i].gameObject.SetActive(false);
            for (int i = li; i < tickNumbers.Count; i++) tickNumbers[i].gameObject.SetActive(false);
        }
    }
}
