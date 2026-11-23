using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using static VirtualLab.Physics141.OhmSheet;

namespace VirtualLab.Physics141
{
    internal sealed class OhmWorkbook
    {
        const float GraphCurrent = .40f, GraphVoltage = 6;
        readonly RectTransform root, table, graph, chart, line, meterPanel, calc;
        readonly InputField[] entries = new InputField[15];
        readonly InputField mean, conclusion, conclusionDraft;
        readonly RectTransform conclusionEditor;
        readonly bool phoneEntry = UnityEngine.Device.Application.isMobilePlatform;
        readonly Text savedPair, result, pointLabel, calculatorLabel, calculatorReading, calculatorResult, calculatorOperation;
        readonly Button observationsTab, graphTab, assignCalculator;
        readonly Text calculatorToggle;
        readonly Button[] pairTabs = new Button[5];
        readonly Image[] tableRows = new Image[5];
        readonly RectTransform[] marks = new RectTransform[5];
        readonly OhmMeasurements measurements;
        readonly Action changed, close;
        OhmProgress data;
        InputField calculatorTarget;
        int row, selectedPoint, linePoint;
        bool drawingLine;

        internal OhmWorkbook(Transform safe, OhmMeasurements instrument, Action onChanged, Action onClose)
        {
            measurements = instrument;
            changed = onChanged;
            close = onClose;
            root = OhmUi.Rect(safe, "Workbook", .052f, .09f, .948f, .91f);
            var sheet = PinnedPaper(root, "Workbook paper");
            Label(sheet, "Ohm’s Law workbook", .046f, .885f, .51f, .965f, 36, true, TextAnchor.MiddleLeft);
            observationsTab = Button(sheet, "Observations", .52f, .884f, .663f, .963f, () => ShowPage(false));
            graphTab = Button(sheet, "Graph", .668f, .884f, .771f, .963f, () => ShowPage(true));
            Button(sheet, "Close workbook", .803f, .884f, .953f, .963f, () => close());
            OhmUi.Paint(OhmUi.Rect(sheet, "Workbook header rule", .04f, .853f, .953f, .855f), Rule, false);
            var status = OhmUi.Rect(sheet, "Workbook status", .04f, .035f, .953f, .104f);
            Surface(status, Note);
            result = Label(status, "", .02f, 0, .98f, 1, 22, alignment: TextAnchor.MiddleLeft);
            table = OhmUi.Rect(sheet, "Observation page", .04f, .13f, .953f, .825f);
            var captured = OhmUi.Rect(table, "Saved meter readings", 0, 0, .305f, 1);
            Surface(captured, Note);
            meterPanel = OhmUi.Rect(captured, "Meter reading controls", 0, 0, 1, 1);
            Label(meterPanel, "Meter readings", .075f, .826f, .925f, .957f, 29, true, TextAnchor.MiddleLeft);
            Label(meterPanel, "Choose an observation to copy its readings.", .075f, .67f, .925f, .82f, 20, alignment: TextAnchor.UpperLeft);
            var readingSlip = OhmUi.Rect(meterPanel, "Selected reading", .075f, .413f, .925f, .645f);
            Surface(readingSlip, GraphPaper);
            savedPair = Label(readingSlip, "", .045f, .05f, .955f, .95f, 29);
            for (int i = 0; i < 5; i++)
            {
                int cell = i;
                pairTabs[i] = Button(meterPanel, (i + 1).ToString(), .075f + i * .174f, .277f, .229f + i * .174f, .372f,
                    () => { row = cell; RefreshPair(); });
            }
            Button(meterPanel, "Retake selected pair", .075f, .071f, .925f, .173f, () =>
            {
                if (!measurements.Pairs[row].saved) return;
                data.submitted = false;
                measurements.Retake(row);
                close();
            });
            var ruledTable = OhmUi.Rect(table, "Observation table", .336f, 0, 1, 1);
            Surface(ruledTable, GraphPaper);
            Surface(OhmUi.Rect(ruledTable, "Column heading band", 0, .842f, 1, 1), Note);
            Label(ruledTable, "No.", .02f, .861f, .12f, .983f, 22, true);
            for (int i = 0; i < tableRows.Length; i++)
            {
                float y = .712f - i * .118f;
                tableRows[i] = OhmUi.Paint(OhmUi.Rect(ruledTable, "Observation row " + (i + 1), 0, y, 1, y + .117f), GraphPaper, false);
                OhmUi.Paint(OhmUi.Rect(ruledTable, "Row rule", 0, y, 1, y + .0025f), Rule, false);
                Label(ruledTable, (i + 1).ToString(), .02f, y, .12f, y + .116f, 22, true);
            }
            var headers = new[] { "V (V)", "I (A)", "R (Ω)" };
            var headings = new[] { "Voltage (V)", "Current (A)", "Resistance (Ω)" };
            for (int col = 0; col < 3; col++)
            {
                float x = .143f + .279f * col;
                Label(ruledTable, headings[col], x, .861f, x + .248f, .983f, 24, true);
                for (int i = 0; i < 5; i++)
                {
                    int id = i * 3 + col;
                    float y = .729f - i * .118f;
                    entries[id] = Field(ruledTable, $"Row {i + 1} {headers[col]}", x, y, x + .248f, y + .083f,
                        true, value => { data.entries[id] = value; data.submitted = false; RefreshResult(); changed(); });
                    TrackCalculatorTarget(entries[id], i);
                }
            }
            Label(ruledTable, "Mean resistance (Ω)", .035f, .105f, .40f, .208f, 22, true, TextAnchor.MiddleLeft);
            mean = Field(ruledTable, "Mean resistance", .422f, .106f, .67f, .208f,
                true, value => { data.mean = value; data.submitted = false; RefreshResult(); changed(); });
            calculatorToggle = Button(ruledTable, "Calculator", .701f, .106f, .949f, .208f,
                () => ShowCalculator(!calc.gameObject.activeSelf), primary: true).GetComponentInChildren<Text>();
            Label(ruledTable, "Divide voltage by current for each row.", .035f, .021f, .95f, .091f, 18, alignment: TextAnchor.MiddleLeft);
            TrackCalculatorTarget(mean);

            graph = OhmUi.Rect(sheet, "Graph page", .04f, .13f, .953f, .825f);
            chart = OhmUi.Rect(graph, "Voltage versus current", .063f, .174f, .639f, .90f);
            OhmUi.Paint(chart, GraphPaper);
            chart.gameObject.AddComponent<OhmGraphInput>().Chosen = PlacePoint;
            Label(graph, "Voltage, V (V)", .062f, .913f, .29f, 1, 22, true, TextAnchor.MiddleLeft);
            Label(graph, "Current, I (A)", .18f, .012f, .522f, .094f, 22, true);
            for (int i = 0; i <= 24; i++)
            {
                float y = i / 24f;
                bool major = i % 4 == 0;
                OhmUi.Paint(OhmUi.Rect(chart, "Horizontal grid", 0, y, 1, y + (major ? .0026f : .0016f)),
                    i == 0 ? Ink : major ? Rule : new Color(.86f, .88f, .83f), false);
                if (major)
                    Label(graph, (i / 4).ToString(), .021f, .149f + y * .726f, .052f, .199f + y * .726f, 18,
                        alignment: TextAnchor.MiddleRight);
            }
            for (int i = 0; i <= 32; i++)
            {
                float x = i / 32f;
                bool major = i % 4 == 0;
                OhmUi.Paint(OhmUi.Rect(chart, "Vertical grid", x, 0, x + (major ? .0017f : .0009f), 1),
                    i == 0 ? Ink : major ? Rule : new Color(.86f, .88f, .83f), false);
                if (major)
                    Label(graph, (i / 4 * .05f).ToString("0.00", CultureInfo.InvariantCulture),
                        .036f + x * .576f, .111f, .09f + x * .576f, .163f, 18);
            }
            line = OhmUi.Rect(chart, "Student trend line", 0, 0, 0, 0);
            OhmUi.Paint(line, Accent, false);
            for (int i = 0; i < 5; i++)
            {
                marks[i] = OhmUi.Rect(chart, "Plotted point " + (i + 1), 0, 0, 0, 0);
                marks[i].sizeDelta = new Vector2(13, 13);
                OhmUi.Paint(marks[i], Ink, false).sprite = PracticalUIController.CircleSprite();
            }
            var graphTools = OhmUi.Rect(graph, "Plot and conclusion", .678f, 0, 1, 1);
            Surface(graphTools, Note);
            Label(graphTools, "Plot the readings", .085f, .876f, .915f, .98f, 26, true, TextAnchor.MiddleLeft);
            pointLabel = Label(graphTools, "", .085f, .715f, .915f, .873f, 21, alignment: TextAnchor.MiddleLeft);
            Button(graphTools, "Next point", .085f, .605f, .487f, .698f,
                () => { selectedPoint = (selectedPoint + 1) % 5; drawingLine = false; RefreshGraph(); });
            Button(graphTools, "Draw / adjust line", .507f, .605f, .915f, .698f,
                () => { drawingLine = true; linePoint = 0; RefreshGraph(); });
            Button(graphTools, "←", .085f, .494f, .277f, .581f, () => Nudge(-.01f, 0));
            Button(graphTools, "→", .298f, .494f, .49f, .581f, () => Nudge(.01f, 0));
            Button(graphTools, "↓", .51f, .494f, .702f, .581f, () => Nudge(0, -.01f));
            Button(graphTools, "↑", .723f, .494f, .915f, .581f, () => Nudge(0, .01f));
            OhmUi.Paint(OhmUi.Rect(graphTools, "Conclusion divider", .085f, .458f, .915f, .461f), Rule, false);
            Label(graphTools, "What does the graph show?", .085f, .347f, .915f, .444f, 22, true, TextAnchor.MiddleLeft);
            conclusion = Field(graphTools, "Conclusion", .085f, .154f, .915f, .341f, false,
                value => { data.conclusion = value; data.submitted = false; RefreshResult(); changed(); });
            Button(graphTools, "Submit workbook", .085f, .027f, .915f, .125f, Submit, primary: true);

            if (phoneEntry)
            {
                AddPhoneEntry(conclusion, EditConclusion);
                conclusionEditor = OhmUi.Rect(root, "Conclusion editor", 0, 0, 1, 1);
                OhmUi.Paint(conclusionEditor, new Color(.03f, .10f, .13f, .76f));
                var writing = OhmUi.Rect(conclusionEditor, "Conclusion writing panel", .04f, .69f, .96f, .985f);
                Surface(writing, Paper);
                Label(writing, "What does the graph show?", .035f, .67f, .68f, .94f, 25, true, TextAnchor.MiddleLeft);
                Button(writing, "Done typing", .77f, .69f, .965f, .94f, CloseConclusionEditor, primary: true);
                conclusionDraft = Field(writing, "Conclusion draft", .035f, .10f, .965f, .63f, false,
                    value => conclusion.text = value);
                conclusionDraft.onEndEdit.AddListener(_ => CloseConclusionEditor());
                conclusionEditor.gameObject.SetActive(false);
            }

            calc = OhmUi.Rect(captured, "Calculator panel", 0, 0, 1, 1);
            calculatorReading = Label(calc, "", .06f, .86f, .94f, .985f, 20, true);
            var keys = OhmUi.Rect(calc, "Calculator keypad", .055f, .04f, .945f, .85f);
            var calculator = PracticalUiStyle.Calculator(keys, AssignCalculation, RefreshCalculator);
            calculatorResult = calculator.result;
            calculatorOperation = calculator.operation;
            calculatorLabel = calculator.target;
            calculatorLabel.color = Ink;
            assignCalculator = calculator.assign;
            foreach (var key in keys.GetComponentsInChildren<Button>())
                PaperButton(key, key == assignCalculator ? Accent : Neutral, key == assignCalculator ? Color.white : Ink);
            calc.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        static Text Label(Transform parent, string caption, float left, float bottom, float right, float top, int size,
            bool bold = false, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = OhmUi.Label(parent, caption, left, bottom, right, top, size);
            label.color = Ink;
            label.alignment = alignment;
            if (bold) label.fontStyle = FontStyle.Bold;
            return label;
        }

        static Button Button(Transform parent, string caption, float left, float bottom, float right, float top,
            Action clicked, bool primary = false) => PaperButton(OhmUi.Button(parent, caption, left, bottom, right, top, () => clicked()),
                primary ? Accent : Neutral, primary ? Color.white : Ink);

        static InputField Field(Transform parent, string name, float left, float bottom, float right, float top,
            bool numeric, Action<string> edited)
        {
            var field = OhmUi.Field(parent, name, left, bottom, right, top, numeric, value => edited(value));
            ((Image)field.targetGraphic).color = Color.white;
            field.textComponent.color = Ink;
            if (!numeric) field.textComponent.alignment = TextAnchor.UpperLeft;
            OhmUi.Paint(OhmUi.Rect(field.transform, "Writing line", 0, 0, 1, .048f), Rule, false);
            return field;
        }

        void TrackCalculatorTarget(InputField field, int observation = -1)
        {
            void Select()
            {
                if (calculatorTarget != null) calculatorTarget.targetGraphic.color = Color.white;
                calculatorTarget = field;
                if (observation >= 0 && row != observation)
                {
                    row = observation;
                    RefreshPair();
                }
                if (phoneEntry) ShowCalculator(true);
                else RefreshCalculator();
            }

            if (phoneEntry) AddPhoneEntry(field, Select);
            else field.gameObject.AddComponent<OhmFieldFocus>().Focused = Select;
        }

        static void AddPhoneEntry(InputField field, Action clicked)
        {
            field.interactable = false;
            var colors = field.colors;
            colors.disabledColor = Color.white;
            field.colors = colors;
            field.targetGraphic.raycastTarget = false;

            var touch = OhmUi.Rect(field.transform, "Enter " + field.name, 0, 0, 1, 1);
            var button = touch.gameObject.AddComponent<Button>();
            button.targetGraphic = OhmUi.Paint(touch, Color.clear);
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
        }

        void EditConclusion()
        {
            conclusionDraft.SetTextWithoutNotify(conclusion.text);
            conclusionEditor.gameObject.SetActive(true);
            conclusionEditor.SetAsLastSibling();
            conclusionDraft.Select();
            conclusionDraft.ActivateInputField();
        }

        void CloseConclusionEditor()
        {
            if (conclusionEditor != null) conclusionEditor.gameObject.SetActive(false);
        }

        static void MarkSelected(Button button, bool selected)
        {
            button.image.color = selected ? Accent : Neutral;
            button.GetComponentInChildren<Text>().color = selected ? Color.white : Ink;
        }

        void ShowCalculator(bool show)
        {
            if (show) calculatorTarget?.DeactivateInputField();
            calc.gameObject.SetActive(show);
            meterPanel.gameObject.SetActive(!show);
            calculatorToggle.text = show ? "Close calculator" : "Calculator";
            RefreshCalculator();
        }

        void RefreshCalculator()
        {
            bool open = calc.gameObject.activeSelf;
            if (calculatorTarget != null)
                calculatorTarget.targetGraphic.color = open ? PracticalUiStyle.EntryHighlight : Color.white;
            calculatorLabel.text = calculatorTarget == null ? "Tap a number field to select it" : $"Assign to {calculatorTarget.name}";
            assignCalculator.interactable = open && CanAssign(out _);
        }

        bool CanAssign(out string entry)
        {
            entry = "";
            if (calculatorTarget == null || !calculatorTarget.gameObject.activeInHierarchy ||
                calculatorOperation.text.Length > 0 || !Number(calculatorResult.text, out double value))
                return false;
            entry = value.ToString("0.######", CultureInfo.InvariantCulture);
            return entry.Length <= calculatorTarget.characterLimit;
        }

        void AssignCalculation()
        {
            if (!CanAssign(out string entry)) return;
            calculatorTarget.text = entry;
            calculatorTarget.ForceLabelUpdate();
        }

        internal void Restore(OhmProgress progress)
        {
            data = progress;
            if (calculatorTarget != null) calculatorTarget.targetGraphic.color = Color.white;
            calculatorTarget = null;
            row = selectedPoint = 0;
            drawingLine = false;
            for (int i = 0; i < entries.Length; i++) entries[i].SetTextWithoutNotify(data.entries[i] ?? "");
            mean.SetTextWithoutNotify(data.mean ?? "");
            conclusion.SetTextWithoutNotify(data.conclusion ?? "");
        }

        internal void Show()
        {
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            ShowPage(false);
        }
        internal void Hide()
        {
            CloseConclusionEditor();
            ShowCalculator(false);
            root.gameObject.SetActive(false);
        }

        void ShowPage(bool showGraph)
        {
            MarkSelected(observationsTab, !showGraph);
            MarkSelected(graphTab, showGraph);
            table.gameObject.SetActive(!showGraph);
            graph.gameObject.SetActive(showGraph);
            if (showGraph) { ShowCalculator(false); RefreshGraph(); }
            else RefreshPair();
            RefreshResult();
        }

        void RefreshPair()
        {
            var pair = measurements.Pairs[row];
            savedPair.text = pair.saved ? $"Observation {row + 1}\n{pair.voltage} V     {pair.current} A"
                : $"Observation {row + 1}\nNo pair captured yet";
            calculatorReading.text = pair.saved ? $"Obs. {row + 1}   ·   {pair.voltage} V   /   {pair.current} A"
                : $"Observation {row + 1}   ·   Awaiting readings";
            for (int i = 0; i < pairTabs.Length; i++)
            {
                MarkSelected(pairTabs[i], i == row);
                tableRows[i].color = i == row ? new Color(.88f, .924f, .86f) : GraphPaper;
            }
        }

        void PlacePoint(Vector2 point)
        {
            if (drawingLine)
            {
                if (linePoint == 0)
                {
                    data.lineStart = point;
                    data.hasLine = false;
                    linePoint = 1;
                }
                else
                {
                    data.lineEnd = point;
                    data.hasLine = true;
                    drawingLine = false;
                }
            }
            else
            {
                if (!Number(data.entries[selectedPoint * 3], out _) || !Number(data.entries[selectedPoint * 3 + 1], out _))
                    return;
                data.points[selectedPoint] = point;
                data.plotted[selectedPoint] = true;
            }
            data.submitted = false;
            RefreshGraph();
            RefreshResult();
            changed();
        }

        void Nudge(float x, float y)
        {
            if (!data.plotted[selectedPoint] || drawingLine) return;
            Vector2 next = data.points[selectedPoint] + new Vector2(x, y);
            PlacePoint(new Vector2(Mathf.Clamp01(next.x), Mathf.Clamp01(next.y)));
        }

        void RefreshGraph()
        {
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i].gameObject.SetActive(data.plotted[i]);
                marks[i].GetComponent<Image>().color = i == selectedPoint && !drawingLine ? Accent : Ink;
                marks[i].anchorMin = marks[i].anchorMax = data.points[i];
                marks[i].anchoredPosition = Vector2.zero;
            }
            line.gameObject.SetActive(data.hasLine);
            var delta = Vector2.Scale(data.lineEnd - data.lineStart, chart.rect.size);
            line.anchorMin = line.anchorMax = (data.lineStart + data.lineEnd) * .5f;
            line.anchoredPosition = Vector2.zero;
            line.sizeDelta = new Vector2(delta.magnitude, 3);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var index = selectedPoint * 3;
            pointLabel.text = drawingLine ? (linePoint == 0 ? "Tap the first end of the line." : "Tap the second end of the line.")
                : Number(data.entries[index + 1], out _) && Number(data.entries[index], out _)
                    ? $"Point {selectedPoint + 1} · I = {data.entries[index + 1]} A, V = {data.entries[index]} V\nTap the graph to place it."
                    : $"Point {selectedPoint + 1} · Enter I and V in Observations first.";
        }

