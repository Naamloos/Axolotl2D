using System.Collections;
using System.Numerics;

namespace Axolotl2D.Timing;

public enum Ease
{
    Linear,
    InQuad,
    OutQuad,
    InOutQuad,
    SmoothStep,
    OutBack
}

public sealed record TweenOptions(
    Ease Ease = Ease.Linear,
    double Delay = 0d,
    int RepeatCount = 0,
    bool Yoyo = false,
    bool UnscaledTime = false);

public sealed class TweenHandle
{
    private Action? cancel;
    internal TweenHandle(Action cancel) => this.cancel = cancel;
    public bool IsComplete { get; internal set; }
    public bool IsCancelled { get; private set; }
    public void Cancel()
    {
        if (IsComplete || IsCancelled) return;
        IsCancelled = true;
        Interlocked.Exchange(ref cancel, null)?.Invoke();
    }
}

/// <summary>Runs scene-scoped value interpolation with easing, delays, repeats, and scaled or unscaled time.</summary>
public sealed class TweenService : IDisposable
{
    private readonly List<TweenState> tweens = [];
    private TweenState[] snapshot = [];

    public TweenHandle To(float from, float to, double duration, Action<float> apply,
        TweenOptions? options = null, Action? completed = null) =>
        Start(duration, progress => apply(from + (to - from) * progress), options, completed);

    public TweenHandle To(Vector2 from, Vector2 to, double duration, Action<Vector2> apply,
        TweenOptions? options = null, Action? completed = null) =>
        Start(duration, progress => apply(Vector2.Lerp(from, to, progress)), options, completed);

    public TweenHandle To(Color from, Color to, double duration, Action<Color> apply,
        TweenOptions? options = null, Action? completed = null) => Start(duration, progress => apply(new Color(
            from.R + (to.R - from.R) * progress,
            from.G + (to.G - from.G) * progress,
            from.B + (to.B - from.B) * progress,
            from.A + (to.A - from.A) * progress)), options, completed);

    public TweenHandle Start(double duration, Action<float> apply, TweenOptions? options = null, Action? completed = null)
    {
        if (duration <= 0d || double.IsNaN(duration) || double.IsInfinity(duration))
            throw new ArgumentOutOfRangeException(nameof(duration));
        ArgumentNullException.ThrowIfNull(apply);
        options ??= new();
        if (options.Delay < 0d || options.RepeatCount < -1)
            throw new ArgumentOutOfRangeException(nameof(options));
        TweenState? state = null;
        var handle = new TweenHandle(() => Remove(state));
        state = new(duration, apply, completed, options, handle);
        tweens.Add(state);
        snapshot = tweens.ToArray();
        apply(Evaluate(options.Ease, 0f));
        return handle;
    }

    internal void Update(double scaledDeltaTime, double unscaledDeltaTime)
    {
        var active = snapshot;
        foreach (var tween in active)
        {
            if (tween.Handle.IsCancelled)
                continue;
            var delta = tween.Options.UnscaledTime ? unscaledDeltaTime : scaledDeltaTime;
            if (tween.DelayRemaining > 0d)
            {
                tween.DelayRemaining -= delta;
                if (tween.DelayRemaining > 0d) continue;
                delta = -tween.DelayRemaining;
            }
            tween.Elapsed += delta;
            var progress = Math.Clamp((float)(tween.Elapsed / tween.Duration), 0f, 1f);
            if (tween.Reversed) progress = 1f - progress;
            tween.Apply(Evaluate(tween.Options.Ease, progress));
            if (tween.Elapsed < tween.Duration) continue;

            if (tween.Options.RepeatCount == -1 || tween.CompletedRepeats < tween.Options.RepeatCount)
            {
                tween.CompletedRepeats++;
                tween.Elapsed -= tween.Duration;
                if (tween.Options.Yoyo) tween.Reversed = !tween.Reversed;
                continue;
            }
            Remove(tween);
            tween.Handle.IsComplete = true;
            tween.Completed?.Invoke();
        }
    }

    public void Dispose()
    {
        var active = snapshot;
        foreach (var tween in active) tween.Handle.Cancel();
        tweens.Clear();
        snapshot = [];
    }

    private void Remove(TweenState? state)
    {
        if (state is null || !tweens.Remove(state)) return;
        snapshot = tweens.ToArray();
    }

