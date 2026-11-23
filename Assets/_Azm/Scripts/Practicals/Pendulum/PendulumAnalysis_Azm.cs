using System.Collections.Generic;
using UnityEngine;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.Pendulum
{
    public struct PendulumAnalysisResult_Azm
    {
        public int validCount;
        public bool hasFit;                 // needs at least 2 valid rows with different L
        public List<Vector2> points;        // x = L in METRES, y = T^2 in s^2 (valid rows only)
        public float slope;                 // s^2 per metre
        public float intercept;             // s^2
        public float g;                     // m/s^2
        public float percentError;          // |g - true g| / true g * 100
        public bool nearOrigin;             // does the fitted line pass close to the origin?
        public float maxPeriodSquared;
    }

    /*
     * Layer: Logic (pure maths).
     * Purpose: Fits T^2 against L over the valid (non-discarded) rows and derives g.
     *   T^2 = (4 pi^2 / g) * L   so the slope of the line is 4 pi^2 / g, and  g = 4 pi^2 / slope.
     * UNITS: the table shows L in centimetres, but the fit MUST use metres - otherwise the slope is
     * 100x too small and g comes out 100x too big. The conversion is done once, explicitly, below.
     * Talks to: PendulumController_Azm calls it; the graph, g chip and results read the result.
     */
    public static class PendulumAnalysis_Azm
    {
        public static PendulumAnalysisResult_Azm Analyse(IEnumerable<TrialRow_Azm> rows, PendulumConfig_Azm config)
        {
            var result = new PendulumAnalysisResult_Azm { points = new List<Vector2>() };

            foreach (var row in rows)
            {
                if (row.Discarded) continue;
                result.validCount++;
                // L is already stored in metres (EffectiveLengthM) - never use EffectiveLengthCm here.
                result.points.Add(new Vector2(row.EffectiveLengthM, row.PeriodSquared));
                result.maxPeriodSquared = Mathf.Max(result.maxPeriodSquared, row.PeriodSquared);
            }

            var fit = LinearFit_Azm.Fit(result.points);
            if (!fit.valid || fit.slope <= 0f) return result;

            result.hasFit = true;
            result.slope = fit.slope;
            result.intercept = fit.intercept;
            result.g = 4f * Mathf.PI * Mathf.PI / fit.slope;
            result.percentError = Mathf.Abs(result.g - config.trueGravity) / config.trueGravity * 100f;
            result.nearOrigin = Mathf.Abs(fit.intercept) <= config.originInterceptFraction * result.maxPeriodSquared;
            return result;
        }
    }
}
