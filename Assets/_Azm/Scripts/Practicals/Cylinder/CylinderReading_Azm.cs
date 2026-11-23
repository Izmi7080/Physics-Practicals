namespace VirtualLab.Azm.Cylinder
{
    public enum CylinderQuantity_Azm { Length, Diameter }

    /*
     * Layer: Logic support (plain data, one row of the observation table).
     * Purpose: One caliper reading of the cylinder, exactly as the textbook table records it:
     *   main-scale reading M, coinciding vernier division n,
     *   fraction to be added   X = n × LC
     *   observed value         Y = M + X
     *   corrected value        Y + ZC, with zero correction ZC = -(zero error)
     * Stored in mm (as entered on the keypad), shown in cm (as in the textbook table).
     * Talks to: created by CylinderController_Azm; shown by CylinderTableUI_Azm.
     */
    public class CylinderReading_Azm
    {
        public CylinderQuantity_Azm Quantity { get; }
        public int Number { get; }                    // 1..readingsPerQuantity
        public int MainScaleMm { get; }               // M, as the student entered it
        public int VernierDivision { get; }           // n, as the student entered it
        public float LeastCountMm { get; }
        public float ZeroErrorMm { get; }             // the zero error the student recorded

        public CylinderReading_Azm(CylinderQuantity_Azm quantity, int number, int mainScaleMm, int vernierDivision,
            float leastCountMm, float zeroErrorMm)
        {
            Quantity = quantity;
            Number = number;
            MainScaleMm = mainScaleMm;
            VernierDivision = vernierDivision;
            LeastCountMm = leastCountMm;
            ZeroErrorMm = zeroErrorMm;
        }

        public float MainScaleCm => MainScaleMm / 10f;
        public float FractionCm => VernierDivision * LeastCountMm / 10f;          // X = n × LC
        public float ObservedCm => MainScaleCm + FractionCm;                        // Y = M + X
        public float ZeroCorrectionCm => -ZeroErrorMm / 10f;                        // ZC = -(zero error)
        public float CorrectedCm => ObservedCm + ZeroCorrectionCm;                  // Y ± ZC

        public string Label => (Quantity == CylinderQuantity_Azm.Length ? "L" : "D") + Number;
    }
}
