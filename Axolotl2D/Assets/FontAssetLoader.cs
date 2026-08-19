using SixLabors.Fonts;

namespace Axolotl2D.Assets;

/// <summary>A loaded TrueType, OpenType, WOFF, or WOFF2 font family.</summary>
public sealed class FontAsset
{
    internal FontCollection Collection { get; }
    internal FontFamily Family { get; }

    internal FontAsset(FontCollection collection, FontFamily family)
    {
        Collection = collection;
        Family = family;
    }

    /// <summary>The family name reported by the font.</summary>
    public string Name => Family.Name;
}

/// <summary>Loads scalable fonts for <see cref="Axolotl2D.Rendering.TextRenderer"/>.</summary>
public sealed class FontAssetLoader : IAssetLoader<FontAsset>
{
    /// <inheritdoc />
    public ValueTask<FontAsset> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var collection = new FontCollection();
        return ValueTask.FromResult(new FontAsset(collection, collection.Add(stream)));
    }
}
