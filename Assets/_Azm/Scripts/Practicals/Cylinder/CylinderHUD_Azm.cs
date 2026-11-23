using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Cylinder
{
    /*
     * Layer: UI (the on-screen layout of the cylinder practical).
     * Purpose: Connects the vernier-cylinder controller to the shared practical shell
     * (PracticalHUDBase_Azm, TASK_hci_and_juice Stage 2). Everything common - instruction card and
     * progress, panels per step, layout regions, message routing, hints, briefing, pause/back,
     * Learn/Perform - lives in the base. This class only:
     *   - turns a CylinderStepDefinition_Azm into the screen it needs (magnifier, keypad, least-count
     *     question, calculation, results);
     *   - maps the controller's events and message kinds to the shell;
     *   - shows the zero correction in the top-right chip ("ZC = +0.03 cm").
     * Talks to: CylinderController_Azm, CylinderResultsUI_Azm, CylinderTexts_Azm, the shell.
     */
    public class CylinderHUD_Azm : PracticalHUDBase_Azm
    {
        public const string BriefingId = "cylinder.briefing";

        public CylinderController_Azm controller;
        public CylinderResultsUI_Azm results;

        protected override string BriefingTutorialId => BriefingId;
        protected override string ExitQuestion => CylinderTexts_Azm.ExitQuestion;
        protected override string RestartQuestion => CylinderTexts_Azm.RestartQuestion;
        protected override int StepIndex => controller.StepIndex;
        protected override int StepCount => controller.StepCount;
        protected override string StepInstruction => controller.CurrentInstruction;
        protected override string StepWhy => controller.CurrentStep.why;
        protected override int CalcLineCount => CylinderController_Azm.CalcLineCount;
        protected override int CalcLinesRevealed => controller.CalcLinesRevealed;
        protected override bool ResultsVisible => results.IsVisible;
        protected override float[] LeastCountChoicesMm => controller.config.leastCountChoicesMm;
        protected override string LeastCountChoiceLabel(float mm) => CylinderTexts_Azm.LeastCountChoice(mm);
        protected override string LeastCountPromptText => CylinderTexts_Azm.LeastCountPrompt;

        protected override StepScreen_Azm CurrentScreen
        {
            get
            {
                var def = controller.CurrentStep;
                return new StepScreen_Azm
                {
                    magnifier = (def.controls & CylinderControls_Azm.Magnifier) != 0,
                    keypad = (def.controls & CylinderControls_Azm.Keypad) != 0,
                    leastCount = (def.controls & CylinderControls_Azm.LeastCountChoice) != 0,
                    calc = (def.controls & CylinderControls_Azm.CalcReveal) != 0,
                    results = def.id == CylinderStepId_Azm.Results,
                    view = (int)def.view,
                    guideProcedureIndex = def.guideProcedureIndex
                };
            }
        }

        protected override void SetPaused(bool paused) => controller.SetPaused(paused);
        protected override void Retry() => CylinderResultsUI_Azm.Retry();
        protected override void ShowResultsPanel(bool visible) => results.SetVisible(visible);
        protected override void AnswerLeastCount(int choice) => controller.AnswerLeastCount(choice);
        protected override void ContinueAfterReading() => controller.ContinueAfterReading();
        protected override void RevealNextCalcLine() => controller.RevealNextCalcLine();
        protected override void RevealAllCalcLines() => controller.RevealAllCalcLines();
        protected override void ShowResults() => controller.ShowResults();

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
            }
            else
            {
                controller.StepChanged -= OnStepChanged;
                controller.Message -= OnControllerMessage;
                controller.Hint -= OnHint;
                controller.ReadingsChanged -= RefreshChip;
                controller.CalcLinesChanged -= OnCalcLines;
                controller.PausedChanged -= OnPausedChanged;
            }
        }

        private void OnControllerMessage(string text, CylinderMessageKind_Azm kind)
        {
            FeedbackKind_Azm mapped;
            switch (kind)
            {
                case CylinderMessageKind_Azm.Success: mapped = FeedbackKind_Azm.Success; break;
                case CylinderMessageKind_Azm.Warning: mapped = FeedbackKind_Azm.Warning; break;
                case CylinderMessageKind_Azm.Error: mapped = FeedbackKind_Azm.Error; break;
                default: mapped = FeedbackKind_Azm.Info; break;
            }
            ShowMessage(text, mapped);
        }

        protected override void RefreshChip()
        {
            SetChip(controller.ZeroErrorKnown,
                $"ZC = {CylinderTexts_Azm.Signed(-controller.RecordedZeroErrorMm / 10f, 2)} cm");
        }
    }
}
