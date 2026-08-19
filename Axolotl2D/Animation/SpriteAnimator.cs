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

    public string? CurrentAnimation { get; private set; }
    public bool IsPlaying { get; private set; }

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
        IsPlaying = true;
        if (renderer is not null)
            renderer.Sprite = current.Frames[0];
    }

    public void Stop() => IsPlaying = false;

    public override void Update(double deltaTime)
    {
        if (!IsPlaying || current is null)
            return;

        elapsed += deltaTime;
        var nextFrame = (int)(elapsed * current.FramesPerSecond);
        if (current.Loop)
            nextFrame %= current.Frames.Count;
        else if (nextFrame >= current.Frames.Count)
        {
            nextFrame = current.Frames.Count - 1;
            IsPlaying = false;
        }

        if (nextFrame != frame)
        {
            frame = nextFrame;
            renderer.Sprite = current.Frames[frame];
        }
    }
}
