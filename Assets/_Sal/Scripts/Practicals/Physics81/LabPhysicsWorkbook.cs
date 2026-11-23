using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualLab.Physics81
{
    public sealed partial class LabSetupHud
    {
        static readonly string[] WorkbookTabs = { "Measurements", "Calculation", "Write-up", "Feedback" };
        static readonly string[] WriteUpTabs = { "Aim & apparatus", "Method & variables", "Observations" };
        static readonly string[] EntryLabels = { "m₁  Empty cup (g)", "m₂  Cup with water (g)", "M₂  Brass bob (g)",
            "θ₁  Initial water (°C)", "θ₂  Hot bath (°C)", "θ  Mixture (°C)" };
        static readonly Color EntryBackground = PracticalUiStyle.Entry, EntryHighlight = PracticalUiStyle.EntryHighlight;
        readonly RectTransform[] workbookSections = new RectTransform[3];
        readonly Image[] workbookTabFaces = new Image[4];
        readonly RectTransform[] writeUpSections = new RectTransform[3];
        readonly Image[] writeUpTabFaces = new Image[3];
        readonly InputField[] workbookEntries = new InputField[6];
        readonly Text[] workbookCaptures = new Text[6];
        readonly InputField[] calculationEntries = new InputField[7];
        readonly Text[] calculationChecks = new Text[7];
        InputField calculatorTarget;
        Tajurbah_Gah.calc borrowedCalculator;
        RectTransform workbook, calculatorSpace, workbookActions;
        Button submitWorkbook, aimAndMethod, assignCalculator;
        InputField aimInput, apparatusInput, methodInput, variablesInput, observationsInput, conclusionInput, reflectionInput;
        Text calculationOutput, workbookAdvice, calculatorToggle, calculatorTargetLabel, calculatorResult, calculatorOperator;
        string calculatorTargetName;
        bool calculatorOpen;
        int workbookTab, writeUpTab;

        void BuildWorkbook()
        {
            workbook = Box("Physics 8.1 workbook", document, new Vector2(.055f, .13f), new Vector2(.945f, .80f), Vector2.zero, Vector2.zero);
            aimAndMethod = ButtonAt("Aim and method", document, Vector2.one, new Vector2(-380, -120), new Vector2(-190, -58),
                () => { session.SaveProgress(); studyFromWorkbook = true; studySection = 0; OpenPage(Page.Study); }, out _);
            for (int i = 0; i < WorkbookTabs.Length; i++)
            {
                int section = i;
                var tab = Box(WorkbookTabs[i], workbook, new Vector2(i * .195f, .87f), new Vector2((i + 1) * .195f, 1), Vector2.zero, new Vector2(-8, 0));
                workbookTabFaces[i] = (Image)ButtonIn(tab, WorkbookTabs[i], () => SelectWorkbookTab(section), out _).targetGraphic;
            }
            ButtonIn(Box("Workbook calculator toggle", workbook, new Vector2(.78f, .87f), Vector2.one, Vector2.zero, Vector2.zero),
                "Calculator", () => { calculatorOpen = !calculatorOpen; RefreshWorkbookAdvice(); }, out calculatorToggle);

            for (int i = 0; i < workbookSections.Length; i++)
                workbookSections[i] = Box(WorkbookTabs[i] + " page", workbook, new Vector2(0, .13f), new Vector2(.69f, .84f), Vector2.zero, new Vector2(-12, 0));

            BuildMeasurementEntries(workbookSections[0]);
            BuildCalculation(workbookSections[1]);
            BuildWriteUp(workbookSections[2]);

            var aside = Box("Workbook reference", workbook, new Vector2(.705f, .13f), new Vector2(1, .84f), Vector2.zero, Vector2.zero);
            ImageFor(aside, InkPanel, false);
            workbookAdvice = Label(Box("Workbook guidance", aside, Vector2.zero, Vector2.one, new Vector2(15, 12), new Vector2(-15, -14)),
                "", 22, TextAnchor.UpperLeft);
            calculatorSpace = Box("Physics calculator", aside, Vector2.zero, Vector2.one, new Vector2(7, 6), new Vector2(-7, -6));
            BuildWorkbookCalculator();

            workbookActions = Box("Trial actions", workbook, Vector2.zero, new Vector2(.69f, .115f), Vector2.zero, new Vector2(-12, 0));
            submitWorkbook = ButtonIn(Box("Submit workbook", workbookActions, Vector2.zero, new Vector2(.33f, 1), Vector2.zero, new Vector2(-8, 0)),
                "Submit workbook", SubmitWorkbook, out _);
            ButtonIn(Box("Retry heating", workbookActions, new Vector2(.34f, 0), new Vector2(.66f, 1), Vector2.zero, new Vector2(-8, 0)),
                "Retry heating", () => { session.RetryHeating(); ResetWorkbookPage(); }, out _);
            ButtonIn(Box("Repeat trial", workbookActions, new Vector2(.67f, 0), Vector2.one, Vector2.zero, Vector2.zero),
                "Repeat trial", () => { session.RepeatTrial(); ResetWorkbookPage(); }, out _);
        }

        void BuildMeasurementEntries(RectTransform parent)
        {
            Label(Box("Instrument heading", parent, new Vector2(0, .88f), new Vector2(.43f, 1), Vector2.zero, Vector2.zero), "Instrument and units", 22, TextAnchor.MiddleLeft);
            Label(Box("Capture heading", parent, new Vector2(.43f, .88f), new Vector2(.77f, 1), Vector2.zero, Vector2.zero), "Recorded value", 22, TextAnchor.MiddleLeft);
            Label(Box("Entry heading", parent, new Vector2(.77f, .88f), Vector2.one, Vector2.zero, Vector2.zero), "Your entry", 22);
            for (int i = 0; i < EntryLabels.Length; i++)
            {
                int index = i;
                float y = .74f - .145f * i;
                var row = Box("Measurement " + i, parent, new Vector2(0, y), new Vector2(1, y + .13f), Vector2.zero, Vector2.zero);
                ImageFor(row, InkPanel, false);
                Label(Box("Instrument", row, Vector2.zero, new Vector2(.43f, 1), new Vector2(12, 0), Vector2.zero), EntryLabels[i], 22, TextAnchor.MiddleLeft);
                workbookCaptures[i] = Label(Box("Original capture", row, new Vector2(.43f, 0), new Vector2(.76f, 1), Vector2.zero, Vector2.zero), "", 20, TextAnchor.MiddleLeft);
                workbookEntries[i] = Field(Box("Entry " + i, row, new Vector2(.77f, 0), Vector2.one, new Vector2(0, 3), new Vector2(-5, -3)), false,
                    value => { session.Trial.draft.readings[index] = value; session.MarkDraftChanged(); });
                RegisterCalculatorTarget(workbookEntries[i], EntryLabels[i]);
            }
        }

        void BuildCalculation(RectTransform parent)
        {
            Label(Box("Formula", parent, new Vector2(0, .87f), Vector2.one, Vector2.zero, Vector2.zero),
                "Heat gained by water = heat lost by bob\nC = (water mass × 4.2 × water rise) ÷ (bob mass × bob drop)", 21, TextAnchor.MiddleLeft);
            string[] fields = { "Water mass  (m₂ − m₁)  ·  g", "Water temperature rise  (θ − θ₁)  ·  K",
                "Bob temperature drop  (θ₂ − θ)  ·  K", "Water mass × 4.2 × water rise  ·  J",
                "Bob mass × bob drop  ·  g·K", "Specific heat  ·  J/(g·K)", "Specific heat × 1000  ·  J/(kg·K)" };
            for (int i = 0; i < fields.Length; i++)
            {
                int index = i;
                float y = .76f - i * .104f;
                var row = Box("Calculation " + i, parent, new Vector2(0, y), new Vector2(1, y + .097f), Vector2.zero, Vector2.zero);
                ImageFor(row, InkPanel, false);
                Label(Box("Label", row, Vector2.zero, new Vector2(.68f, 1), new Vector2(9, 0), Vector2.zero), fields[i], 20, TextAnchor.MiddleLeft);
                calculationEntries[i] = Field(Box("Calculation entry " + i, row, new Vector2(.68f, 0), new Vector2(.92f, 1),
                    new Vector2(0, 2), new Vector2(0, -2)), false, value =>
                {
                    session.Trial.draft.calculations[index] = value;
                    calculationChecks[index].text = "";
                    session.MarkDraftChanged();
                });
                calculationChecks[i] = Label(Box("Check", row, new Vector2(.92f, 0), Vector2.one, Vector2.zero, Vector2.zero), "", 19);
                RegisterCalculatorTarget(calculationEntries[i], fields[i]);
            }
            ButtonIn(Box("Check calculations", parent, Vector2.zero, new Vector2(.32f, .10f), Vector2.zero, Vector2.zero),
                "Check", () => CheckCalculations(), out _);
            calculationOutput = Label(Box("Calculation output", parent, new Vector2(.34f, 0), new Vector2(1, .10f), Vector2.zero, Vector2.zero),
                "", 18, TextAnchor.MiddleLeft);
        }

        void BuildWorkbookCalculator()
        {
            (borrowedCalculator, calculatorResult, calculatorOperator, calculatorTargetLabel, assignCalculator) =
                PracticalUiStyle.Calculator(calculatorSpace, AssignCalculator, RefreshCalculatorTarget, () => session.PlaySound("Click"));
        }

        void RegisterCalculatorTarget(InputField field, string title)
        {
            var trigger = field.gameObject.AddComponent<EventTrigger>();
            foreach (var eventId in new[] { EventTriggerType.PointerDown, EventTriggerType.Select })
            {
                var entry = new EventTrigger.Entry { eventID = eventId };
                entry.callback.AddListener(_ =>
                {
                    ClearCalculatorTarget();
                    calculatorTarget = field;
                    calculatorTargetName = title;
                    RefreshCalculatorTarget();
                });
                trigger.triggers.Add(entry);
            }
        }

        void ClearCalculatorTarget()
        {
            if (calculatorTarget != null) calculatorTarget.targetGraphic.color = EntryBackground;
            calculatorTarget = null;
            calculatorTargetName = null;
        }

        void RefreshCalculatorTarget()
        {
            if (calculatorTarget != null) calculatorTarget.targetGraphic.color = EntryHighlight;
            calculatorTargetLabel.text = calculatorTarget == null ? "Tap a number field to select it" : "Selected · " + calculatorTargetName;
            assignCalculator.interactable = calculatorTarget != null && calculatorOperator.text.Length == 0 &&
                Physics81Calculation.TryRead(calculatorResult.text, out _);
        }

        void AssignCalculator()
        {
            if (!assignCalculator.interactable || !calculatorTarget.gameObject.activeInHierarchy) return;
            string value = borrowedCalculator.GetOutputValue();
            calculatorTarget.text = value;
            calculatorTarget.ForceLabelUpdate();
            pageFooter.text = $"Entered {value} in {calculatorTargetName}.";
        }

        bool CheckCalculations(bool submitting = false)
        {
            var draft = session.Trial.draft;
            if (!Physics81Calculation.TryCalculate(draft.readings, out var calc, out string error))
            {
                calculationOutput.text = error;
                return false;
            }
            Physics81Calculation.TryRead(draft.readings[2], out double bobGrams);
            double[] expected = { calc.WaterGrams, calc.WaterRise, calc.BobDrop, calc.WaterGrams * 4.2 * calc.WaterRise,
                bobGrams * calc.BobDrop, calc.JoulesPerGramKelvin, calc.JoulesPerKilogramKelvin };
            int matches = 0, entered = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                bool filled = Physics81Calculation.TryRead(draft.calculations[i], out double value);
                bool correct = filled && Math.Abs(value - expected[i]) <= Math.Max(i == 5 ? .001 : .05, Math.Abs(expected[i]) * .002);
                if (filled) entered++;
                if (correct) matches++;
                calculationChecks[i].text = !filled ? "" : correct ? "✓" : "×";
                calculationChecks[i].color = correct ? new Color(.38f, 1f, .69f) : new Color(1f, .52f, .44f);
            }
            if (submitting && !session.Data.Guided) return entered == expected.Length;
            calculationOutput.text = matches == expected.Length ? "All calculations match." : $"{matches}/{expected.Length} match. Check the marked entries.";
            if (!submitting) session.PlaySound(matches == expected.Length ? "Correct" : "Wrong");
            return matches == expected.Length;
        }

        void BuildWriteUp(RectTransform parent)
        {
            for (int i = 0; i < WriteUpTabs.Length; i++)
            {
                int tab = i;
                var slot = Box(WriteUpTabs[i], parent, new Vector2(i / 3f, .86f), new Vector2((i + 1) / 3f, 1),
                    Vector2.zero, new Vector2(-6, 0));
                writeUpTabFaces[i] = (Image)ButtonIn(slot, WriteUpTabs[i], () =>
                {
                    SelectWriteUpTab(tab);
                    RefreshWorkbookAdvice();
                }, out _).targetGraphic;
                writeUpSections[i] = Box(WriteUpTabs[i] + " entries", parent, Vector2.zero, new Vector2(1, .83f),
                    Vector2.zero, Vector2.zero);
            }
            aimInput = WritingField(writeUpSections[0], "Aim", 1, 2, value => session.Trial.draft.aim = value);
            apparatusInput = WritingField(writeUpSections[0], "Apparatus and materials", 0, 2, value => session.Trial.draft.apparatus = value);
            methodInput = WritingField(writeUpSections[1], "Method", 1, 2, value => session.Trial.draft.method = value);
            variablesInput = WritingField(writeUpSections[1], "What stays the same, what changes, and why", 0, 2, value => session.Trial.draft.variables = value);
            observationsInput = WritingField(writeUpSections[2], "Observations", 2, 3, value => session.Trial.draft.observations = value);
            conclusionInput = WritingField(writeUpSections[2], "Conclusion", 1, 3, value => session.Trial.draft.conclusion = value);
            reflectionInput = WritingField(writeUpSections[2], "Heat-loss reflection (optional)", 0, 3, value => session.Trial.draft.reflection = value);
        }

        void SelectWriteUpTab(int tab)
        {
            writeUpTab = tab;
            for (int i = 0; i < writeUpSections.Length; i++)
            {
                writeUpSections[i].gameObject.SetActive(i == tab);
                writeUpTabFaces[i].color = i == tab ? Selected : Teal;
            }
        }

        InputField WritingField(RectTransform parent, string title, int row, int rows, Action<string> edit)
        {
            float bottom = (float)row / rows;
            float labelStart = (row + .72f) / rows;
            Label(Box(title, parent, new Vector2(0, labelStart), new Vector2(1, (float)(row + 1) / rows),
                Vector2.zero, Vector2.zero), title, 22, TextAnchor.MiddleLeft);
            return Field(Box(title + " entry", parent, new Vector2(0, bottom + .01f), new Vector2(1, labelStart),
                Vector2.zero, Vector2.zero),
                true, value => { edit(value); session.MarkDraftChanged(); });
        }

        InputField Field(RectTransform rect, bool multiline, Action<string> edit)
        {
            var field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = ImageFor(rect, EntryBackground, true);
            var inset = new Vector2(9, multiline ? 4 : 0);
            var align = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            var text = Label(Box("Text", rect, Vector2.zero, Vector2.one, inset, -inset), "", multiline ? 22 : 20, align);
            text.color = Paper;
            text.supportRichText = false;
            if (!multiline) text.verticalOverflow = VerticalWrapMode.Overflow;
            field.textComponent = text;
            field.contentType = multiline ? InputField.ContentType.Standard : InputField.ContentType.DecimalNumber;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.characterLimit = multiline ? 1200 : 18;
            field.onValueChanged.AddListener(value => edit(value));
            return field;
        }

        void RefreshWorkbookInputs()
        {
            var draft = session.Trial.draft;
            var values = Physics81Calculation.Captured(session.Data);
            for (int i = 0; i < workbookEntries.Length; i++)
            {
                workbookEntries[i].SetTextWithoutNotify(draft.readings[i] ?? "");
                workbookCaptures[i].text = i < 3 ? values[i].HasValue ? $"{values[i]:F2} g" : "Awaiting record" : Mark(i switch
                { 3 => session.Data.InitialMark, 4 => session.Data.BathMark, _ => session.Data.MixtureMark });
            }
            for (int i = 0; i < calculationEntries.Length; i++)
            {
                calculationEntries[i].SetTextWithoutNotify(draft.calculations[i] ?? "");
                calculationChecks[i].text = "";
            }
            aimInput.SetTextWithoutNotify(draft.aim ?? "");
            apparatusInput.SetTextWithoutNotify(draft.apparatus ?? "");
            methodInput.SetTextWithoutNotify(draft.method ?? "");
            variablesInput.SetTextWithoutNotify(draft.variables ?? "");
            observationsInput.SetTextWithoutNotify(draft.observations ?? "");
            conclusionInput.SetTextWithoutNotify(draft.conclusion ?? "");
            reflectionInput.SetTextWithoutNotify(draft.reflection ?? "");
        }

        void SelectWorkbookTab(int tab)
        {
            workbookTab = tab;
            ClearCalculatorTarget();
            if (tab == 1) calculatorOpen = true;
            for (int i = 0; i < workbookTabFaces.Length; i++) workbookTabFaces[i].color = i == tab ? Selected : Teal;
            for (int i = 0; i < workbookSections.Length; i++) workbookSections[i].gameObject.SetActive(i == tab);
            pageScroll.gameObject.SetActive(tab == 3);
            workbookActions.gameObject.SetActive(session.Trial.finished && tab == 3);
            submitWorkbook.interactable = session.Trial.evidence?.Complete == true && !session.Trial.draft.submitted;
            pageFooter.text = session.SaveFailed ? "Local save failed. Keep the practical open and try again." :
                session.Trial.draft.submitted ? "Workbook submitted and saved on this device." :
                tab == 0 ? "Lab m:ss shows when each temperature was observed on the simulation clock." :
                "Workbook draft saves on this device. Original captures stay fixed.";
            if (tab == 1) calculationOutput.text = "Tap a number field, calculate, then tap Assign result.";
            if (tab == 2) SelectWriteUpTab(writeUpTab);
            if (tab == 3) SetBody(FeedbackText(), .663f, .68f);
            RefreshWorkbookAdvice();
        }

        string CalculationText()
        {
            if (!Physics81Calculation.TryCalculate(session.Trial.draft.readings, out var calc, out string error)) return error;
            return $"Water mass (m₂ − m₁)    {calc.WaterGrams:F1} g\n\nWater rise (θ − θ₁)    {calc.WaterRise:F1} K\n\nBob drop (θ₂ − θ)    {calc.BobDrop:F1} K\n\n" +
                $"Specific heat    {calc.JoulesPerGramKelvin:F3} J/(g·K)\n                         {calc.JoulesPerKilogramKelvin:F0} J/(kg·K)";
        }

        string FeedbackText()
        {
            if (!session.Trial.finished) return "Finish the practical to review your technique and submit the workbook.";
            var draft = session.Trial.draft;
            int matched = Physics81Calculation.Matched(session.Data, draft);
            int total = Physics81Calculation.Captured(session.Data).Count(value => value.HasValue);
            string feedback = $"Reading transcription  {matched} / {total} saved readings matched\n\n" + Physics81Calculation.Technique(session.Trial);
            if (draft.submitted)
                feedback += "\n\n" + CalculationText() + $"\n\nModel brass reference  {Physics81ThermalModel.BrassSpecificHeat:F0} J/(kg·K)";
            return feedback;
        }

        void SubmitWorkbook()
        {
            if (!session.Trial.finished || session.Trial.evidence?.Complete != true) return;
            var draft = session.Trial.draft;
            if (!Physics81Calculation.TryCalculate(draft.readings, out _, out string error)) { pageFooter.text = error; return; }
            if (!CheckCalculations(submitting: true))
            {
                SelectWorkbookTab(1);
                CheckCalculations(submitting: true);
                pageFooter.text = "Complete the seven calculation entries before submitting.";
                return;
            }
            if (string.IsNullOrWhiteSpace(draft.observations) || string.IsNullOrWhiteSpace(draft.conclusion))
            {
                SelectWorkbookTab(2);
                SelectWriteUpTab(2);
                RefreshWorkbookAdvice();
                pageFooter.text = "Write your observations and a conclusion before submitting.";
                return;
            }
            draft.submitted = true;
            session.PlaySound("Correct");
            session.SaveProgress();
            SelectWorkbookTab(3);
        }

        void RefreshWorkbookAdvice()
        {
            bool canUseCalculator = workbookTab is 0 or 1;
            bool showCalculator = canUseCalculator && calculatorOpen;
            calculatorToggle.transform.parent.gameObject.SetActive(canUseCalculator);
            calculatorSpace.gameObject.SetActive(showCalculator);
            workbookAdvice.gameObject.SetActive(!showCalculator);
            calculatorToggle.text = calculatorOpen ? "Close calculator" : "Calculator";
            workbookAdvice.text = (workbookTab, session.Data.Guided) switch
            {
                (0, _) => "One trial uses three mass readings and three temperatures. Lab times are simulated minutes and seconds. The mixture time marks its highest observed reading.",
                (1, true) => "Worked example\n\nWith 40 g of water and a 50 g bob, a rise of 7 K and a bob drop of 63 K gives:\n\n40 × 4.2 × 7 ÷ (50 × 63)\n= 0.373 J/(g·K)\n= 373 J/(kg·K)\n\nUse your own readings on the left.",
                (2, _) when writeUpTab == 0 => "State the quantity you set out to find. List the equipment and materials you actually used.",
                (2, _) when writeUpTab == 1 => "Describe how you measured, heated, transferred and read the mixture. Which readings can vary? What would you keep the same across repeats? Explain what changes if a control varies. See Aim and method → Repeat trials for the reference.",
                (2, true) => "Describe the rise and cooling after adding the bob. Give your result with units and explain one source of error. Open Feedback to submit.",
                (2, false) => "Write your observations and conclusion in your own words. Open Feedback to submit.",
                (3, _) => "Reading accuracy compares what you entered at the instrument with its saved reading. Technique checks your actions.\n\nRetry heating restores the prepared setup and masses. Repeat trial starts a fresh setup.",
                _ => "Use your recorded readings and observations. You can open the calculator without covering your work."
            };
            bool useKeypad = showCalculator && Application.isMobilePlatform;
            foreach (var field in workbookEntries) field.readOnly = useKeypad;
            foreach (var field in calculationEntries) field.readOnly = useKeypad;
            RefreshCalculatorTarget();
        }

        void ResetWorkbookPage()
        {
            workbookTab = 0;
            writeUpTab = 0;
            calculatorOpen = false;
            ClosePage();
        }
    }
}
