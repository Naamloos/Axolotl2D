namespace Axolotl2D.Rendering;

/// <summary>An RGBA texture shared by every sprite that references it.</summary>
public sealed class Texture2D
{
    internal byte[]? Pixels { get; }
    internal uint Handle { get; set; }

    public int Width { get; internal set; }
    public int Height { get; internal set; }

    public Texture2D(int width, int height, byte[] rgbaPixels)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
        if (rgbaPixels.Length != width * height * 4)
            throw new ArgumentException("Texture data must contain exactly four bytes per pixel.", nameof(rgbaPixels));

        Width = width;
        Height = height;
        Pixels = rgbaPixels;
    }

    internal Texture2D(int width, int height, uint handle)
    {
        Width = width;
        Height = height;
        Handle = handle;
    }
}

/// <summary>A pixel rectangle inside a texture.</summary>
public readonly record struct TextureRegion(int X, int Y, int Width, int Height)
{
    internal void Validate(Texture2D texture)
    {
        if (X < 0 || Y < 0 || Width <= 0 || Height <= 0 || X + Width > texture.Width || Y + Height > texture.Height)
            throw new ArgumentOutOfRangeException(nameof(texture), "The region must fit inside its texture.");
    }
}
