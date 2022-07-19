using System;
using System.Diagnostics;

namespace Utils
{
    /// <summary>
    /// Various utilities to manipulate StopWatch ticks.
    /// </summary>
    static class StopwatchUtils
    {
        /// <summary>
        /// Returns the elapsed time between now and referenceTimestamp.
        /// </summary>
        /// <param name="referenceTimestamp"><see cref="Stopwatch.GetTimestamp"/> from which to compute the elapsed
        /// time.</param>
        /// <returns></returns>
        public static TimeSpan ElapsedSince(long referenceTimestamp)
        {
            long now = Stopwatch.GetTimestamp();
            return new TimeSpan((now - referenceTimestamp) * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
        }

        /// <summary>
        /// Returns the <see cref="Stopwatch.GetTimestamp"/> value after <paramref name="interval"/> has passed.
        /// </summary>
        /// <param name="interval">Time interval</param>
        public static long TimestampIn(TimeSpan interval)
        {
            return Stopwatch.GetTimestamp() + (interval.Ticks * Stopwatch.Frequency) / TimeSpan.TicksPerSecond;
        }

        /// <summary>
        /// Returns the time to go before reaching the specified timestamp.
        /// </summary>
        /// <param name="targetTimestamp">Future timestamp as it would be returned by
        /// <see cref="Stopwatch.GetTimestamp"/>.</param>
        public static TimeSpan TimeUntil(long targetTimestamp)
        {
            long now = Stopwatch.GetTimestamp();
            if (targetTimestamp > now)
            {
                return new TimeSpan((targetTimestamp - now) * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
            }
            else
            {
                return TimeSpan.Zero;
            }
        }
    }
}
