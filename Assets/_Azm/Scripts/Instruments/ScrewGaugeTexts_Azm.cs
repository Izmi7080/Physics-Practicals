using System.Globalization;
using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Logic support (one place for the screw gauge's user-facing sentences - CLAUDE.md: no
     * text scattered through scripts; a teacher reviews the wording here).
     * Purpose: Feedback for reading the screw gauge, following the textbook (Exp 1.3): M is the
     * main-scale mark just under or before the edge of the thimble (half-millimetre marks are
     * below the datum line); n is the circular-scale division on the datum line; zero error is
     * positive when the circular-scale 0 is BELOW the datum line.
     * Talks to: ScrewGaugeMeasurement_Azm and the screw gauge UI.
     */
    public static class ScrewGaugeTexts_Azm
    {
        public static string F(float value, int decimals) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        public static string Signed(float value, int decimals) => CaliperTexts_Azm.Signed(value, decimals);

        public static string WaitingHint(string objectName) => $"Close the spindle on the {objectName} with the ratchet until it clicks";
        public static string ReadyHint(string objectName) => $"The ratchet clicked on the {objectName} - now read the scales";
        public const string ZeroWaitingHint = "Close the spindle on the anvil with the ratchet until it clicks";
        public const string ZeroReadyHint = "Closed - read the zero error";
        public const string MainLabel = "Main scale  M (mm)";
        public const string DivisionLabel = "Circular scale  n";
        public const string SignLabel = "Sign  (tap: + / −)";
        public const string ZeroDivisionLabel = "Line on datum  n";

        public static string NotTouching(string objectName) =>
            $"Close the spindle on the {objectName} first, gently, with the ratchet.";
        public const string NotClicked =
            "Finish closing with the RATCHET until it clicks. The ratchet slips at the same light pressure every time; the thimble alone can press too hard.";
        public const string ThimbleContact =
            "Careful - you closed it with the thimble. Use the ratchet for the last turn so the spindle presses with the right, light force.";
        public const string Locked = "The lock is on. Unlock it before turning the thimble or ratchet.";
        public const string Incomplete = "Enter both numbers: the main-scale reading M and the circular-scale division n.";
        public const string MainScaleWrong =
            "Check the main scale: M is the last mark you can see just before the edge of the thimble.";
        public const string HalfMillimetreWrong =
            "Check the half-millimetre marks BELOW the datum line: if one is visible after the last mm mark, add 0.5 mm to M.";
        public const string DivisionWrong = "Check the circular scale: n is the division that lies exactly on the datum line.";
        public const string BothWrong =
            "Check both scales: M is the last mark before the thimble edge (include a half-mm mark below the line); n is the circular division on the datum line.";

        public static string Formula(string symbol, string mainText, string divisionText, float leastCountMm, float? resultMm)
        {
            string m = string.IsNullOrEmpty(mainText) ? "M" : mainText;
            string n = string.IsNullOrEmpty(divisionText) ? "n" : divisionText;
            string tail = resultMm.HasValue ? $" = {F(resultMm.Value, 2)} mm" : "";
            return $"{symbol} = M + n × LC = {m} + {n} × {F(leastCountMm, 2)}{tail}";
        }

        public static string ZeroFormula(string sign, string divisionText, float leastCountMm, int divisions, float? resultMm)
        {
            string n = string.IsNullOrEmpty(divisionText) ? "n" : divisionText;
            string lc = F(leastCountMm, 2);
            string body = sign == "−" ? $"−({divisions} − {n}) × {lc}" : (sign == "+" ? $"+{n} × {lc}" : $"± … × {lc}");
            string tail = resultMm.HasValue ? $" = {Signed(resultMm.Value, 2)} mm" : "";
            return $"Zero error = {body}{tail}";
        }

        public static string Correct(string symbol, float mainMm, int division, float leastCountMm) =>
            $"Correct! {symbol} = {F(mainMm, 1)} + {division} × {F(leastCountMm, 2)} = {F(mainMm + division * leastCountMm, 2)} mm";

        // ----- escalated help (the same mistake twice in a row): HOW to look, never the number -----
        public const string MainScaleWrongStep2 =
            "Look at the sleeve right next to the thimble's edge. Count the mm marks ABOVE the datum line up to the edge; then check BELOW the line: if a half-mm mark shows after the last mm mark, add 0.5.";
        public const string DivisionWrongStep2 =
            "Follow the long datum line on the sleeve to where it meets the thimble. The circular-scale division sitting exactly on that line is n (count up from the nearest number).";
        public const string BothWrongStep2 =
            "One scale at a time. M: the last mark visible before the thimble edge (with any half-mm mark below the line). n: the circular division exactly on the datum line.";
        public const string ZeroSignWrongStep2 =
            "With the gauge closed, find the circular-scale 0. Below the datum line: the gauge reads too much, +. Above it: −.";
        public const string ZeroVernierWrongStep2 =
            "Find the division exactly on the datum line: that is n. If the 0 is above the line (−), the zero error is (50 − n) × LC.";

        public const string ZeroVernierWrong = "Look again at which circular-scale division lies exactly on the datum line - that is n.";
        public const string ZeroSignWrong =
            "Check the sign: + if the circular-scale 0 is BELOW the datum line, − if it is ABOVE it.";

        public static string ZeroCorrect(float zeroErrorMm)
        {
            if (Mathf.Abs(zeroErrorMm) < 1e-4f) return "Correct! There is no zero error, so no zero correction is needed.";
            string where = zeroErrorMm > 0f ? "below" : "above";
            string effect = zeroErrorMm > 0f ? "too large" : "too small";
            return $"Correct! Zero error = {Signed(zeroErrorMm, 2)} mm (circular 0 is {where} the datum line, so every reading is {effect}). Zero correction = {Signed(-zeroErrorMm, 2)} mm.";
        }
    }
}
