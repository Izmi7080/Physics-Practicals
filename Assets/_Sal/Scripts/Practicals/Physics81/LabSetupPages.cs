using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics81
{
    public sealed partial class LabSetupHud
    {
        enum Page { Welcome, Study, Tasks, Readings, Workbook }
        static readonly Color Paper = new(.033f, .10f, .13f), InkPanel = new(.070f, .185f, .218f);
        static readonly string[] StudyTabs = { "Overview", "Procedure", "Controls", "Precautions", "Repeat trials" };
        RectTransform document, welcome, diagram, readings, tabs;
        ScrollRect pageScroll;
        Button closeDocument, allPracticals;
        Button resumeTrial, openWorkbook;
        Text variationLabel;
        bool trialVariation, studyFromWorkbook;
        Text pageTitle, pageFooter, pageBody;
        Image[] tabFaces;
        Text[] recordValues;
        Page activePage;
        int studySection;
        float bodyWidth;
        bool PageVisible => document.gameObject.activeSelf;

        void BuildPages()
        {
            document = Fill("Physics 8.1 pages", safe);
            var backdrop = document.gameObject.AddComponent<Image>();
            backdrop.color = Paper;
            Label(Box("Eyebrow", document, new Vector2(.055f, .92f), new Vector2(.76f, .99f), Vector2.zero, Vector2.zero), "GRADE 9  ·  PHYSICS 8.1", 22, TextAnchor.MiddleLeft);
            pageTitle = Label(Box("Page title", document, new Vector2(.055f, .82f), new Vector2(.79f, .93f), Vector2.zero, Vector2.zero), "", 38, TextAnchor.MiddleLeft);
            closeDocument = ButtonAt("Close", document, Vector2.one, new Vector2(-178, -120), new Vector2(-30, -58), ClosePage, out _);
            allPracticals = ButtonAt("All practicals", document, Vector2.one, new Vector2(-236, -54),
                new Vector2(-30, -7), ReturnToLauncher, out _);
            pageFooter = Label(Box("Page footer", document, new Vector2(.055f, .034f), new Vector2(.945f, .118f), Vector2.zero, Vector2.zero), "", 21, TextAnchor.MiddleLeft);

            welcome = Box("Welcome choices", document, new Vector2(.055f, .15f), new Vector2(.945f, .79f), Vector2.zero, Vector2.zero);
            Label(Box("Aim and stations", welcome, new Vector2(0, .48f), Vector2.one, Vector2.zero, Vector2.zero),
                "Find the specific heat capacity of a brass bob by mixing it with cooler water.\n\nStudy the method, then begin at the digital balance. You will use the main tap to fill the cylinder and heating beaker.", 30, TextAnchor.UpperLeft);
            ButtonIn(Box("Study method", welcome, new Vector2(0, .24f), new Vector2(.31f, .43f), Vector2.zero, Vector2.zero), "Study the method", () => OpenPage(Page.Study), out _);
            ButtonIn(Box("Start Guided", welcome, new Vector2(0, 0), new Vector2(.31f, .18f), Vector2.zero, Vector2.zero), "Begin Guided", () => BeginMode(true), out _);
            ButtonIn(Box("Start Assessment", welcome, new Vector2(.34f, 0), new Vector2(.65f, .18f), Vector2.zero, Vector2.zero), "Begin Assessment", () => BeginMode(false), out _);
            resumeTrial = ButtonIn(Box("Resume saved trial", welcome, new Vector2(.68f, 0), new Vector2(1, .18f), Vector2.zero, Vector2.zero),
                "Resume saved trial", ResumeSavedTrial, out _);
            ButtonIn(Box("Trial variation", welcome, new Vector2(.34f, .24f), new Vector2(.65f, .43f), Vector2.zero, Vector2.zero),
                "", ToggleTrialVariation, out variationLabel);
            variationLabel.text = "Extra assessment variation: Off";

            tabs = Box("Study sections", document, new Vector2(.055f, .725f), new Vector2(.945f, .805f), Vector2.zero, Vector2.zero);
            tabFaces = new Image[StudyTabs.Length];
            for (int i = 0; i < StudyTabs.Length; i++)
            {
                int section = i;
                var tab = Box(StudyTabs[i], tabs, new Vector2((float)i / StudyTabs.Length, 0),
                    new Vector2((float)(i + 1) / StudyTabs.Length, 1), Vector2.zero, new Vector2(-10, 0));
                tabFaces[i] = (Image)ButtonIn(tab, StudyTabs[i], () => SelectStudy(section), out _).targetGraphic;
            }

            var scrollBox = Box("Document scroll", document, new Vector2(.055f, .14f), new Vector2(.945f, .795f), Vector2.zero, Vector2.zero);
            pageScroll = scrollBox.gameObject.AddComponent<ScrollRect>();
            var viewport = Box("Document viewport", scrollBox, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-32, 0));
            viewport.gameObject.AddComponent<Image>().color = Paper;
            viewport.gameObject.AddComponent<RectMask2D>();
            pageBody = Label(Box("Document text", viewport, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero), "", 27, TextAnchor.UpperLeft);
            pageBody.rectTransform.pivot = Vector2.up;
            pageScroll.content = pageBody.rectTransform;
            pageScroll.viewport = viewport;
            pageScroll.horizontal = false;
            pageScroll.movementType = ScrollRect.MovementType.Clamped;
            pageScroll.scrollSensitivity = 58f;
            var scrollTrack = Box("Scroll track", scrollBox, Vector2.right, Vector2.one, new Vector2(-22, 0), Vector2.zero);
            ImageFor(scrollTrack, InkPanel, true);
            var scrollHandle = Fill("Scroll thumb", scrollTrack);
            var bar = scrollTrack.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = scrollHandle;
            bar.targetGraphic = ImageFor(scrollHandle, Selected, true);
            bar.direction = Scrollbar.Direction.BottomToTop;
            pageScroll.verticalScrollbar = bar;
            pageScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            diagram = Box("Lab setup diagram", document, new Vector2(.625f, .22f), new Vector2(.945f, .68f), Vector2.zero, Vector2.zero);
            BuildSetupDiagram();
            readings = Box("Readings side by side", document, new Vector2(.055f, .18f), new Vector2(.945f, .795f), Vector2.zero, Vector2.zero);
            recordValues = new Text[6];
            BuildReadingColumn(0, "BALANCE  ·  MASS", new[] { "m₁  Empty cup", "m₂  Cup with water", "M₂  Brass bob" });
            BuildReadingColumn(1, "THERMOMETERS  ·  TEMPERATURE", new[] { "θ₁  Initial cup water", "θ₂  Hot water bath", "θ  Mixture" });
            openWorkbook = ButtonAt("Open workbook", document, Vector2.one, new Vector2(-380, -120), new Vector2(-190, -58),
                () => OpenPage(Page.Workbook), out _);
            BuildWorkbook();
            OpenPage(Page.Welcome);
        }

        void BuildSetupDiagram()
        {
            ImageFor(diagram, InkPanel, false);
            Label(Box("Diagram title", diagram, new Vector2(0, .82f), Vector2.one, Vector2.zero, Vector2.zero), "Two stations in the same lab", 23);
            Label(Box("Sink and walk", diagram, new Vector2(.05f, .57f), new Vector2(.95f, .82f), Vector2.zero, Vector2.zero), "Main tap and sink\n↑  carry the cylinder or beaker  ↓", 22);
            var slab = Box("Centre slab", diagram, new Vector2(.06f, .12f), new Vector2(.94f, .55f), Vector2.zero, Vector2.zero);
            ImageFor(slab, Paper, false);
            string[] labels = { "Digital\nbalance", "Cup\n+ probe", "Bath\n+ burner" };
            for (int i = 0; i < labels.Length; i++)
            {
                var cell = Box(labels[i], slab, new Vector2(i / 3f, .18f), new Vector2((i + 1) / 3f, .88f), new Vector2(5, 0), new Vector2(-5, 0));
                ImageFor(cell, Teal, false);
                Label(Fill("Station label", cell), labels[i], 20);
            }
        }

        void BuildReadingColumn(int column, string title, string[] labels)
        {
            float left = column == 0 ? 0f : .516f;
            var card = Box(title, readings, new Vector2(left, 0), new Vector2(left + .484f, 1), Vector2.zero, Vector2.zero);
            ImageFor(card, InkPanel, false);
            Label(Box("Record group", card, new Vector2(.05f, .82f), new Vector2(.95f, 1), Vector2.zero, Vector2.zero), title, 22, TextAnchor.MiddleLeft);
            for (int row = 0; row < labels.Length; row++)
            {
                float bottom = .08f + (2 - row) * .24f;
                var item = Box("Record row", card, new Vector2(.045f, bottom), new Vector2(.955f, bottom + .20f), Vector2.zero, Vector2.zero);
                ImageFor(item, Paper, false);
                Label(Box("Instrument", item, Vector2.zero, new Vector2(.49f, 1), new Vector2(12, 0), Vector2.zero), labels[row], 23, TextAnchor.MiddleLeft);
                recordValues[3 * column + row] = Label(Box("Saved value", item, new Vector2(.51f, 0), Vector2.one, Vector2.zero, new Vector2(-12, 0)), "", 23, TextAnchor.MiddleRight);
            }
        }

        void BeginMode(bool guided)
        {
            session.Begin(guided, trialVariation);
            ClosePage();
        }

        void ToggleTrialVariation()
        {
            trialVariation = !trialVariation;
            variationLabel.text = trialVariation ? "Extra assessment variation: On" : "Extra assessment variation: Off";
        }

        void RefreshWelcome() => resumeTrial.gameObject.SetActive(session.SavedTrial != null);

        void ResumeSavedTrial()
        {
            session.ResumeTrial();
            if (!session.Started) return;
            if (session.Trial.finished && !session.Data.LowWaterStop)
            {
                lastStep = SetupStep.Review;
                calculatorOpen = true;
                OpenPage(Page.Workbook);
            }
            else ClosePage();
        }

        void OpenPage(Page page)
        {
            activePage = page;
            session.SetPageOpen(true);
            document.gameObject.SetActive(true);
            closeDocument.gameObject.SetActive(page != Page.Welcome);
            allPracticals.gameObject.SetActive(PracticalLauncher.Available);
            openWorkbook.gameObject.SetActive(page == Page.Readings);
            aimAndMethod.gameObject.SetActive(page == Page.Workbook);
            welcome.gameObject.SetActive(page == Page.Welcome);
            tabs.gameObject.SetActive(page == Page.Study);
            readings.gameObject.SetActive(page == Page.Readings);
            workbook.gameObject.SetActive(page == Page.Workbook);
            pageScroll.gameObject.SetActive(page is Page.Study or Page.Tasks);
            diagram.gameObject.SetActive(false);
            pageFooter.text = session.Started ? "Experiment paused while this page is open." : "Study the method or choose a mode to begin at the centre slab.";
            switch (page)
            {
                case Page.Welcome: pageTitle.text = "Specific heat of a solid"; RefreshWelcome(); break;
                case Page.Study:
                    pageTitle.text = "Study the experiment";
                    bool guided = !session.Started || session.Data.Guided || studyFromWorkbook && session.Trial.finished;
                    tabFaces[0].gameObject.SetActive(guided);
                    tabFaces[1].gameObject.SetActive(guided);
                    tabFaces[4].gameObject.SetActive(guided);
                    SelectStudy(guided ? studySection : 2);
                    break;
                case Page.Workbook:
                    pageTitle.text = "Practical workbook";
                    RefreshWorkbookInputs();
                    SelectWorkbookTab(workbookTab);
                    break;
                case Page.Tasks:
                    pageTitle.text = "Procedure and progress";
                    SetBody(Physics81ProcedureText.Procedure(session.Data.Guided, session.Step), .945f, .795f);
                    break;
                case Page.Readings:
                    pageTitle.text = "Recorded observations";
                    ShowRecordedValues();
                    pageFooter.text = session.Data.LowWaterStop ? "The lamp switched off when the bath ran low. These are the readings collected so far." :
                        "Lab times are minutes:seconds on the simulation clock. Mixture time marks its highest observed reading.";
                    break;
            }
        }

        void ReturnToLauncher()
        {
            if (!PracticalLauncher.Available) return;
            if (session.Started) session.SaveProgress();
            if (session.Started && session.SaveFailed)
            {
                if (PageVisible) pageFooter.text = "Could not save progress. Try All practicals again.";
                return;
            }
            PracticalLauncher.ReturnToMenu();
        }

        void ClosePage()
        {
            if (activePage == Page.Study && studyFromWorkbook)
            {
                studyFromWorkbook = false;
                OpenPage(Page.Workbook);
                return;
            }
            if (!session.Started) { if (activePage != Page.Welcome) OpenPage(Page.Welcome); return; }
            if (activePage == Page.Workbook) session.SaveProgress();
            document.gameObject.SetActive(false);
            session.SetPageOpen(false);
        }

        void SelectStudy(int section)
        {
            studySection = section;
            for (int i = 0; i < tabFaces.Length; i++) tabFaces[i].color = section == i ? Selected : Teal;
            diagram.gameObject.SetActive(section == 0);
            string copy = section switch
            {
                0 => Physics81ProcedureText.Overview,
                1 => Physics81ProcedureText.Procedure(true),
                2 => Physics81ProcedureText.Controls,
                3 => Physics81ProcedureText.Precautions,
                _ => Physics81ProcedureText.RepeatTrials
            };
            SetBody(copy, section == 0 ? .598f : .945f, .69f);
        }

        void SetBody(string copy, float right, float top)
        {
            ((RectTransform)pageScroll.transform).anchorMax = new Vector2(right, top);
            pageBody.text = copy;
            ResizePageBody();
            pageScroll.StopMovement();
            pageScroll.verticalNormalizedPosition = 1f;
            pageBody.rectTransform.anchoredPosition = Vector2.zero;
        }

        void ResizePageBody()
        {
            Canvas.ForceUpdateCanvases();
            var body = pageBody.rectTransform;
            bodyWidth = body.rect.width;
            body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(pageScroll.viewport.rect.height, pageBody.preferredHeight + 28f));
        }

        void ShowRecordedValues()
        {
            var data = session.Data;
            recordValues[0].text = Mass(data.EmptyReading);
            recordValues[1].text = Mass(data.FullReading);
            recordValues[2].text = Mass(data.BobReading);
            recordValues[3].text = Mark(data.InitialMark);
            recordValues[4].text = Mark(data.BathMark);
            recordValues[5].text = Mark(data.MixtureMark);
        }

        static string Mass(float grams) => grams > 0f ? $"{grams:F2} g" : "Awaiting record";
        static string Mark(TemperatureCapture? capture)
        {
            if (!capture.HasValue) return "Awaiting record";
            var record = capture.Value;
            int seconds = (int)record.LabSeconds;
            return $"{record.StudentEntry ?? record.Celsius:F1} °C   ·   Lab {seconds / 60}:{seconds % 60:D2}";
        }
    }
}
