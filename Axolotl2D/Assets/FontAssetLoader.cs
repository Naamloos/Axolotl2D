using SkiaSharp;

namespace Axolotl2D.Assets;

/// <summary>A loaded TrueType or OpenType font family.</summary>
public sealed class FontAsset : IDisposable
{
    internal SKTypeface Typeface { get; }

    internal FontAsset(SKTypeface typeface)
    {
        Typeface = typeface;
    }

    /// <summary>The family name reported by the font.</summary>
    public string Name => Typeface.FamilyName;

    /// <inheritdoc />
    public void Dispose() => Typeface.Dispose();
}

/// <summary>Loads scalable fonts for <see cref="Axolotl2D.Rendering.TextRenderer"/>.</summary>
public sealed class FontAssetLoader : IAssetLoader<FontAsset>
{
    /// <inheritdoc />
    public async ValueTask<FontAsset> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        using var data = SKData.CreateCopy(buffer.ToArray());
        var typeface = SKTypeface.FromData(data)
            ?? throw new InvalidDataException("The stream does not contain a supported TrueType or OpenType font.");
        return new FontAsset(typeface);
    }
}
