namespace Axolotl2D.Assets;

/// <summary>Narrow mutable registry for loaders added by trusted runtime modules.</summary>
public sealed class AssetLoaderRegistry : IDisposable
{
    private readonly Dictionary<Type, object> loaders = [];
    private readonly object gate = new();

    internal void Register(IReadOnlyList<(Type AssetType, object Loader)> registrations)
    {
        lock (gate)
        {
            var types = new HashSet<Type>();
            foreach (var registration in registrations)
                if (!types.Add(registration.AssetType) || loaders.ContainsKey(registration.AssetType))
                    throw new InvalidOperationException($"A module asset loader for '{registration.AssetType.FullName}' is already registered.");
            foreach (var registration in registrations)
                loaders.Add(registration.AssetType, registration.Loader);
        }
    }

    internal bool TryGet<TAsset>(out IAssetLoader<TAsset>? loader) where TAsset : class
    {
        lock (gate)
        {
            loader = loaders.TryGetValue(typeof(TAsset), out var value) ? (IAssetLoader<TAsset>)value : null;
            return loader is not null;
        }
    }

    internal void Unregister(IReadOnlyList<Type> assetTypes)
    {
        lock (gate)
        {
            foreach (var assetType in assetTypes)
                if (loaders.Remove(assetType, out var loader) && loader is IDisposable disposable)
                    disposable.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            foreach (var loader in loaders.Values.OfType<IDisposable>()) loader.Dispose();
            loaders.Clear();
        }
    }
}
