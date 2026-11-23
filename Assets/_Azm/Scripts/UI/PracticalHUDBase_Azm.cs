using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Instruments;

namespace VirtualLab.Azm.UI
{
    // What the HUD shows for the current step (each practical turns its own step flags into this).
    public struct StepScreen_Azm
    {
        public bool magnifier;       // the code-drawn reading magnifier
        public bool keypad;          // the reading keypad (right-hand panel)
        public bool leastCount;      // the least-count question (right-hand panel)
        public bool calc;            // "Show next line" / "See my result"
        public bool results;         // the results panel
        public int view;             // camera view index
        public int guideProcedureIndex;
    }

    /*
     * Layer: UI (the shared on-screen shell of a practical; no experiment logic).
     * Purpose: Everything the practical HUDs used to duplicate (TASK_hci_and_juice Stage 2):
     *   - Learn vs Perform mode (Perform = no hints, no "Show me", no escalating help, the worked
     *     calculation shown at once);
     *   - the instruction card (step label, short action, progress bar, "Why?", hints);
     *   - which panels a step shows, and telling ScreenRegions_Azm so the layout and the camera
     *     framing make room for them;
     *   - MESSAGE ROUTING: feedback about an answer appears in the panel where the answer was given
     *     (keypad / least-count panel); anything else uses the toast in the left column. Hints go to
     *     the instruction card. Nothing is drawn over the instrument being read;
     *   - hints once per session, only in Learn mode and with the Hints setting on, held back while
     *     the briefing is open;
     *   - briefing on the first visit, pause / restart / guide / exit, the Android back-button chain,
     *     camera view buttons, the calculation buttons, the session statistics.
     * A practical subclasses this and only connects its controller (events in, intents out).
     * Talks to: InstructionCardUI_Azm, ScreenRegions_Azm, PracticalCameraRig_Azm, DrawerUI_Azm,
     * ToastUI_Azm, PanelFeedback_Azm, CaliperReadingPadUI_Azm, BriefingUI_Azm, GuideViewUI_Azm,
     * ConfirmDialog_Azm, UISound_Azm, BackButtonInput_Azm, SettingsManager_Azm, AppManager_Azm.
     */
    public abstract class PracticalHUDBase_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        public PracticalCameraRig_Azm cameraRig;
        public ScreenRegions_Azm regions;
        public DrawerUI_Azm drawer;
        public ToastUI_Azm toast;
        public ConfirmDialog_Azm confirm;
        public BackButtonInput_Azm backInput;
        public UISound_Azm sound;
        public BriefingUI_Azm briefing;
        public GuideViewUI_Azm guideView;
        public PracticalGuide_Azm guide;

        [Header("Top")]
        public Button pauseButton;
        public InstructionCardUI_Azm instructionCard;
        [Tooltip("A status chip on the top right (e.g. the zero correction).")]
        public GameObject infoChip;
        public TMP_Text infoChipText;

        [Header("Onboarding (HCI restructure Stage 5)")]
        public HudTourUI_Azm tour;
        public CameraGestureChip_Azm cameraChip;

        [Header("View buttons")]
        public Button[] viewButtons;

        [Header("Measuring")]
        public PanelTransition_Azm magnifierPanel;
        public PanelTransition_Azm padPanel;
        public CaliperReadingPadUI_Azm pad;

        [Header("Least count")]
        public PanelTransition_Azm leastCountPanel;
        public TMP_Text leastCountPrompt;
        public Button[] leastCountButtons;
        public PanelFeedback_Azm leastCountFeedback;

        [Header("Calculation")]
        public GameObject calcGroup;
        public Button nextLineButton;
        public Button seeResultButton;

        [Header("Pause")]
        public PanelTransition_Azm pausePanel;
        public Button resumeButton;
        public Button restartButton;
        public Button guideButton;
        public Button exitButton;
        [Tooltip("Opens the settings panel (same settings as the main menu) without leaving the practical.")]
        public Button settingsButton;
        public PanelTransition_Azm settingsPanel;
        public Button settingsCloseButton;

        // Drawer tab indices (the pendulum has Data, Graph, Calc, Guide; the others Data, Calc, Guide).
        protected virtual int CalcTab => 1;
        protected virtual int GuideTab => 2;

        public bool LearnMode { get; private set; } = true;
        // Created in Awake (it reads the clock, which Unity forbids in a MonoBehaviour constructor).
        public PracticalSessionStats_Azm Stats { get; private set; }

