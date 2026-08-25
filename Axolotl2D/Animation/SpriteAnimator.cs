using Axolotl2D.GameObjects;

namespace Axolotl2D.Animation;

/// <summary>Advances a SpriteRenderer through named sprite animations.</summary>
public sealed class SpriteAnimator(GameObject gameObject) : Component(gameObject)
{
    private readonly Dictionary<string, SpriteAnimation> animations = [];
    private SpriteRenderer renderer = null!;
    private SpriteAnimation? current;
    private double elapsed;
    private int frame;
    private int direction = 1;
    private float playbackSpeed = 1f;

    public string? CurrentAnimation { get; private set; }
    public bool IsPlaying { get; private set; }
    public int CurrentFrameIndex => frame;
    public float FrameProgress => current is null
        ? 0f
        : (float)Math.Clamp(elapsed / current.TimedFrames[frame].Duration, 0d, 1d);

    public float PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(PlaybackSpeed));
            playbackSpeed = value;
        }
    }

    public event Action<int>? FrameChanged;
    public event Action<string>? MarkerReached;
    public event Action? LoopCompleted;
    public event Action? Completed;

    public override void Start()
    {
        renderer = GameObject.GetComponent<SpriteRenderer>()
            ?? throw new InvalidOperationException("SpriteAnimator requires a SpriteRenderer on the same GameObject.");
        if (current is not null)
            renderer.Sprite = current.Frames[frame];
    }

    public void Add(string name, SpriteAnimation animation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(animation);
        animations.Add(name, animation);
    }

    public void Play(string name, bool restart = false)
    {
        if (!animations.TryGetValue(name, out var animation))
            throw new KeyNotFoundException($"Animation '{name}' is not registered.");
        if (!restart && CurrentAnimation == name && IsPlaying)
            return;

        CurrentAnimation = name;
        current = animation;
        elapsed = 0;
        frame = 0;
        direction = 1;
        IsPlaying = true;
        ApplyFrame(notify: true);
    }

    public void Stop() => IsPlaying = false;

    public void Pause() => IsPlaying = false;

    public void Resume()
    {
        if (current is null)
            return;
        var completedOnce = current.PlaybackMode == SpriteAnimationPlayback.Once &&
            frame == current.Frames.Count - 1 && elapsed >= current.TimedFrames[frame].Duration;
        if (!completedOnce)
            IsPlaying = true;
    }

    public void Restart()
    {
        if (CurrentAnimation is not null)
            Play(CurrentAnimation, restart: true);
    }

    public void SeekFrame(int index)
    {
        if (current is null)
            throw new InvalidOperationException("No animation is selected.");
        if ((uint)index >= (uint)current.Frames.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        frame = index;
        elapsed = 0d;
        direction = 1;
        ApplyFrame(notify: true);
    }

    public override void Update(double deltaTime)
    {
        if (!IsPlaying || current is null)
            return;

        elapsed += deltaTime * playbackSpeed;
        while (IsPlaying && elapsed >= current.TimedFrames[frame].Duration)
        {
            elapsed -= current.TimedFrames[frame].Duration;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        var completedLoop = false;
        switch (current!.PlaybackMode)
        {
            case SpriteAnimationPlayback.Once:
                if (frame == current.Frames.Count - 1)
                {
                    elapsed = current.TimedFrames[frame].Duration;
                    IsPlaying = false;
                    Completed?.Invoke();
                    return;
                }
                frame++;
                break;
            case SpriteAnimationPlayback.Loop:
                frame++;
                if (frame == current.Frames.Count)
                {
                    frame = 0;
                    completedLoop = true;
                }
                break;
            case SpriteAnimationPlayback.PingPong:
                if (current.Frames.Count == 1)
                {
                    completedLoop = true;
                }
                else
                {
                    var next = frame + direction;
                    if (next == current.Frames.Count)
                    {
                        direction = -1;
                        next = current.Frames.Count - 2;
                    }
                    else if (next < 0)
                    {
                        direction = 1;
                        next = 1;
                        completedLoop = true;
                    }
                    frame = next;
                }
                break;
        }
        ApplyFrame(notify: true);
        if (completedLoop)
            LoopCompleted?.Invoke();
    }

    private void ApplyFrame(bool notify)
    {
        if (current is null)
            return;
        var animation = current;
        var currentFrame = frame;
        if (renderer is not null)
            renderer.Sprite = animation.Frames[currentFrame];
        if (!notify)
            return;
        FrameChanged?.Invoke(currentFrame);
        if (animation.TimedFrames[currentFrame].Marker is { } marker)
            MarkerReached?.Invoke(marker);
    }
}
