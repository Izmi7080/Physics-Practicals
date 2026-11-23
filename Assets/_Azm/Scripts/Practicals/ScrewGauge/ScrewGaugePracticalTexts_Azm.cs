using System.Globalization;
using VirtualLab.Azm.Instruments;

namespace VirtualLab.Azm.ScrewGaugePractical
{
    /*
     * Layer: Logic support (one place for the practical's user-facing sentences - CLAUDE.md).
     * Purpose: Feedback, hints and the worked-calculation lines of "Screw Gauge: Thickness of a Metal
     * Strip" (Exp 1.3). Step instructions are in ScrewGaugePracticalConfig_Azm.steps; the written
     * guide is PracticalGuide_ScrewGauge_Azm; reading sentences are in ScrewGaugeTexts_Azm.
     * Talks to: ScrewGaugeController_Azm and the screw gauge practical UI.
     */
    public static class ScrewGaugePracticalTexts_Azm
    {
        public static string F(float value, int decimals) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        public static string Signed(float value, int decimals) => CaliperTexts_Azm.Signed(value, decimals);

        // ----- pitch and least count -----
        public const string TurnOneTurn = "Turn one full turn";
        public const string LeastCountPrompt =
            "Turn the thimble one full turn and watch the thimble edge on the main scale. Then: the circular scale has 50 divisions. What is the least count?";
        public static string PitchShown(float pitchMm) =>
            $"One full turn moved the spindle {F(pitchMm, 1)} mm along the main scale - this distance is the PITCH.";
        public const string TurnFirst = "First turn the thimble one full turn to find the pitch.";
        public static string LeastCountChoice(float mm) => $"{F(mm, mm < 0.1f - 1e-5f ? 2 : (mm < 1f ? 1 : 0))} mm";
        public static string LeastCountCorrect(float pitch, int divisions, float lc) =>
            $"Correct! LC = pitch ÷ divisions = {F(pitch, 1)} mm ÷ {divisions} = {F(lc, 2)} mm.";
        public const string LeastCountWrong = "Not quite. Least count = pitch ÷ number of divisions on the circular scale.";

        // ----- readings -----
        public static string ReadingRecorded(ScrewGaugeReading_Azm r) =>
            $"{r.Label}: Y = {F(r.ObservedMm, 2)} mm, t = Y + ZC = {F(r.ObservedMm, 2)} {(r.ZeroCorrectionMm < 0 ? "−" : "+")} {F(System.Math.Abs(r.ZeroCorrectionMm), 2)} = {F(r.CorrectedMm, 2)} mm";
        public static string MovedForNext(string objectName) => $"The {objectName} has been moved - measure it again at a different place.";
        public const string AllReadingsDone = "All three readings recorded. Now work out the mean thickness.";
        public const string Locked = "Locked - the spindle cannot turn now.";
        public const string Unlocked = "Unlocked.";

        // ----- hints (Learn mode, once per session) -----
        public const string HintPitch = "The pitch is how far the spindle moves for one full turn of the thimble.";
        public const string HintRatchet = "Why the ratchet? It slips once the spindle touches, so every reading is taken with the same light pressure - the thimble alone could squeeze the strip.";
        public const string HintZeroSign = "Circular 0 BELOW the datum line = positive zero error; ABOVE = negative. The zero correction has the opposite sign.";
        public static string HintFlat(string objectName) => objectName == "wire"
            ? "The wire must be kink-free: a kink holds the spindle away and the reading comes out too large."
            : "The strip must be flat: a bend holds the spindle away and the reading comes out too large.";
        public const string HintPlaces = "Take the readings at different places and average them - a real strip is not equally thick everywhere.";
        public const string HintBackAndForth = "Do not turn the thimble back and forth unnecessarily - it wears the screw.";

        // ----- worked calculation -----
        public static string CorrectedLine(ScrewGaugeReading_Azm r) =>
            $"t{r.Number} = Y{r.Number} + ZC = {F(r.ObservedMm, 2)} {(r.ZeroCorrectionMm < 0 ? "−" : "+")} {F(System.Math.Abs(r.ZeroCorrectionMm), 2)} = <b>{F(r.CorrectedMm, 2)} mm</b>";
        public static string MeanLine(float a, float b, float c, float mean) =>
            $"Mean thickness t = (t₁ + t₂ + t₃) / 3 = ({F(a, 2)} + {F(b, 2)} + {F(c, 2)}) / 3 = <b>{F(mean, 3)} mm</b>";
        public const string NextLine = "Show next line";
        public const string SeeResult = "See my result";

        // ----- results -----
        public static string ResultValue(string quantity, float t) => $"{quantity} = {F(t, 2)} mm";
        public static string ResultDetail(float zeroErrorMm, float trueValue, float diffPercent) =>
            $"Mean of three corrected readings · zero correction {Signed(-zeroErrorMm, 2)} mm applied\n" +
            $"Accepted value: {F(trueValue, 2)} mm (your result differs by {F(diffPercent, 1)} %)";

        public static string DataHeader(bool lcKnown, float lcMm, bool zeroKnown, float zeroErrorMm)
        {
            string lc = lcKnown ? $"LC = {F(lcMm, 2)} mm" : "LC = …";
            string ze = zeroKnown ? $"Zero error = {Signed(zeroErrorMm, 2)} mm · ZC = {Signed(-zeroErrorMm, 2)} mm" : "Zero error = … · ZC = …";
            return lc + "  ·  " + ze;
        }

        public const string ExitQuestion = "Exit to the main menu? Your readings in this practical will be lost.";
        public const string RestartQuestion = "Restart the practical? Your readings will be lost.";
    }
}
