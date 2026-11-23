using UnityEngine;

namespace VirtualLab.Azm.Instruments
{
    /*
     * Layer: Logic support (pure maths, no Unity objects; shared by every practical that uses the
     * vernier caliper - Simple Pendulum and Matric Physics Exp 1.1/1.2).
     * Purpose: The arithmetic of a vernier scale, in one place:
     *   least count     LC = (one main-scale division) / (number of vernier divisions)
     *                   = 1 mm / 10 = 0.1 mm = 0.01 cm for the school caliper
     *   reading         D = M + n × LC
     *                   M = main-scale reading (the last main mark at or before the vernier 0)
     *                   n = the vernier division that lines up exactly with a main-scale mark
     * A vernier division is (N - 1)/N of a main division (10 vernier divisions = 9 mm), so vernier
     * line n sits at M + n·LC + n·(N - 1)/N = M + n main divisions: it lines up with main mark M + n.
     * Talks to: used by CaliperMeasurement_Azm and CaliperMagnifierUI_Azm.
     */
    public static class VernierScale_Azm
    {
        // Splits an opening (mm) into the main-scale reading M (whole main divisions, mm) and the
        // coinciding vernier division n, as a student reads it. The opening is first rounded to the
        // least count, because that is the finest the instrument can show.
        public static void Decompose(float openingMm, float mainDivisionMm, int vernierDivisions,
            out int mainScaleMm, out int vernierDivision)
        {
            float leastCount = LeastCount(mainDivisionMm, vernierDivisions);
            int steps = Mathf.RoundToInt(openingMm / leastCount);       // opening in least counts
            int mainDivisions = steps / vernierDivisions;
            mainScaleMm = Mathf.RoundToInt(mainDivisions * mainDivisionMm);
            vernierDivision = steps - mainDivisions * vernierDivisions;
        }

        public static float LeastCount(float mainDivisionMm, int vernierDivisions)
        {
            return mainDivisionMm / vernierDivisions;
        }

        // D = M + n × LC (all in mm).
        public static float Reading(float mainScaleMm, int vernierDivision, float leastCountMm)
        {
            return mainScaleMm + vernierDivision * leastCountMm;
        }

        // Spacing of the vernier lines (mm): N vernier divisions span N - 1 main divisions.
        public static float VernierSpacing(float mainDivisionMm, int vernierDivisions)
        {
            return mainDivisionMm * (vernierDivisions - 1) / vernierDivisions;
        }

        // ZERO ERROR, read with the jaws closed (valid while it is smaller than one main division):
        //   positive - the vernier 0 is to the RIGHT of the main-scale 0: zero error = +n × LC
        //   negative - the vernier 0 is to the LEFT of the main-scale 0:  zero error = -(N - n) × LC
        // where n is the vernier line that lines up with a main-scale mark. (For a negative error the
        // vernier 0 is (N - n) least counts short of the main 0: line n then sits on main mark n - 1.)
        // ASSUMPTION (teacher to confirm): this is the counting method students are taught for a
        // negative zero error; the textbook only states the sign rule.
        // Zero correction = -(zero error); corrected reading = observed reading + zero correction.
        public static float ZeroError(bool negative, int vernierDivision, float leastCountMm, int vernierDivisions)
        {
            if (vernierDivision == 0) return 0f;
            return negative ? -(vernierDivisions - vernierDivision) * leastCountMm : vernierDivision * leastCountMm;
        }

        // The vernier line n a student sees for a given zero error (the inverse of ZeroError).
        public static int ZeroErrorDivision(float zeroErrorMm, float leastCountMm, int vernierDivisions)
        {
            int k = Mathf.RoundToInt(Mathf.Abs(zeroErrorMm) / leastCountMm);
            if (k == 0) return 0;
            return zeroErrorMm > 0f ? k : vernierDivisions - k;
        }
    }
}
