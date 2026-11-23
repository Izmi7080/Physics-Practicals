using System.Globalization;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: Logic support (one place for the practical's user-facing messages).
     * Purpose: Keeps every feedback sentence of the pendulum in one file (CLAUDE.md: no text
     * scattered through scripts), so a teacher can review the wording and a later localisation has
     * a single place to start. Step instructions live in PendulumConfig_Azm.steps.
     * Talks to: read by PendulumController_Azm (which raises them as messages) and the UI.
     */
    public static class PendulumTexts_Azm
    {
        private static string F(float value, int decimals)
        {
            return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        // Stopwatch face text, "mm:ss.cc".
        public static string Stopwatch(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + rest.ToString("00.00", CultureInfo.InvariantCulture);
        }

        public const string LengthHint = "Hold the bob and drag it up or down";
        public const string ReleaseHint = "Hold the bob, drag it sideways, then lift your finger to let go";
        public const string TapToStart = "Tap at the centre to start";
        public const string TapToCount = "Tap after each full swing";

        public static string LengthConfirmed(float lengthCm, float radiusCm) =>
            $"L = ℓ + r = {F(lengthCm, 1)} + {F(radiusCm, 2)} = {F(lengthCm + radiusCm, 2)} cm";

        public static string DiameterRecorded(float diameterCm, float radiusCm) =>
            $"d = {F(diameterCm, 2)} cm, so r = d/2 = {F(radiusCm, 2)} cm. Now set the first length ℓ.";

        public static string LengthTooClose(int rowNumber, float separationCm) =>
            $"That length is within {F(separationCm, 0)} cm of row {rowNumber}. Choose a clearly different length so the graph gets a separate point.";

        public static string AngleTooSmall(float minDeg) =>
            $"Pull the bob out a little further - at least {F(minDeg, 0)}° - so the swing is easy to time.";

        public static string AngleTooLarge(float maxDeg) =>
            $"The angle was more than {F(maxDeg, 0)}°. Beyond that the swing is no longer simple harmonic motion, so T = 2π√(L/g) stops being accurate. Try a smaller angle.";

        public const string PushedRelease =
            "Release from rest - don't push. A push gives the bob extra energy and spoils the simple-pendulum assumption. Let go gently.";

        public const string HalfSwing =
            "You are tapping on every pass through the centre. One oscillation is a full swing there and back. This run was discarded - tap to start again.";

        public const string RunRestarted = "Run restarted - tap at the centre to begin.";

        public static string RunDone(int runIndex, float seconds) =>
            $"Run {runIndex}: t = {F(seconds, 2)} s for 20 oscillations";

        public const string SuspectRow =
            "A count may have been missed in this row (one interval was about twice as long as it should be). Check it, and discard the row if you think the timing was wrong.";

        public static string NeedMoreRows(int have, int need) =>
            $"Only {have} valid row{(have == 1 ? "" : "s")} left - add lengths until you have {need}.";

        public static string DiscardQuestion(int rowNumber) =>
            $"Discard row {rowNumber}? It will be removed from the graph and the g calculation.";

        public static string TrialChip(int validRows, int needed) =>
            validRows < needed ? $"Trial {validRows + 1} of {needed}" : $"{validRows} trials done";

        public static string GChip(float g) => $"g ≈ {F(g, 2)} m/s²";

        public const string OriginGood = "A straight line through the origin confirms that T² is proportional to L.";

        public static string OriginOff(float intercept) =>
            $"The line misses the origin (T² = {F(intercept, 3)} s² at L = 0). Check that you used L = ℓ + r and measured each length carefully.";

        // ----- Learn-mode hints (TASK_menus_and_pendulum 4.4; shown once per session) -----
        public const string HintLength = "Measure from the pivot to the top of the bob. The bob's radius is added separately: L = ℓ + r.";
        public const string HintAngle = "Beyond about 15° the pendulum no longer behaves as simple harmonic motion, so T = 2π√(L/g) stops being accurate.";
        public const string HintPush = "Release from rest. A push gives the bob extra energy and invalidates the simple-pendulum assumption.";
        public const string HintOffMean = "Start and stop at the centre. The bob moves fastest there, so the moment is easier to judge.";
        public const string HintHalfSwing = "One oscillation is a full swing there and back, returning to the centre in the same direction.";
        public const string HintRun2 = "Now time it again. Two runs averaged reduce the effect of a single mistimed run.";
        public const string HintGraph = "A straight line through the origin confirms that T² is proportional to L.";

        // ----- pause: restart this length -----
        public const string RestartLengthLabel = "Restart this length";
        public const string LengthRestarted = "Runs for this length discarded - set the length and release again.";
        public const string ExitQuestion = "Exit to the main menu? Your measurements in this practical will be lost.";
        public const string RestartQuestion = "Restart the whole practical? Every row will be lost.";

        // ----- prediction prompt (4.5): one tap, not scored -----
        public static string PredictionQuestion(bool longer) =>
            $"Your new length is {(longer ? "LONGER" : "SHORTER")} than the last one. Will the time period T be:";
        public const string PredictLarger = "Larger";
        public const string PredictSmaller = "Smaller";
        public const string PredictSame = "About the same";
        public static string PredictionResult(bool matched, string actual, float previousT, float newT) =>
            (matched ? "Your prediction was right: " : "Not quite: ") +
            $"T is {actual} ({F(previousT, 3)} s → {F(newT, 3)} s). A longer pendulum swings more slowly (T² is proportional to L).";

        // ----- oscillation demonstration (4.3) -----
        public const string DemoTitle = "What is ONE oscillation?";
        public static readonly string[] DemoCaptions =
        {
            "Start the stopwatch as the bob passes the centre.",
            "It swings out to one side...",
            "...back through the centre...",
            "...out to the other side...",
            "...and back to the centre, moving the same way: that is ONE oscillation. Tap here."
        };
        public const string DemoGotIt = "Got it";
        public const string DemoReplay = "What is 1 oscillation?";

        // ----- Calc tab (4.6): worked lines for one row -----
        public static string CalcRowTitle(int number) => $"Row {number}";
        public static string CalcRadius(float d, float r) => $"r = d/2 = {F(d, 2)}/2 = <b>{F(r, 2)} cm</b>";
        public static string CalcEffectiveLength(float l, float r, float L) => $"L = ℓ + r = {F(l, 1)} + {F(r, 2)} = <b>{F(L, 2)} cm</b>";
        public static string CalcMeanTime(float t1, float t2, float t) => $"mean t = (t₁ + t₂)/2 = ({F(t1, 2)} + {F(t2, 2)})/2 = <b>{F(t, 2)} s</b>";
        public static string CalcPeriod(float t, int n, float T) => $"T = t/n = {F(t, 2)}/{n} = <b>{F(T, 3)} s</b>";
        public static string CalcPeriodSquared(float T, float T2) => $"T² = {F(T, 3)}² = <b>{F(T2, 3)} s²</b>";
        public static string CalcG(float slope, float g) => $"Graph: g = 4π² / slope = 39.48 / {F(slope, 3)} = <b>{F(g, 2)} m/s²</b>";
        public const string CalcNoRows = "The worked calculation appears here once you have recorded a row.";
        public const string CalcNeedFit = "Graph: g appears once 2 valid rows with different lengths are recorded.";
        public const string CalcNextLine = "Show next line";
        public const string CalculatorButton = "Calculator";

        // ----- reflection (4.8): multi-select, not graded, all are real causes -----
        public const string ReflectionQuestion = "Why might your value of g differ from 9.8 m/s²?";
        public static readonly string[] ReflectionOptions =
        {
            "Reaction time when tapping the stopwatch",
            "Miscounting oscillations",
            "Angle too large in some trials",
            "Reading the length inaccurately",
            "Air resistance and friction at the pivot"
        };
        // One line per chosen cause, tying it to the precaution that controls it.
        public static readonly string[] ReflectionLinks =
        {
            "timing 20 oscillations spreads reaction-time error",
            "count full swings back to the centre, moving the same way",
            "keep the angle at 15° or less",
            "measure ℓ at eye level and add r",
            "small swings and a light, inextensible thread keep these small"
        };
        public const string ReflectionNone = "Tap the causes you think played a part.";
        public static string ReflectionSummary(System.Collections.Generic.List<int> chosen)
        {
            if (chosen.Count == 0) return ReflectionNone;
            var parts = new System.Collections.Generic.List<string>();
            foreach (int i in chosen) parts.Add(ReflectionLinks[i]);
            return "Good thinking. The precautions that help: " + string.Join("; ", parts) + ".";
        }
    }
}
