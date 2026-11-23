using System.Collections.Generic;
using UnityEngine;

/*
 * Layer: Practical logic
 * Purpose: The pendulum experiment's state machine. Owns the measured bob radius, the current
 * trial's length/angle/prediction, the tap-based oscillation counter for BOTH timing runs, and
 * turns a completed pair of runs into a logged ITrialData. Deliberately has NO knowledge of
 * mouse/touch/gaze, cameras, or UI widgets: it only receives (a) drag deltas already decomposed
 * into vertical/horizontal axes from DraggableBobHandle, (b) a caliper reading from the vernier
 * caliper apparatus, and (c) discrete Confirm/Start/Tap/Next calls from UI Buttons. This
 * separation is what lets VR input be swapped in later without any changes here.
 *
 * Stage 1 additions over v2 (see TASK_system_architecture.md, Stage 1.1):
 * - EFFECTIVE LENGTH: the syllabus procedure measures the string length ell from the pivot to
 *   the TOP of the bob, then adds the bob's radius r (from a vernier-caliper reading) to get the
 *   quantity that actually drives the physics, L = ell + r. ell is still set by dragging, exactly
 *   as before; r is measured ONCE per session (the bob never changes size) via a new
 *   "MeasuringDiameter" step before the first trial, and reused for every subsequent trial's L.
 * - TWO TIMING RUNS PER LENGTH: the student times 20 oscillations twice (t1, t2) for the same
 *   length/angle, and the app averages them (mean t) before computing T = mean t / n. This is
 *   why a "RunComplete" step now sits between the two Swinging phases - the state machine simply
 *   returns to Swinging a second time with the same release angle/length rather than restarting
 *   the whole per-length setup.
 * - TAP-PHASE CAPTURE: each tap records how close the bob was to the mean (equilibrium) position
 *   at that instant, as a fraction of the release amplitude (0 = exactly at the mean position,
 *   1 = at a swing extreme). This is pure data capture for now - Stage 3's teaching-mode hints
 *   ("start at the mean position...") will read LastRunTapPhaseFractions; Stage 1 does not fire
 *   any feedback from it.
 * - RELEASE FROM REST: the syllabus requires the bob be released from rest, not pushed. This is
 *   already structurally guaranteed here rather than something that needed new code: the swing
 *   is pure closed-form SHM starting at t=0 from the release angle with zero velocity (see
 *   PendulumPhysicsModel) and there is no input path that could impart extra velocity at
 *   release - so "released from rest" is an invariant of this design, not a runtime check.
 *
 * Talks to: reads drag axes from a DraggableBobHandle (interaction layer), reads a caliper
 * reading from the vernier-caliper apparatus (Practicals/Pendulum/Apparatus), uses
 * PendulumPhysicsModel for the swing math, logs finished trials into a TrialLoopController
 * (trial-loop layer) as PendulumTrialData. Raises StepChanged/ValidationFailed/
 * AngleLimitExceeded/OscillationTapped for the UI layer and exposes CurrentAngleDegrees/
 * CurrentLengthMeters/RadiusMeters/CurrentSwingAngleDegrees/TapCount/LastTrialPrediction for
 * presentation scripts to read each frame or on event.
 */
public enum PendulumStep
{
    MeasuringDiameter,
    SettingLength,
    Predicting,
    SettingAngle,
    ReadyToRelease,
    Swinging,
    RunComplete,
    Stopped,
    Finished,
    Reflecting
}

// Student's prediction of how the period T responds as the string length L increases. Recorded
// before the swing so the later reveal (see PredictionPromptUI) is an honest before/after check,
// not something answered with hindsight.
public enum PredictionChoice
{
    Up,
    Down,
    Same
}

public class PendulumExperimentController : MonoBehaviour
{
    [Header("Dependencies")]
    public DraggableBobHandle bobHandle;
    public TrialLoopController trialLoop;

    [Header("Physics (true value the student must recover experimentally)")]
    public float gravity = 9.8f;

    [Header("Bob (vernier caliper measurement target)")]
    [Tooltip("TRUE physical diameter of the bob in metres - ground truth the student's caliper reading is checked against. 2cm, per a typical Matric/FSc pendulum bob - confirm against the real lab kit if this changes.")]
    public float bobDiameterMeters = 0.02f;
    [Tooltip("Vernier caliper least count (smallest reliably-readable division) in metres. 0.01cm is the standard least count for this class of caliper - a reading is accepted within one least count of the true diameter.")]
    public float caliperLeastCountMeters = 0.0001f;

