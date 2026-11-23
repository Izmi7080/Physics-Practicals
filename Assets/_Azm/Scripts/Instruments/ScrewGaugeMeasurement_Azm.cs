using System;
using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    public enum ScrewGaugeMode_Azm
    {
        Object,     // an object between anvil and spindle; the student reads M and n
        ZeroError   // anvil and spindle closed on nothing; the student reads the zero error
    }

    /*
     * Layer: Logic (scene-local; no singleton). Reusable by any practical that measures with the
     * screw gauge (Matric Physics Exp 1.3).
     * Purpose: The screw gauge as a measuring instrument:
     *   gap (mm) = thimble angle / 360° × pitch      (pitch 0.5 mm: one turn moves the spindle 0.5 mm)
     *   least count LC = pitch / circular divisions = 0.5 / 50 = 0.01 mm
     *   reading  Y = M + n × LC   (M = main-scale mark just before the thimble edge, in 0.5 mm steps;
     *                              n = circular division on the datum line)
     * Turning: the thimble (TurnThimbleDeg, from the drag interaction) moves the spindle quickly;
     * the RATCHET (TurnRatchet while its button is held) turns it gently and SLIPS with a click once
     * the spindle touches - the instrument's way of always pressing with the same light force.
     * The spindle can never pass through the object (the gap is clamped at its size). A reading is
     * only accepted after the ratchet has clicked; closing with the thimble alone is allowed but
     * raises ContactByThimble so the practical can warn (textbook caution: turn gently using the
     * ratchet, do not press the jaws very hard). The lock stops all turning.
     * Zero error e (+ = reads high): the scales show gap + e. The textbook rule for reading it:
     * circular 0 BELOW the datum line -> positive, +n × LC; ABOVE -> negative, -(50 - n) × LC.
     * Talks to: ScrewGaugeConfig_Azm, ScrewGaugeTexts_Azm, VernierScale_Azm (zero-error arithmetic);
     * the shared keypad through IReadingInstrument_Azm.
     */
    public class ScrewGaugeMeasurement_Azm : MonoBehaviour, IReadingInstrument_Azm
    {
        public ScrewGaugeConfig_Azm config;
        [Tooltip("Name used in feedback sentences, e.g. \"strip\" or \"wire\".")]
        public string objectName = "strip";
        [Tooltip("Symbol of the observed value in the formula line.")]
        public string quantitySymbol = "Y";

        public event Action GapChanged;
        public event Action<bool> ReadyChanged;
        public event Action StateChanged;
        public event Action<CaliperReadingResult_Azm, string> ReadingChecked;
        public event Action Clicked;              // the ratchet slipped (sound + wobble)
        public event Action ContactByThimble;     // closed with the thimble, not the ratchet
        public event Action TurnBlockedByLock;

        private float gapMm;
        private float objectSizeMm;
        private float zeroErrorMm;
        private ScrewGaugeMode_Azm mode;
        private bool measuring;
        private bool interactionEnabled = true;
        private bool touching;
        private bool clicked;
        private bool accepted;
        private bool showMe;
        private bool locked;
        private float acceptedReadingMm;
        private float lastClickTime = -10f;

        public float GapMm => gapMm;
        public float ObjectSizeMm => objectSizeMm;
        public float ZeroErrorMm => zeroErrorMm;
        public float ScaleReadingMm => gapMm + zeroErrorMm;   // what the scales show
        public ScrewGaugeMode_Azm Mode => mode;
        public bool IsMeasuring => measuring;
        public bool IsTouching => touching;
        public bool IsClicked => clicked;
        public bool IsLocked => locked;
        public bool IsAccepted => accepted;
        public bool ShowMeActive => showMe;
        public float AcceptedReadingMm => acceptedReadingMm;
        public string AcceptedMessage { get; private set; } = "";
        public float LeastCountMm => config.LeastCountMm;
        // Ready to read: touching AND the ratchet has clicked.
        public bool IsReady => touching && clicked;
        public bool CanTurn => measuring && !accepted && interactionEnabled && !locked;

        // ----- IReadingInstrument_Azm -----
        bool IReadingInstrument_Azm.ZeroErrorMode => mode == ScrewGaugeMode_Azm.ZeroError;
        int IReadingInstrument_Azm.Divisions => config.divisions;
        bool IReadingInstrument_Azm.AllowsHalfMillimetre => true;
        string IReadingInstrument_Azm.WaitingHint => mode == ScrewGaugeMode_Azm.ZeroError ? ScrewGaugeTexts_Azm.ZeroWaitingHint : ScrewGaugeTexts_Azm.WaitingHint(objectName);
        string IReadingInstrument_Azm.ReadyHint => mode == ScrewGaugeMode_Azm.ZeroError ? ScrewGaugeTexts_Azm.ZeroReadyHint : ScrewGaugeTexts_Azm.ReadyHint(objectName);
        string IReadingInstrument_Azm.MainLabel => mode == ScrewGaugeMode_Azm.ZeroError ? ScrewGaugeTexts_Azm.SignLabel : ScrewGaugeTexts_Azm.MainLabel;
        string IReadingInstrument_Azm.DivisionLabel => mode == ScrewGaugeMode_Azm.ZeroError ? ScrewGaugeTexts_Azm.ZeroDivisionLabel : ScrewGaugeTexts_Azm.DivisionLabel;
        float? IReadingInstrument_Azm.ReadingValueMm(float? m, int? n) => m.HasValue && n.HasValue ? m.Value + n.Value * LeastCountMm : (float?)null;
        float? IReadingInstrument_Azm.ZeroErrorValueMm(bool? negative, int? n) =>
            n.HasValue && (n.Value == 0 || negative.HasValue) ? VernierScale_Azm.ZeroError(negative ?? false, n.Value, LeastCountMm, config.divisions) : (float?)null;
        string IReadingInstrument_Azm.ReadingFormula(string m, string n, float? r) => ScrewGaugeTexts_Azm.Formula(quantitySymbol, m, n, LeastCountMm, r);
        string IReadingInstrument_Azm.ZeroFormula(string sign, string n, float? r) => ScrewGaugeTexts_Azm.ZeroFormula(sign, n, LeastCountMm, config.divisions, r);

        // Escalated help after the same mistake twice in a row (explains where to look, not the number).
        string IReadingInstrument_Azm.EscalatedHint(CaliperReadingResult_Azm result)
        {
            if (mode == ScrewGaugeMode_Azm.ZeroError)
                return result == CaliperReadingResult_Azm.SignWrong ? ScrewGaugeTexts_Azm.ZeroSignWrongStep2 : ScrewGaugeTexts_Azm.ZeroVernierWrongStep2;
            switch (result)
            {
                case CaliperReadingResult_Azm.MainScaleWrong: return ScrewGaugeTexts_Azm.MainScaleWrongStep2;
                case CaliperReadingResult_Azm.VernierWrong: return ScrewGaugeTexts_Azm.DivisionWrongStep2;
                default: return ScrewGaugeTexts_Azm.BothWrongStep2;
            }
        }

        // The reading the scales show right now (what a perfect reader would enter).
        public void CurrentScaleReading(out float mainScaleMm, out int division)
        {
            if (mode == ScrewGaugeMode_Azm.ZeroError)
            {
                mainScaleMm = 0f;
                division = VernierScale_Azm.ZeroErrorDivision(zeroErrorMm, LeastCountMm, config.divisions);
                return;
            }
            int steps = Mathf.RoundToInt(ScaleReadingMm / LeastCountMm);    // reading in least counts
            int turns = steps / config.divisions;                             // whole half-millimetres
            mainScaleMm = turns * config.pitchMm;
            division = steps - turns * config.divisions;
        }

        // ----- control (from the practical's controller) -----

        public void Begin(float trueObjectSizeMm, ScrewGaugeMode_Azm measurementMode)
        {
            mode = measurementMode;
            objectSizeMm = mode == ScrewGaugeMode_Azm.ZeroError ? 0f : trueObjectSizeMm;
            gapMm = Mathf.Clamp(objectSizeMm + config.startClearanceMm, objectSizeMm, config.rangeMm);
            measuring = true;
            accepted = false;
            showMe = false;
            locked = false;
            AcceptedMessage = "";
            SetContact(false);
            GapChanged?.Invoke();
            StateChanged?.Invoke();
        }

        public void End()
        {
            measuring = false;
            showMe = false;
            StateChanged?.Invoke();
        }

        public void SetZeroError(float mm)
        {
            zeroErrorMm = mm;
            GapChanged?.Invoke();
        }

        public void SetInteractionEnabled(bool enabled) => interactionEnabled = enabled;

        // ----- intent from the interaction / UI layers -----

        // Thimble drag: + opens, - closes.
        public void TurnThimbleDeg(float degrees)
        {
            if (!Guard()) return;
            SetGap(gapMm + degrees / 360f * config.pitchMm, false);
        }

        // Ratchet held for dt seconds: closes gently; at contact it slips and clicks.
        public void TurnRatchet(float dt)
        {
            if (!Guard()) return;
            if (touching)
            {
                Click();
                return;
            }
            SetGap(gapMm - dt * config.ratchetTurnsPerSecond * config.pitchMm, true);
        }

        // "Open" held for dt seconds: turns the thimble back.
        public void Open(float dt)
        {
            if (!Guard()) return;
            SetGap(gapMm + dt * config.openTurnsPerSecond * config.pitchMm, false);
        }

        // For a scripted demonstration (the pitch step): turn by an angle regardless of contact rules.
        public void DemoTurnDeg(float degrees)
        {
            if (!measuring) return;
            SetGap(gapMm + degrees / 360f * config.pitchMm, false);
        }

        public void ToggleLock()
        {
            if (!measuring) return;
            locked = !locked;
            StateChanged?.Invoke();
        }

        private bool Guard()
        {
            if (locked && measuring && !accepted) { TurnBlockedByLock?.Invoke(); return false; }
            return CanTurn;
        }

        private void SetGap(float value, bool viaRatchet)
        {
            float clamped = Mathf.Clamp(value, objectSizeMm, config.rangeMm);
            if (Mathf.Approximately(clamped, gapMm)) return;
            gapMm = clamped;
            GapChanged?.Invoke();
            bool nowTouching = gapMm <= objectSizeMm + 1e-5f;
            if (nowTouching && !touching)
            {
                SetContact(true);
                if (viaRatchet) Click(true);   // the click at contact always counts
                else ContactByThimble?.Invoke();
            }
            else if (!nowTouching && touching) SetContact(false);
        }

        private void SetContact(bool value)
        {
            bool wasReady = IsReady;
            touching = value;
            if (!value) { clicked = false; showMe = false; }
            if (wasReady != IsReady) ReadyChanged?.Invoke(IsReady);
        }

        // Holding the ratchet at contact repeats the click, at most once per clickInterval.
        private void Click(bool force = false)
        {
            if (!force && Time.unscaledTime - lastClickTime < config.clickInterval) return;
            lastClickTime = Time.unscaledTime;
            bool wasReady = IsReady;
            clicked = true;
            Clicked?.Invoke();
            if (!wasReady && IsReady) ReadyChanged?.Invoke(true);
        }

        public CaliperReadingResult_Azm SubmitReading(float? mainScaleMm, int? division)
        {
            if (mode == ScrewGaugeMode_Azm.ZeroError) return SubmitZeroError(null, division);

            CaliperReadingResult_Azm result;
            string message;
            float lc = LeastCountMm;
            if (!touching) { result = CaliperReadingResult_Azm.NotTouching; message = ScrewGaugeTexts_Azm.NotTouching(objectName); }
            else if (!clicked) { result = CaliperReadingResult_Azm.NotTouching; message = ScrewGaugeTexts_Azm.NotClicked; }
            else if (!mainScaleMm.HasValue || !division.HasValue) { result = CaliperReadingResult_Azm.Incomplete; message = ScrewGaugeTexts_Azm.Incomplete; }
            else
            {
                float reading = mainScaleMm.Value + division.Value * lc;
                if (Mathf.Abs(reading - ScaleReadingMm) <= config.toleranceLeastCounts * lc + 1e-4f)
                {
                    result = CaliperReadingResult_Azm.Correct;
                    message = ScrewGaugeTexts_Azm.Correct(quantitySymbol, mainScaleMm.Value, division.Value, lc);
                    Accept(reading, message);
                }
                else
                {
                    CurrentScaleReading(out float trueMain, out int trueN);
                    bool mainWrong = Mathf.Abs(mainScaleMm.Value - trueMain) > 1e-4f;
                    bool nWrong = division.Value != trueN;
                    if (mainWrong && nWrong) { result = CaliperReadingResult_Azm.BothWrong; message = ScrewGaugeTexts_Azm.BothWrong; }
                    else if (mainWrong)
                    {
                        result = CaliperReadingResult_Azm.MainScaleWrong;
                        message = Mathf.Abs(Mathf.Abs(mainScaleMm.Value - trueMain) - config.pitchMm) < 1e-4f
                            ? ScrewGaugeTexts_Azm.HalfMillimetreWrong : ScrewGaugeTexts_Azm.MainScaleWrong;
                    }
                    else { result = CaliperReadingResult_Azm.VernierWrong; message = ScrewGaugeTexts_Azm.DivisionWrong; }
                }
            }
            ReadingChecked?.Invoke(result, message);
            if (result == CaliperReadingResult_Azm.Correct) StateChanged?.Invoke();
            return result;
        }

        // Zero error is read exactly: it is a fixed property of the instrument.
        public CaliperReadingResult_Azm SubmitZeroError(bool? negative, int? division)
        {
            CaliperReadingResult_Azm result;
            string message;
            float lc = LeastCountMm;
            int trueN = VernierScale_Azm.ZeroErrorDivision(zeroErrorMm, lc, config.divisions);
            if (!touching) { result = CaliperReadingResult_Azm.NotTouching; message = ScrewGaugeTexts_Azm.NotTouching("anvil"); }
            else if (!clicked) { result = CaliperReadingResult_Azm.NotTouching; message = ScrewGaugeTexts_Azm.NotClicked; }
            else if (!division.HasValue || (division.Value != 0 && !negative.HasValue))
            {
                result = CaliperReadingResult_Azm.Incomplete;
                message = "Enter the division n on the datum line, and tap the sign field: + if the circular 0 is BELOW the datum line, − if it is ABOVE it.";
            }
            else
            {
                float entered = VernierScale_Azm.ZeroError(negative ?? false, division.Value, lc, config.divisions);
                if (Mathf.Abs(entered - zeroErrorMm) < lc * 0.5f)
                {
                    result = CaliperReadingResult_Azm.Correct;
                    message = ScrewGaugeTexts_Azm.ZeroCorrect(entered);
                    Accept(entered, message);
                }
                else if (division.Value != trueN) { result = CaliperReadingResult_Azm.VernierWrong; message = ScrewGaugeTexts_Azm.ZeroVernierWrong; }
                else { result = CaliperReadingResult_Azm.SignWrong; message = ScrewGaugeTexts_Azm.ZeroSignWrong; }
            }
            ReadingChecked?.Invoke(result, message);
            if (result == CaliperReadingResult_Azm.Correct) StateChanged?.Invoke();
            return result;
        }

        private void Accept(float readingMm, string message)
        {
            accepted = true;
            showMe = false;
            acceptedReadingMm = readingMm;
            AcceptedMessage = message;
        }

        public void SetShowMe(bool value)
        {
            value = value && IsReady && !accepted;
            if (showMe == value) return;
            showMe = value;
            StateChanged?.Invoke();
        }
    }
}
