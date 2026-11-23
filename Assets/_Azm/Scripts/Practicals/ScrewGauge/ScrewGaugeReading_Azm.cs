namespace VirtualLab.Azm.ScrewGaugePractical
{
    /*
     * Layer: Logic support (plain data, one row of the observation table).
     * Purpose: One screw gauge reading, exactly as the textbook table (Exp 1.3) records it, in mm:
     *   main-scale reading M, circular-scale division n on the datum line,
     *   fraction to be added   X = n × LC
     *   observed thickness     Y = M + X
     *   corrected thickness    t = Y + ZC, with zero correction ZC = -(zero error)
     * Talks to: created by ScrewGaugeController_Azm; shown by ScrewGaugeTableUI_Azm.
     */
    public class ScrewGaugeReading_Azm
    {
        public int Number { get; }
        public float MainScaleMm { get; }       // M (a multiple of 0.5 mm)
        public int Division { get; }            // n
        public float LeastCountMm { get; }
        public float ZeroErrorMm { get; }       // the zero error the student recorded

        public ScrewGaugeReading_Azm(int number, float mainScaleMm, int division, float leastCountMm, float zeroErrorMm)
        {
            Number = number;
            MainScaleMm = mainScaleMm;
            Division = division;
            LeastCountMm = leastCountMm;
            ZeroErrorMm = zeroErrorMm;
        }

        public float FractionMm => Division * LeastCountMm;          // X = n × LC
        public float ObservedMm => MainScaleMm + FractionMm;         // Y = M + X
        public float ZeroCorrectionMm => -ZeroErrorMm;               // ZC
        public float CorrectedMm => ObservedMm + ZeroCorrectionMm;   // t = Y ± ZC
        public string Label => "t" + Number;
    }
}
