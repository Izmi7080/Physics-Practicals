using System;
using System.Collections.Generic;
using UnityEngine;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Instruments;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    public enum SGMessageKind_Azm { Info, Success, Warning, Error }

    /*
     * Layer: Logic (scene-local; no singleton, no DontDestroyOnLoad).
     * Purpose: "Screw Gauge: Thickness of a Metal Strip" - Matric Physics Exp 1.3, following the
     * textbook procedure:
     *   1. Pitch and least count: one full turn moves the spindle 0.5 mm (the pitch);
     *      LC = pitch / circular divisions = 0.5 mm / 50 = 0.01 mm.
     *   2. Zero error: close anvil and spindle with the ratchet until it clicks; circular 0 below the
     *      datum line = positive (+n × LC), above = negative (-(50 - n) × LC); ZC = -(zero error).
     *   3. Thickness: hold the strip between anvil and spindle, close gently with the ratchet, read M
     *      (mark just before the thimble edge) and n (division on the datum line): X = n × LC,
     *      Y = M + X, t = Y + ZC. Three readings at different places.
     *   4. Result: mean t = (t1 + t2 + t3) / 3.
     * The instrument itself (turning, ratchet, contact, checking a reading) is ScrewGaugeMeasurement_Azm;
     * this controller decides the true sizes, the zero error, the step order and the table. Each
     * reading is accepted only within the instrument's tolerance of what the scales show, so the
     * result is the student's own measurement (CLAUDE.md: no shortcut).
     * It never reads a device or touches UI: the UI calls intent methods and listens to events.
     * Talks to: ScrewGaugePracticalConfig_Azm, ScrewGaugeMeasurement_Azm, ScrewGaugeReading_Azm,
     * ScrewGaugePracticalTexts_Azm; subscribes to AppManager_Azm.AppPauseChanged.
     */
    public class ScrewGaugeController_Azm : MonoBehaviour
    {
        public ScrewGaugePracticalConfig_Azm config;
        public ScrewGaugeMeasurement_Azm gauge;

        public event Action<int> StepChanged;            // also raised when a new reading starts
        public event Action ReadingsChanged;
        public event Action<int> CalcLinesChanged;
        public event Action<string, SGMessageKind_Azm> Message;
        public event Action<string, string> Hint;        // (hint id, text) - the HUD decides whether to show it
        public event Action<bool> PausedChanged;
        public event Action PitchStateChanged;           // the one-turn demonstration started / finished
        public event Action RatchetClicked;              // for the click sound

        private readonly List<ScrewGaugeReading_Azm> readings = new List<ScrewGaugeReading_Azm>();
        private readonly List<float> sizesMm = new List<float>();
        private int stepIndex;
        private int readingIndex;
        private float trueZeroErrorMm;
        private bool leastCountKnown;
        private bool zeroErrorKnown;
        private float recordedZeroErrorMm;
        private int calcLinesRevealed;
        private bool paused;
        private bool pitchShown;
        private float pitchTurnLeftDeg;      // > 0 while the one-turn demonstration runs
        private float lastLockMessage = -10f;

        public IReadOnlyList<ScrewGaugeReading_Azm> Readings => readings;
        public int StepIndex => stepIndex;
        public int StepCount => config.steps.Count;
        public SGStepDefinition_Azm CurrentStep => config.steps[stepIndex];
        public SGStepId_Azm CurrentStepId => config.steps[stepIndex].id;
        public int ReadingNumber => readingIndex + 1;
        public bool LeastCountKnown => leastCountKnown;
        public bool ZeroErrorKnown => zeroErrorKnown;
        public float RecordedZeroErrorMm => recordedZeroErrorMm;
        public float LeastCountMm => gauge.LeastCountMm;
        public int CalcLinesRevealed => calcLinesRevealed;
        public int CalcLineCount => config.readings + 1;      // t1..tn, then the mean
        public bool IsPaused => paused;
        public bool PitchShown => pitchShown;
        public bool PitchTurning => pitchTurnLeftDeg > 0f;
        public string CurrentInstruction => string.Format(CurrentStep.instruction, ReadingNumber, config.readings);

        // ----- lifecycle -----

        private void Awake()
        {
            float lc = gauge.LeastCountMm;
            trueZeroErrorMm = PickZeroErrorLeastCounts() * lc;
            // Each place on the strip: true thickness ± a few least counts (a whole number of least
            // counts, so a circular division lies exactly on the datum line).
            var random = new System.Random();
            for (int i = 0; i < config.readings; i++)
                sizesMm.Add(config.trueThicknessMm + random.Next(-config.variationLeastCounts, config.variationLeastCounts + 1) * lc);
            gauge.objectName = config.ObjectName;
        }

        private int PickZeroErrorLeastCounts()
        {
            switch (config.zeroErrorSetting)
            {
                case SGZeroErrorSetting_Azm.Fixed: return config.fixedZeroErrorLeastCounts;
                case SGZeroErrorSetting_Azm.Random:
                    int size = UnityEngine.Random.Range(1, Mathf.Max(1, config.randomZeroErrorMaxLeastCounts) + 1);
                    return UnityEngine.Random.value < 0.5f ? -size : size;
                default: return 0;
            }
        }

        private void OnEnable()
        {
            if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.AppPauseChanged += OnAppPause;
            gauge.ReadingChecked += OnReading;
            gauge.Clicked += OnClicked;
            gauge.ContactByThimble += OnThimbleContact;
            gauge.TurnBlockedByLock += OnLockedTurn;
        }

        private void OnDisable()
        {
            if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.AppPauseChanged -= OnAppPause;
            gauge.ReadingChecked -= OnReading;
            gauge.Clicked -= OnClicked;
            gauge.ContactByThimble -= OnThimbleContact;
            gauge.TurnBlockedByLock -= OnLockedTurn;
        }

        private void Start()
        {
            gauge.SetZeroError(trueZeroErrorMm);
            // Pitch step and zero-error step share one "nothing between the jaws" measurement.
            gauge.Begin(0f, ScrewGaugeMode_Azm.ZeroError);
            stepIndex = 0;
            StepChanged?.Invoke(stepIndex);
            ReadingsChanged?.Invoke();
            Hint?.Invoke("pitch", ScrewGaugePracticalTexts_Azm.HintPitch);
        }

        private void Update()
        {
            if (paused || pitchTurnLeftDeg <= 0f) return;
            // The one-turn demonstration: the thimble turns exactly 360 degrees (opening by one pitch).
            float step = Mathf.Min(pitchTurnLeftDeg, 360f * Time.deltaTime / Mathf.Max(0.1f, config.pitchTurnSeconds));
            gauge.DemoTurnDeg(step);
            pitchTurnLeftDeg -= step;
            if (pitchTurnLeftDeg <= 0f)
            {
                pitchTurnLeftDeg = 0f;
                pitchShown = true;
                Message?.Invoke(ScrewGaugePracticalTexts_Azm.PitchShown(gauge.config.pitchMm), SGMessageKind_Azm.Info);
                PitchStateChanged?.Invoke();
            }
        }

        private void OnAppPause(bool appPaused)
        {
            if (appPaused) SetPaused(true);   // never auto-resume
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            gauge.SetInteractionEnabled(!paused);
            PausedChanged?.Invoke(paused);
        }

        private void GoTo(SGStepId_Azm id)
        {
            for (int i = 0; i < config.steps.Count; i++)
            {
                if (config.steps[i].id != id) continue;
                stepIndex = i;
                StepChanged?.Invoke(stepIndex);
                return;
            }
            Debug.LogError($"[ScrewGaugeController_Azm] Step {id} is missing from the config.");
        }

        // ----- Step 1: pitch and least count -----

        public void TurnOneTurn()
        {
            if (paused || CurrentStepId != SGStepId_Azm.LeastCount || PitchTurning) return;
            pitchTurnLeftDeg = 360f;
            PitchStateChanged?.Invoke();
        }

        public void AnswerLeastCount(int choiceIndex)
        {
            if (paused || CurrentStepId != SGStepId_Azm.LeastCount) return;
            if (!pitchShown) { Message?.Invoke(ScrewGaugePracticalTexts_Azm.TurnFirst, SGMessageKind_Azm.Warning); return; }
            float chosen = config.leastCountChoicesMm[choiceIndex];
            float lc = gauge.LeastCountMm;
            if (Mathf.Abs(chosen - lc) < 1e-5f)
            {
                leastCountKnown = true;
                Message?.Invoke(ScrewGaugePracticalTexts_Azm.LeastCountCorrect(gauge.config.pitchMm, gauge.config.divisions, lc), SGMessageKind_Azm.Success);
                ReadingsChanged?.Invoke();
                GoTo(SGStepId_Azm.ZeroError);
                Hint?.Invoke("zerosign", ScrewGaugePracticalTexts_Azm.HintZeroSign);
            }
            else Message?.Invoke(ScrewGaugePracticalTexts_Azm.LeastCountWrong, SGMessageKind_Azm.Error);
        }

        // ----- turning the gauge (from the HUD's hold buttons) -----

        public void HoldRatchet(float dt) { if (!paused) gauge.TurnRatchet(dt); }
        public void HoldOpen(float dt) { if (!paused) gauge.Open(dt); }

        public void ToggleLock()
        {
            if (paused) return;
            gauge.ToggleLock();
            Message?.Invoke(gauge.IsLocked ? ScrewGaugePracticalTexts_Azm.Locked : ScrewGaugePracticalTexts_Azm.Unlocked, SGMessageKind_Azm.Info);
        }

        private void OnClicked()
        {
            RatchetClicked?.Invoke();
            Hint?.Invoke("ratchet", ScrewGaugePracticalTexts_Azm.HintRatchet);
        }

        // Textbook caution: rotate gently using the ratchet; do not press the jaws very hard.
        private void OnThimbleContact() => Message?.Invoke(ScrewGaugeTexts_Azm.ThimbleContact, SGMessageKind_Azm.Warning);

        private void OnLockedTurn()
        {
            if (Time.unscaledTime - lastLockMessage < 3f) return;
            lastLockMessage = Time.unscaledTime;
            Message?.Invoke(ScrewGaugeTexts_Azm.Locked, SGMessageKind_Azm.Warning);
        }

        // ----- Steps 2-3: readings -----

        private void OnReading(CaliperReadingResult_Azm result, string message)
        {
            var id = CurrentStepId;
            if (id != SGStepId_Azm.ZeroError && id != SGStepId_Azm.Thickness) return;
            if (result != CaliperReadingResult_Azm.Correct)
            {
                bool notReady = result == CaliperReadingResult_Azm.NotTouching || result == CaliperReadingResult_Azm.Incomplete;
                Message?.Invoke(message, notReady ? SGMessageKind_Azm.Warning : SGMessageKind_Azm.Error);
                return;
            }

            if (id == SGStepId_Azm.ZeroError)
            {
                zeroErrorKnown = true;
                recordedZeroErrorMm = gauge.AcceptedReadingMm;
                Message?.Invoke(message, SGMessageKind_Azm.Success);
                ReadingsChanged?.Invoke();
                return;
            }

            // Record the row exactly as the student read it (AcceptedReadingMm = their M + n × LC).
            float lc = gauge.LeastCountMm;
            int steps = Mathf.RoundToInt(gauge.AcceptedReadingMm / lc);
            int turns = steps / gauge.config.divisions;
            var row = new ScrewGaugeReading_Azm(ReadingNumber, turns * gauge.config.pitchMm, steps - turns * gauge.config.divisions, lc, recordedZeroErrorMm);
            readings.Add(row);
            Message?.Invoke(ScrewGaugePracticalTexts_Azm.ReadingRecorded(row), SGMessageKind_Azm.Success);
            ReadingsChanged?.Invoke();
            if (readings.Count == 1) Hint?.Invoke("places", ScrewGaugePracticalTexts_Azm.HintPlaces);
        }

        // "Continue" after a correct reading (from the keypad).
        public void ContinueAfterReading()
        {
            if (paused || !gauge.IsAccepted) return;
            if (CurrentStepId == SGStepId_Azm.ZeroError)
            {
                readingIndex = 0;
                for (int i = 0; i < config.steps.Count; i++) if (config.steps[i].id == SGStepId_Azm.Thickness) stepIndex = i;
                BeginReading();
                StepChanged?.Invoke(stepIndex);
                Hint?.Invoke("flat", ScrewGaugePracticalTexts_Azm.HintFlat(config.ObjectName));
                return;
            }
            if (CurrentStepId != SGStepId_Azm.Thickness) return;

            readingIndex++;
            if (readingIndex < config.readings)
            {
                Message?.Invoke(ScrewGaugePracticalTexts_Azm.MovedForNext(config.ObjectName), SGMessageKind_Azm.Info);
                BeginReading();
                StepChanged?.Invoke(stepIndex);
                if (readingIndex == 1) Hint?.Invoke("backforth", ScrewGaugePracticalTexts_Azm.HintBackAndForth);
            }
            else
            {
                gauge.End();
                calcLinesRevealed = 0;
                GoTo(SGStepId_Azm.Calculate);
                Message?.Invoke(ScrewGaugePracticalTexts_Azm.AllReadingsDone, SGMessageKind_Azm.Info);
                CalcLinesChanged?.Invoke(calcLinesRevealed);
            }
        }

        private void BeginReading() => gauge.Begin(sizesMm[readingIndex], ScrewGaugeMode_Azm.Object);

        // ----- Step 4: result -----

        public float MeanThicknessMm
        {
            get
            {
                if (readings.Count == 0) return 0f;
                float sum = 0f;
                foreach (var r in readings) sum += r.CorrectedMm;
                return sum / readings.Count;     // t = (t1 + t2 + t3) / 3
            }
        }

        public float TrueThicknessMm => config.trueThicknessMm;

        public void RevealNextCalcLine()
        {
            if (paused || CurrentStepId != SGStepId_Azm.Calculate || calcLinesRevealed >= CalcLineCount) return;
            calcLinesRevealed++;
            CalcLinesChanged?.Invoke(calcLinesRevealed);
        }

        public void RevealAllCalcLines()
        {
            if (CurrentStepId != SGStepId_Azm.Calculate) return;
            calcLinesRevealed = CalcLineCount;
            CalcLinesChanged?.Invoke(calcLinesRevealed);
        }

        public void ShowResults()
        {
            if (paused || CurrentStepId != SGStepId_Azm.Calculate || calcLinesRevealed < CalcLineCount) return;
            GoTo(SGStepId_Azm.Results);
        }
    }
}
