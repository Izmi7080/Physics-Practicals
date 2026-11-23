using System;
using System.Collections.Generic;
using UnityEngine;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Instruments;

namespace VirtualLab.Azm.Cylinder
{
    public enum CylinderMessageKind_Azm { Info, Success, Warning, Error }

    /*
     * Layer: Logic (scene-local; no singleton, no DontDestroyOnLoad).
     * Purpose: "Vernier Calipers: Solid Cylinder" - Matric Physics Exp 1.1 (area of cross-section)
     * and Exp 1.2 (volume) as one practical, following the textbook procedure:
     *   1. Least count: LC = smallest main-scale division / number of vernier divisions = 0.1 mm.
     *   2. Zero error: close the jaws and read it with its sign; zero correction ZC = -(zero error).
     *   3. Diameter D: grip the cylinder, read M and n, three times (the cylinder is turned a
     *      little between readings).
     *   4. Length L: the same, three times, with the cylinder lengthwise.
     *   5. Results: corrected value = Y + ZC for every reading; mean D and mean L;
     *      r = D/2;  A = πr² (Exp 1.1);  V = πr²L (Exp 1.2).
     * The caliper measurement itself (jaw contact, checking a reading) is CaliperMeasurement_Azm;
     * this controller decides the sizes, the zero error, the order of the steps and the table.
     * The TRUE sizes and zero error come from CylinderConfig_Azm; the student's values come only
     * from what they read (each reading is accepted within the caliper's tolerance of what the
     * scales show), so the results are the student's own measurement (CLAUDE.md: no shortcut).
     * It never reads a device or touches UI: the UI calls intent methods and listens to events.
     * Talks to: CylinderConfig_Azm, CaliperMeasurement_Azm, CylinderReading_Azm, CylinderTexts_Azm;
     * subscribes to AppManager_Azm.AppPauseChanged.
     */
    public class CylinderController_Azm : MonoBehaviour
    {
        public CylinderConfig_Azm config;
        public CaliperMeasurement_Azm caliper;

        // ----- events -----
        public event Action<int> StepChanged;               // also raised when a new reading starts
        public event Action ReadingsChanged;                 // table / header need refreshing
        public event Action<int> CalcLinesChanged;           // number of worked lines revealed
        public event Action<string, CylinderMessageKind_Azm> Message;
        public event Action<string, string> Hint;            // (hint id, text) - the HUD decides whether to show it
        public event Action<bool> PausedChanged;

        // ----- state -----
        private readonly List<CylinderReading_Azm> readings = new List<CylinderReading_Azm>();
        private readonly List<float> diameterSizesMm = new List<float>();
        private readonly List<float> lengthSizesMm = new List<float>();
        private int stepIndex;
        private int readingIndex;                 // 0-based reading within the Diameter / Length step
        private float trueZeroErrorMm;
        private bool leastCountKnown;
        private bool zeroErrorKnown;
        private float recordedZeroErrorMm;
        private int calcLinesRevealed;
        private bool paused;

        public const int CalcLineCount = 5;       // mean D, r, A, mean L, V

        public IReadOnlyList<CylinderReading_Azm> Readings => readings;
        public int StepIndex => stepIndex;
        public int StepCount => config.steps.Count;
        public CylinderStepDefinition_Azm CurrentStep => config.steps[stepIndex];
        public CylinderStepId_Azm CurrentStepId => config.steps[stepIndex].id;
        public int ReadingNumber => readingIndex + 1;
        public bool LeastCountKnown => leastCountKnown;
        public bool ZeroErrorKnown => zeroErrorKnown;
        public float RecordedZeroErrorMm => recordedZeroErrorMm;
        public float LeastCountMm => caliper.LeastCountMm;
        public int CalcLinesRevealed => calcLinesRevealed;
        public bool IsPaused => paused;

        // The step instruction with the reading number filled in.
        public string CurrentInstruction => string.Format(CurrentStep.instruction, ReadingNumber, config.readingsPerQuantity);

        // ----- lifecycle -----

        private void Awake()
        {
            float lc = caliper.LeastCountMm;
            trueZeroErrorMm = PickZeroErrorLeastCounts() * lc;

            // Each reading measures a slightly different place on the cylinder: true size ± a few
            // least counts (always a whole number of least counts, so a vernier line coincides).
            var random = new System.Random();
            for (int i = 0; i < config.readingsPerQuantity; i++)
            {
                diameterSizesMm.Add(config.trueDiameterCm * 10f + random.Next(-config.variationLeastCounts, config.variationLeastCounts + 1) * lc);
                lengthSizesMm.Add(config.trueLengthCm * 10f + random.Next(-config.variationLeastCounts, config.variationLeastCounts + 1) * lc);
            }
        }

