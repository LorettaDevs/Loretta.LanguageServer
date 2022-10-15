using System.Diagnostics;

namespace Loretta.LanguageServer;

internal static class Performance
{
    private const long TicksPerMillisecond = 10000;
    private const long TicksPerSecond = TicksPerMillisecond * 1000;

    // performance-counter frequency, in counts per ticks.
    // This can speed up conversion from high frequency performance-counter
    // to ticks.
    private static readonly double s_tickFrequency = (double) TicksPerSecond / Stopwatch.Frequency;

    public static long GetTimestamp() => unchecked((long) (Stopwatch.GetTimestamp() * s_tickFrequency));
}