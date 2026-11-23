using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core (practical-agnostic maths).
     * Purpose: Ordinary least-squares straight-line fit y = slope * x + intercept through a set of
     * (x, y) points. Copied and adapted from the older pendulum code's LinearRegression so the new
     * code does not depend on the old one. Any practical that plots a best-fit line can reuse it.
     * Talks to: pure maths - called by PendulumAnalysis_Azm.
     */
    public static class LinearFit_Azm
    {
        public struct Result
        {
            public bool valid;       // false when there are fewer than 2 points or all x are equal
            public float slope;
            public float intercept;
        }

        public static Result Fit(IList<Vector2> points)
        {
            int n = points.Count;
            var result = new Result();
            if (n < 2) return result;

            // Sums in double precision: squares of small T^2 values lose accuracy in float.
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += points[i].x;
                sumY += points[i].y;
                sumXY += (double)points[i].x * points[i].y;
                sumXX += (double)points[i].x * points[i].x;
            }

            double denominator = (n * sumXX) - (sumX * sumX);
            if (System.Math.Abs(denominator) < 1e-12) return result;

            double slope = ((n * sumXY) - (sumX * sumY)) / denominator;
            double intercept = (sumY - (slope * sumX)) / n;
            result.valid = true;
            result.slope = (float)slope;
            result.intercept = (float)intercept;
            return result;
        }
    }
}
