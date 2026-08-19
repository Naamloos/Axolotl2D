using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>A reusable texture region with a normalized pivot.</summary>
public sealed class Sprite
{
    public Texture2D Texture { get; }
    public TextureRegion Source { get; }
    public Vector2 Origin { get; set; } = new(0.5f);

    public Vector2 Size => new(Source.Width, Source.Height);

    public Sprite(Texture2D texture, TextureRegion? source = null)
    {
        Texture = texture;
        Source = source ?? new TextureRegion(0, 0, texture.Width, texture.Height);
        Source.Validate(texture);
    }
}
