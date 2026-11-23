using System;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Logic (contract between a measuring instrument and the shared reading keypad).
     * Purpose: What CaliperReadingPadUI_Azm needs from an instrument, so the same keypad serves the
     * vernier caliper (M in whole mm, n = 0..9) and the screw gauge (M in steps of 0.5 mm,
     * n = 0..49), in both the normal and the zero-error mode. The instrument provides its own
     * wording (hints, labels, the formula line) and judges every reading; the keypad only collects
     * the numbers.
     * Talks to: implemented by CaliperMeasurement_Azm and ScrewGaugeMeasurement_Azm; used by
     * CaliperReadingPadUI_Azm.
     */
    public interface IReadingInstrument_Azm
    {
        event Action<bool> ReadyChanged;     // ready = the scales can be read (jaws touching / ratchet clicked)
        event Action StateChanged;           // begun / ended / accepted / show-me

        bool IsReady { get; }
        bool IsAccepted { get; }
        bool ShowMeActive { get; }
        string AcceptedMessage { get; }
        bool ZeroErrorMode { get; }
        int Divisions { get; }               // vernier or circular-scale divisions
        bool AllowsHalfMillimetre { get; }   // main-scale reading may end in .5

        string WaitingHint { get; }
        string ReadyHint { get; }
        string MainLabel { get; }
        string DivisionLabel { get; }

        void SetShowMe(bool on);
        float? ReadingValueMm(float? mainScaleMm, int? division);
        float? ZeroErrorValueMm(bool? negative, int? division);
        string ReadingFormula(string mainText, string divisionText, float? resultMm);
        string ZeroFormula(string sign, string divisionText, float? resultMm);
        CaliperReadingResult_Azm SubmitReading(float? mainScaleMm, int? division);
        CaliperReadingResult_Azm SubmitZeroError(bool? negative, int? division);

        // ESCALATING HELP (HCI restructure): after the SAME kind of mistake twice in a row, the keypad
        // replaces the short correction with this more concrete "where to look" explanation. It
        // describes HOW to find the line, never which number it is (the student still reads it).
        string EscalatedHint(CaliperReadingResult_Azm result);
    }
}
