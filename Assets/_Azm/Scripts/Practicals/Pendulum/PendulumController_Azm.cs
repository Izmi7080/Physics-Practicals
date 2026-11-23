using System;
using System.Collections.Generic;
using UnityEngine;
using VirtualLab.Azm.Core;
using VirtualLab.Azm.Instruments;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: Logic (scene-local; no singleton, no DontDestroyOnLoad).
     * Purpose: The simple-pendulum experiment: the step sequence, the thread length and release
     * angle the student sets, the validation of a release (angle limit, "push"), the scripted swing,
     * the two timing runs, the observation table and the analysis. It follows the step list in
     * PendulumConfig_Azm (instruction, camera view, controls, drawer action per step), so the same
     * pattern can drive other practicals.
     *
     * It NEVER reads a device, a camera or a UI object. It is fed by intent calls from the
     * interaction layer (SetLengthCm, SetDisplacementDeg, ReleaseBob, Tap...) and from UI buttons
     * (ConfirmLength, NextLength, DiscardRow...), and it reports with C# events that the UI and the
     * camera controller subscribe to (CLAUDE.md three-layer rule).
     *
     * Simulation clock: the swing advances only while the practical is NOT paused (pause menu or the
     * app in the background), so a pause never changes a timing result.
     * Procedure source: Matric syllabus "Simple Pendulum Experiment Flow" (see Docs/). Numbers that
     * are not in the syllabus are marked ASSUMPTION in PendulumConfig_Azm.
     * Bob diameter (Stage 3): with config.useCaliperStep the first step is MeasureDiameter - the
     * student measures the bob with the vernier caliper (CaliperMeasurement_Azm) and the reading
     * they enter becomes d, so r = d/2 in the table and in L = ℓ + r is THEIR measured value. The
     * swing itself always uses the TRUE radius (config.bobDiameterCm / 2): the student's reading is
     * accepted only within one least count of it, and the simulation never depends on their answer.
     * With the flag off (fallback) the Intro step gives d instead.
     * Talks to: PendulumPhysics_Azm, TimingRun_Azm, PendulumAnalysis_Azm, TrialRow_Azm,
     * CaliperMeasurement_Azm; subscribes to AppManager_Azm.AppPauseChanged.
     */
    public class PendulumController_Azm : MonoBehaviour
    {
        public PendulumConfig_Azm config;
        [Tooltip("The caliper measurement for the MeasureDiameter step (optional when useCaliperStep is off).")]
        public CaliperMeasurement_Azm caliper;

        // ----- events (the UI / camera / rig view subscribe) -----
        public event Action<int> StepChanged;                 // new step index
        public event Action LengthChanged;
        public event Action AngleChanged;
        public event Action<int> TapCounted;                  // oscillations counted so far in this run
        public event Action RunStarted;
        public event Action<int, float> RunFinished;          // run index (1/2), seconds
        public event Action RunRestarted;
        public event Action<TrialRow_Azm> RowAdded;
        public event Action<TrialRow_Azm> RowChanged;
        public event Action AnalysisChanged;
        public event Action<string, MessageKind_Azm> Message;
        public event Action<bool> PausedChanged;
        // Learn-mode teaching moments (hint id, text). The controller only says WHEN a hint applies;
        // the HUD decides whether to show it (Learn mode, once per session, Hints setting).
        public event Action<string, string> Hint;

        // ----- state -----
        private readonly List<TrialRow_Azm> rows = new List<TrialRow_Azm>();
        private readonly List<StepDefinition_Azm> steps = new List<StepDefinition_Azm>();   // the steps that run this session
        private int stepIndex;
        private float measuredDiameterCm = -1f;   // the student's caliper reading; < 0 = not measured yet
        private float lengthCm;                 // l: pivot to top of the bob
        private float displacementDeg;          // where the student has pulled the bob
        private float returnVelocity;           // SmoothDamp helper for the bob returning to rest
        private bool bobHeld;

        private bool swinging;
        private float amplitudeDeg;
        private float effectiveLengthM;
        private float simTime;
        private TimingRun_Azm run;
        private int runIndex = 1;
        private float time1;
        private bool rowSuspect;
        private bool paused;
        private float lastRunSeconds;
        private bool hadFit;   // for the "before the first graph" hint

        public IReadOnlyList<TrialRow_Azm> Rows => rows;
        public PendulumAnalysisResult_Azm Analysis { get; private set; }

        public int StepIndex => stepIndex;
        public int StepCount => steps.Count;
        public StepDefinition_Azm CurrentStep => steps[stepIndex];
        public StepId_Azm CurrentStepId => steps[stepIndex].id;
        public bool UsesCaliper => config.useCaliperStep && caliper != null;

        public float LengthCm => lengthCm;
        // d and r as RECORDED by the student (their caliper reading, or the given value in the fallback).
        public bool HasDiameter => !UsesCaliper || measuredDiameterCm > 0f;
        public float DiameterCm => measuredDiameterCm > 0f ? measuredDiameterCm : config.bobDiameterCm;
        public float RadiusCm => DiameterCm * 0.5f;
        public float EffectiveLengthCm => lengthCm + RadiusCm;
        // The bob's TRUE radius: the 3D bob, the drag mapping and the physics use this.
        public float TrueRadiusCm => config.bobDiameterCm * 0.5f;
        public float DisplacementDeg => displacementDeg;
        public bool BobHeld => bobHeld;
        public bool IsSwinging => swinging;
        public bool IsPaused => paused;
        public int RunIndex => runIndex;
        public int RunCount => run != null ? run.Count : 0;
        public bool RunStartedFlag => run != null && run.Started;
        public int ValidRowCount => Analysis.validCount;
        public bool CanFinish => ValidRowCount >= config.minimumLengths;
        public float SimTime => simTime;
        public float LastRunSeconds => lastRunSeconds;

        // Seconds on the stopwatch for the run in progress (or the last finished run).
        public float StopwatchSeconds => run != null && run.Started ? run.Elapsed(simTime) : (swinging ? 0f : lastRunSeconds);

        // What the bob interaction should do with a drag right now.
        public DragMode_Azm DragMode
        {
            get
            {
                if (paused) return DragMode_Azm.None;
                switch (CurrentStepId)
                {
                    case StepId_Azm.SetLength: return DragMode_Azm.Length;
                    case StepId_Azm.Release: return swinging ? DragMode_Azm.None : DragMode_Azm.Angle;
                    default: return DragMode_Azm.None;
                }
            }
        }

        // The angle the rig should DRAW this frame: the real swing while swinging, otherwise where
        // the student has pulled the bob.
        public float DisplayAngleDeg =>
            swinging ? PendulumPhysics_Azm.AngleDeg(amplitudeDeg, effectiveLengthM, config.trueGravity, simTime) : displacementDeg;

        // ----- lifecycle -----

        private void Awake()
        {
            lengthCm = config.startingLengthCm;
            Analysis = PendulumAnalysis_Azm.Analyse(rows, config);

            // Exactly one way of getting d runs: measured with the caliper, or given (fallback flag).
            foreach (var step in config.steps)
            {
                if (step.id == StepId_Azm.Intro && UsesCaliper) continue;
                if (step.id == StepId_Azm.MeasureDiameter && !UsesCaliper) continue;
                steps.Add(step);
            }
            if (config.useCaliperStep && caliper == null)
                Debug.LogWarning("[PendulumController_Azm] useCaliperStep is on but no caliper is assigned - falling back to the given diameter.", this);
        }

        private void OnEnable()
        {
            if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.AppPauseChanged += OnAppPause;
            if (caliper != null) caliper.ReadingChecked += OnCaliperReading;
        }

        private void OnDisable()
        {
            if (AppManager_Azm.Instance != null) AppManager_Azm.Instance.AppPauseChanged -= OnAppPause;
            if (caliper != null) caliper.ReadingChecked -= OnCaliperReading;
        }

        private void Start()
        {
            stepIndex = 0;
            if (CurrentStepId == StepId_Azm.MeasureDiameter) caliper.Begin(config.bobDiameterCm * 10f);
            StepChanged?.Invoke(stepIndex);
            AnalysisChanged?.Invoke();
        }

        private void Update()
        {
            if (paused) return;

            if (swinging)
                simTime += Time.deltaTime;
            else if (!bobHeld && Mathf.Abs(displacementDeg) > 0.01f)
            {
                // The bob eases back to rest after a rejected or cancelled release.
                displacementDeg = Mathf.SmoothDamp(displacementDeg, 0f, ref returnVelocity, 0.25f);
                AngleChanged?.Invoke();
            }
        }

        private void OnAppPause(bool appPaused)
        {
            if (appPaused) SetPaused(true);   // never auto-resume: the student chooses when to continue
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            if (caliper != null) caliper.SetInteractionEnabled(!paused);
            PausedChanged?.Invoke(paused);
        }

        // ----- step navigation -----

        private void GoTo(StepId_Azm id)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].id != id) continue;
                stepIndex = i;
                StepChanged?.Invoke(stepIndex);
                // Teaching moment: the first time the student sets a length, remind them what ℓ is
                // and that r is added (the most common mistake in this practical).
                if (id == StepId_Azm.SetLength) Hint?.Invoke("length", PendulumTexts_Azm.HintLength);
                return;
            }
            Debug.LogError($"[PendulumController_Azm] Step {id} is missing from the config.");
        }

        public void ConfirmIntro()
        {
            if (CurrentStepId == StepId_Azm.Intro) GoTo(StepId_Azm.SetLength);
        }

        // ----- Step: measure the bob's diameter (syllabus procedure step 2) -----

        // Feedback for every reading the student checks; a correct one becomes d.
        private void OnCaliperReading(CaliperReadingResult_Azm result, string message)
        {
            if (CurrentStepId != StepId_Azm.MeasureDiameter) return;
            if (result == CaliperReadingResult_Azm.Correct)
            {
                measuredDiameterCm = caliper.AcceptedReadingMm / 10f;   // mm -> cm
                // No Message here: the reading keypad already shows the caliper's own "Correct!" line
                // (HCI restructure: the same text twice on screen was finding-level noise).
                AnalysisChanged?.Invoke();                              // the table header shows the new d and r
            }
            else
            {
                Message?.Invoke(message, result == CaliperReadingResult_Azm.NotTouching || result == CaliperReadingResult_Azm.Incomplete
                    ? MessageKind_Azm.Warning : MessageKind_Azm.Error);
            }
        }

        // Continue after a correct reading: r = d/2 is shown, then the first length.
        public void ConfirmDiameter()
        {
            if (CurrentStepId != StepId_Azm.MeasureDiameter || caliper == null || !caliper.IsAccepted) return;
            caliper.End();
            Message?.Invoke(PendulumTexts_Azm.DiameterRecorded(DiameterCm, RadiusCm), MessageKind_Azm.Info);
            GoTo(StepId_Azm.SetLength);
        }

        // ----- Step: set the length l -----

        // Called by the bob-drag interaction with the length the finger is pointing at.
        public void SetLengthCm(float cm)
        {
            if (CurrentStepId != StepId_Azm.SetLength) return;
            float snapped = Mathf.Round(cm / config.lengthResolutionCm) * config.lengthResolutionCm;
            snapped = Mathf.Clamp(snapped, config.minLengthCm, config.maxLengthCm);
            if (Mathf.Approximately(snapped, lengthCm)) return;
            lengthCm = snapped;
            LengthChanged?.Invoke();
        }

        public void ConfirmLength()
        {
            if (CurrentStepId != StepId_Azm.SetLength) return;

            // Lengths must differ enough to give separate points on the graph.
            foreach (var row in rows)
            {
                if (row.Discarded) continue;
                if (Mathf.Abs(row.LengthCm - lengthCm) < config.minLengthSeparationCm)
                {
                    Message?.Invoke(PendulumTexts_Azm.LengthTooClose(row.Number, config.minLengthSeparationCm), MessageKind_Azm.Error);
                    return;
                }
            }

            Message?.Invoke(PendulumTexts_Azm.LengthConfirmed(lengthCm, RadiusCm), MessageKind_Azm.Info);
            displacementDeg = 0f;
            GoTo(StepId_Azm.Release);
        }

        // ----- Step: displace and release -----

        // Live angle while the finger drags the bob sideways. Allowed beyond the limit (up to
        // maxDragAngleDeg) so the student SEES the red zone before crossing it.
        public void SetDisplacementDeg(float deg)
        {
            if (DragMode != DragMode_Azm.Angle) return;
            displacementDeg = Mathf.Clamp(deg, -config.maxDragAngleDeg, config.maxDragAngleDeg);
            returnVelocity = 0f;
            AngleChanged?.Invoke();
        }

        public void SetBobHeld(bool held)
        {
            bobHeld = held;
            if (held) returnVelocity = 0f;
        }

        // Lifting the finger releases the bob. releaseSpeedDegPerSec is how fast the bob was still
        // moving at that moment; a release that is not "from rest" is a push.
        public void ReleaseBob(float releaseSpeedDegPerSec)
        {
            bobHeld = false;
            if (DragMode != DragMode_Azm.Angle) return;

            float absAngle = Mathf.Abs(displacementDeg);
            if (absAngle < config.minReleaseAngleDeg)
            {
                Message?.Invoke(PendulumTexts_Azm.AngleTooSmall(config.minReleaseAngleDeg), MessageKind_Azm.Warning);
                return; // Update() eases the bob back to rest
            }
            if (absAngle > config.maxReleaseAngleDeg)
            {
                Message?.Invoke(PendulumTexts_Azm.AngleTooLarge(config.maxReleaseAngleDeg), MessageKind_Azm.Error);
                Hint?.Invoke("angle", PendulumTexts_Azm.HintAngle);
                return;
            }
            if (Mathf.Abs(releaseSpeedDegPerSec) > config.pushSpeedThresholdDegPerSec)
            {
                Message?.Invoke(PendulumTexts_Azm.PushedRelease, MessageKind_Azm.Error);
                Hint?.Invoke("push", PendulumTexts_Azm.HintPush);
                return;
            }

            StartSwing(displacementDeg);
        }

        // A gesture that was interrupted (second finger etc.): just let the bob settle back.
        public void CancelDrag()
        {
            bobHeld = false;
        }

        private void StartSwing(float angleDeg)
        {
            amplitudeDeg = angleDeg;
            // L = l + r with the bob's TRUE radius, converted to metres for the physics. (The table's L
            // uses the student's measured r, which differs from the true one by at most one least count.)
            effectiveLengthM = (lengthCm + TrueRadiusCm) / 100f;
            simTime = 0f;
            swinging = true;
            runIndex = 1;
            rowSuspect = false;
            NewRun();
            GoTo(StepId_Azm.TimeRun1);
        }

        // ----- Steps: timing runs -----

        private void NewRun()
        {
            float truePeriod = PendulumPhysics_Azm.Period(effectiveLengthM, config.trueGravity);
            run = new TimingRun_Azm(config.oscillationCount, truePeriod, config);
        }

        // The big round button: the first tap starts the stopwatch, each following tap marks one
        // completed oscillation, and the n-th following tap stops it.
        public void Tap()
        {
            StepId_Azm id = CurrentStepId;
            if (paused || !swinging || (id != StepId_Azm.TimeRun1 && id != StepId_Azm.TimeRun2)) return;

            float angle = PendulumPhysics_Azm.AngleDeg(amplitudeDeg, effectiveLengthM, config.trueGravity, simTime);
            float fraction = Mathf.Abs(angle) / Mathf.Abs(amplitudeDeg);

            var outcome = run.Tap(simTime, fraction);
            // Teaching moment: several taps far from the centre in one run (ASSUMPTION: 2 is
            // "several"; the threshold of one tap being off-centre is offMeanAngleFraction).
            if (run.OffMeanTaps >= 2) Hint?.Invoke("offmean", PendulumTexts_Azm.HintOffMean);

            switch (outcome)
            {
                case TapOutcome_Azm.Started:
                    RunStarted?.Invoke();
                    TapCounted?.Invoke(0);
                    break;
                case TapOutcome_Azm.Counted:
                    TapCounted?.Invoke(run.Count);
                    break;
                case TapOutcome_Azm.HalfSwingDetected:
                    // One oscillation is a full swing: discard this run and start over.
                    Message?.Invoke(PendulumTexts_Azm.HalfSwing, MessageKind_Azm.Error);
                    Hint?.Invoke("halfswing", PendulumTexts_Azm.HintHalfSwing);
                    NewRun();
                    RunRestarted?.Invoke();
                    TapCounted?.Invoke(0);
                    break;
                case TapOutcome_Azm.Finished:
                    FinishRun();
                    break;
            }
        }

        public void RestartRun()
        {
            StepId_Azm id = CurrentStepId;
            if (!swinging || (id != StepId_Azm.TimeRun1 && id != StepId_Azm.TimeRun2)) return;
            NewRun();
            Message?.Invoke(PendulumTexts_Azm.RunRestarted, MessageKind_Azm.Info);
            RunRestarted?.Invoke();
            TapCounted?.Invoke(0);
        }

        private void FinishRun()
        {
            float seconds = run.ElapsedAtFinish;
            lastRunSeconds = seconds;
            if (run.MissedCount) rowSuspect = true;
            RunFinished?.Invoke(runIndex, seconds);
            Message?.Invoke(PendulumTexts_Azm.RunDone(runIndex, seconds), MessageKind_Azm.Info);

            if (runIndex == 1)
            {
                // Run 2 is at the SAME length and the SAME swing - the bob just keeps swinging.
                time1 = seconds;
                runIndex = 2;
                NewRun();
                GoTo(StepId_Azm.TimeRun2);
                Hint?.Invoke("run2", PendulumTexts_Azm.HintRun2);
                return;
            }

            AddRow(time1, seconds);
        }

        // ----- Step: record -----

        private void AddRow(float t1, float t2)
        {
            var row = new TrialRow_Azm(rows.Count + 1, lengthCm / 100f, RadiusCm / 100f, config.oscillationCount, t1, t2);
            if (rowSuspect)
            {
                row.Suspect = true;
                row.SuspectReason = PendulumTexts_Azm.SuspectRow;
            }
            rows.Add(row);

            // The swing has served its purpose: the bob settles back to rest.
            displacementDeg = PendulumPhysics_Azm.AngleDeg(amplitudeDeg, effectiveLengthM, config.trueGravity, simTime);
            swinging = false;
            run = null;

            Analysis = PendulumAnalysis_Azm.Analyse(rows, config);
            RowAdded?.Invoke(row);
            AnalysisChanged?.Invoke();
            GoTo(StepId_Azm.Record);
            // Teaching moment: the first time there is a graph line to look at.
            if (Analysis.hasFit && !hadFit) Hint?.Invoke("graph", PendulumTexts_Azm.HintGraph);
            hadFit |= Analysis.hasFit;
        }

        // Pause menu "Restart this length" (TASK 4.9): throws away any runs in progress for the
        // current length (the swing stops, nothing is recorded) and goes back to setting the length,
        // keeping the length the student chose. Rows already recorded are kept.
        public void RestartLength()
        {
            var id = CurrentStepId;
            if (id != StepId_Azm.Release && id != StepId_Azm.TimeRun1 && id != StepId_Azm.TimeRun2) return;
            swinging = false;
            run = null;
            runIndex = 1;
            rowSuspect = false;
            displacementDeg = 0f;
            returnVelocity = 0f;
            AngleChanged?.Invoke();
            Message?.Invoke(PendulumTexts_Azm.LengthRestarted, MessageKind_Azm.Info);
            GoTo(StepId_Azm.SetLength);
        }

        public bool CanRestartLength
        {
            get
            {
                var id = CurrentStepId;
                return id == StepId_Azm.Release || id == StepId_Azm.TimeRun1 || id == StepId_Azm.TimeRun2;
            }
        }

        public void NextLength()
        {
            if (CurrentStepId != StepId_Azm.Record) return;
            displacementDeg = 0f;
            GoTo(StepId_Azm.SetLength);
        }

        public void FinishAndAnalyse()
        {
            if (CurrentStepId != StepId_Azm.Record || !CanFinish) return;
            GoTo(StepId_Azm.Analyse);
        }

        public void ShowResults()
        {
            if (CurrentStepId == StepId_Azm.Analyse) GoTo(StepId_Azm.Results);
        }

        // ----- Discarding rows -----

        public void DiscardRow(int number)
        {
            foreach (var row in rows)
            {
                if (row.Number != number || row.Discarded) continue;
                row.Discarded = true;
                Analysis = PendulumAnalysis_Azm.Analyse(rows, config);
                RowChanged?.Invoke(row);
                AnalysisChanged?.Invoke();

                // Discarding can leave too few rows to analyse: go back and collect more.
                if ((CurrentStepId == StepId_Azm.Analyse || CurrentStepId == StepId_Azm.Results) && !CanFinish)
                {
                    Message?.Invoke(PendulumTexts_Azm.NeedMoreRows(ValidRowCount, config.minimumLengths), MessageKind_Azm.Warning);
                    GoTo(StepId_Azm.Record);
                }
                return;
            }
        }
    }
}