    private static float Evaluate(Ease ease, float value) => ease switch
    {
        Ease.Linear => value,
        Ease.InQuad => value * value,
        Ease.OutQuad => 1f - (1f - value) * (1f - value),
        Ease.InOutQuad => value < 0.5f ? 2f * value * value : 1f - MathF.Pow(-2f * value + 2f, 2f) / 2f,
        Ease.SmoothStep => value * value * (3f - 2f * value),
        Ease.OutBack => 1f + 2.70158f * MathF.Pow(value - 1f, 3f) + 1.70158f * MathF.Pow(value - 1f, 2f),
        _ => throw new ArgumentOutOfRangeException(nameof(ease))
    };

    private sealed class TweenState(double duration, Action<float> apply, Action? completed,
        TweenOptions options, TweenHandle handle)
    {
        public double Duration { get; } = duration;
        public Action<float> Apply { get; } = apply;
        public Action? Completed { get; } = completed;
        public TweenOptions Options { get; } = options;
        public TweenHandle Handle { get; } = handle;
        public double DelayRemaining { get; set; } = options.Delay;
        public double Elapsed { get; set; }
        public int CompletedRepeats { get; set; }
        public bool Reversed { get; set; }
    }
}

public abstract record CoroutineYield;
public sealed record WaitForSeconds(double Duration, bool UnscaledTime = false) : CoroutineYield;
public sealed record WaitUntil(Func<bool> Predicate) : CoroutineYield;
public sealed record WaitForNextFrame : CoroutineYield
{
    public static WaitForNextFrame Instance { get; } = new();
}

public sealed class CoroutineHandle
{
    private Action? cancel;
    internal CoroutineHandle(Action cancel) => this.cancel = cancel;
    public bool IsComplete { get; internal set; }
    public bool IsCancelled { get; private set; }
    public void Cancel()
    {
        if (IsComplete || IsCancelled) return;
        IsCancelled = true;
        Interlocked.Exchange(ref cancel, null)?.Invoke();
    }
}

/// <summary>Runs scene-scoped iterator coroutines with frame, time, and predicate waits.</summary>
public sealed class CoroutineService : IDisposable
{
    private readonly List<CoroutineState> coroutines = [];
    private CoroutineState[] snapshot = [];

    public CoroutineHandle Start(IEnumerable<CoroutineYield?> routine)
    {
        ArgumentNullException.ThrowIfNull(routine);
        CoroutineState? state = null;
        var handle = new CoroutineHandle(() => Remove(state));
        state = new(routine.GetEnumerator(), handle);
        coroutines.Add(state);
        snapshot = coroutines.ToArray();
        Advance(state);
        return handle;
    }

    internal void Update(double scaledDeltaTime, double unscaledDeltaTime)
    {
        var active = snapshot;
        foreach (var state in active)
        {
            if (state.Handle.IsCancelled)
                continue;
            var ready = state.Current switch
            {
                null => true,
                WaitForNextFrame => true,
                WaitUntil wait => wait.Predicate(),
                WaitForSeconds wait => (state.Remaining -= wait.UnscaledTime ? unscaledDeltaTime : scaledDeltaTime) <= 0d,
                _ => throw new InvalidOperationException($"Unknown coroutine yield type {state.Current.GetType().FullName}.")
            };
            if (ready) Advance(state);
        }
    }

    public void Dispose()
    {
        var active = snapshot;
        foreach (var state in active) state.Handle.Cancel();
        coroutines.Clear();
        snapshot = [];
    }

    private void Advance(CoroutineState state)
    {
        if (!state.Enumerator.MoveNext())
        {
            Remove(state);
            state.Handle.IsComplete = true;
            return;
        }
        state.Current = state.Enumerator.Current;
        state.Remaining = state.Current is WaitForSeconds wait
            ? wait.Duration >= 0d ? wait.Duration : throw new ArgumentOutOfRangeException(nameof(wait.Duration))
            : 0d;
    }

    private void Remove(CoroutineState? state)
    {
        if (state is null || !coroutines.Remove(state)) return;
        snapshot = coroutines.ToArray();
        state.Enumerator.Dispose();
    }

    private sealed class CoroutineState(IEnumerator<CoroutineYield?> enumerator, CoroutineHandle handle)
    {
        public IEnumerator<CoroutineYield?> Enumerator { get; } = enumerator;
        public CoroutineHandle Handle { get; } = handle;
        public CoroutineYield? Current { get; set; }
        public double Remaining { get; set; }
    }
}
