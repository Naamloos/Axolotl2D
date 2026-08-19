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
    public IReadOnlyList<Sprite> Frames { get; }
    public float FramesPerSecond { get; }
    public bool Loop { get; }

    public SpriteAnimation(IEnumerable<Sprite> frames, float framesPerSecond, bool loop = true)
    {
        Frames = frames.ToArray();
        if (Frames.Count == 0)
            throw new ArgumentException("An animation needs at least one frame.", nameof(frames));
        if (framesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        FramesPerSecond = framesPerSecond;
        Loop = loop;
    }
}