        private readonly HashSet<string> hintsShown = new HashSet<string>();   // per session (instance, never static)
        private readonly Queue<string> pendingHints = new Queue<string>();
        private bool chipWasVisible;

        // ----- what each practical provides -----
        protected abstract string BriefingTutorialId { get; }
        protected abstract string ExitQuestion { get; }
        protected abstract string RestartQuestion { get; }
        protected abstract int StepIndex { get; }
        protected abstract int StepCount { get; }
        protected abstract string StepInstruction { get; }
        protected abstract string StepWhy { get; }
        protected abstract StepScreen_Azm CurrentScreen { get; }
        protected abstract int CalcLineCount { get; }
        protected abstract int CalcLinesRevealed { get; }
        protected abstract bool ResultsVisible { get; }
        protected abstract float[] LeastCountChoicesMm { get; }
        protected abstract string LeastCountChoiceLabel(float mm);
        protected abstract string LeastCountPromptText { get; }

        protected abstract void SetPaused(bool paused);
        protected abstract void Retry();
        protected abstract void ShowResultsPanel(bool visible);
        protected abstract void AnswerLeastCount(int choice);
        protected abstract void ContinueAfterReading();
        protected abstract void RevealNextCalcLine();
        protected abstract void RevealAllCalcLines();
        protected abstract void ShowResults();
        // Subscribe / unsubscribe the practical's controller events.
        protected abstract void Connect(bool connect);

        // ----- lifecycle -----

        protected virtual void Awake()
        {
            Stats = new PracticalSessionStats_Azm();
            var session = AppManager_Azm.Instance != null ? AppManager_Azm.Instance.CurrentSession : null;
            // Played straight from the Editor (no session) counts as Learn.
            LearnMode = session == null || session.Mode == PracticalMode_Azm.Learn;

            pauseButton.onClick.AddListener(() => SetPaused(true));
            resumeButton.onClick.AddListener(() => SetPaused(false));
            restartButton.onClick.AddListener(() => confirm.Ask(RestartQuestion, "Restart", Retry));
            guideButton.onClick.AddListener(() => { SetPaused(false); drawer.Open(GuideTab); });
            exitButton.onClick.AddListener(AskExit);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => { settingsPanel.Show(); FeedbackFX_Azm.PanelSlide(); });
            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(() => settingsPanel.Hide());

            for (int i = 0; i < viewButtons.Length; i++)
            {
                int view = i;
                viewButtons[i].onClick.AddListener(() => cameraRig.GoTo(view));
            }
            for (int i = 0; i < leastCountButtons.Length; i++)
            {
                int choice = i;
                leastCountButtons[i].onClick.AddListener(() => AnswerLeastCount(choice));
            }
            nextLineButton.onClick.AddListener(RevealNextCalcLine);
            seeResultButton.onClick.AddListener(ShowResults);

            pad.ShowMeAllowed = LearnMode;
            pad.EscalationAllowed = LearnMode;
            pad.ContinuePressed += ContinueAfterReading;
            pad.Checked += OnPadChecked;
            pad.ShowMeUsed += Stats.ShowMe;
            pad.Escalated += Stats.Escalated;

