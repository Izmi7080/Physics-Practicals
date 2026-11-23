using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI (presentation of the observation table).
     * Purpose: Shows the observation table from the controller's rows. Two layouts share this script:
     *   compact (inside the drawer): No. | l | L | t1 | t2 | mean t | T | T^2  (n and r are in the
     *     header line above the table, because the drawer is narrow)
     *   full (the Expand overlay):   every syllabus column, including r and n.
     * Rows are built in code. A new row fades and scales in; a discarded row is dimmed and struck
     * through (it stays visible as an honest record but no longer counts). Each row has a discard (x)
     * button and, for suspect rows, a warning button that explains why.
     * Display precision (task spec): lengths 1 decimal place, times 2, T^2 3 - with ONE deliberate
     * exception: T is shown to 3 decimals so a student can check T^2 = T x T from what they see
     * (with 2 decimals the squares visibly disagree). Flagged in the report.
     * Talks to: PendulumController_Azm (rows and events); raises DiscardRequested / SuspectTapped
     * for the HUD (which shows the confirmation / explanation).
     */
    public class TableView_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public PendulumController_Azm controller;
        public bool fullColumns;
        public RectTransform headerContainer;
        public RectTransform rowContainer;
        public TMP_Text emptyLabel;
        public float fontSize = 24f;
        public float rowHeight = 76f;

        public event Action<int> DiscardRequested;
        public event Action<TrialRow_Azm> SuspectTapped;

        private struct Column
        {
            public string header;
            public float width;
            public Func<TrialRow_Azm, string> cell;
        }

        private class RowItem
        {
            public TrialRow_Azm row;
            public RectTransform rect;
            public CanvasGroup group;
            public Image background;
            public TMP_Text[] cells;
            public Button discard;
            public Button warn;
            public float animTime;
        }

        private readonly List<Column> columns = new List<Column>();
        private readonly Dictionary<int, RowItem> items = new Dictionary<int, RowItem>();
        private bool built;

        private static string F(float v, int d) => v.ToString("F" + d, CultureInfo.InvariantCulture);

        private void OnEnable()
        {
            if (controller == null) return;
            if (!built) Build();
            controller.RowAdded += OnRowAdded;
            controller.RowChanged += OnRowChanged;

            // This view may have been hidden (inactive drawer tab / closed overlay) while rows were
            // added or discarded, so catch up now.
            foreach (var row in controller.Rows)
            {
                if (!items.ContainsKey(row.Number)) AddItem(row, false);
                else ApplyState(items[row.Number]);
            }
            RefreshEmpty();
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.RowAdded -= OnRowAdded;
            controller.RowChanged -= OnRowChanged;
        }

        private void Build()
        {
            built = true;
            DefineColumns();

            var headerLayout = headerContainer.GetComponent<HorizontalLayoutGroup>();
            foreach (var col in columns)
                MakeCell(headerContainer, col.header, col.width, ThemeColor_Azm.Accent, FontStyles.Bold);
            MakeSpacer(headerContainer, fullColumns ? 150f : 140f);

            foreach (var row in controller.Rows)
                AddItem(row, false);
        }

        private void DefineColumns()
        {
            columns.Clear();
            if (fullColumns)
            {
                columns.Add(new Column { header = "No.", width = 80f, cell = r => r.Number.ToString() });
                columns.Add(new Column { header = "ℓ (cm)", width = 120f, cell = r => F(r.LengthCm, 1) });
                columns.Add(new Column { header = "r (cm)", width = 100f, cell = r => F(r.RadiusCm, 2) });
                columns.Add(new Column { header = "L = ℓ+r (cm)", width = 140f, cell = r => F(r.EffectiveLengthCm, 1) });
                columns.Add(new Column { header = "n", width = 70f, cell = r => r.Oscillations.ToString() });
                columns.Add(new Column { header = "t₁ (s)", width = 110f, cell = r => F(r.Time1, 2) });
                columns.Add(new Column { header = "t₂ (s)", width = 110f, cell = r => F(r.Time2, 2) });
                columns.Add(new Column { header = "Mean t (s)", width = 140f, cell = r => F(r.MeanTime, 2) });
                columns.Add(new Column { header = "T = t/n (s)", width = 140f, cell = r => F(r.Period, 3) });
                columns.Add(new Column { header = "T² (s²)", width = 120f, cell = r => F(r.PeriodSquared, 3) });
            }
            else
            {
                columns.Add(new Column { header = "No.", width = 40f, cell = r => r.Number.ToString() });
                columns.Add(new Column { header = "ℓ (cm)", width = 66f, cell = r => F(r.LengthCm, 1) });
                columns.Add(new Column { header = "L (cm)", width = 64f, cell = r => F(r.EffectiveLengthCm, 1) });
                columns.Add(new Column { header = "t₁ (s)", width = 72f, cell = r => F(r.Time1, 2) });
                columns.Add(new Column { header = "t₂ (s)", width = 72f, cell = r => F(r.Time2, 2) });
                columns.Add(new Column { header = "mean t (s)", width = 78f, cell = r => F(r.MeanTime, 2) });
                columns.Add(new Column { header = "T (s)", width = 68f, cell = r => F(r.Period, 3) });
                columns.Add(new Column { header = "T² (s²)", width = 72f, cell = r => F(r.PeriodSquared, 3) });
            }
        }

        private TMP_Text MakeCell(Transform parent, string text, float width, ThemeColor_Azm role, FontStyles style)
        {
            var label = UIFactory_Azm.NewText(parent, "Cell", theme, text, fontSize, role, TextAlignmentOptions.Center, style);
            label.overflowMode = TextOverflowModes.Overflow;
            // Values never wrap onto a second line; headers may (two-line headers like "mean t (s)").
            if (style != FontStyles.Bold) label.textWrappingMode = TextWrappingModes.NoWrap;
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            return label;
        }

        private static void MakeSpacer(Transform parent, float width)
        {
            var spacer = UIFactory_Azm.NewRect("Spacer", parent);
            var layout = spacer.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
        }

        private void OnRowAdded(TrialRow_Azm row)
        {
            if (!built) Build();
            if (!items.ContainsKey(row.Number)) AddItem(row, true);
            RefreshEmpty();
        }

        private void OnRowChanged(TrialRow_Azm row)
        {
            if (items.TryGetValue(row.Number, out RowItem item)) ApplyState(item);
        }

        private void AddItem(TrialRow_Azm row, bool animate)
        {
            var image = UIFactory_Azm.NewImage(rowContainer, "Row " + row.Number, theme, ThemeColor_Azm.Panel);
            var rect = image.rectTransform;
            var layoutElement = image.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = rowHeight;
            layoutElement.minHeight = rowHeight;

            var horizontal = image.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 2f;
            horizontal.padding = new RectOffset(4, 4, 4, 4);
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            var item = new RowItem { row = row, rect = rect, background = image, group = image.gameObject.AddComponent<CanvasGroup>() };
            item.cells = new TMP_Text[columns.Count];
            for (int i = 0; i < columns.Count; i++)
                item.cells[i] = MakeCell(rect, columns[i].cell(row), columns[i].width, ThemeColor_Azm.TextPrimary, FontStyles.Normal);

            // Warning (suspect row) and discard buttons.
            var actions = UIFactory_Azm.NewRect("Actions", rect);
            var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 6f;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.childControlWidth = false;
            actionLayout.childControlHeight = false;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;
            var actionSize = actions.gameObject.AddComponent<LayoutElement>();
            actionSize.preferredWidth = fullColumns ? 150f : 140f;
            actionSize.minWidth = actionSize.preferredWidth;

            float b = rowHeight - 12f;
            item.warn = UIFactory_Azm.NewButton(actions, "Warn", theme, "!", ThemeColor_Azm.Accent, new Vector2(b, b), 34f, true);
            item.warn.GetComponentInChildren<ThemedText_Azm>().SetRole(ThemeColor_Azm.OnPrimary);
            item.warn.onClick.AddListener(() => SuspectTapped?.Invoke(row));
            item.discard = UIFactory_Azm.NewButton(actions, "Discard", theme, "×", ThemeColor_Azm.Secondary, new Vector2(b, b), 40f);
            item.discard.onClick.AddListener(() => DiscardRequested?.Invoke(row.Number));

            items[row.Number] = item;
            ApplyState(item);

            item.animTime = animate ? 0f : 1f;
            item.group.alpha = animate ? 0f : 1f;
        }

        private void ApplyState(RowItem item)
        {
            item.warn.gameObject.SetActive(item.row.Suspect && !item.row.Discarded);
            item.discard.gameObject.SetActive(!item.row.Discarded);
            foreach (var cell in item.cells)
            {
                cell.fontStyle = item.row.Discarded ? FontStyles.Strikethrough : FontStyles.Normal;
                cell.GetComponent<ThemedText_Azm>().SetRole(item.row.Discarded ? ThemeColor_Azm.TextSecondary : ThemeColor_Azm.TextPrimary);
                cell.alpha = item.row.Discarded ? 0.55f : 1f;
            }
        }

        private void RefreshEmpty()
        {
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(items.Count == 0);
        }

        // New rows animate in (fade + scale, plus a short highlight flash) instead of popping.
        private void Update()
        {
            foreach (var item in items.Values)
            {
                if (item.animTime >= 1f) continue;
                item.animTime = Mathf.Min(1f, item.animTime + Time.unscaledDeltaTime / 0.45f);
                float eased = Mathf.SmoothStep(0f, 1f, item.animTime);
                item.group.alpha = eased;
                item.rect.localScale = new Vector3(Mathf.Lerp(0.94f, 1f, eased), Mathf.Lerp(0.6f, 1f, eased), 1f);

                Color normal = theme.GetColor(ThemeColor_Azm.Panel);
                Color flash = theme.GetColor(ThemeColor_Azm.Primary); flash.a = 0.55f;
                item.background.color = Color.Lerp(flash, normal, eased);
            }
        }
    }
}
