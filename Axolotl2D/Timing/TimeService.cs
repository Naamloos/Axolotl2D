namespace Axolotl2D.Timing;

/// <summary>Provides scaled frame time, fixed-step time, totals, and counters.</summary>
public sealed class TimeService
{
    private double timeScale = 1d;

    public double DeltaTime { get; private set; }
    public double UnscaledDeltaTime { get; private set; }
    public double FixedDeltaTime { get; private set; } = 1d / 60d;
    public double TotalTime { get; private set; }
    public double UnscaledTotalTime { get; private set; }
    public ulong FrameCount { get; private set; }
    public ulong FixedFrameCount { get; private set; }
    public bool IsPaused { get; set; }

    public double TimeScale
    {
        get => timeScale;
        set
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "TimeScale must be finite and greater than or equal to zero.");
            timeScale = value;
        }
    }

    internal void BeginFrame(double unscaledDeltaTime)
    {
        UnscaledDeltaTime = unscaledDeltaTime;
        DeltaTime = IsPaused ? 0d : unscaledDeltaTime * TimeScale;
        UnscaledTotalTime += UnscaledDeltaTime;
        TotalTime += DeltaTime;
        FrameCount++;
    }

    internal void BeginFixedStep(double fixedDeltaTime)
    {
        FixedDeltaTime = fixedDeltaTime;
        FixedFrameCount++;
    }
}
