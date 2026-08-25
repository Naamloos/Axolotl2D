using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>A reusable texture region with a normalized pivot.</summary>
public sealed class Sprite
{
    public Texture2D Texture { get; }
    public TextureArray2D? TextureArray { get; }
    public int TextureLayer { get; }
    internal bool IsSdf { get; init; }
    /// <summary>An optional tangent-space normal map using the same dimensions and source region as the color texture.</summary>
    public Texture2D? NormalMap { get; set; }
    public TextureRegion Source { get; }
    public Vector2 Origin { get; set; } = new(0.5f);

    public Vector2 Size => new(Source.Width, Source.Height);

    public Sprite(Texture2D texture, TextureRegion? source = null)
    {
        Texture = texture;
        Source = source ?? new TextureRegion(0, 0, texture.Width, texture.Height);
        Source.Validate(texture);
    }

    public Sprite(TextureArray2D textureArray, int layer, TextureRegion? source = null)
    {
        ArgumentNullException.ThrowIfNull(textureArray);
        if ((uint)layer >= (uint)textureArray.Layers) throw new ArgumentOutOfRangeException(nameof(layer));
        TextureArray = textureArray;
        TextureLayer = layer;
        Texture = textureArray.GetFallbackTexture(layer);
        NormalMap = textureArray.GetFallbackNormalMap(layer);
        Source = source ?? new TextureRegion(0, 0, textureArray.Width, textureArray.Height);
        if (Source.X < 0 || Source.Y < 0 || Source.Width <= 0 || Source.Height <= 0 ||
            Source.X + Source.Width > textureArray.Width || Source.Y + Source.Height > textureArray.Height)
            throw new ArgumentOutOfRangeException(nameof(source), "The region must fit inside its texture array layer.");
    }
}
