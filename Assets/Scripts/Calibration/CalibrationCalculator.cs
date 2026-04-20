using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeyFlow
{
    public static class CalibrationCalculator
    {
        public struct Result
        {
            public int offsetMs;
            public int madMs;
            public bool reliable;
        }

        public static Result Compute(double[] expectedDspTimes, double[] tapDspTimes)
        {
            if (tapDspTimes == null || tapDspTimes.Length == 0)
                return new Result { offsetMs = 0, madMs = 0, reliable = false };

            int pairCount = Math.Min(tapDspTimes.Length, expectedDspTimes.Length);
            var deltas = new List<double>(pairCount);
            for (int i = 0; i < pairCount; i++)
            {
                deltas.Add(tapDspTimes[i] - expectedDspTimes[i]);
            }

            deltas.Sort();
            // Trim outliers when enough samples
            if (deltas.Count >= 6)
            {
                deltas.RemoveAt(deltas.Count - 1);
                deltas.RemoveAt(0);
            }

            double median = Median(deltas);
            var absDev = new List<double>(deltas.Count);
            foreach (var d in deltas) absDev.Add(Math.Abs(d - median));
            absDev.Sort();
            double mad = Median(absDev);

            int offsetMs = Mathf.RoundToInt((float)(median * 1000.0));
            offsetMs = Mathf.Clamp(offsetMs, -500, 500);
            int madMs = Mathf.RoundToInt((float)(mad * 1000.0));

            return new Result
            {
                offsetMs = offsetMs,
                madMs = madMs,
                reliable = madMs <= 50
            };
        }

        private static double Median(List<double> sorted)
        {
            if (sorted.Count == 0) return 0.0;
            int n = sorted.Count;
            return (n % 2 == 1) ? sorted[n / 2] : 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]);
        }
    }
}
