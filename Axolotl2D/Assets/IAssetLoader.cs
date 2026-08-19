namespace Axolotl2D.Assets;

/// <summary>Converts an asset stream into an engine asset.</summary>
public interface IAssetLoader<TAsset> where TAsset : class
{
    /// <summary>Loads the complete stream. The caller retains ownership of the stream.</summary>
    ValueTask<TAsset> LoadAsync(Stream stream, CancellationToken cancellationToken = default);
}
