using System;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Exponential-backoff-with-jitter reconnect timing (per the 2026 robust-sender guidance): delays double
    /// from <c>baseSeconds</c> (1s → 2s → 4s …), cap at <c>maxSeconds</c>, and get ±<c>jitterSeconds</c> of
    /// jitter to decouple many devices reconnecting at once. <see cref="Reset"/> on a successful connection.
    /// Pure and deterministic (the jitter source is injectable) so the cadence is unit-tested.
    /// </summary>
    public sealed class BackoffPolicy
    {
        private const double FloorSeconds = 0.1;

        private readonly double baseSeconds;
        private readonly double maxSeconds;
        private readonly double jitterSeconds;
        private readonly Func<double> jitter; // returns a value in [-1, 1]
        private readonly Random random = new Random(12345); // fixed seed keeps runs reproducible; jitter only decorrelates
        private int failures;

        public BackoffPolicy(double baseSeconds = 1.0, double maxSeconds = 30.0, double jitterSeconds = 0.5, Func<double>? jitter = null)
        {
            this.baseSeconds = baseSeconds;
            this.maxSeconds = maxSeconds;
            this.jitterSeconds = jitterSeconds;
            this.jitter = jitter ?? DefaultJitter;
        }

        /// <summary>Number of failures recorded since the last <see cref="Reset"/>.</summary>
        public int FailureCount => failures;

        /// <summary>The wait before the next attempt; records one failure (grows the next delay).</summary>
        public TimeSpan NextDelay()
        {
            var exponential = baseSeconds * Math.Pow(2, failures);
            var capped = Math.Min(maxSeconds, exponential);
            failures++;

            var seconds = Math.Max(FloorSeconds, capped + jitterSeconds * jitter());
            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>Call on a successful connection to return to the base delay.</summary>
        public void Reset() => failures = 0;

        private double DefaultJitter() => (random.NextDouble() * 2.0) - 1.0;
    }
}