        void RefreshResult()
        {
            result.text = data.submitted ? "Workbook complete." : data.guided ? ValidateTable(out _) : "";
        }

        string ValidateTable(out bool valid)
        {
            valid = false;
            double total = 0;
            for (int i = 0; i < 5; i++)
            {
                var pair = measurements.Pairs[i];
                int index = i * 3;
                if (!pair.saved) return $"Capture observation {i + 1} at the bench.";
                if (!Number(data.entries[index], out double v) || !Number(data.entries[index + 1], out double a) || a <= 0)
                    return $"Enter V and I for row {i + 1}.";
                if (!Number(pair.voltage, out double measuredV) || !Number(pair.current, out double measuredA)
                    || Math.Abs(v - measuredV) > .005 || Math.Abs(a - measuredA) > .005)
                    return $"Check the recorded readings for row {i + 1}.";
                if (!Number(data.entries[index + 2], out double resistance) || Math.Abs(resistance - v / a) > .11)
                    return $"Check R = V/I for row {i + 1}.";
                total += resistance;
            }
            if (!Number(data.mean, out double average) || Math.Abs(average - total / 5) > .11)
                return "Check the mean resistance using your five R values.";
            valid = true;
            return "Observations and mean checked.";
        }

        void Submit()
        {
            result.text = ValidateTable(out bool valid);
            if (!valid) return;
            for (int i = 0; i < 5; i++)
            {
                Number(data.entries[i * 3], out double v);
                Number(data.entries[i * 3 + 1], out double a);
                if (!data.plotted[i] || Math.Abs(data.points[i].x * GraphCurrent - a) > .013
                    || Math.Abs(data.points[i].y * GraphVoltage - v) > .18)
                {
                    result.text = $"Check graph point {i + 1}.";
                    return;
                }
            }
            var delta = data.lineEnd - data.lineStart;
            if (!data.hasLine || Mathf.Abs(delta.x) < .3f || delta.y / delta.x < .75f || delta.y / delta.x > 1.28f
                || Mathf.Abs(data.lineStart.y - data.lineStart.x * delta.y / delta.x) > .13f)
            {
                result.text = "Adjust the trend line to follow the points.";
                return;
            }
            if (string.IsNullOrWhiteSpace(data.conclusion))
            {
                result.text = "Write a conclusion about voltage and current.";
                return;
            }
            data.submitted = true;
            result.text = "Workbook complete.";
            changed();
        }

        static bool Number(string value, out double result) =>
            double.TryParse(value?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            && !double.IsNaN(result) && !double.IsInfinity(result);
    }
}
