using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    /*
     * Layer: UI (the on-screen layout of the screw gauge practical).
     * Purpose: Connects the screw gauge controller to the shared practical shell
     * (PracticalHUDBase_Azm, TASK_hci_and_juice Stage 2). The base does everything common; this
     * class adds what only the screw gauge has:
     *   - the gauge controls in the bottom row: OPEN and RATCHET act while held (the student closes
     *     gently with the ratchet until it clicks), LOCK toggles;
     *   - the "turn one full turn" demonstration on the least-count panel - the least-count choices
     *     stay disabled until the pitch has been seen (teaching order); tapping a disabled choice
     *     now says why instead of doing nothing;
     *   - the ratchet click and the lock as felt feedback (sound + vibration);
     *   - the zero correction in the top-right chip ("ZC = +0.02 mm").
     * Talks to: ScrewGaugeController_Azm, ScrewGaugeResultsUI_Azm, HoldButton_Azm, the shell.
     */
    public class ScrewGaugeHUD_Azm : PracticalHUDBase_Azm
    {
        public const string BriefingId = "screwgauge.briefing";

        public ScrewGaugeController_Azm controller;
        public ScrewGaugeResultsUI_Azm results;

        [Header("Gauge controls")]
        public GameObject gaugeControls;
        public HoldButton_Azm openHold;
        public HoldButton_Azm ratchetHold;
        public Button lockButton;
        public TMP_Text lockLabel;

        [Header("Least count")]
        public Button turnButton;

        private bool wasLocked;

        protected override string BriefingTutorialId => BriefingId;
        protected override string ExitQuestion => ScrewGaugePracticalTexts_Azm.ExitQuestion;
        protected override string RestartQuestion => ScrewGaugePracticalTexts_Azm.RestartQuestion;
        protected override int StepIndex => controller.StepIndex;
        protected override int StepCount => controller.StepCount;
        protected override string StepInstruction => controller.CurrentInstruction;
        protected override string StepWhy => controller.CurrentStep.why;
        protected override int CalcLineCount => controller.CalcLineCount;
        protected override int CalcLinesRevealed => controller.CalcLinesRevealed;
        protected override bool ResultsVisible => results.IsVisible;
        protected override float[] LeastCountChoicesMm => controller.config.leastCountChoicesMm;
        protected override string LeastCountChoiceLabel(float mm) => ScrewGaugePracticalTexts_Azm.LeastCountChoice(mm);
        protected override string LeastCountPromptText => ScrewGaugePracticalTexts_Azm.LeastCountPrompt;

        protected override StepScreen_Azm CurrentScreen
        {
            get
            {
                var def = controller.CurrentStep;
                return new StepScreen_Azm
                {
                    magnifier = (def.controls & SGControls_Azm.Magnifier) != 0,
                    keypad = (def.controls & SGControls_Azm.Keypad) != 0,
                    leastCount = (def.controls & SGControls_Azm.LeastCountPanel) != 0,
                    calc = (def.controls & SGControls_Azm.CalcReveal) != 0,
                    results = def.id == SGStepId_Azm.Results,
                    view = (int)def.view,
                    guideProcedureIndex = def.guideProcedureIndex
                };
            }
        }

        protected override void SetPaused(bool paused) => controller.SetPaused(paused);
        protected override void Retry() => ScrewGaugeResultsUI_Azm.Retry();
        protected override void ShowResultsPanel(bool visible) => results.SetVisible(visible);
        protected override void AnswerLeastCount(int choice) => controller.AnswerLeastCount(choice);
        protected override void ContinueAfterReading() => controller.ContinueAfterReading();
        protected override void RevealNextCalcLine() => controller.RevealNextCalcLine();
        protected override void RevealAllCalcLines() => controller.RevealAllCalcLines();
        protected override void ShowResults() => controller.ShowResults();

        protected override void Awake()
        {
            base.Awake();
            turnButton.onClick.AddListener(controller.TurnOneTurn);
            lockButton.onClick.AddListener(controller.ToggleLock);
        }

        protected override void Start()
        {
            base.Start();
            RefreshLeastCount();
            wasLocked = controller.gauge.IsLocked;
        }

        protected override void Connect(bool connect)
        {
            if (controller == null) return;
            if (connect)
            {
                controller.StepChanged += OnStepChanged;
                controller.Message += OnControllerMessage;
                controller.Hint += OnHint;
                controller.ReadingsChanged += RefreshChip;
                controller.CalcLinesChanged += OnCalcLines;
                controller.PausedChanged += OnPausedChanged;
                controller.PitchStateChanged += RefreshLeastCount;
                controller.RatchetClicked += OnRatchetClicked;
            }
            else
            {
                controller.StepChanged -= OnStepChanged;
                controller.Message -= OnControllerMessage;
                controller.Hint -= OnHint;
                controller.ReadingsChanged -= RefreshChip;
                controller.CalcLinesChanged -= OnCalcLines;
                controller.PausedChanged -= OnPausedChanged;
                controller.PitchStateChanged -= RefreshLeastCount;
                controller.RatchetClicked -= OnRatchetClicked;
            }
        }

        protected override void Update()
        {
            base.Update();
            // Hold buttons: the ratchet closes gently (and clicks at contact); Open turns back.
            if (gaugeControls.activeInHierarchy)
            {
                if (ratchetHold.IsHeld) controller.HoldRatchet(Time.deltaTime);
                else if (openHold.IsHeld) controller.HoldOpen(Time.deltaTime);

                bool locked = controller.gauge.IsLocked;
                lockLabel.text = locked ? "Unlock" : "Lock";
                if (locked != wasLocked)
                {
                    FeedbackFX_Azm.Lock();
                    Motion_Azm.Punch(lockButton.transform, 0.15f);
                    wasLocked = locked;
                }
            }
        }

        protected override void OnStepScreenApplied(StepScreen_Azm screen)
        {
            gaugeControls.SetActive((controller.CurrentStep.controls & SGControls_Azm.GaugeControls) != 0);
        }

        // The choices only become active once the pitch has been seen (teaching order).
        private void RefreshLeastCount()
        {
            bool shown = controller.PitchShown;
            foreach (var b in leastCountButtons) b.interactable = shown;
            // The button tint alone dims only the background (white labels still look tappable), so
            // the whole group of choices fades. It still receives taps: a tap explains why it waits.
            if (leastCountButtons.Length > 0)
            {
                var parent = leastCountButtons[0].transform.parent;
                var group = parent.GetComponent<CanvasGroup>();
                if (group == null) group = parent.gameObject.AddComponent<CanvasGroup>();
                group.alpha = shown ? 1f : 0.4f;
            }
            turnButton.interactable = !controller.PitchTurning;
            if (shown) Motion_Azm.StopPulse(turnButton.transform);
            else if (!controller.PitchTurning) Motion_Azm.Pulse(turnButton.transform, 1.04f, 1.2f);
        }

        protected override void OnDisabledTapped(ButtonFeel_Azm button)
        {
            foreach (var b in leastCountButtons)
                if (b != null && b.gameObject == button.gameObject && !controller.PitchShown)
                {
                    ShowMessage(ScrewGaugePracticalTexts_Azm.TurnFirst, FeedbackKind_Azm.Warning);
                    return;
                }
        }

        private void OnRatchetClicked() => FeedbackFX_Azm.Ratchet();

        private void OnControllerMessage(string text, SGMessageKind_Azm kind)
        {
            FeedbackKind_Azm mapped;
            switch (kind)
            {
                case SGMessageKind_Azm.Success: mapped = FeedbackKind_Azm.Success; break;
                case SGMessageKind_Azm.Warning: mapped = FeedbackKind_Azm.Warning; break;
                case SGMessageKind_Azm.Error: mapped = FeedbackKind_Azm.Error; break;
                default: mapped = FeedbackKind_Azm.Info; break;
            }
            ShowMessage(text, mapped);
        }

        protected override void RefreshChip()
        {
            SetChip(controller.ZeroErrorKnown,
                $"ZC = {ScrewGaugePracticalTexts_Azm.Signed(-controller.RecordedZeroErrorMm, 2)} mm");
        }
    }
}