    [Header("Length setup")]
    public float startingLengthMeters = 0.6f;
    public float minLengthMeters = 0.2f;
    public float maxLengthMeters = 1.0f;
    [Tooltip("Meters of length change per pixel of vertical drag.")]
    public float lengthMetersPerPixel = 0.002f;

    [Header("Angle setup")]
    [Tooltip("Small-angle approximation limit in degrees - matches the real-lab assumption the SHM formula relies on.")]
    public float maxAngleDegrees = 15f;
    [Tooltip("Smallest release angle allowed, so a trial always produces a visible, measurable swing.")]
    public float minAngleDegrees = 3f;
    [Tooltip("Degrees of angle change per pixel of horizontal drag.")]
    public float angleDegreesPerPixel = 0.15f;

    [Header("Timing")]
    [Tooltip("Number of oscillations the student taps through before each run auto-finalizes. 20 is a standard lab convention that keeps timing error small without making a run too slow for a demo.")]
    public int oscillationCount = 20;

    public event System.Action<PendulumStep> StepChanged;
    public event System.Action<string> ValidationFailed;
    // Fired specifically (alongside ValidationFailed) when the angle-limit is exceeded, so the UI
    // can show a longer one-time explanation without having to pattern-match message text.
    public event System.Action AngleLimitExceeded;
    public event System.Action<int> OscillationTapped;

