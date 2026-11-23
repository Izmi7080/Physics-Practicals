using System;
using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    public enum CaliperReadingResult_Azm { Correct, NotTouching, Incomplete, MainScaleWrong, VernierWrong, BothWrong, SignWrong }

    // What the jaws are doing in this measurement.
    public enum CaliperMode_Azm
    {
        Outside,    // an object between the outside jaws; the student reads M and n
        ZeroError   // jaws closed on nothing; the student reads the zero error (sign and n)
    }

    /*
     * Layer: Logic (scene-local; no singleton). Reusable by every practical that measures with the
     * vernier caliper (Simple Pendulum: bob diameter; Exp 1.1/1.2: cylinder length and diameter).
     * Purpose: One measurement with the caliper:
     *   1. Begin(objectSize, mode): Outside - the object sits between the outside jaws; ZeroError -
     *      nothing between the jaws (size 0), the student closes them to read the zero error.
     *   2. The student moves the sliding jaw (RequestOpeningMm, from the interaction layer). The jaw
     *      cannot pass through the object: the opening is clamped at the object's size, which is
     *      the moment of CONTACT ("stops at contact" - the jaw grips the object).
     *   3. The student reads the scales and submits the reading. Outside: M (main-scale mm) and n
     *      (vernier division); D = M + n × LC is accepted within the configured number of least
     *      counts of what the SCALES show. ZeroError: the sign and n (see VernierScale_Azm.ZeroError);
     *      it must match exactly. A wrong answer says which part was misread.
     * Zero error: SetZeroError(e) makes the instrument read e too high with the jaws closed, like a
     * real caliper with a worn or bent jaw. The scales then show opening + e, so an observed reading
     * must be corrected: corrected = observed - e. (The 3D model's VernierPlate_Azm is shifted by e
     * in CaliperRigView_Azm; the magnifier draws the vernier at opening + e.) The pendulum uses e = 0.
     * It reads no device and touches no UI: the interaction layer calls RequestOpeningMm, the UI calls
     * SubmitReading / SubmitZeroError / SetShowMe, and everyone else listens to the events.
     * Talks to: CaliperConfig_Azm, VernierScale_Azm, CaliperTexts_Azm.
     */
    public class CaliperMeasurement_Azm : MonoBehaviour, IReadingInstrument_Azm
    {
        // ----- IReadingInstrument_Azm (the shared keypad) -----
        event Action<bool> IReadingInstrument_Azm.ReadyChanged { add => TouchingChanged += value; remove => TouchingChanged -= value; }
        bool IReadingInstrument_Azm.IsReady => touching;
        bool IReadingInstrument_Azm.ZeroErrorMode => mode == CaliperMode_Azm.ZeroError;
        int IReadingInstrument_Azm.Divisions => config.vernierDivisions;
        bool IReadingInstrument_Azm.AllowsHalfMillimetre => false;
        string IReadingInstrument_Azm.WaitingHint => mode == CaliperMode_Azm.ZeroError ? CaliperTexts_Azm.CloseJawsHint : CaliperTexts_Azm.DragHint(objectName);
        string IReadingInstrument_Azm.ReadyHint => mode == CaliperMode_Azm.ZeroError ? CaliperTexts_Azm.JawsClosedHint : CaliperTexts_Azm.TouchingHint(objectName);
        string IReadingInstrument_Azm.MainLabel => mode == CaliperMode_Azm.ZeroError ? CaliperTexts_Azm.SignLabel : CaliperTexts_Azm.MainScaleLabel;
        string IReadingInstrument_Azm.DivisionLabel => mode == CaliperMode_Azm.ZeroError ? CaliperTexts_Azm.ZeroDivisionLabel : CaliperTexts_Azm.VernierLabel;
        float? IReadingInstrument_Azm.ReadingValueMm(float? m, int? n) =>
            m.HasValue && n.HasValue ? VernierScale_Azm.Reading(m.Value, n.Value, config.LeastCountMm) : (float?)null;
        float? IReadingInstrument_Azm.ZeroErrorValueMm(bool? negative, int? n) =>
            n.HasValue && (n.Value == 0 || negative.HasValue) ? VernierScale_Azm.ZeroError(negative ?? false, n.Value, config.LeastCountMm, config.vernierDivisions) : (float?)null;
        string IReadingInstrument_Azm.ReadingFormula(string m, string n, float? r) => CaliperTexts_Azm.Formula(quantitySymbol, m, n, config.LeastCountMm, r);
        string IReadingInstrument_Azm.ZeroFormula(string sign, string n, float? r) => CaliperTexts_Azm.ZeroFormula(sign, n, config.LeastCountMm, config.vernierDivisions, r);

        // Escalated help after the same mistake twice in a row (explains where to look, not the number).
        string IReadingInstrument_Azm.EscalatedHint(CaliperReadingResult_Azm result)
        {
            if (mode == CaliperMode_Azm.ZeroError)
                return result == CaliperReadingResult_Azm.SignWrong ? CaliperTexts_Azm.ZeroSignWrongStep2 : CaliperTexts_Azm.ZeroVernierWrongStep2;
            switch (result)
            {
                case CaliperReadingResult_Azm.MainScaleWrong: return CaliperTexts_Azm.MainScaleWrongStep2;
                case CaliperReadingResult_Azm.VernierWrong: return CaliperTexts_Azm.VernierWrongStep2;
                default: return CaliperTexts_Azm.BothWrongStep2;
            }
        }
        // The caliper's main-scale reading is a whole number of millimetres.
        CaliperReadingResult_Azm IReadingInstrument_Azm.SubmitReading(float? m, int? n) =>
            SubmitReading(m.HasValue ? Mathf.RoundToInt(m.Value) : (int?)null, n);

        public CaliperConfig_Azm config;
        [Tooltip("Name used in feedback sentences, e.g. \"bob\" or \"cylinder\".")]
        public string objectName = "bob";
        [Tooltip("Symbol of the measured quantity in the formula line, e.g. \"d\" or \"L\".")]
        public string quantitySymbol = "d";

        public event Action OpeningChanged;
        public event Action<bool> TouchingChanged;
        public event Action StateChanged;                       // begun / ended / accepted / show-me
        public event Action<CaliperReadingResult_Azm, string> ReadingChecked;

        private float openingMm;
        private float objectSizeMm;
        private float zeroErrorMm;
        private CaliperMode_Azm mode;
        private bool measuring;
        private bool interactionEnabled = true;
        private bool touching;
        private bool accepted;
        private bool showMe;
        private float acceptedReadingMm;

        public float OpeningMm => openingMm;
        public float ObjectSizeMm => objectSizeMm;
        public float ZeroErrorMm => zeroErrorMm;
        // What the scales show: the jaw opening plus the instrument's zero error.
        public float ScaleReadingMm => openingMm + zeroErrorMm;
        public CaliperMode_Azm Mode => mode;
        public bool IsMeasuring => measuring;
        public bool IsTouching => touching;
        public bool IsAccepted => accepted;
        public bool ShowMeActive => showMe;
        // Outside: the student's observed reading (mm). ZeroError: the zero error they read (mm, signed).
        public float AcceptedReadingMm => acceptedReadingMm;
        public string AcceptedMessage { get; private set; } = "";
        public float LeastCountMm => config.LeastCountMm;

        // The jaw can be dragged while a measurement is running, not yet accepted, and not paused.
        public bool DragEnabled => measuring && !accepted && interactionEnabled;

        // The reading the scales show right now (what a perfect reader would enter).
        // ZeroError mode: M = 0 and n = the coinciding vernier line for the zero error.
        public void CurrentScaleReading(out int mainScaleMm, out int vernierDivision)
        {
            if (mode == CaliperMode_Azm.ZeroError)
            {
                mainScaleMm = 0;
                vernierDivision = VernierScale_Azm.ZeroErrorDivision(zeroErrorMm, config.LeastCountMm, config.vernierDivisions);
                return;
            }
            VernierScale_Azm.Decompose(ScaleReadingMm, config.mainDivisionMm, config.vernierDivisions, out mainScaleMm, out vernierDivision);
        }

        // ----- control (called by the practical's controller) -----

        public void Begin(float trueObjectSizeMm)
        {
            Begin(trueObjectSizeMm, CaliperMode_Azm.Outside);
        }

        public void Begin(float trueObjectSizeMm, CaliperMode_Azm measurementMode)
        {
            mode = measurementMode;
            objectSizeMm = mode == CaliperMode_Azm.ZeroError ? 0f : trueObjectSizeMm;
            openingMm = Mathf.Clamp(Mathf.Max(config.startOpeningMm, objectSizeMm + 5f), objectSizeMm, config.maxOpeningMm);
            measuring = true;
            accepted = false;
            showMe = false;
            AcceptedMessage = "";
            SetTouching(false);
            OpeningChanged?.Invoke();
            StateChanged?.Invoke();
        }

        public void End()
        {
            measuring = false;
            showMe = false;
            StateChanged?.Invoke();
        }

        // The instrument's zero error in mm (+ = reads too high). Set once per session.
        public void SetZeroError(float mm)
        {
            zeroErrorMm = mm;
            OpeningChanged?.Invoke();
        }

        // Pause menu / app in background: the jaw cannot be moved.
        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        // ----- intent from the interaction layer -----

        // The opening the finger asks for. The jaw stops at contact: it cannot close past the object.
        public void RequestOpeningMm(float requestedMm)
        {
            if (!DragEnabled) return;
            float clamped = Mathf.Clamp(requestedMm, objectSizeMm, config.maxOpeningMm);
            if (Mathf.Approximately(clamped, openingMm)) return;
            openingMm = clamped;
            OpeningChanged?.Invoke();
            // Contact is exact because the clamp puts the jaw exactly on the object.
            SetTouching(openingMm <= objectSizeMm + 1e-4f);
        }

        private void SetTouching(bool value)
        {
            if (touching == value) return;
            touching = value;
            if (!touching) showMe = false;
            TouchingChanged?.Invoke(touching);
        }

        // ----- intent from the UI -----

        // Outside mode. M = main-scale reading in mm, n = vernier division (0..N-1). Null = not entered.
        public CaliperReadingResult_Azm SubmitReading(int? mainScaleMm, int? vernierDivision)
        {
            if (mode == CaliperMode_Azm.ZeroError)
                return SubmitZeroError(null, vernierDivision);

            CaliperReadingResult_Azm result;
            string message;

            if (!touching)
            {
                result = CaliperReadingResult_Azm.NotTouching;
                message = CaliperTexts_Azm.NotTouching(objectName);
            }
            else if (!mainScaleMm.HasValue || !vernierDivision.HasValue)
            {
                result = CaliperReadingResult_Azm.Incomplete;
                message = CaliperTexts_Azm.Incomplete;
            }
            else
            {
                float lc = config.LeastCountMm;
                float reading = VernierScale_Azm.Reading(mainScaleMm.Value, vernierDivision.Value, lc);
                // Accept within the tolerance (in least counts) of what the scales show; the small
                // epsilon absorbs float noise.
                bool withinTolerance = Mathf.Abs(reading - ScaleReadingMm) <= config.toleranceLeastCounts * lc + 1e-4f;
                if (withinTolerance)
                {
                    result = CaliperReadingResult_Azm.Correct;
                    message = CaliperTexts_Azm.Correct(quantitySymbol, mainScaleMm.Value, vernierDivision.Value, lc);
                    Accept(reading, message);
                }
                else
                {
                    CurrentScaleReading(out int trueMain, out int trueVernier);
                    bool mainWrong = mainScaleMm.Value != trueMain;
                    bool vernierWrong = vernierDivision.Value != trueVernier;
                    if (mainWrong && vernierWrong) { result = CaliperReadingResult_Azm.BothWrong; message = CaliperTexts_Azm.BothWrong; }
                    else if (mainWrong) { result = CaliperReadingResult_Azm.MainScaleWrong; message = CaliperTexts_Azm.MainScaleWrong; }
                    else { result = CaliperReadingResult_Azm.VernierWrong; message = CaliperTexts_Azm.VernierWrong; }
                }
            }

            ReadingChecked?.Invoke(result, message);
            if (result == CaliperReadingResult_Azm.Correct) StateChanged?.Invoke();
            return result;
        }

        // ZeroError mode. negative = the vernier 0 is left of the main 0 (null = sign not chosen);
        // n = the coinciding vernier line. A zero error is read exactly (no tolerance): it is a
        // fixed property of the instrument and every later reading is corrected with it.
        public CaliperReadingResult_Azm SubmitZeroError(bool? negative, int? vernierDivision)
        {
            CaliperReadingResult_Azm result;
            string message;
            float lc = config.LeastCountMm;
            int trueN = VernierScale_Azm.ZeroErrorDivision(zeroErrorMm, lc, config.vernierDivisions);

            if (!touching)
            {
                result = CaliperReadingResult_Azm.NotTouching;
                message = CaliperTexts_Azm.JawsNotClosed;
            }
            else if (!vernierDivision.HasValue || (vernierDivision.Value != 0 && !negative.HasValue))
            {
                result = CaliperReadingResult_Azm.Incomplete;
                message = CaliperTexts_Azm.ZeroIncomplete;
            }
            else
            {
                float entered = VernierScale_Azm.ZeroError(negative ?? false, vernierDivision.Value, lc, config.vernierDivisions);
                if (Mathf.Abs(entered - zeroErrorMm) < lc * 0.5f)
                {
                    result = CaliperReadingResult_Azm.Correct;
                    message = CaliperTexts_Azm.ZeroCorrect(entered);
                    Accept(entered, message);
                }
                else if (vernierDivision.Value != trueN)
                {
                    result = CaliperReadingResult_Azm.VernierWrong;
                    message = CaliperTexts_Azm.ZeroVernierWrong;
                }
                else
                {
                    result = CaliperReadingResult_Azm.SignWrong;
                    message = CaliperTexts_Azm.ZeroSignWrong;
                }
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

        // Learn mode: highlight the coinciding lines on the magnifier.
        public void SetShowMe(bool value)
        {
            value = value && touching && !accepted;
            if (showMe == value) return;
            showMe = value;
            StateChanged?.Invoke();
        }
    }
}