        private int PickZeroErrorLeastCounts()
        {
            switch (config.zeroErrorSetting)
            {
                case ZeroErrorSetting_Azm.Fixed: return config.fixedZeroErrorLeastCounts;
                case ZeroErrorSetting_Azm.Random:
                    int size = UnityEngine.Random.Range(1, Mathf.Max(1, config.randomZeroErrorMaxLeastCounts) + 1);
                    return UnityEngine.Random.value < 0.5f ? -size : size;
                default: return 0;
            }
        }

        private void OnEnable()
        {
            if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.AppPauseChanged += OnAppPause;
            caliper.ReadingChecked += OnCaliperReading;
        }

        private void OnDisable()
        {
            if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.AppPauseChanged -= OnAppPause;
            caliper.ReadingChecked -= OnCaliperReading;
        }

        private void Start()
        {
            caliper.SetZeroError(trueZeroErrorMm);
            // The least-count step shows the scales with the jaws a little open; the student then
            // closes them for the zero error, so both steps use one zero-error measurement.
            caliper.Begin(0f, CaliperMode_Azm.ZeroError);
            stepIndex = 0;
            StepChanged?.Invoke(stepIndex);
            ReadingsChanged?.Invoke();
            Hint?.Invoke("leastcount", CylinderTexts_Azm.HintLeastCount);
        }

        private void OnAppPause(bool appPaused)
        {
            if (appPaused) SetPaused(true);   // never auto-resume: the student chooses when to continue
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            caliper.SetInteractionEnabled(!paused);
            PausedChanged?.Invoke(paused);
        }

        private void GoTo(CylinderStepId_Azm id)
        {
            for (int i = 0; i < config.steps.Count; i++)
            {
                if (config.steps[i].id != id) continue;
                stepIndex = i;
                StepChanged?.Invoke(stepIndex);
                return;
            }
            Debug.LogError($"[CylinderController_Azm] Step {id} is missing from the config.");
        }

        // ----- Step 1: least count -----

        public void AnswerLeastCount(int choiceIndex)
        {
            if (paused || CurrentStepId != CylinderStepId_Azm.LeastCount) return;
            float chosen = config.leastCountChoicesMm[choiceIndex];
            float lc = caliper.LeastCountMm;
            if (Mathf.Abs(chosen - lc) < 1e-5f)
            {
                leastCountKnown = true;
                Message?.Invoke(CylinderTexts_Azm.LeastCountCorrect(lc), CylinderMessageKind_Azm.Success);
                ReadingsChanged?.Invoke();
                GoTo(CylinderStepId_Azm.ZeroError);
                Hint?.Invoke("closegently", CylinderTexts_Azm.HintCloseGently);
            }
            else
            {
                Message?.Invoke(CylinderTexts_Azm.LeastCountWrong, CylinderMessageKind_Azm.Error);
            }
        }

        // ----- Steps 2-4: caliper readings -----

        private void OnCaliperReading(CaliperReadingResult_Azm result, string message)
        {
            var id = CurrentStepId;
            if (id != CylinderStepId_Azm.ZeroError && id != CylinderStepId_Azm.Diameter && id != CylinderStepId_Azm.Length) return;

            if (result != CaliperReadingResult_Azm.Correct)
            {
                bool notReady = result == CaliperReadingResult_Azm.NotTouching || result == CaliperReadingResult_Azm.Incomplete;
                Message?.Invoke(message, notReady ? CylinderMessageKind_Azm.Warning : CylinderMessageKind_Azm.Error);
                return;
            }

            if (id == CylinderStepId_Azm.ZeroError)
            {
                zeroErrorKnown = true;
                recordedZeroErrorMm = caliper.AcceptedReadingMm;
                Message?.Invoke(message, CylinderMessageKind_Azm.Success);
                ReadingsChanged?.Invoke();
                return;
            }

            // A diameter or length reading: record the row exactly as the student read it
            // (AcceptedReadingMm = their M + n × LC; split back into M and n for the table).
            float readMm = caliper.AcceptedReadingMm;
            float lc = caliper.LeastCountMm;
            int steps = Mathf.RoundToInt(readMm / lc);
            int mainMm = Mathf.FloorToInt(steps * lc / caliper.config.mainDivisionMm + 1e-4f);
            int vernier = steps - Mathf.RoundToInt(mainMm / lc);
            var quantity = id == CylinderStepId_Azm.Diameter ? CylinderQuantity_Azm.Diameter : CylinderQuantity_Azm.Length;
            var row = new CylinderReading_Azm(quantity, ReadingNumber, mainMm, vernier, lc, recordedZeroErrorMm);
            readings.Add(row);
            Message?.Invoke(CylinderTexts_Azm.ReadingRecorded(row), CylinderMessageKind_Azm.Success);
            ReadingsChanged?.Invoke();
            if (readings.Count == 1) Hint?.Invoke("zerocorrection", CylinderTexts_Azm.HintZeroCorrection);
        }

