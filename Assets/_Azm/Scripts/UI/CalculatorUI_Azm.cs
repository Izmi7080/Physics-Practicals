using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (a tool for the student; it knows nothing about any experiment).
     * Purpose: The simple calculator in the Calc tab (TASK_menus_and_pendulum 4.6): digits, point,
     * + − × ÷, =, x², √, π, clear and backspace - no scientific mode, like the basic calculator a
     * student may use in the practical exam. It works like a pocket calculator ("immediate
     * execution"): 2 + 3 × 4 = gives 20, operations apply left to right as they are pressed.
     * Errors (÷ 0, √ of a negative number) show "Error" until C is pressed.
     * Talks to: its own buttons and display (built by the scene builder); FeedbackFX via ButtonFeel.
     */
    public class CalculatorUI_Azm : MonoBehaviour
    {
        public TMP_Text display;
        public TMP_Text expression;          // small line above the display: "12.5 ×"
        [Tooltip("Index = digit.")]
        public Button[] digitButtons = new Button[10];
        public Button pointButton, addButton, subtractButton, multiplyButton, divideButton;
        public Button equalsButton, clearButton, backspaceButton, squareButton, rootButton, piButton;

        private string entry = "0";          // the number being typed
        private double accumulator;
        private char pending;                // '+', '-', '*', '/' or '\0'
        private bool startNew = true;        // the next digit starts a new number
        // True once the display holds a value meant as the next operand (typed, π, or x² / √ of it),
        // so "4 × π x² =" uses π² as the operand instead of dropping it.
        private bool operandReady;
        private bool error;

        private const int MaxDigits = 12;

        private void Awake()
        {
            for (int d = 0; d < digitButtons.Length; d++)
            {
                int digit = d;
                if (digitButtons[d] != null) digitButtons[d].onClick.AddListener(() => Digit(digit));
            }
            pointButton.onClick.AddListener(Point);
            addButton.onClick.AddListener(() => Operator('+'));
            subtractButton.onClick.AddListener(() => Operator('-'));
            multiplyButton.onClick.AddListener(() => Operator('*'));
            divideButton.onClick.AddListener(() => Operator('/'));
            equalsButton.onClick.AddListener(PressEquals);
            clearButton.onClick.AddListener(Clear);
            backspaceButton.onClick.AddListener(Backspace);
            squareButton.onClick.AddListener(() => Unary(x => x * x));
            rootButton.onClick.AddListener(() => Unary(x => x < 0 ? double.NaN : System.Math.Sqrt(x)));
            piButton.onClick.AddListener(() => { if (error) return; entry = Format(System.Math.PI); startNew = true; operandReady = true; Refresh(); });
            Refresh();
        }

        private double Value => double.TryParse(entry, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0;

        private void Digit(int d)
        {
            if (error) return;
            operandReady = true;
            if (startNew) { entry = d.ToString(); startNew = false; }
            else if (entry.Replace(".", "").Replace("-", "").Length < MaxDigits)
                entry = entry == "0" ? d.ToString() : entry + d;
            Refresh();
        }

        private void Point()
        {
            if (error) return;
            operandReady = true;
            if (startNew) { entry = "0."; startNew = false; }
            else if (!entry.Contains(".")) entry += ".";
            Refresh();
        }

        private void Operator(char op)
        {
            if (error) return;
            if (pending != '\0' && operandReady) Apply();     // chain: 2 + 3 × ... applies the + first
            else if (pending == '\0') accumulator = Value;     // the first operand
            // (an operator pending and no new operand yet: the student just changed the operator)
            pending = op;
            startNew = true;
            operandReady = false;
            Refresh();
        }

        private void PressEquals()
        {
            if (error || pending == '\0') return;
            Apply();
            pending = '\0';
            startNew = true;
            operandReady = false;
            Refresh();
        }

        // accumulator = accumulator (pending) entry
        private void Apply()
        {
            double b = Value;
            switch (pending)
            {
                case '+': accumulator += b; break;
                case '-': accumulator -= b; break;
                case '*': accumulator *= b; break;
                case '/': accumulator = b == 0 ? double.NaN : accumulator / b; break;
            }
            SetEntry(accumulator);
        }

        private void Unary(System.Func<double, double> f)
        {
            if (error) return;
            // Only the number on the display changes; a pending "4 ×" is kept.
            SetEntry(f(Value));
            startNew = true;
            operandReady = true;
            Refresh();
        }

        private void SetEntry(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) { error = true; entry = "Error"; return; }
            entry = Format(v);
        }

        private void Clear()
        {
            entry = "0";
            accumulator = 0;
            pending = '\0';
            startNew = true;
            operandReady = false;
            error = false;
            Refresh();
        }

        private void Backspace()
        {
            if (error || startNew) return;
            entry = entry.Length > 1 ? entry.Substring(0, entry.Length - 1) : "0";
            if (entry == "-") entry = "0";
            Refresh();
        }

        // Up to 10 significant figures, without trailing zeros.
        private static string Format(double v)
        {
            string s = v.ToString("G10", CultureInfo.InvariantCulture);
            return s.Contains("E") ? v.ToString("0.######E+0", CultureInfo.InvariantCulture) : s;
        }

        private static string Symbol(char op)
        {
            switch (op)
            {
                case '+': return "+";
                case '-': return "−";
                case '*': return "×";
                case '/': return "÷";
                default: return "";
            }
        }

        private void Refresh()
        {
            display.text = entry;
            if (expression != null)
                expression.text = pending != '\0' ? Format(accumulator) + " " + Symbol(pending) : "";
        }
    }
}
