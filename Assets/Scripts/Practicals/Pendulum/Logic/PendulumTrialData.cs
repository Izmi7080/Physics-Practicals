/*
 * Layer: Practical logic
 * Purpose: One completed pendulum trial (a single string length, timed twice and averaged, per
 * the Matric syllabus procedure in TASK_system_architecture.md Stage 1.1/1.2). Implements the
 * generic ITrialData contract so the shared TrialLoopController/TrialDataTableUI can display it
 * without any pendulum-specific code.
 *
 * Column set matches the syllabus observation table exactly:
 *   Obs. No. | ell (cm) | r (cm) | L = ell+r (cm) | n | t1 (s) | t2 (s) | mean t (s) | T (s) | T^2 (s^2)
 * (Obs. No. is the trial number, added separately by TrialDataTableUI/TrialDataRowUI - not a
 * column here.) ell and r are entered/measured by the student; L, mean t, T and T^2 are always
 * computed by the system, never entered - per the syllabus, derived quantities are calculated,
 * not measured directly.
 *
 * Units: all fields are stored in METRES/SECONDS internally (SI, consistent with the rest of
 * the logic layer). The table displays centimetres for ell/r/L (ToRowStrings multiplies by 100)
 * - the g calculation in ResultsGraphUI/LiveGEstimateUI must keep using the metres-valued
 * properties (EffectiveLengthMeters), never the display strings, or the result is wrong by a
 * factor of 100 (an easy, hard-to-notice mistake - see CLAUDE.md's units warning).
 */
public class PendulumTrialData : ITrialData
{
    // ell: string length from pivot to the TOP of the bob, as set by the student.
    public readonly float StringLengthMeters;

    // r: bob radius, measured once per session via the vernier caliper (see
    // PendulumExperimentController.RadiusMeters) and reused for every trial - the bob itself
    // never changes size, so re-measuring it per trial would just be repeated busywork.
    public readonly float RadiusMeters;

    // L = ell + r: the quantity that actually drives the physics (see PendulumPhysicsModel).
    public readonly float EffectiveLengthMeters;

    // n: number of oscillations timed. Fixed at 20 for every trial (see
    // PendulumExperimentController.oscillationCount) but included as its own column because the
    // syllabus table lists it explicitly.
    public readonly int OscillationCount;

    // t1, t2: the two independent timing runs for this length. Averaging two runs instead of
    // trusting a single run is a core lab skill the syllabus specifically asks for - a single
    // mistimed run is far more likely to go unnoticed than a pair that disagree.
    public readonly float Time1Seconds;
    public readonly float Time2Seconds;

    public readonly float MeanTimeSeconds; // (t1 + t2) / 2
    public readonly float PeriodSeconds;   // T = mean t / n
    public readonly float PeriodSquared;   // T^2, the quantity plotted against L

    public PendulumTrialData(float stringLengthMeters, float radiusMeters, int oscillationCount,
        float time1Seconds, float time2Seconds)
    {
        StringLengthMeters = stringLengthMeters;
        RadiusMeters = radiusMeters;
        EffectiveLengthMeters = stringLengthMeters + radiusMeters;
        OscillationCount = oscillationCount;
        Time1Seconds = time1Seconds;
        Time2Seconds = time2Seconds;

        MeanTimeSeconds = (time1Seconds + time2Seconds) * 0.5f;
        PeriodSeconds = MeanTimeSeconds / oscillationCount;
        PeriodSquared = PeriodSeconds * PeriodSeconds;
    }

    public string[] ColumnLabels => new[]
    {
        "ℓ (cm)", "r (cm)", "L (cm)", "n", "t₁ (s)", "t₂ (s)", "mean t (s)", "T (s)", "T² (s²)"
    };

    public string[] ToRowStrings() => new[]
    {
        (StringLengthMeters * 100f).ToString("F1"),
        (RadiusMeters * 100f).ToString("F2"),
        (EffectiveLengthMeters * 100f).ToString("F2"),
        OscillationCount.ToString(),
        Time1Seconds.ToString("F2"),
        Time2Seconds.ToString("F2"),
        MeanTimeSeconds.ToString("F2"),
        PeriodSeconds.ToString("F3"),
        PeriodSquared.ToString("F3")
    };
}