    public PendulumStep CurrentStep { get; private set; } = PendulumStep.MeasuringDiameter;
    public float CurrentLengthMeters { get; private set; }
    public float CurrentAngleDegrees { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public int TapCount { get; private set; }

    // r: bob radius accepted from the student's caliper reading (see SubmitCaliperReading).
    // Zero/unset until MeasuringDiameter is passed - nothing downstream reads this before then.
    public float RadiusMeters { get; private set; }

    // L = ell + r, the quantity PendulumPhysicsModel actually integrates/evaluates. Live during
    // SettingLength (r is already fixed for the session by that point); frozen into
    // releaseEffectiveLengthMeters at BeginRun for the swing itself.
    public float EffectiveLengthMeters => CurrentLengthMeters + RadiusMeters;

    // The prediction recorded for whichever trial was most recently logged - read by the UI right
    // after TrialLoopController.TrialAdded fires, to reveal predicted-vs-actual.
    public PredictionChoice? LastTrialPrediction { get; private set; }
    private PredictionChoice? currentPrediction;

    // The prediction for the trial currently in progress (set the moment the student taps
    // Up/Down/Same, well before the trial resolves). Exposed so the UI can immediately
    // acknowledge the choice was registered, rather than the student only finding out via the
    // reveal text after the whole swing/tap sequence completes.
    public PredictionChoice? PendingPrediction => currentPrediction;

    // Which timing run (1 or 2) is either about to start or currently in progress.
    public int CurrentRunIndex { get; private set; } = 1;

    // Tap-phase fractions captured during the run that just finished: for each tap, how far the
    // bob was from the mean position at that instant, as a fraction of the release amplitude
    // (0 = mean position, 1 = an extreme). Stage 3 hints ("start at the mean position") will read
    // this; Stage 1 only captures it.
    public IReadOnlyList<float> LastRunTapPhaseFractions { get; private set; } = new List<float>();
    private readonly List<float> currentRunTapPhaseFractions = new List<float>();

    // Amplitude/effective-length the swing was actually released at - frozen at BeginRun so the
    // SHM formula always uses the values the student committed to for BOTH runs of this trial,
    // even though CurrentAngleDegrees/CurrentLengthMeters are not updated while swinging.
    private float releaseAngleDegrees;
    private float releaseEffectiveLengthMeters;
    private float swingElapsed;
    private bool hasWarnedAngleLimit;

    // The two timing-run results for the trial currently in progress, set as each run finishes.
    private float time1Seconds;
    private float time2Seconds;

    // Current angle of the bob mid-swing, derived from the SHM model. Only meaningful while
    // CurrentStep == Swinging; PendulumVisualPresenter checks the step before reading this.
    public float CurrentSwingAngleDegrees =>
        PendulumPhysicsModel.AngleAtTime(releaseAngleDegrees, releaseEffectiveLengthMeters, gravity, swingElapsed);

    void Awake()
    {
        CurrentLengthMeters = startingLengthMeters;
        CurrentAngleDegrees = 0f;
    }

    void OnEnable()
    {
        if (bobHandle != null)
            bobHandle.DragAxes += HandleDragAxes;
    }

    void OnDisable()
    {
        if (bobHandle != null)
            bobHandle.DragAxes -= HandleDragAxes;
    }

    void Update()
    {
        if (CurrentStep == PendulumStep.Swinging)
        {
            swingElapsed += Time.deltaTime;
            ElapsedSeconds = swingElapsed;
        }
    }

    // Called by the vernier-caliper apparatus once the student commits a reading (see
    // Practicals/Pendulum/Apparatus/VernierCaliperReadingUI). Validated here, not in the
    // apparatus script, so the acceptance tolerance has one home alongside the ground-truth
    // value it is checked against.
    public void SubmitCaliperReading(float diameterMetersRead)
    {
        if (CurrentStep != PendulumStep.MeasuringDiameter) return;

        float error = Mathf.Abs(diameterMetersRead - bobDiameterMeters);
        if (error > caliperLeastCountMeters)
        {
            ValidationFailed?.Invoke(
                $"Reading not accepted - re-check the main scale and vernier alignment (off by {(error * 1000f):F2} mm).");
            return;
        }

        // Use the STUDENT's accepted reading (not the hidden true value) for every downstream L
        // calculation - matches real lab practice, where a student's own recorded measurement
        // feeds their own analysis, small reading error and all.
        RadiusMeters = diameterMetersRead * 0.5f;
        SetStep(PendulumStep.SettingLength);
    }

    // Called by DraggableBobHandle while the bob is held and dragged. The same physical drag
    // means different things depending on which step the experiment is in - that
    // interpretation is exactly why this logic must live here rather than in the interaction
    // layer.
    private void HandleDragAxes(float verticalPixels, float horizontalPixels)
    {
        switch (CurrentStep)
        {
            case PendulumStep.SettingLength:
                ApplyLengthDrag(verticalPixels);
                break;
            case PendulumStep.SettingAngle:
                ApplyAngleDrag(horizontalPixels);
                break;
            default:
                // Dragging the bob outside these two steps (e.g. mid-swing) has no effect.
                break;
        }
    }

    private void ApplyLengthDrag(float verticalPixels)
    {
        // Dragging down (negative screen delta) lengthens the string, matching pulling the bob
        // further from the pivot; dragging up shortens it. This drags ell (pivot-to-top-of-bob),
        // not the effective length L - see EffectiveLengthMeters for the derived quantity.
        float newLength = CurrentLengthMeters - (verticalPixels * lengthMetersPerPixel);
        CurrentLengthMeters = Mathf.Clamp(newLength, minLengthMeters, maxLengthMeters);
    }

    private void ApplyAngleDrag(float horizontalPixels)
    {
        float proposedAngle = CurrentAngleDegrees + (horizontalPixels * angleDegreesPerPixel);

        if (Mathf.Abs(proposedAngle) > maxAngleDegrees)
        {
            // Reject the increment entirely rather than silently clamping, per CLAUDE.md's
            // corrective-feedback requirement - the student needs to notice they overshot, not
            // just feel the drag "stop working". The visual angle-limit arc (see
            // AngleLimitArcPresenter) now shows this boundary live, so the routine message here
            // can stay short; a longer one-time explanation is shown separately by the UI on the
            // first AngleLimitExceeded of the session.
            if (!hasWarnedAngleLimit)
            {
                ValidationFailed?.Invoke($"Angle limit exceeded (max {maxAngleDegrees:F0}°).");
                AngleLimitExceeded?.Invoke();
                hasWarnedAngleLimit = true;
            }
            return;
        }

        hasWarnedAngleLimit = false;
        CurrentAngleDegrees = proposedAngle;
    }

    // --- Explicit step transitions, wired to UI Buttons' OnClick. Buttons go through Unity's
    // own device-agnostic EventSystem (mouse/touch/gaze-raycaster all produce the same OnClick),
    // so they intentionally do not go through the custom IPracticalInput path. ---

    public void ConfirmLength()
    {
        if (CurrentStep != PendulumStep.SettingLength) return;
        SetStep(PendulumStep.Predicting);
    }

    public void SubmitPrediction(PredictionChoice choice)
    {
        if (CurrentStep != PendulumStep.Predicting) return;
        currentPrediction = choice;
        SetStep(PendulumStep.SettingAngle);
    }

    public void ConfirmAngle()
    {
        if (CurrentStep != PendulumStep.SettingAngle) return;
        if (Mathf.Abs(CurrentAngleDegrees) < minAngleDegrees)
        {
            ValidationFailed?.Invoke($"Drag the bob sideways at least {minAngleDegrees:F0}° so the swing is measurable.");
            return;
        }
        SetStep(PendulumStep.ReadyToRelease);
    }

    public void StartSwing()
    {
        if (CurrentStep != PendulumStep.ReadyToRelease) return;
        CurrentRunIndex = 1;
        releaseAngleDegrees = CurrentAngleDegrees;
        releaseEffectiveLengthMeters = EffectiveLengthMeters;
        BeginRun();
    }

    // Called from the RunComplete step to start the trial's second timing run, at the SAME
    // length/angle as run 1 (the syllabus times the same setup twice and averages - it is not a
    // fresh trial with new parameters), per TASK_system_architecture.md Stage 1.1.
    public void StartSecondRun()
    {
        if (CurrentStep != PendulumStep.RunComplete) return;
        CurrentRunIndex = 2;
        BeginRun();
    }

    private void BeginRun()
    {
        swingElapsed = 0f;
        ElapsedSeconds = 0f;
        TapCount = 0;
        currentRunTapPhaseFractions.Clear();
        SetStep(PendulumStep.Swinging);
    }

    // Called once per oscillation the student counts (bob passing back through center). Tying
    // the logged N directly to the student's own taps - rather than a hidden separately-running
    // count - means what gets logged is exactly what the student experienced, per
    // TASK_pendulum_demo_v2.md item 4.
    public void TapOscillation()
    {
        if (CurrentStep != PendulumStep.Swinging) return;

        // Capture how close this tap was to the mean position BEFORE incrementing, while
        // CurrentSwingAngleDegrees still reflects the instant of the tap. releaseAngleDegrees is
        // never zero here (ConfirmAngle enforces minAngleDegrees > 0), so this division is safe.
        float phaseFraction = Mathf.Abs(CurrentSwingAngleDegrees) / Mathf.Abs(releaseAngleDegrees);
        currentRunTapPhaseFractions.Add(Mathf.Clamp01(phaseFraction));

        TapCount++;
        OscillationTapped?.Invoke(TapCount);

        if (TapCount >= oscillationCount)
            FinalizeSwing();
    }

    private void FinalizeSwing()
    {
        // Freeze the visual angle where the swing was stopped, so the bob doesn't visually jump
        // before the student presses the next button.
        CurrentAngleDegrees = CurrentSwingAngleDegrees;
        LastRunTapPhaseFractions = new List<float>(currentRunTapPhaseFractions);

        if (CurrentRunIndex == 1)
        {
            time1Seconds = swingElapsed;
            SetStep(PendulumStep.RunComplete);
            return;
        }

        time2Seconds = swingElapsed;
        LastTrialPrediction = currentPrediction;
        trialLoop.AddTrial(new PendulumTrialData(CurrentLengthMeters, RadiusMeters, oscillationCount, time1Seconds, time2Seconds));
        SetStep(PendulumStep.Stopped);
    }

    public void NextTrial()
    {
        if (CurrentStep != PendulumStep.Stopped) return;

        if (trialLoop.IsComplete)
        {
            SetStep(PendulumStep.Finished);
            return;
        }

        CurrentAngleDegrees = 0f;
        currentPrediction = null;
        SetStep(PendulumStep.SettingLength);
    }

    public void ContinueToReflection()
    {
        if (CurrentStep != PendulumStep.Finished) return;
        SetStep(PendulumStep.Reflecting);
    }

    private void SetStep(PendulumStep step)
    {
        CurrentStep = step;
        StepChanged?.Invoke(step);
    }
}
