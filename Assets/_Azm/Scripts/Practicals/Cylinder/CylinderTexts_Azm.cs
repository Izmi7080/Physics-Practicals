using System.Globalization;
using VirtualLab.Azm.Instruments;

namespace VirtualLab.Azm.Cylinder
{
    /*
     * Layer: Logic support (one place for the practical's user-facing sentences - CLAUDE.md: no text
     * scattered through scripts; a teacher reviews the wording here).
     * Purpose: Feedback, hints and the worked-calculation lines of "Vernier Calipers: Solid
     * Cylinder". Step instructions are in CylinderConfig_Azm.steps; the written guide is the
     * PracticalGuide_Cylinder_Azm asset; caliper sentences are in CaliperTexts_Azm.
     * Talks to: CylinderController_Azm and the cylinder UI.
     */
    public static class CylinderTexts_Azm
    {
        public static string F(float value, int decimals) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        public static string Signed(float value, int decimals) => CaliperTexts_Azm.Signed(value, decimals);

        // ----- least count -----
        public const string LeastCountPrompt =
            "One main-scale division is 1 mm. The 10 vernier divisions span 9 mm. What is the least count?";
        public static string LeastCountChoice(float mm) => $"{F(mm, mm < 0.1f ? 2 : 1)} mm";
        public static string LeastCountCorrect(float mm) =>
            $"Correct! LC = 1 mm ÷ 10 = {F(mm, 1)} mm = {F(mm / 10f, 2)} cm - the smallest length this caliper can read.";
        public const string LeastCountWrong =
            "Not quite. Least count = smallest main-scale division ÷ number of vernier divisions.";

        // ----- readings -----
        public static string ReadingRecorded(CylinderReading_Azm r) =>
            $"{r.Label}: Y = {F(r.ObservedCm, 2)} cm, corrected = {F(r.ObservedCm, 2)} {(r.ZeroCorrectionCm < 0 ? "−" : "+")} {F(System.Math.Abs(r.ZeroCorrectionCm), 2)} = {F(r.CorrectedCm, 2)} cm";
        public const string TurnedForNext = "The cylinder has been turned a little - measure again at a new place.";
        public const string LengthwiseNow = "Now the cylinder lies lengthwise between the jaws: measure its length L.";
        public const string AllReadingsDone = "All readings recorded. Now work out the results.";

        // ----- hints (Learn mode, once per session) -----
        public const string HintLeastCount = "Least count = smallest main-scale division ÷ number of vernier divisions.";
        public const string HintCloseGently = "Close the jaws gently - pressing hard can bend them and change every reading.";
        public const string HintBothJaws = "Both jaws must just touch the cylinder - not loose, not squeezed.";
        public const string HintZeroCorrection = "Every reading is corrected with the zero correction: corrected value = Y + ZC.";
        public const string HintMean = "Use the mean of your three CORRECTED readings - averaging reduces the effect of one poor reading.";

        // ----- worked calculation -----
        public static string MeanLine(string symbol, float a, float b, float c, float mean) =>
            $"Mean {(symbol == "D" ? "diameter" : "length")} {symbol} = ({symbol}₁ + {symbol}₂ + {symbol}₃) / 3 = ({F(a, 2)} + {F(b, 2)} + {F(c, 2)}) / 3 = <b>{F(mean, 3)} cm</b>";
        public static string RadiusLine(float d, float r) => $"Radius r = D / 2 = {F(d, 3)} / 2 = <b>{F(r, 3)} cm</b>";
        public static string AreaLine(float r, float a) => $"Area of cross-section A = πr² = π × {F(r, 3)}² = <b>{F(a, 3)} cm²</b>  (Exp 1.1)";
        public static string VolumeLine(float r, float l, float v) => $"Volume V = πr²L = π × {F(r, 3)}² × {F(l, 3)} = <b>{F(v, 2)} cm³</b>  (Exp 1.2)";
        public const string NextLine = "Show next line";
        public const string SeeResult = "See my result";

        // ----- results -----
        public static string AreaResult(float a) => $"A = {F(a, 3)} cm²";
        public static string VolumeResult(float v) => $"V = {F(v, 2)} cm³";
        public static string ResultDetail(float d, float l, float zeroErrorCm, float trueA, float trueV, float areaDiff, float volumeDiff) =>
            $"Mean D = {F(d, 3)} cm · mean L = {F(l, 3)} cm · zero correction {Signed(-zeroErrorCm, 2)} cm applied\n" +
            $"Accepted values: A = {F(trueA, 3)} cm², V = {F(trueV, 2)} cm³ (your results differ by {F(areaDiff, 1)} % and {F(volumeDiff, 1)} %)";

        public static string DataHeader(bool lcKnown, float lcMm, bool zeroKnown, float zeroErrorMm)
        {
            string lc = lcKnown ? $"LC = {F(lcMm, 1)} mm = {F(lcMm / 10f, 2)} cm" : "LC = …";
            string ze = zeroKnown ? $"Zero error = {Signed(zeroErrorMm / 10f, 2)} cm · ZC = {Signed(-zeroErrorMm / 10f, 2)} cm" : "Zero error = … · ZC = …";
            return lc + "  ·  " + ze;
        }

        public const string ExitQuestion = "Exit to the main menu? Your readings in this practical will be lost.";
        public const string RestartQuestion = "Restart the practical? Your readings will be lost.";
    }
}