        // "Continue" after a correct reading (from the keypad).
        public void ContinueAfterReading()
        {
            if (paused || !caliper.IsAccepted) return;
            switch (CurrentStepId)
            {
                case CylinderStepId_Azm.ZeroError:
                    StartQuantity(CylinderStepId_Azm.Diameter);
                    Hint?.Invoke("bothjaws", CylinderTexts_Azm.HintBothJaws);
                    break;
                case CylinderStepId_Azm.Diameter:
                case CylinderStepId_Azm.Length:
                    readingIndex++;
                    if (readingIndex < config.readingsPerQuantity)
                    {
                        Message?.Invoke(CylinderTexts_Azm.TurnedForNext, CylinderMessageKind_Azm.Info);
                        BeginReading();
                        StepChanged?.Invoke(stepIndex);   // the pill shows the new reading number
                    }
                    else if (CurrentStepId == CylinderStepId_Azm.Diameter)
                    {
                        StartQuantity(CylinderStepId_Azm.Length);
                        Message?.Invoke(CylinderTexts_Azm.LengthwiseNow, CylinderMessageKind_Azm.Info);
                    }
                    else
                    {
                        caliper.End();
                        calcLinesRevealed = 0;
                        GoTo(CylinderStepId_Azm.Calculate);
                        Message?.Invoke(CylinderTexts_Azm.AllReadingsDone, CylinderMessageKind_Azm.Info);
                        Hint?.Invoke("mean", CylinderTexts_Azm.HintMean);
                        CalcLinesChanged?.Invoke(calcLinesRevealed);
                    }
                    break;
            }
        }

        private void StartQuantity(CylinderStepId_Azm id)
        {
            readingIndex = 0;
            // Set the step first so listeners (the cylinder's 3D placement) know the orientation.
            for (int i = 0; i < config.steps.Count; i++) if (config.steps[i].id == id) stepIndex = i;
            BeginReading();
            StepChanged?.Invoke(stepIndex);
        }

        private void BeginReading()
        {
            float size = CurrentStepId == CylinderStepId_Azm.Diameter ? diameterSizesMm[readingIndex] : lengthSizesMm[readingIndex];
            caliper.Begin(size, CaliperMode_Azm.Outside);
        }

        // ----- Step 5: results -----

        public float Mean(CylinderQuantity_Azm quantity)
        {
            float sum = 0f; int count = 0;
            foreach (var r in readings) if (r.Quantity == quantity) { sum += r.CorrectedCm; count++; }
            return count > 0 ? sum / count : 0f;
        }

        public float CorrectedValue(CylinderQuantity_Azm quantity, int number)
        {
            foreach (var r in readings) if (r.Quantity == quantity && r.Number == number) return r.CorrectedCm;
            return 0f;
        }

        public float MeanDiameterCm => Mean(CylinderQuantity_Azm.Diameter);
        public float MeanLengthCm => Mean(CylinderQuantity_Azm.Length);
        public float RadiusCm => MeanDiameterCm / 2f;                                      // r = D/2
        public float AreaCm2 => Mathf.PI * RadiusCm * RadiusCm;                            // A = πr²  (Exp 1.1)
        public float VolumeCm3 => AreaCm2 * MeanLengthCm;                                  // V = πr²L (Exp 1.2)
        public float TrueAreaCm2 => Mathf.PI * Mathf.Pow(config.trueDiameterCm / 2f, 2f);
        public float TrueVolumeCm3 => TrueAreaCm2 * config.trueLengthCm;

        // Learn mode reveals the worked calculation one line per tap; Perform shows it all at once.
        public void RevealNextCalcLine()
        {
            if (paused || CurrentStepId != CylinderStepId_Azm.Calculate || calcLinesRevealed >= CalcLineCount) return;
            calcLinesRevealed++;
            CalcLinesChanged?.Invoke(calcLinesRevealed);
        }

        public void RevealAllCalcLines()
        {
            if (CurrentStepId != CylinderStepId_Azm.Calculate) return;
            calcLinesRevealed = CalcLineCount;
            CalcLinesChanged?.Invoke(calcLinesRevealed);
        }

        public void ShowResults()
        {
            if (paused || CurrentStepId != CylinderStepId_Azm.Calculate || calcLinesRevealed < CalcLineCount) return;
            GoTo(CylinderStepId_Azm.Results);
        }
    }
}
