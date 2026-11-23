using System.Globalization;
using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Logic support (one place for the caliper's user-facing sentences - CLAUDE.md: no text
     * scattered through scripts; a teacher reviews the wording here).
     * Purpose: Feedback for measuring with the vernier caliper. The object's name ("bob",
     * "cylinder") is passed in so every practical can reuse these sentences.
     * Talks to: CaliperMeasurement_Azm (feedback) and the caliper UI (labels).
     */
    public static class CaliperTexts_Azm
    {
        private static string F(float value, int decimals)
        {
            return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        public static string DragHint(string objectName) => $"Drag the sliding jaw until it touches the {objectName}";
        public static string TouchingHint(string objectName) => $"Both jaws touch the {objectName} - now read the scales";
        public const string PadTitle = "Your reading";
        public const string MainScaleLabel = "Main scale  M (mm)";
        public const string VernierLabel = "Vernier division  n";
        public const string CheckButton = "Check";
        public const string ShowMeButton = "Show me";
        public const string ContinueButton = "Continue";
        public const string MagnifierMainLabel = "Main scale (mm)";
        public const string MagnifierVernierLabel = "Vernier";

        // "d = M + n × LC = 20 + 3 × 0.1 = 20.3 mm"
        public static string Formula(string symbol, string mainText, string vernierText, float leastCountMm, float? resultMm)
        {
            string lc = F(leastCountMm, 1);
            string m = string.IsNullOrEmpty(mainText) ? "M" : mainText;
            string n = string.IsNullOrEmpty(vernierText) ? "n" : vernierText;
            string tail = resultMm.HasValue ? $" = {F(resultMm.Value, 1)} mm = {F(resultMm.Value / 10f, 2)} cm" : "";
            return $"{symbol} = M + n × LC = {m} + {n} × {lc}{tail}";
        }

        public static string NotTouching(string objectName) =>
            $"Close the sliding jaw onto the {objectName} first. The scales only give its size when both jaws touch it.";

        public const string Incomplete = "Enter both numbers: the main-scale reading M and the vernier division n.";

        public const string MainScaleWrong =
            "Check the main scale: M is the last millimetre mark at, or just before, the vernier's 0 line.";

        public const string VernierWrong =
            "Check the vernier: n is the vernier line that lines up exactly with a main-scale mark.";

        public const string BothWrong =
            "Check both scales: M is the last mm mark before the vernier 0, and n is the vernier line that lines up exactly with a main-scale mark.";

        public static string Correct(string symbol, int mainMm, int vernier, float leastCountMm) =>
            $"Correct! {symbol} = {mainMm} + {vernier} × {F(leastCountMm, 1)} = {F(mainMm + vernier * leastCountMm, 1)} mm = {F((mainMm + vernier * leastCountMm) / 10f, 2)} cm";

        // ----- zero error (jaws closed) -----
        public const string CloseJawsHint = "Close the jaws gently until they touch";
        public const string JawsClosedHint = "Jaws closed - read the zero error";
        public const string SignLabel = "Vernier 0 is  (tap: + / −)";
        public const string ZeroDivisionLabel = "Coinciding line  n";
        public const string JawsNotClosed = "Close the jaws first: the zero error is read with the jaws gently closed.";
        public const string ZeroIncomplete = "Enter the coinciding vernier line n, and tap the sign field: + if the vernier 0 is right of the main-scale 0, − if it is left of it.";
        public const string ZeroVernierWrong = "Look again at which vernier line lines up exactly with a main-scale mark - that line is n.";
        public const string ZeroSignWrong = "Check the sign: + if the vernier 0 is to the RIGHT of the main-scale 0, − if it is to the LEFT.";

        // "Zero error = +n × LC = ..." / "Zero error = −(10 − n) × LC = ..."
        public static string ZeroFormula(string sign, string vernierText, float leastCountMm, int divisions, float? resultMm)
        {
            string n = string.IsNullOrEmpty(vernierText) ? "n" : vernierText;
            string lc = F(leastCountMm, 1);
            string body = sign == "−" ? $"−({divisions} − {n}) × {lc}" : (sign == "+" ? $"+{n} × {lc}" : $"± … × {lc}");
            string tail = resultMm.HasValue ? $" = {Signed(resultMm.Value, 1)} mm = {Signed(resultMm.Value / 10f, 2)} cm" : "";
            return $"Zero error = {body}{tail}";
        }

        public static string ZeroCorrect(float zeroErrorMm)
        {
            if (Mathf.Abs(zeroErrorMm) < 1e-4f)
                return "Correct! There is no zero error, so no zero correction is needed.";
            string side = zeroErrorMm > 0f ? "right" : "left";
            string effect = zeroErrorMm > 0f ? "too large" : "too small";
            return $"Correct! Zero error = {Signed(zeroErrorMm / 10f, 2)} cm (vernier 0 is {side} of the main 0, so every reading is {effect}). Zero correction = {Signed(-zeroErrorMm / 10f, 2)} cm.";
        }

        public static string Signed(float value, int decimals)
        {
            string s = Mathf.Abs(value).ToString("F" + decimals, CultureInfo.InvariantCulture);
            if (Mathf.Abs(value) < 0.5f * Mathf.Pow(10f, -decimals)) return s;
            return (value > 0f ? "+" : "−") + s;
        }

        // ----- escalated help (the same mistake twice in a row): HOW to look, never the number -----
        public const string MainScaleWrongStep2 =
            "Find the vernier's 0 line first. Then look just to its LEFT on the main scale: count the small mm marks after the last numbered cm. That count (plus 10 for each cm) is M in mm.";
        public const string VernierWrongStep2 =
            "Run your eye along the vernier lines 0, 1, 2 ... 10. Only ONE of them forms a perfectly straight line with a main-scale mark above it - the others are slightly off to one side. Its number is n.";
        public const string BothWrongStep2 =
            "One scale at a time. M: the last mm mark before the vernier 0. n: the one vernier line that is exactly in line with a main-scale mark.";
        public const string ZeroSignWrongStep2 =
            "Look only at the two zeros with the jaws closed. Vernier 0 to the right of the main 0 means the caliper reads too much: +. To the left: −.";
        public const string ZeroVernierWrongStep2 =
            "Ignore the zeros. Look along the vernier for the single line that is exactly in line with a main-scale mark: that is n. For a − error the zero error is (10 − n) × LC.";
        public const string StillStuck = "Still stuck? Tap \"Show me\" to see which lines to read.";

        public static string ShowMe(int mainMm, int vernier) =>
            $"The vernier 0 is just past {mainMm} mm, so M = {mainMm}. Vernier line {vernier} lines up with a main mark (highlighted), so n = {vernier}.";
    }
}
