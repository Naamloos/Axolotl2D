using Axolotl2D.Rendering;
using StbImageSharp;

namespace Axolotl2D.Assets;

/// <summary>Loads PNG, JPEG, BMP, and other stb-supported images as RGBA textures.</summary>
public sealed class TextureAssetLoader : IAssetLoader<Texture2D>
{
    /// <inheritdoc />
    public ValueTask<Texture2D> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        return ValueTask.FromResult(new Texture2D(image.Width, image.Height, image.Data));
    }
}
