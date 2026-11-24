using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Qin.UnityPerformance.Cases
{
    public sealed class MeasurementCases
    {
        [Test]
        public void MatchedTicksReportTailCostAndPreserveEveryOutcome()
        {
            const int samples = 128;
            const int population = 4096;
            int[] values = new int[population];
            Random random = new Random(542);
            for (int i = 0; i < values.Length; i++)
                values[i] = random.Next(1, 1024);

            // Warm both paths before recording. The same membership and values drive each pair.
            SumActive(values, 4, false, out _);
            SumActive(values, 4, true, out _);
            long[] full = new long[samples];
            long[] bounded = new long[samples];
            int fullVisits = 0;
            int boundedVisits = 0;
            for (int tick = 0; tick < samples; tick++)
            {
                int active = tick % 8 == 0 ? population : 4;
                int first;
                int second;
                int checkedFull;
                int checkedBounded;
                if ((tick & 1) == 0)
                {
                    first = Measure(values, active, false, out full[tick], out checkedFull);
                    second = Measure(values, active, true, out bounded[tick], out checkedBounded);
                }
                else
                {
                    second = Measure(values, active, true, out bounded[tick], out checkedBounded);
                    first = Measure(values, active, false, out full[tick], out checkedFull);
                }
                Assert.That(second, Is.EqualTo(first), "Inventory sum differs at tick " + tick);
                fullVisits += checkedFull;
                boundedVisits += checkedBounded;
            }

            Assert.That(fullVisits, Is.EqualTo(samples * population));
            Assert.That(boundedVisits, Is.EqualTo(16 * population + 112 * 4));
            Report("full scan", full, fullVisits);
            Report("active range", bounded, boundedVisits);
        }

        private static int Measure(int[] values, int active, bool bounded, out long duration, out int visits)
        {
            long start = Stopwatch.GetTimestamp();
            int result = SumActive(values, active, bounded, out visits);
            duration = Stopwatch.GetTimestamp() - start;
            return result;
        }

        private static int SumActive(int[] values, int active, bool bounded, out int visits)
        {
            visits = bounded ? active : values.Length;
            int sum = 0;
            for (int i = 0; i < visits; i++)
                if (i < active) sum += values[i];
            return sum;
        }

        private static void Report(string path, long[] samples, int visits)
        {
            long[] sorted = (long[])samples.Clone();
            Array.Sort(sorted);
            int worstTick = Array.IndexOf(samples, sorted[sorted.Length - 1]);
            double microsPerTick = 1_000_000.0 / Stopwatch.Frequency;
            TestContext.Out.WriteLine(
                $"QIN_CASE {path} | samples={samples.Length} visits={visits} " +
                $"median_us={sorted[(sorted.Length - 1) / 2] * microsPerTick:F3} " +
                $"p95_us={sorted[(int)Math.Ceiling(samples.Length * 0.95) - 1] * microsPerTick:F3} " +
                $"max_us={samples[worstTick] * microsPerTick:F3} worst_tick={worstTick}");
        }
    }
}
