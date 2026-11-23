using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (generic x/y plot drawn with Unity UI - no charting package).
     * Purpose: Draws axes with tick labels and units, gridlines, one marker per data point and an
     * optional best-fit line, all in ONE mesh (cheap on a phone), plus a small pool of TMP labels.
     * Both axes start at zero so a line through the origin is visible as such.
     * It knows nothing about pendulums: give it points in display units (x, y), and optionally
     * the fit as y = slope * x + intercept in the same units.
     * Talks to: fed by the graph tab / results panel via SetData; reads UITheme_Azm for colours.
     */
    [RequireComponent(typeof(CanvasRenderer))]
    public class GraphView_Azm : MaskableGraphic
    {
        public UITheme_Azm theme;
        public string xTitle = "L (cm)";
        public string yTitle = "T² (s²)";
        public float marginLeft = 130f;
        public float marginBottom = 92f;
        public float marginRight = 36f;
        public float marginTop = 30f;
        public float labelFontSize = 26f;
        public float pointSize = 22f;
        public float lineWidth = 6f;

        private readonly List<Vector2> points = new List<Vector2>();
        private bool hasFit;
        private float fitSlope;
        private float fitIntercept;
        private float xMax = 100f, yMax = 3f, xStep = 20f, yStep = 0.5f;
        private readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        private bool labelsDirty = true;

        // Juice (HCI restructure Stage 6): a newly added point drops onto its place with a small
        // bounce, so the student SEES their new row land on the graph. Only the newest point moves;
        // it lands exactly on its value (the animation never shows a different reading).
        private int droppingIndex = -1;
        private float dropStart;
        private const float DropSeconds = 0.45f;
        private const float DropHeight = 70f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;

            // UI graphics also run in the Editor, so a label set may have been saved into the scene.
            // The pool below is built at run time only; clear any stale labels so there are no duplicates.
            if (Application.isPlaying)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                    Destroy(transform.GetChild(i).gameObject);
            }
        }

        // points: (x, y) in display units. The fit, if any, is y = slope * x + intercept in the SAME
        // units (so if x is shown in cm, slope is per cm).
        public void SetData(IList<Vector2> data, bool fitValid, float slope, float intercept)
        {
            int previousCount = points.Count;
            points.Clear();
            float maxX = 0f, maxY = 0f;
            foreach (var p in data)
            {
                points.Add(p);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }
            hasFit = fitValid;
            fitSlope = slope;
            fitIntercept = intercept;

            // Axis ranges: start at 0, end on a "nice" number just above the data.
            xStep = NiceStep(Mathf.Max(maxX * 1.1f, 10f) / 5f);
            xMax = Mathf.Ceil(Mathf.Max(maxX * 1.08f, 10f) / xStep) * xStep;
            float fitTop = fitValid ? Mathf.Max(0f, slope * xMax + intercept) : 0f;
            float yRange = Mathf.Max(maxY * 1.12f, fitTop * 0.98f, 0.5f);
            yStep = NiceStep(yRange / 5f);
            yMax = Mathf.Ceil(yRange / yStep) * yStep;

            // Exactly one more point than before: that row was just added - drop it in.
            if (points.Count == previousCount + 1 && previousCount > 0 && isActiveAndEnabled && !Motion_Azm.ReduceMotion)
            {
                droppingIndex = points.Count - 1;
                dropStart = Time.unscaledTime;
            }

            labelsDirty = true;
            SetVerticesDirty();
            UpdateLabels();
        }

        private void Update()
        {
            if (droppingIndex < 0) return;
            if (Time.unscaledTime - dropStart > DropSeconds) droppingIndex = -1;
            SetVerticesDirty();
        }

        // 0 -> 1 with a small overshoot ("lands and settles").
        private float DropProgress()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - dropStart) / DropSeconds);
            const float c1 = 1.70158f, c3 = c1 + 1f;   // ease-out-back
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            labelsDirty = true;
            if (isActiveAndEnabled) UpdateLabels();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            labelsDirty = true;
            UpdateLabels();
        }

        private Rect PlotRect()
        {
            Rect r = rectTransform.rect;
            return new Rect(r.xMin + marginLeft, r.yMin + marginBottom,
                Mathf.Max(10f, r.width - marginLeft - marginRight), Mathf.Max(10f, r.height - marginBottom - marginTop));
        }

        private Vector2 ToPlot(Rect plot, float x, float y)
        {
            return new Vector2(plot.xMin + x / xMax * plot.width, plot.yMin + y / yMax * plot.height);
        }

        // 1, 2 or 5 x a power of ten, at or just above the wanted step.
        private static float NiceStep(float raw)
        {
            if (raw <= 0f) return 1f;
            float pow = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            float frac = raw / pow;
            float nice = frac <= 1f ? 1f : frac <= 2f ? 2f : frac <= 5f ? 5f : 10f;
            return nice * pow;
        }

        private static string FormatTick(float value, float step)
        {
            int decimals = step >= 1f ? 0 : (Mathf.Abs(step * 10f - Mathf.Round(step * 10f)) < 0.001f ? 1 : 2);
            return value.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ----- mesh -----

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (theme == null) return;

            Rect plot = PlotRect();
            Color grid = theme.GetColor(ThemeColor_Azm.TextSecondary); grid.a = 0.22f;
            Color axis = theme.GetColor(ThemeColor_Azm.TextSecondary);
            Color fitColor = theme.GetColor(ThemeColor_Azm.Accent);
            Color pointColor = theme.GetColor(ThemeColor_Azm.Primary);
            Color halo = theme.GetColor(ThemeColor_Azm.PanelStrong); halo.a = 1f;

            for (float x = xStep; x <= xMax + 0.001f; x += xStep)
                AddLine(vh, ToPlot(plot, x, 0f), ToPlot(plot, x, yMax), 2f, grid);
            for (float y = yStep; y <= yMax + 0.001f; y += yStep)
                AddLine(vh, ToPlot(plot, 0f, y), ToPlot(plot, xMax, y), 2f, grid);

            AddLine(vh, ToPlot(plot, 0f, 0f), ToPlot(plot, xMax, 0f), 4f, axis);
            AddLine(vh, ToPlot(plot, 0f, 0f), ToPlot(plot, 0f, yMax), 4f, axis);

            if (hasFit)
            {
                // Clip the line y = m x + c to the plot box (0..xMax, 0..yMax).
                float x0 = 0f, x1 = xMax;
                if (fitSlope > 1e-6f)
                {
                    x0 = Mathf.Max(x0, (0f - fitIntercept) / fitSlope);
                    x1 = Mathf.Min(x1, (yMax - fitIntercept) / fitSlope);
                }
                if (x1 > x0)
                    AddLine(vh, ToPlot(plot, x0, fitSlope * x0 + fitIntercept), ToPlot(plot, x1, fitSlope * x1 + fitIntercept), lineWidth, fitColor);
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 c = ToPlot(plot, points[i].x, points[i].y);
                float scale = 1f;
                if (i == droppingIndex)
                {
                    float k = DropProgress();
                    c.y += (1f - k) * DropHeight;
                    scale = Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(k));
                }
                AddDiamond(vh, c, pointSize * 0.75f * scale, halo);
                AddDiamond(vh, c, pointSize * 0.5f * scale, pointColor);
            }
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 dir = (b - a);
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero);
            vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void AddDiamond(VertexHelper vh, Vector2 c, float r, Color color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(c + new Vector2(0f, r), color, Vector2.zero);
            vh.AddVert(c + new Vector2(r, 0f), color, Vector2.zero);
            vh.AddVert(c + new Vector2(0f, -r), color, Vector2.zero);
            vh.AddVert(c + new Vector2(-r, 0f), color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        // ----- labels -----

        private void UpdateLabels()
        {
            // Labels are created at run time only (never saved into a scene).
            if (!labelsDirty || theme == null || !Application.isPlaying) return;
            labelsDirty = false;

            Rect plot = PlotRect();
            Rect r = rectTransform.rect;
            int used = 0;

            // X tick labels (below the axis)
            for (float x = 0f; x <= xMax + 0.001f; x += xStep)
            {
                var label = GetLabel(used++, FormatTick(x, xStep), TextAlignmentOptions.Top, ThemeColor_Azm.TextSecondary);
                PlaceLabel(label, ToPlot(plot, x, 0f) + new Vector2(0f, -10f), new Vector2(120f, 40f), new Vector2(0.5f, 1f), r, 0f);
            }
            // Y tick labels (left of the axis)
            for (float y = 0f; y <= yMax + 0.001f; y += yStep)
            {
                var label = GetLabel(used++, FormatTick(y, yStep), TextAlignmentOptions.Right, ThemeColor_Azm.TextSecondary);
                PlaceLabel(label, ToPlot(plot, 0f, y) + new Vector2(-12f, 0f), new Vector2(110f, 40f), new Vector2(1f, 0.5f), r, 0f);
            }
            // Axis titles
            var xt = GetLabel(used++, xTitle, TextAlignmentOptions.Center, ThemeColor_Azm.TextPrimary);
            PlaceLabel(xt, new Vector2(plot.center.x, r.yMin + 4f), new Vector2(400f, 44f), new Vector2(0.5f, 0f), r, 0f);
            var yt = GetLabel(used++, yTitle, TextAlignmentOptions.Center, ThemeColor_Azm.TextPrimary);
            PlaceLabel(yt, new Vector2(r.xMin + 20f, plot.center.y), new Vector2(400f, 44f), new Vector2(0.5f, 0.5f), r, 90f);

            for (int i = used; i < labels.Count; i++) labels[i].gameObject.SetActive(false);
        }

        private TextMeshProUGUI GetLabel(int index, string text, TextAlignmentOptions align, ThemeColor_Azm role)
        {
            while (labels.Count <= index)
            {
                var label = UIFactory_Azm.NewText(transform, "Label", theme, "", labelFontSize, role, align);
                label.overflowMode = TextOverflowModes.Overflow;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                labels.Add(label);
            }
            var l = labels[index];
            l.gameObject.SetActive(true);
            l.fontSize = labelFontSize;
            l.text = text;
            l.alignment = align;
            l.GetComponent<ThemedText_Azm>().SetRole(role);
            return l;
        }

        // localPoint is in this graphic's local space (origin = its pivot).
        private static void PlaceLabel(TextMeshProUGUI label, Vector2 localPoint, Vector2 size, Vector2 labelPivot, Rect parentRect, float rotation)
        {
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = labelPivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = localPoint - new Vector2(parentRect.xMin, parentRect.yMin);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }
    }
}
