using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: UI (the on-screen layout of the pendulum practical).
     * Purpose: Connects the pendulum controller to the SHARED practical shell (PracticalHUDBase_Azm,
     * TASK_hci_and_juice Stage 4). The base does everything common (instruction card and progress,
     * layout regions and camera framing, message routing, hints, briefing, pause with settings,
     * back button, Learn/Perform). This class adds what only the pendulum has:
     *   - the CONTEXT AREA in the left column: Confirm length / release hint / the big round TAP
     *     button with stopwatch and "Restart run" / Next length + Finish & Analyse / See my result;
     *     it sits to the right of the view buttons, and the scene window ends above it, so the
     *     swinging bob is never behind the tap button;
     *   - the Timing camera view that follows the bob when the length changes;
     *   - the Trial chip ("Trial 2 of 5") and the g chip;
     *   - view buttons that shrink to icons while the drawer is open (four views + the drawer);
     *   - LEARN MODE (TASK_menus_and_pendulum Stage 4): the oscillation demonstration before the
     *     first timing run (and on demand), the prediction prompt from the second length, the
     *     worked calculation revealed line by line, the reflection on the results panel;
     *   - the pause menu's "Restart this length";
     *   - the tap button's feel: ripple, count pop and a tick vibration per tap, a burst on the 20th.
     * Talks to: PendulumController_Azm, PendulumRigView_Azm, DrawerContentUI_Azm, PendulumCalcUI_Azm,
     * OscillationDemoUI_Azm, PredictionUI_Azm, ResultsPanelUI_Azm, the shell.
     */
    public class PendulumHUD_Azm : PracticalHUDBase_Azm
    {
        public const string BriefingId = "pendulum.briefing";
        public const string DemoTutorialId = "pendulum.demo";
        private const int TimingViewIndex = (int)CameraViewId_Azm.Timing;

        public PendulumController_Azm controller;
        public PendulumRigView_Azm rigView;
        public ResultsPanelUI_Azm results;
        public DrawerContentUI_Azm drawerContent;
        public PendulumCalcUI_Azm calcUI;
        public OscillationDemoUI_Azm demo;
        public PredictionUI_Azm prediction;
        public Button restartLengthButton;

        [Header("Top")]
        public TMP_Text trialChipText;

        [Header("View buttons collapse to icons while the drawer is open")]
        public RectTransform viewPanel;
        public LayoutElement[] viewButtonLayouts;
        public TMP_Text[] viewLabels;
        public float viewButtonWidthFull = 150f;
        public float viewButtonWidthCompact = 100f;

        [Header("Context area (left column, right of the view buttons)")]
        public RectTransform contextRegion;
        public GameObject introGroup;
        public Button continueButton;
        public GameObject lengthGroup;
        public Button confirmLengthButton;
        public TMP_Text lengthHintText;
        public GameObject releaseGroup;
        public TMP_Text releaseHintText;
        public GameObject timingGroup;
        public Button tapButton;
        public TMP_Text tapCountText;
        public TMP_Text tapCaptionText;
        public TMP_Text stopwatchText;
        public Button restartRunButton;
        public Button demoReplayButton;
        public Image tapRipple;
        public GameObject recordGroup;
        public Button nextLengthButton;
        public Button finishButton;
        public GameObject analyseGroup;
        public Button showResultsButton;

        [Header("Space the context area needs above the bottom of the left column (scene window ends above)")]
        public float timingReserve = 480f;
        public float lengthReserve = 300f;
        public float recordReserve = 200f;

        private Prediction_Azm? pendingPrediction;
        private int askedForRows = -1;

        // ----- what the shell needs -----
        protected override string BriefingTutorialId => BriefingId;
        protected override string ExitQuestion => PendulumTexts_Azm.ExitQuestion;
        protected override string RestartQuestion => PendulumTexts_Azm.RestartQuestion;
        protected override int StepIndex => controller.StepIndex;
        protected override int StepCount => controller.StepCount;
        protected override string StepInstruction => controller.CurrentStep.instruction;
        protected override string StepWhy => controller.CurrentStep.why;
        protected override int CalcLineCount => 0;          // the Calc tab reveals its own lines
        protected override int CalcLinesRevealed => 0;
        protected override bool ResultsVisible => results.IsVisible;
        protected override float[] LeastCountChoicesMm => new float[0];
        protected override string LeastCountChoiceLabel(float mm) => "";
        protected override string LeastCountPromptText => "";
        protected override int CalcTab => 2;                // Data, Graph, Calc, Guide
        protected override int GuideTab => 3;

        protected override StepScreen_Azm CurrentScreen
        {
            get
            {
                var def = controller.CurrentStep;
                bool caliper = (def.controls & ContextControls_Azm.Caliper) != 0;
                return new StepScreen_Azm
                {
                    magnifier = caliper,
                    keypad = caliper,
                    results = def.id == StepId_Azm.Results,
                    view = (int)def.view,
                    guideProcedureIndex = def.guideProcedureIndex
                };
            }
        }

        protected override void SetPaused(bool paused) => controller.SetPaused(paused);
        protected override void Retry() => ResultsPanelUI_Azm.Retry();
        protected override void ShowResultsPanel(bool visible) => results.SetVisible(visible);
        protected override void AnswerLeastCount(int choice) { }
        protected override void ContinueAfterReading() => controller.ConfirmDiameter();
        protected override void RevealNextCalcLine() { }
        protected override void RevealAllCalcLines() { }
        protected override void ShowResults() => controller.ShowResults();

        // ----- lifecycle -----

        protected override void Awake()
        {
            base.Awake();
            continueButton.onClick.AddListener(controller.ConfirmIntro);
            confirmLengthButton.onClick.AddListener(controller.ConfirmLength);
            tapButton.onClick.AddListener(controller.Tap);
            restartRunButton.onClick.AddListener(controller.RestartRun);
            nextLengthButton.onClick.AddListener(controller.NextLength);
            finishButton.onClick.AddListener(controller.FinishAndAnalyse);
            showResultsButton.onClick.AddListener(controller.ShowResults);
            restartLengthButton.onClick.AddListener(() =>
            {
                controller.SetPaused(false);
                controller.RestartLength();
            });
            demoReplayButton.onClick.AddListener(() => demo.Show());
            if (guideView.replayDemoButton != null) guideView.replayDemoButton.onClick.AddListener(() => demo.Show());
            prediction.Chosen += p => pendingPrediction = p;

            lengthHintText.text = PendulumTexts_Azm.LengthHint;
            releaseHintText.text = PendulumTexts_Azm.ReleaseHint;
            demoReplayButton.gameObject.SetActive(LearnMode);
            calcUI.RevealStepByStep = LearnMode;
            results.ShowReflection = LearnMode;
            // The tap button keeps its own feel (ripple + tick below) instead of the generic click.
            var feel = tapButton.GetComponent<ButtonFeel_Azm>();
            if (feel != null) { feel.clickFeedback = false; feel.pressScale = 0.96f; }
        }

        protected override void Start()
        {
            base.Start();
            UpdateTimingView();
            if (tapRipple != null) tapRipple.gameObject.SetActive(false);
        }

        protected override void Connect(bool connect)
        {
            if (controller == null) return;
            if (connect)
            {
                controller.StepChanged += OnStepChanged;
                controller.Message += OnControllerMessage;
                controller.Hint += OnHint;
                controller.AnalysisChanged += RefreshChip;
                controller.PausedChanged += OnPausedChanged;
                controller.TapCounted += OnTapCounted;
                controller.RunStarted += OnRunStarted;
                controller.RunRestarted += OnRunRestarted;
                controller.RunFinished += OnRunFinished;
                controller.LengthChanged += UpdateTimingView;
                controller.RowAdded += OnRowAdded;
            }
            else
            {
                controller.StepChanged -= OnStepChanged;
                controller.Message -= OnControllerMessage;
                controller.Hint -= OnHint;
                controller.AnalysisChanged -= RefreshChip;
                controller.PausedChanged -= OnPausedChanged;
                controller.TapCounted -= OnTapCounted;
                controller.RunStarted -= OnRunStarted;
                controller.RunRestarted -= OnRunRestarted;
                controller.RunFinished -= OnRunFinished;
                controller.LengthChanged -= UpdateTimingView;
                controller.RowAdded -= OnRowAdded;
            }
        }

        protected override void Update()
        {
            base.Update();

            // View buttons shrink to icons while the drawer is open, leaving room for the context area.
            float open = drawer.OpenAmount;
            float width = Mathf.Lerp(viewButtonWidthFull, viewButtonWidthCompact, open);
            for (int i = 0; i < viewButtonLayouts.Length; i++)
            {
                viewButtonLayouts[i].preferredWidth = width;
                viewButtonLayouts[i].minWidth = width;
                viewLabels[i].alpha = 1f - open;
            }
            // The context area starts right of the view buttons (anchored, so it fits any width).
            float left = viewPanel.rect.width + theme.spaceS;
            contextRegion.offsetMin = new Vector2(left, contextRegion.offsetMin.y);

            if (timingGroup.activeSelf)
                stopwatchText.text = PendulumTexts_Azm.Stopwatch(controller.StopwatchSeconds);
            restartLengthButton.interactable = controller.CanRestartLength;
        }

        // ----- steps -----

        protected override void OnStepScreenApplied(StepScreen_Azm screen)
        {
            var def = controller.CurrentStep;
            introGroup.SetActive((def.controls & ContextControls_Azm.Continue) != 0);
            lengthGroup.SetActive((def.controls & ContextControls_Azm.ConfirmLength) != 0);
            releaseGroup.SetActive(def.id == StepId_Azm.Release);
            timingGroup.SetActive((def.controls & ContextControls_Azm.TapTimer) != 0);
            recordGroup.SetActive((def.controls & (ContextControls_Azm.NextLength | ContextControls_Azm.FinishAnalyse)) != 0);
            analyseGroup.SetActive((def.controls & ContextControls_Azm.ShowResults) != 0);

            // The scene window ends above whatever the context area shows.
            float reserve = 0f;
            if (timingGroup.activeSelf) reserve = timingReserve;
            else if (lengthGroup.activeSelf || releaseGroup.activeSelf) reserve = lengthReserve;
            else if (recordGroup.activeSelf || analyseGroup.activeSelf || introGroup.activeSelf) reserve = recordReserve;
            regions.ExtraBottomReserve = reserve;

            if (timingGroup.activeSelf) ResetTapDisplay();
            RefreshChip();

            switch (def.drawer)
            {
                case DrawerAction_Azm.OpenData: drawer.Open(0); break;
                case DrawerAction_Azm.OpenGraph: drawer.Open(1); break;
                case DrawerAction_Azm.Close: drawer.Close(); break;
            }

            if (def.id == StepId_Azm.Release) MaybeAskPrediction();
            else prediction.Hide();
            if (def.id == StepId_Azm.TimeRun1) MaybeShowDemo();
            UpdateTimingView();
        }

        // The Timing view frames the bob's whole swing (15° each side plus a margin) at its rest
        // position, so it follows the thread length.
        private void UpdateTimingView()
        {
            if (rigView == null || rigView.anchor == null) return;
            float lengthM = controller.EffectiveLengthCm / 100f;
            Vector3 rest = rigView.anchor.position - Vector3.up * lengthM;
            float swingWidth = 2f * lengthM * Mathf.Sin(controller.config.maxReleaseAngleDeg * Mathf.Deg2Rad) + 0.12f;
            cameraRig.UpdateView(TimingViewIndex, rest + Vector3.up * 0.05f, new Vector2(Mathf.Max(0.35f, swingWidth), 0.32f));
        }

        protected override void RefreshChip()
        {
            var cfg = controller.config;
            trialChipText.text = PendulumTexts_Azm.TrialChip(controller.ValidRowCount, cfg.minimumLengths);
            var a = controller.Analysis;
            SetChip(a.validCount >= 2 && a.hasFit, PendulumTexts_Azm.GChip(a.g));
            // "Finish & Analyse" appears once enough valid rows exist (and disappears if one is discarded).
            bool couldFinish = finishButton.gameObject.activeSelf;
            finishButton.gameObject.SetActive(controller.CanFinish);
            if (controller.CanFinish && !couldFinish && recordGroup.activeInHierarchy) FeedbackFX_Azm.Reveal(finishButton.transform);
        }

        // ----- Learn mode: oscillation demo and prediction -----

        private void MaybeShowDemo()
        {
            if (!LearnMode) return;
            var settings = VirtualLab.Azm.Core.SettingsManager_Azm.Instance;
            if (settings != null && settings.HasSeenTutorial(DemoTutorialId)) return;
            if (settings != null) settings.MarkTutorialSeen(DemoTutorialId);
            demo.Show();
        }

        // From the second length onward (Learn mode), ask once per new length before timing.
        private void MaybeAskPrediction()
        {
            if (!LearnMode || controller.IsSwinging) return;
            var rows = controller.Rows;
            if (controller.ValidRowCount < 1 || askedForRows == rows.Count) return;
            TrialRow_Azm last = null;
            for (int i = rows.Count - 1; i >= 0 && last == null; i--) if (!rows[i].Discarded) last = rows[i];
            if (last == null) return;
            askedForRows = rows.Count;
            pendingPrediction = null;
            prediction.Ask(controller.LengthCm > last.LengthCm);
        }

        // After the row is recorded: compare the prediction with what happened (not scored).
        private void OnRowAdded(TrialRow_Azm row)
        {
            if (pendingPrediction == null) return;
            var rows = controller.Rows;
            TrialRow_Azm previous = null;
            for (int i = rows.Count - 2; i >= 0 && previous == null; i--) if (!rows[i].Discarded) previous = rows[i];
            if (previous == null) { pendingPrediction = null; return; }

            // ASSUMPTION: periods within 2 % count as "about the same" (timing noise is ~1 %).
            float change = (row.Period - previous.Period) / previous.Period;
            var actual = change > 0.02f ? Prediction_Azm.Larger : (change < -0.02f ? Prediction_Azm.Smaller : Prediction_Azm.Same);
            string word = actual == Prediction_Azm.Larger ? "larger" : (actual == Prediction_Azm.Smaller ? "smaller" : "about the same");
            bool matched = actual == pendingPrediction.Value;
            ShowMessage(PendulumTexts_Azm.PredictionResult(matched, word, previous.Period, row.Period),
                matched ? FeedbackKind_Azm.Success : FeedbackKind_Azm.Info);
            pendingPrediction = null;
        }

        // ----- timing display and feel -----

        private void ResetTapDisplay()
        {
            tapCountText.text = $"0 / {controller.config.oscillationCount}";
            tapCaptionText.text = PendulumTexts_Azm.TapToStart;
            restartRunButton.gameObject.SetActive(false);
        }

        private void OnTapCounted(int count)
        {
            tapCountText.text = $"{count} / {controller.config.oscillationCount}";
            tapCaptionText.text = PendulumTexts_Azm.TapToCount;
            // Feel of a real stopwatch button: a ripple, the count pops, a short tick vibration.
            Motion_Azm.Punch(tapCountText.transform, 0.18f);
            Ripple(1.35f);
            FeedbackFX_Azm.Tick();
        }

        private void OnRunStarted()
        {
            tapCaptionText.text = PendulumTexts_Azm.TapToCount;
            restartRunButton.gameObject.SetActive(true);
            FeedbackFX_Azm.Reveal(restartRunButton.transform);
        }

        private void OnRunRestarted() => ResetTapDisplay();

        // The 20th tap: a bigger ring and the "correct" feel.
        private void OnRunFinished(int runIndex, float seconds)
        {
            Ripple(1.8f);
            FeedbackFX_Azm.Correct(tapButton.transform);
        }

        private void Ripple(float scale)
        {
            if (tapRipple == null) return;
            Motion_Azm.Ripple(tapRipple.transform, tapRipple.GetComponent<CanvasGroup>(), scale);
        }

        // ----- feedback -----

        private void OnControllerMessage(string text, MessageKind_Azm kind)
        {
            FeedbackKind_Azm mapped = kind == MessageKind_Azm.Error ? FeedbackKind_Azm.Error
                : kind == MessageKind_Azm.Warning ? FeedbackKind_Azm.Warning : FeedbackKind_Azm.Info;
            ShowMessage(text, mapped);
        }

        // ----- back -----

        protected override bool HandleBack()
        {
            if (demo.IsOpen) { demo.Close(); return true; }
            if (prediction.IsOpen) { prediction.Hide(); return true; }
            if (calcUI.CalculatorOpen) { calcUI.CloseCalculator(); return true; }
            if (drawerContent.IsExpandedOpen) { drawerContent.CloseExpanded(); return true; }
            return false;
        }
    }
}
