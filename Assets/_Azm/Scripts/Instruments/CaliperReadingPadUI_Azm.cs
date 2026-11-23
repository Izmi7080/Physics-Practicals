using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: UI (collects the student's reading; the instrument judges it).
     * Purpose: The shared numeric keypad for an instrument reading, entered the way the syllabus
     * tables record it: the main-scale reading M (mm) and the division n that coincides - vernier
     * caliper (n 0-9) or screw gauge (circular scale, n 0-49; M may end in .5 with the optional
     * decimal key). The line under the fields shows the formula being worked out as it is typed,
     * so the student sees the method, not just a number. Check sends M and n to the instrument.
     * The keys only work once the instrument is ready to read (jaws touching / ratchet clicked).
     * In ZERO-ERROR mode the left field becomes the SIGN: tapping it toggles + / −; the digits go
     * to n, and the formula line shows the zero error being worked out.
     * "Show me" (Learn mode only; the practical decides via ShowMeAllowed) asks the magnifier to
     * highlight the lines to read. After a correct reading the keys are replaced by Continue, which
     * the practical's controller listens to.
     * HCI restructure:
     *   - FEEDBACK IN THE PANEL: the HUD sends the feedback for a reading here (ShowFeedback) when a
     *     PanelFeedback_Azm is assigned, so it appears beside the answer instead of over the 3D
     *     instrument. Typing again clears it.
     *   - ESCALATING HELP (Learn mode, EscalationAllowed): the same mistake twice in a row replaces
     *     the short correction with the instrument's "where to look" explanation (EscalatedHint);
     *     after three wrong tries on one reading the "Show me" button pulses and the student is told
     *     it is there. Never the answer itself.
     *   - FEEL: a wrong field shakes; a correct reading punches the Continue button (FeedbackFX_Azm).
     * Talks to: an IReadingInstrument_Azm (CaliperMeasurement_Azm or ScrewGaugeMeasurement_Azm), UITheme_Azm,
     * PanelFeedback_Azm, FeedbackFX_Azm, Motion_Azm.
     */
    public class CaliperReadingPadUI_Azm : MonoBehaviour
    {
        public UITheme_Azm theme;
        [Tooltip("The instrument: a component implementing IReadingInstrument_Azm.")]
        public MonoBehaviour instrument;

        [Header("Parts (built by the scene builder)")]
        public TMP_Text statusText;
        public Button mainField;
        public TMP_Text mainValueText;
        [Tooltip("Optional caption above the left field (its text changes in zero-error mode).")]
        public TMP_Text mainLabelText;
        public Button vernierField;
        public TMP_Text vernierValueText;
        [Tooltip("Optional caption above the right field.")]
        public TMP_Text vernierLabelText;
        public TMP_Text formulaText;
        [Tooltip("Index = digit.")]
        public Button[] digitButtons = new Button[10];
        [Tooltip("Optional '.' key for main-scale readings that end in .5 (screw gauge).")]
        public Button decimalButton;
        public Button backspaceButton;
        public Button clearButton;
        public Button checkButton;
        public Button showMeButton;
        public Button continueButton;
        public CanvasGroup keysGroup;
        [Tooltip("Optional: feedback card over the status line (HCI restructure).")]
        public PanelFeedback_Azm feedback;

        public event Action ContinuePressed;
        // Raised after every Check with the instrument's verdict (the HUD counts wrong readings).
        public event Action<CaliperReadingResult_Azm> Checked;
        public event Action ShowMeUsed;
        public event Action Escalated;

        // Set by the practical (Learn mode on/off).
        public bool ShowMeAllowed { get; set; } = true;
        // Learn mode: replace repeated corrections with the "where to look" help.
        public bool EscalationAllowed { get; set; } = true;
        public bool HasFeedbackPanel => feedback != null;

        private CaliperReadingResult_Azm lastWrong = CaliperReadingResult_Azm.Correct;
        private int sameMistakeStreak;
        private int wrongThisReading;
        private bool showMePulsing;
        private bool checking, hasPending;
        private string pendingText;
        private FeedbackKind_Azm pendingKind;

        private const int MainMaxDigits = 3;
        private const string Plus = "+";
        private const string Minus = "−";
        private string mainEntry = "";       // M (digits, maybe ".5"), or the sign in zero-error mode
        private string vernierEntry = "";
        private bool editingMain = true;

        private IReadingInstrument_Azm Inst => instrument as IReadingInstrument_Azm;
        private bool ZeroMode => Inst.ZeroErrorMode;

        private void Awake()
        {
            if (Inst == null) Debug.LogError("[CaliperReadingPadUI_Azm] instrument does not implement IReadingInstrument_Azm.", this);
            for (int d = 0; d < digitButtons.Length; d++)
            {
                int digit = d;
                if (digitButtons[d] != null) digitButtons[d].onClick.AddListener(() => Type(digit));
            }
            if (decimalButton != null) decimalButton.onClick.AddListener(TypeDecimal);
            if (backspaceButton != null) backspaceButton.onClick.AddListener(Backspace);
            if (clearButton != null) clearButton.onClick.AddListener(ClearActive);
            mainField.onClick.AddListener(OnMainField);
            vernierField.onClick.AddListener(() => { editingMain = false; Refresh(); });
            checkButton.onClick.AddListener(Check);
            showMeButton.onClick.AddListener(() =>
            {
                StopShowMePulse();
                if (!Inst.ShowMeActive) ShowMeUsed?.Invoke();
                Inst.SetShowMe(!Inst.ShowMeActive);
            });
            continueButton.onClick.AddListener(() => ContinuePressed?.Invoke());
        }

        // ----- feedback in the panel -----

        // Called by the HUD with the controller's message for the last Check (or a warning).
        public void ShowFeedback(string text, FeedbackKind_Azm kind)
        {
            if (feedback == null) return;
            // The instrument reports its verdict (and the HUD calls this) DURING Check, before the
            // mistake counters are updated; show it once Check has finished counting.
            if (checking) { pendingText = text; pendingKind = kind; hasPending = true; return; }
            if (kind == FeedbackKind_Azm.Error && EscalationAllowed)
            {
                if (sameMistakeStreak >= 2)
                {
                    text = Inst.EscalatedHint(lastWrong);
                    Escalated?.Invoke();
                }
                if (wrongThisReading >= 3 && ShowMeAllowed && showMeButton.gameObject.activeInHierarchy)
                {
                    text += "\n" + CaliperTexts_Azm.StillStuck;
                    if (!showMePulsing) { Motion_Azm.Pulse(showMeButton.transform); showMePulsing = true; }
                }
            }
            feedback.Show(text, kind);
            // The status line sits under the card; hide it so it cannot show through.
            statusText.gameObject.SetActive(false);
        }

        private void HideFeedback()
        {
            if (feedback != null && feedback.IsShowing) feedback.Hide();
            statusText.gameObject.SetActive(true);
        }

        private void StopShowMePulse()
        {
            if (!showMePulsing) return;
            showMePulsing = false;
            Motion_Azm.StopPulse(showMeButton.transform);
        }

        private void OnEnable()
        {
            Inst.ReadyChanged += OnReadyChanged;
            Inst.StateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Inst.ReadyChanged -= OnReadyChanged;
            Inst.StateChanged -= Refresh;
        }

        // A new measurement starts with empty fields.
        public void ResetEntry()
        {
            mainEntry = "";
            vernierEntry = "";
            editingMain = !ZeroMode;
            sameMistakeStreak = 0;
            wrongThisReading = 0;
            lastWrong = CaliperReadingResult_Azm.Correct;
            StopShowMePulse();
            if (feedback != null && feedback.gameObject.activeSelf) feedback.gameObject.SetActive(false);
            statusText.gameObject.SetActive(true);
            Refresh();
        }

        private void OnReadyChanged(bool ready) => Refresh();

        private bool CanType => Inst.IsReady && !Inst.IsAccepted;

        private void OnMainField()
        {
            if (ZeroMode)
            {
                // The sign field is a toggle; the digits keep going to n.
                if (CanType) mainEntry = mainEntry == Plus ? Minus : Plus;
                editingMain = false;
            }
            else editingMain = true;
            Refresh();
        }

        private void Type(int digit)
        {
            if (!CanType) return;
            HideFeedback();
            if (editingMain && !ZeroMode)
            {
                int dot = mainEntry.IndexOf('.');
                if (dot >= 0)
                {
                    // After the point only ".0" or ".5" is a main-scale reading (half-millimetre marks).
                    if (mainEntry.Length == dot + 1 && (digit == 5 || digit == 0)) mainEntry += digit;
                }
                else if (mainEntry.Length < MainMaxDigits)
                    mainEntry = mainEntry == "0" ? digit.ToString() : mainEntry + digit;
            }
            else
            {
                int divisions = Inst.Divisions;
                int maxDigits = divisions > 10 ? 2 : 1;
                string next = vernierEntry.Length < maxDigits && vernierEntry != "0" ? vernierEntry + digit : digit.ToString();
                // n is 0 .. divisions-1: a value that is too large starts again from this digit.
                vernierEntry = int.Parse(next, CultureInfo.InvariantCulture) < divisions ? next : digit.ToString();
            }
            Refresh();
        }

        private void TypeDecimal()
        {
            if (!CanType || ZeroMode || !Inst.AllowsHalfMillimetre) return;
            editingMain = true;
            if (mainEntry.IndexOf('.') < 0) mainEntry = (mainEntry.Length == 0 ? "0" : mainEntry) + ".";
            Refresh();
        }

        private void Backspace()
        {
            HideFeedback();
            if (editingMain && !ZeroMode) { if (mainEntry.Length > 0) mainEntry = mainEntry.Substring(0, mainEntry.Length - 1); }
            else if (vernierEntry.Length > 0) vernierEntry = vernierEntry.Substring(0, vernierEntry.Length - 1);
            Refresh();
        }

        private void ClearActive()
        {
            if (editingMain && !ZeroMode) mainEntry = ""; else vernierEntry = "";
            Refresh();
        }

        private static int? ParseInt(string s) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : (int?)null;

        private static float? ParseMain(string s)
        {
            if (string.IsNullOrEmpty(s) || s.EndsWith(".")) return null;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : (float?)null;
        }

        private bool? Sign => mainEntry == Minus ? true : (mainEntry == Plus ? false : (bool?)null);

        private void Check()
        {
            CaliperReadingResult_Azm result;
            checking = true;
            hasPending = false;
            if (ZeroMode)
            {
                result = Inst.SubmitZeroError(Sign, ParseInt(vernierEntry));
                editingMain = false;
            }
            else
            {
                result = Inst.SubmitReading(ParseMain(mainEntry), ParseInt(vernierEntry));
                // After an incomplete entry, move the cursor to the missing field.
                if (result == CaliperReadingResult_Azm.Incomplete) editingMain = ParseMain(mainEntry) == null;
            }
            Refresh();
            React(result);
            checking = false;
            if (hasPending) { hasPending = false; ShowFeedback(pendingText, pendingKind); }
        }

        // Feel and help bookkeeping for one Check. The wrong FIELD shakes, so the student knows which
        // number to look at again (main scale, division, or the zero-error sign).
        private void React(CaliperReadingResult_Azm result)
        {
            switch (result)
            {
                case CaliperReadingResult_Azm.Correct:
                    StopShowMePulse();
                    FeedbackFX_Azm.Correct(continueButton.transform);
                    break;
                case CaliperReadingResult_Azm.Incomplete:
                case CaliperReadingResult_Azm.NotTouching:
                    FeedbackFX_Azm.Warning((RectTransform)(editingMain && !ZeroMode ? mainField.transform : vernierField.transform));
                    break;
                default:
                    sameMistakeStreak = result == lastWrong ? sameMistakeStreak + 1 : 1;
                    lastWrong = result;
                    wrongThisReading++;
                    var field = result == CaliperReadingResult_Azm.MainScaleWrong || result == CaliperReadingResult_Azm.SignWrong
                        ? mainField.transform : vernierField.transform;
                    if (result == CaliperReadingResult_Azm.BothWrong)
                    {
                        FeedbackFX_Azm.Wrong((RectTransform)mainField.transform);
                        Motion_Azm.Shake((RectTransform)vernierField.transform);
                    }
                    else FeedbackFX_Azm.Wrong((RectTransform)field);
                    break;
            }
            Checked?.Invoke(result);
        }

        private void Refresh()
        {
            if (statusText == null || Inst == null) return;
            bool ready = Inst.IsReady;
            bool accepted = Inst.IsAccepted;
            bool zero = ZeroMode;

            statusText.text = accepted ? Inst.AcceptedMessage : (ready ? Inst.ReadyHint : Inst.WaitingHint);
            SetTextRole(statusText, accepted ? ThemeColor_Azm.Valid : (ready ? ThemeColor_Azm.TextPrimary : ThemeColor_Azm.Accent));

            if (mainLabelText != null) mainLabelText.text = Inst.MainLabel;
            if (vernierLabelText != null) vernierLabelText.text = Inst.DivisionLabel;

            mainValueText.text = string.IsNullOrEmpty(mainEntry) ? (zero ? "±" : "–") : mainEntry;
            vernierValueText.text = string.IsNullOrEmpty(vernierEntry) ? "–" : vernierEntry;
            SetFieldActive(mainField, mainValueText, editingMain && !zero && !accepted);
            SetFieldActive(vernierField, vernierValueText, (!editingMain || zero) && !accepted);

            int? n = ParseInt(vernierEntry);
            formulaText.text = zero
                ? Inst.ZeroFormula(mainEntry, vernierEntry, Inst.ZeroErrorValueMm(Sign, n))
                : Inst.ReadingFormula(mainEntry, vernierEntry, Inst.ReadingValueMm(ParseMain(mainEntry), n));

            keysGroup.alpha = accepted ? 0f : (ready ? 1f : 0.45f);
            keysGroup.interactable = ready && !accepted;
            keysGroup.blocksRaycasts = !accepted;
            if (decimalButton != null) decimalButton.gameObject.SetActive(Inst.AllowsHalfMillimetre);

            showMeButton.gameObject.SetActive(ShowMeAllowed && ready && !accepted);
            SetLabel(showMeButton, Inst.ShowMeActive ? "Hide" : CaliperTexts_Azm.ShowMeButton);
            checkButton.gameObject.SetActive(!accepted);
            continueButton.gameObject.SetActive(accepted);
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = text;
        }

        private static void SetTextRole(TMP_Text text, ThemeColor_Azm role)
        {
            var themed = text.GetComponent<ThemedText_Azm>();
            if (themed != null && themed.role != role) themed.SetRole(role);
        }

        private static void SetFieldActive(Button field, TMP_Text value, bool active)
        {
            var image = field.GetComponent<ThemedImage_Azm>();
            if (image != null) image.SetRole(active ? ThemeColor_Azm.Primary : ThemeColor_Azm.Secondary);
            SetTextRole(value, active ? ThemeColor_Azm.OnPrimary : ThemeColor_Azm.TextPrimary);
        }
    }
}
