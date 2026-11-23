namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: Logic (plain data class).
     * Purpose: One row of the observation table: one thread length, timed twice and averaged.
     *   L = l + r            effective length
     *   mean t = (t1 + t2)/2
     *   T = mean t / n       time period
     *   T^2                  the quantity plotted against L
     * Everything is stored in SI units (metres, seconds). The TABLE shows centimetres, so the
     * *Cm properties exist for display only - the g calculation must always use the metre values
     * (a cm/m mix-up would make g wrong by a factor of 100).
     * Talks to: created by PendulumController_Azm; read by the table, graph and analysis.
     */
    public class TrialRow_Azm
    {
        public readonly int Number;
        public readonly float LengthM;      // l, pivot to top of the bob
        public readonly float RadiusM;      // r
        public readonly int Oscillations;   // n
        public readonly float Time1;        // t1, seconds for n oscillations
        public readonly float Time2;        // t2

        public bool Discarded;
        public bool Suspect;
        public string SuspectReason = "";

        public TrialRow_Azm(int number, float lengthM, float radiusM, int oscillations, float time1, float time2)
        {
            Number = number;
            LengthM = lengthM;
            RadiusM = radiusM;
            Oscillations = oscillations;
            Time1 = time1;
            Time2 = time2;
        }

        public float EffectiveLengthM => LengthM + RadiusM;
        public float MeanTime => (Time1 + Time2) * 0.5f;
        public float Period => MeanTime / Oscillations;
        public float PeriodSquared => Period * Period;

        // Display helpers (centimetres).
        public float LengthCm => LengthM * 100f;
        public float RadiusCm => RadiusM * 100f;
        public float EffectiveLengthCm => EffectiveLengthM * 100f;
    }
}