            if (guideView.replayBriefingButton != null)
            {
                guideView.replayBriefingButton.onClick.AddListener(ShowBriefing);
                guideView.replayBriefingButton.gameObject.SetActive(LearnMode);
            }
            foreach (var feel in GetComponentsInChildren<ButtonFeel_Azm>(true))
                feel.DisabledTapped += OnDisabledTapped;
            drawer.OpenChanged += OnDrawerOpenChanged;
        }

        protected virtual void OnDestroy()
        {
            if (pad != null)
            {
                pad.ContinuePressed -= ContinueAfterReading;
                pad.Checked -= OnPadChecked;
                pad.ShowMeUsed -= Stats.ShowMe;
                pad.Escalated -= Stats.Escalated;
            }
            if (drawer != null) drawer.OpenChanged -= OnDrawerOpenChanged;
        }

        protected virtual void OnEnable()
        {
            Connect(true);
            cameraRig.ViewChanged += OnViewChanged;
            if (backInput != null) backInput.BackPressed += OnBack;
        }

        protected virtual void OnDisable()
        {
            Connect(false);
            cameraRig.ViewChanged -= OnViewChanged;
            if (backInput != null) backInput.BackPressed -= OnBack;
        }

        protected virtual void Start()
        {
            pausePanel.Hide(false);
            if (settingsPanel != null) settingsPanel.Hide(false);
            magnifierPanel.Hide(false);
            padPanel.Hide(false);
            leastCountPanel.Hide(false);

            var choices = LeastCountChoicesMm;
            for (int i = 0; i < leastCountButtons.Length; i++)
            {
                bool used = i < choices.Length;
                leastCountButtons[i].gameObject.SetActive(used);
                if (used) leastCountButtons[i].GetComponentInChildren<TMP_Text>().text = LeastCountChoiceLabel(choices[i]);
            }
            leastCountPrompt.text = LeastCountPromptText;

            if (sound != null) sound.HookButtons(transform);   // any button without ButtonFeel_Azm
            OnViewChanged(cameraRig.CurrentView);
            OnStepChanged(StepIndex);
            RefreshChip();

            // Briefing: Learn mode, the first time this practical is opened.
            var settings = SettingsManager_Azm.Instance;
            if (LearnMode && (settings == null || !settings.HasSeenTutorial(BriefingTutorialId)))
                ShowBriefing(true);
            else
                IntroFinished();
        }

        // ----- onboarding (Stage 5) -----
        public const string TourTutorialId = "hud.tour";

        // True while something that wants the student's full attention is open; the gesture coach
        // and other hints wait until it is closed.
        public bool OnboardingBusy =>
            (briefing != null && briefing.IsOpen) || (tour != null && tour.IsOpen) ||
            (pausePanel != null && pausePanel.IsVisible) || (settingsPanel != null && settingsPanel.IsVisible);

        // After the briefing (or straight away): the HUD tour the first time ever (both modes - every
        // student needs to find the controls), then the camera gesture chip; then the practical's own hook.
        private void IntroFinished()
        {
            var settings = SettingsManager_Azm.Instance;
            bool tourSeen = settings == null || settings.HasSeenTutorial(TourTutorialId);
            if (tour != null && !tourSeen && viewButtons != null && viewButtons.Length > 0)
            {
                var targets = new[] { (RectTransform)instructionCard.transform, (RectTransform)viewButtons[0].transform.parent, drawer.strip };
                tour.Show(targets, () =>
                {
                    if (SettingsManager_Azm.Instance != null) SettingsManager_Azm.Instance.MarkTutorialSeen(TourTutorialId);
                    ShowCameraChip();
                });
            }
            else ShowCameraChip();
            OnIntroFinished();
        }

        private void ShowCameraChip()
        {
            if (cameraChip != null) cameraChip.ShowOnce(cameraRig);
        }

        // Called once per session when the student can start working: after the first briefing
        // closes, or straight away when there is none (onboarding hooks in here - Stage 5).
        protected virtual void OnIntroFinished() { }

        protected virtual void Update()
        {
            cameraRig.SetDrawerOpenAmount(drawer.OpenAmount);   // only used if the rig has no regions
            // A hint that fired during the briefing / tour / pause is shown once that has closed.
            if (pendingHints.Count > 0 && !OnboardingBusy)
                instructionCard.ShowHint(pendingHints.Dequeue());
            KeepToastBelowDetail();
        }

        // The toast hangs under the top bar; when the instruction card's Hint / Why? detail is open the
        // toast moves down below it, so a hint and a message are never drawn on top of each other.
        private float toastBaseY = float.NaN;
        private void KeepToastBelowDetail()
        {
            if (toast == null) return;
            var rect = (RectTransform)toast.transform;
            if (float.IsNaN(toastBaseY)) toastBaseY = rect.anchoredPosition.y;
            float target = toastBaseY - instructionCard.VisibleDetailHeight;
            if (!Mathf.Approximately(rect.anchoredPosition.y, target))
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Lerp(rect.anchoredPosition.y, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)));
        }

        // ----- steps -----

        // Subclasses call this from the controller's StepChanged event.
        protected void OnStepChanged(int index)
        {
            var screen = CurrentScreen;
            Stats.EnterStep(index);
            instructionCard.SetStep(index, StepCount, StepInstruction, StepWhy);

            bool magnifierWasOpen = magnifierPanel.IsVisible;
            SetPanel(magnifierPanel, screen.magnifier);
            if (screen.magnifier && !magnifierWasOpen) OnMagnifierOpened();
            SetPanel(padPanel, screen.keypad);
            SetPanel(leastCountPanel, screen.leastCount);
            regions.SetRightPanel(screen.keypad || screen.leastCount);
            regions.SetMagnifier(screen.magnifier);
            if (screen.keypad) pad.ResetEntry();
            if (!screen.leastCount && leastCountFeedback != null) leastCountFeedback.gameObject.SetActive(false);
            calcGroup.SetActive(screen.calc);

            cameraRig.GoTo(screen.view);
            guideView.SetCurrentStep(screen.guideProcedureIndex);

            if (screen.calc)
            {
                drawer.Open(CalcTab);
                // Perform mode shows the whole worked calculation at once.
                if (!LearnMode) RevealAllCalcLines();
                OnCalcLines(CalcLinesRevealed);
            }
            else if (screen.magnifier || screen.results)
            {
                // The keypad and magnifier need the screen; the student can still open the drawer.
                drawer.Close();
            }

            ShowResultsPanel(screen.results);
            // The camera view buttons are hidden under the results panel (they overlapped its bottom
            // edge at 16:9, and changing view behind the result has no purpose).
            if (viewButtons != null && viewButtons.Length > 0)
                viewButtons[0].transform.parent.gameObject.SetActive(!screen.results);
            OnStepScreenApplied(screen);
        }

        // Juice (Stage 6): the magnifier "zooms" open; in Learn mode, the first time in a session,
        // the vernier's zero line pulses - that is the line the student reads M against.
        private bool zeroPulsed;
        private void OnMagnifierOpened()
        {
            Motion_Azm.PopIn(magnifierPanel.transform, 0.9f);
            if (!LearnMode || zeroPulsed) return;
            var vernier = magnifierPanel.GetComponent<CaliperMagnifierUI_Azm>();
            if (vernier == null) return;
            zeroPulsed = true;
            vernier.PulseVernierZero();
        }

        // For practical-specific controls (e.g. the screw gauge's Open / Ratchet / Lock bar).
        protected virtual void OnStepScreenApplied(StepScreen_Azm screen) { }

        protected static void SetPanel(PanelTransition_Azm panel, bool visible)
        {
            if (visible && !panel.IsVisible)
            {
                panel.Show();
                FeedbackFX_Azm.PanelSlide();
            }
            else if (!visible && panel.IsVisible) panel.Hide();
        }

        protected void OnCalcLines(int revealed)
        {
            bool done = revealed >= CalcLineCount;
            bool wasNext = nextLineButton.gameObject.activeSelf;
            nextLineButton.gameObject.SetActive(!done);
            seeResultButton.gameObject.SetActive(done);
            if (revealed > 0) FeedbackFX_Azm.Reveal(null);
            if (done && wasNext) Motion_Azm.PopIn(seeResultButton.transform);
        }

        // The top-right chip: shown with a pop the first time it gets a value.
        protected void SetChip(bool visible, string text)
        {
            if (infoChip == null) return;
            infoChip.SetActive(visible);
            if (visible) infoChipText.text = text;
            if (visible && !chipWasVisible) FeedbackFX_Azm.Reveal(infoChip.transform);
            chipWasVisible = visible;
        }

        protected abstract void RefreshChip();

        private void OnViewChanged(int view)
        {
            for (int i = 0; i < viewButtons.Length; i++)
            {
                bool active = i == view;
                var image = viewButtons[i].GetComponent<ThemedImage_Azm>();
                if (image != null) image.SetRole(active ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
                foreach (var label in viewButtons[i].GetComponentsInChildren<ThemedText_Azm>(true))
                    label.SetRole(active ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
                foreach (var icon in viewButtons[i].GetComponentsInChildren<ThemedIcon_Azm>(true))
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null) img.color = theme.GetColor(active ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
                }
            }
        }

        private void OnDrawerOpenChanged(bool open) => FeedbackFX_Azm.PanelSlide();

        // ----- feedback -----

        // Subclasses map their controller's message kind and call this.
        protected void ShowMessage(string text, FeedbackKind_Azm kind)
        {
            // 1) Feedback about a reading: in the keypad, beside the answer.
            if (padPanel.IsVisible && pad.HasFeedbackPanel && kind != FeedbackKind_Azm.Info)
            {
                pad.ShowFeedback(text, kind);
                return;   // the keypad plays its own feel for checks
            }
            // 2) Feedback about the least-count answer: in that panel.
            if (leastCountPanel.IsVisible && leastCountFeedback != null)
            {
                leastCountFeedback.Show(text, kind);
                PlayFeel(kind, (RectTransform)leastCountFeedback.transform);
                return;
            }
            // 3) Anything else: the toast in the left column (never over the right-hand panels).
            Color? tint = null;
            Color strong = theme.GetColor(ThemeColor_Azm.PanelStrong);
            if (kind == FeedbackKind_Azm.Error) tint = Color.Lerp(strong, theme.GetColor(ThemeColor_Azm.Invalid), 0.55f);
            else if (kind == FeedbackKind_Azm.Warning) tint = Color.Lerp(strong, theme.GetColor(ThemeColor_Azm.Accent), 0.45f);
            else if (kind == FeedbackKind_Azm.Success) tint = Color.Lerp(strong, theme.GetColor(ThemeColor_Azm.Valid), 0.45f);
            toast.Show(text, tint);
            PlayFeel(kind, null);
        }

        private static void PlayFeel(FeedbackKind_Azm kind, RectTransform target)
        {
            switch (kind)
            {
                case FeedbackKind_Azm.Success: FeedbackFX_Azm.Correct(target); break;
                case FeedbackKind_Azm.Error: FeedbackFX_Azm.Wrong(target); break;
                case FeedbackKind_Azm.Warning: FeedbackFX_Azm.Warning(target); break;
            }
        }

        private void OnPadChecked(CaliperReadingResult_Azm result)
        {
            if (result == CaliperReadingResult_Azm.MainScaleWrong || result == CaliperReadingResult_Azm.VernierWrong ||
                result == CaliperReadingResult_Azm.BothWrong || result == CaliperReadingResult_Azm.SignWrong)
                Stats.WrongReading();
        }

        // Hints are guidance: Learn mode only, once per session, and only with the Hints setting on.
        protected void OnHint(string id, string text)
        {
            if (!LearnMode || hintsShown.Contains(id)) return;
            var settings = SettingsManager_Azm.Instance;
            if (settings != null && !settings.HintsEnabled) return;
            hintsShown.Add(id);
            Stats.Hint();
            if (OnboardingBusy) pendingHints.Enqueue(text);
            else instructionCard.ShowHint(text);
        }

        // A tap on a disabled button: the practical may explain why (default: nothing more than the shake).
        protected virtual void OnDisabledTapped(ButtonFeel_Azm button) { }

        // ----- briefing -----

        protected void ShowBriefing() => ShowBriefing(false);

        // Aim -> (Hypothesis, if the guide has one) -> Apparatus -> What you will measure.
        private void ShowBriefing(bool firstTime)
        {
            if (guide == null) { if (firstTime) IntroFinished(); return; }
            var cards = new List<BriefingUI_Azm.Card> { new BriefingUI_Azm.Card("Aim", guide.aim) };
            if (!string.IsNullOrEmpty(guide.hypothesis)) cards.Add(new BriefingUI_Azm.Card("Hypothesis", guide.hypothesis));
            cards.Add(new BriefingUI_Azm.Card("Apparatus", "• " + string.Join("\n• ", guide.apparatus)));
            cards.Add(new BriefingUI_Azm.Card("What you will measure", guide.whatYouMeasure));
            briefing.Show(cards, () =>
            {
                if (SettingsManager_Azm.Instance != null) SettingsManager_Azm.Instance.MarkTutorialSeen(BriefingTutorialId);
                if (firstTime) IntroFinished();
            });
        }

        // A practical's own overlays (e.g. the pendulum's expanded table) close first; true = handled.
        protected virtual bool HandleBack() => false;

        // ----- pause / back -----

        // Pausing also happens without a tap (the app going to the background), so the pause panel
        // follows the controller's paused state.
        protected void OnPausedChanged(bool paused)
        {
            if (paused && !pausePanel.IsVisible) { pausePanel.Show(); FeedbackFX_Azm.PanelSlide(); }
            else if (!paused && pausePanel.IsVisible) pausePanel.Hide();
        }

        protected void AskExit()
        {
            confirm.Ask(ExitQuestion, "Exit", () =>
            {
                if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.GoToMainMenu();
            });
        }

        // The Android back button (Escape): closes the top-most thing, otherwise opens/closes pause.
        private void OnBack()
        {
            if (confirm.IsOpen) { confirm.Close(); return; }
            if (settingsPanel != null && settingsPanel.IsVisible) { settingsPanel.Hide(); return; }
            if (briefing.IsOpen) { briefing.Close(); return; }
            if (tour != null && tour.IsOpen) { tour.Close(); return; }
            if (HandleBack()) return;
            if (instructionCard.DetailOpen) { instructionCard.CloseDetail(); return; }
            if (ResultsVisible) { AskExit(); return; }
            if (pausePanel.IsVisible) { SetPaused(false); return; }
            if (drawer.IsOpen) { drawer.Close(); return; }
            SetPaused(true);
        }
    }
}
