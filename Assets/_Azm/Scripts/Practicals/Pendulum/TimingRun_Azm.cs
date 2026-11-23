namespace VirtualLab.Azm.Pendulum
{
    public enum TapOutcome_Azm
    {
        Started,            // first tap: the stopwatch started
        Counted,            // one more full oscillation counted
        Finished,           // the n-th count: the stopwatch stopped
        HalfSwingDetected   // the student is tapping on every pass through the centre
    }

    /*
     * Layer: Logic (plain class).
     * Purpose: One timing run, as the student really does it with a stopwatch.
     *   - The FIRST tap starts the stopwatch (as the bob passes the mean position).
     *   - Each following tap marks one completed oscillation (bob back at the mean position,
     *     moving the same way). The n-th following tap stops the stopwatch.
     *   - The recorded time is the real elapsed time between the start tap and the last tap, so
     *     the student's own reaction time is part of the measurement, as in a real lab.
     * It also tracks, silently, how good the tapping was (used for feedback in Stage 4):
     *   off-mean taps        taps made while the bob was far from the mean position
     *   half-swing counting  average interval about T/2 -> the student taps on every pass
     *   missed counts        an interval of about 2T -> a count was missed
     * Talks to: driven by PendulumController_Azm; pure logic, no input or UI.
     */
    public class TimingRun_Azm
    {
        private readonly int oscillations;
        private readonly float truePeriod;
        private readonly PendulumConfig_Azm config;

        private float lastTapTime;
        private float intervalSum;

        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public int Count { get; private set; }
        public float StartTime { get; private set; }
        public float ElapsedAtFinish { get; private set; }
        public int OffMeanTaps { get; private set; }
        public bool MissedCount { get; private set; }

        public TimingRun_Azm(int oscillations, float truePeriod, PendulumConfig_Azm config)
        {
            this.oscillations = oscillations;
            this.truePeriod = truePeriod;
            this.config = config;
        }

        // Seconds on the stopwatch right now.
        public float Elapsed(float simTime)
        {
            if (!Started) return 0f;
            return Finished ? ElapsedAtFinish : simTime - StartTime;
        }

        // simTime = the simulation clock at the tap; angleFraction = |theta| / amplitude at that
        // instant (0 = exactly at the mean position, 1 = at an extreme).
        public TapOutcome_Azm Tap(float simTime, float angleFraction)
        {
            if (angleFraction > config.offMeanAngleFraction) OffMeanTaps++;

            if (!Started)
            {
                Started = true;
                StartTime = simTime;
                lastTapTime = simTime;
                return TapOutcome_Azm.Started;
            }

            float interval = simTime - lastTapTime;
            lastTapTime = simTime;
            intervalSum += interval;
            Count++;

            if (interval > config.missedCountIntervalRatio * truePeriod) MissedCount = true;

            // Judge half-swing counting early (after a few taps) so the student is told straight
            // away, not after timing all 20.
            if (Count >= config.halfSwingMinIntervals && intervalSum / Count < config.halfSwingIntervalRatio * truePeriod)
                return TapOutcome_Azm.HalfSwingDetected;

            if (Count >= oscillations)
            {
                Finished = true;
                ElapsedAtFinish = simTime - StartTime;
                return TapOutcome_Azm.Finished;
            }
            return TapOutcome_Azm.Counted;
        }
    }
}
