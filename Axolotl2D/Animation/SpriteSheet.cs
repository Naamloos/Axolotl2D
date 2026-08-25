using Axolotl2D.Rendering;

namespace Axolotl2D.Animation;

/// <summary>Splits a uniformly spaced texture atlas into row-major sprites.</summary>
public sealed class SpriteSheet
{
    private readonly List<Sprite> sprites = [];
    public Texture2D Texture { get; }
    public IReadOnlyList<Sprite> Sprites => sprites;

    public SpriteSheet(Texture2D texture, int frameWidth, int frameHeight, int margin = 0, int spacing = 0)
    {
        if (frameWidth <= 0 || frameHeight <= 0 || margin < 0 || spacing < 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));
        Texture = texture;

        for (var y = margin; y + frameHeight <= texture.Height - margin; y += frameHeight + spacing)
            for (var x = margin; x + frameWidth <= texture.Width - margin; x += frameWidth + spacing)
                sprites.Add(new Sprite(texture, new TextureRegion(x, y, frameWidth, frameHeight)));

        if (sprites.Count == 0)
            throw new ArgumentException("The frame dimensions do not fit inside the texture.");
    }

    public Sprite this[int index] => sprites[index];
}

/// <summary>A timed sprite sequence.</summary>
public sealed class SpriteAnimation
{
    public IReadOnlyList<SpriteAnimationFrame> TimedFrames { get; }
    public IReadOnlyList<Sprite> Frames { get; }
    public float FramesPerSecond { get; }
    public bool Loop => PlaybackMode != SpriteAnimationPlayback.Once;
    public SpriteAnimationPlayback PlaybackMode { get; }
    public double Duration { get; }

    public SpriteAnimation(IEnumerable<Sprite> frames, float framesPerSecond, bool loop = true)
    {
        if (!float.IsFinite(framesPerSecond) || framesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        ArgumentNullException.ThrowIfNull(frames);
        var duration = 1d / framesPerSecond;
        TimedFrames = frames.Select(frame => new SpriteAnimationFrame(frame, duration)).ToArray();
        ValidateFrames(TimedFrames, nameof(frames));
        Frames = TimedFrames.Select(frame => frame.Sprite).ToArray();
        FramesPerSecond = framesPerSecond;
        PlaybackMode = loop ? SpriteAnimationPlayback.Loop : SpriteAnimationPlayback.Once;
        Duration = TimedFrames.Sum(frame => frame.Duration);
    }

    public SpriteAnimation(IEnumerable<SpriteAnimationFrame> frames,
        SpriteAnimationPlayback playbackMode = SpriteAnimationPlayback.Loop)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (!Enum.IsDefined(playbackMode))
            throw new ArgumentOutOfRangeException(nameof(playbackMode));
        TimedFrames = frames.ToArray();
        ValidateFrames(TimedFrames, nameof(frames));
        Frames = TimedFrames.Select(frame => frame.Sprite).ToArray();
        var firstDuration = TimedFrames[0].Duration;
        FramesPerSecond = TimedFrames.All(frame => frame.Duration == firstDuration)
            ? (float)(1d / firstDuration)
            : 0f;
        PlaybackMode = playbackMode;
        Duration = TimedFrames.Sum(frame => frame.Duration);
    }

    private static void ValidateFrames(IReadOnlyList<SpriteAnimationFrame> frames, string parameterName)
    {
        if (frames.Count == 0)
            throw new ArgumentException("An animation needs at least one frame.", parameterName);
    }
}

public sealed class SpriteAnimationFrame
{
    public Sprite Sprite { get; }
    public double Duration { get; }
    public string? Marker { get; }

    public SpriteAnimationFrame(Sprite sprite, double duration, string? marker = null)
    {
        Sprite = sprite ?? throw new ArgumentNullException(nameof(sprite));
        if (!double.IsFinite(duration) || duration <= 0d)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (marker is not null && string.IsNullOrWhiteSpace(marker))
            throw new ArgumentException("Animation markers cannot be empty.", nameof(marker));
        Duration = duration;
        Marker = marker;
    }
}

public enum SpriteAnimationPlayback
{
    Once,
    Loop,
    PingPong
}
