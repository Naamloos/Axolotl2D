using System.Collections.Concurrent;
using System.Reflection;
using Axolotl2D.Packages;

namespace Axolotl2D.Assets;

/// <summary>Loads and caches typed assets through DI-provided loaders.</summary>
public sealed class AssetManager(IServiceProvider services, AssetLoaderRegistry loaderRegistry,
    AxolotlPackageManager packages) : IDisposable
{
    private readonly ConcurrentDictionary<(Type Type, string Key), Lazy<Task<object>>> assets = [];

    /// <summary>Loads an asset once and caches it under a type-safe key.</summary>
    public async ValueTask<TAsset> LoadAsync<TAsset>(string key, Stream stream, CancellationToken cancellationToken = default)
        where TAsset : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(stream);

        var assetKey = (typeof(TAsset), key);
        var lazy = assets.GetOrAdd(assetKey, _ => new Lazy<Task<object>>(
            async () => await GetLoader<TAsset>().LoadAsync(stream, cancellationToken).ConfigureAwait(false),
            LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return (TAsset)await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            assets.TryRemove(assetKey, out _);
            throw;
        }
    }

    /// <summary>Loads an asset from a file and caches it.</summary>
    public async ValueTask<TAsset> LoadFileAsync<TAsset>(string key, string path, CancellationToken cancellationToken = default)
        where TAsset : class
    {
        await using var stream = File.OpenRead(path);
        return await LoadAsync<TAsset>(key, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads an embedded resource and caches it.</summary>
    public async ValueTask<TAsset> LoadEmbeddedAsync<TAsset>(string key, Assembly assembly, string resourceName, CancellationToken cancellationToken = default)
        where TAsset : class
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' was not found.", resourceName);
        return await LoadAsync<TAsset>(key, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads an asset by package ID and unambiguous package-local name.</summary>
    public async ValueTask<TAsset> LoadPackageAsync<TAsset>(string key, string packageId, string assetName,
        CancellationToken cancellationToken = default) where TAsset : class
    {
        var package = packages.Get(packageId);
        var metadata = package.GetAsset(assetName);
        var expectedType = typeof(TAsset).FullName!;
        if (!string.Equals(metadata.RuntimeType, expectedType, StringComparison.Ordinal) &&
            !metadata.RuntimeType.StartsWith(expectedType + ",", StringComparison.Ordinal))
            throw new InvalidOperationException($"Package asset '{packageId}:{assetName}' is '{metadata.RuntimeType}', not '{expectedType}'.");
        await using var stream = package.OpenAsset(assetName);
        return await LoadAsync<TAsset>(key, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns a previously loaded asset.</summary>
    public TAsset Get<TAsset>(string key) where TAsset : class =>
        TryGet<TAsset>(key, out var asset)
            ? asset!
            : throw new KeyNotFoundException($"Asset '{key}' of type {typeof(TAsset).Name} is not loaded.");

    /// <summary>Attempts to return a loaded asset without blocking for in-progress loads.</summary>
    public bool TryGet<TAsset>(string key, out TAsset? asset) where TAsset : class
    {
        asset = null;
        return assets.TryGetValue((typeof(TAsset), key), out var lazy)
            && lazy.IsValueCreated
            && lazy.Value.IsCompletedSuccessfully
            && (asset = (TAsset)lazy.Value.Result) is not null;
    }

    /// <summary>Removes and disposes an asset when it has finished loading.</summary>
    public bool Unload<TAsset>(string key) where TAsset : class
    {
        if (!assets.TryRemove((typeof(TAsset), key), out var lazy))
            return false;

        if (lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully && lazy.Value.Result is IDisposable disposable)
            disposable.Dispose();
        return true;
    }

    /// <summary>Returns a point-in-time view of cached and in-progress assets.</summary>
    public IReadOnlyList<AssetInfo> GetLoadedAssets() => assets
        .Select(entry => new AssetInfo(
            entry.Key.Type,
            entry.Key.Key,
            !entry.Value.IsValueCreated ? AssetLoadState.Pending
                : !entry.Value.Value.IsCompleted ? AssetLoadState.Loading
                : entry.Value.Value.IsCompletedSuccessfully ? AssetLoadState.Loaded
                : AssetLoadState.Faulted))
        .OrderBy(info => info.Type.Name)
        .ThenBy(info => info.Key, StringComparer.Ordinal)
        .ToArray();

    private IAssetLoader<TAsset> GetLoader<TAsset>() where TAsset : class =>
        loaderRegistry.TryGet<TAsset>(out var loader) ? loader! :
        services.GetService(typeof(IAssetLoader<TAsset>)) as IAssetLoader<TAsset>
        ?? throw new InvalidOperationException($"No IAssetLoader<{typeof(TAsset).Name}> is registered.");

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var lazy in assets.Values)
            if (lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully && lazy.Value.Result is IDisposable disposable)
                disposable.Dispose();
        assets.Clear();
    }
}

/// <summary>The current state of a cached asset.</summary>
public enum AssetLoadState
{
    Pending,
    Loading,
    Loaded,
    Faulted
}

/// <summary>Identifies one entry in the asset cache.</summary>
public readonly record struct AssetInfo(Type Type, string Key, AssetLoadState State);
