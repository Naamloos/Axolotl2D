using System.Reflection;
using System.Runtime.Loader;
using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Scenes;
using Axolotl2D.Prefabs;
using Microsoft.Extensions.DependencyInjection;

namespace Axolotl2D.Packages;

/// <summary>Optional explicit entrypoint for executable module initialization.</summary>
public interface IAxolotlModule
{
    /// <summary>Initializes trusted module code.</summary>
    void Initialize(AxolotlModuleContext context);
}

/// <summary>Contract implemented by compile-time generated module registration.</summary>
public interface IAxolotlModuleRegistration
{
    /// <summary>Registers generated module services without assembly-wide scanning.</summary>
    void Register(AxolotlModuleContext context);
}

/// <summary>The intentionally small API exposed while initializing trusted module code.</summary>
public sealed class AxolotlModuleContext
{
    private readonly AssetLoaderRegistry loaders;
    private readonly AxolotlModuleRegistry modules;
    private readonly PrefabComponentRegistry prefabs;
    private readonly List<(Type AssetType, object Loader)> registrations = [];
    private readonly List<(string Id, Type SceneType)> scenes = [];
    private readonly List<(string Id, AxolotlGameObjectFactory Factory)> gameObjects = [];
    private readonly List<(Type Contract, string Id, object Extension)> extensions = [];
    private readonly List<PrefabComponentRegistration> prefabComponents = [];
    private bool committed;

    internal AxolotlModuleContext(AxolotlPackage package, AssetLoaderRegistry loaders,
        AxolotlModuleRegistry modules, PrefabComponentRegistry prefabs, IServiceProvider services)
    {
        Package = package;
        this.loaders = loaders;
        this.modules = modules;
        this.prefabs = prefabs;
        Services = services;
    }

    /// <summary>The package being initialized.</summary>
    public AxolotlPackage Package { get; }

    /// <summary>The game's root service provider. Registrations cannot mutate the built container.</summary>
    public IServiceProvider Services { get; }

    internal IReadOnlyList<Type> RegisteredAssetTypes { get; private set; } = [];

    /// <summary>Stages a runtime loader for registration after initialization succeeds.</summary>
    public void RegisterAssetLoader(Type assetType, Type loaderType)
    {
        ArgumentNullException.ThrowIfNull(assetType);
        ArgumentNullException.ThrowIfNull(loaderType);
        var expected = typeof(IAssetLoader<>).MakeGenericType(assetType);
        if (!expected.IsAssignableFrom(loaderType))
            throw new InvalidOperationException($"{loaderType.FullName} does not implement IAssetLoader<{assetType.FullName}>.");
        registrations.Add((assetType, ActivatorUtilities.CreateInstance(Services, loaderType)));
    }

    /// <summary>Stages a package scene under a stable game-wide ID.</summary>
    public void RegisterScene<TScene>(string id) where TScene : BaseScene
        => RegisterScene(id, typeof(TScene));

    /// <summary>Stages a package scene type discovered at build time.</summary>
    public void RegisterScene(string id, Type sceneType)
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(sceneType);
        if (!sceneType.IsAssignableTo(typeof(BaseScene)) || sceneType.IsAbstract)
            throw new ArgumentException($"{sceneType.FullName} must be a concrete BaseScene type.", nameof(sceneType));
        scenes.Add((id, sceneType));
    }

    /// <summary>Stages a package GameObject factory under a stable game-wide ID.</summary>
    public void RegisterGameObject(string id, AxolotlGameObjectFactory factory)
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(factory);
        gameObjects.Add((id, factory));
    }

    /// <summary>Stages a trusted module component under a stable prefab ID.</summary>
    public void RegisterPrefabComponent<TComponent>(string id)
        where TComponent : Component, IPrefabDataReceiver
    {
        ValidateId(id);
        prefabComponents.Add(PrefabComponentRegistration.Create<TComponent>(id));
    }

    /// <summary>Stages an implementation of a game-defined extension contract.</summary>
    public void RegisterExtension<TContract>(string id, TContract extension) where TContract : class
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(extension);
        extensions.Add((typeof(TContract), id, extension));
    }

    internal void Commit()
    {
        var registeredAssetTypes = registrations.Select(registration => registration.AssetType).ToArray();
        loaders.Register(registrations);
        try
        {
            modules.Register(Package.Manifest.Id, scenes, gameObjects, extensions);
            prefabs.Register(Package.Manifest.Id, prefabComponents);
            RegisteredAssetTypes = registeredAssetTypes;
            committed = true;
            registrations.Clear();
            scenes.Clear();
            gameObjects.Clear();
            extensions.Clear();
            prefabComponents.Clear();
        }
        catch
        {
            modules.RemovePackage(Package.Manifest.Id);
            prefabs.RemovePackage(Package.Manifest.Id);
            loaders.Unregister(registeredAssetTypes);
            registrations.Clear();
            throw;
        }
    }

    internal void Cancel()
    {
        if (committed)
        {
            modules.RemovePackage(Package.Manifest.Id);
            prefabs.RemovePackage(Package.Manifest.Id);
            loaders.Unregister(RegisteredAssetTypes);
            RegisteredAssetTypes = [];
            committed = false;
            return;
        }
        foreach (var registration in registrations)
            if (registration.Loader is IDisposable disposable) disposable.Dispose();
        foreach (var extension in extensions.Select(registration => registration.Extension).OfType<IDisposable>())
            extension.Dispose();
        registrations.Clear();
        scenes.Clear();
        gameObjects.Clear();
        extensions.Clear();
        prefabComponents.Clear();
    }

    private static void ValidateId(string id) => ArgumentException.ThrowIfNullOrWhiteSpace(id);
}

/// <summary>Explicitly mounts validated packages and only then loads permitted module code.</summary>
public sealed class AxolotlPackageManager(
    IServiceProvider services,
    AssetLoaderRegistry loaders,
    AxolotlModuleRegistry modules,
    PrefabComponentRegistry prefabs) : IDisposable
{
    private readonly Dictionary<string, MountedAxolotlPackage> mounted = new(StringComparer.Ordinal);
    private bool disposed;

    /// <summary>The packages explicitly mounted in this manager.</summary>
    public IReadOnlyCollection<MountedAxolotlPackage> MountedPackages => mounted.Values;

    /// <summary>Validates and mounts a package, loading code only when the policy permits it.</summary>
    public ValueTask<MountedAxolotlPackage> LoadAsync(string path, PackageTrustPolicy trustPolicy,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(trustPolicy);
        cancellationToken.ThrowIfCancellationRequested();
        var package = AxolotlPackageReader.Open(path);
        ModuleLoadContext? loadContext = null;
        AxolotlModuleContext? context = null;
        try
        {
            if (mounted.ContainsKey(package.Manifest.Id))
                throw new InvalidOperationException($"Package '{package.Manifest.Id}' is already mounted.");
            ValidateDependencies(package.Manifest);
            trustPolicy.Validate(package);
            cancellationToken.ThrowIfCancellationRequested();

            Assembly? assembly = null;
            if (trustPolicy.AllowExecutableCode)
            {
                var assemblyEntry = package.Entries.Single(entry => entry.Name == package.Manifest.Assembly);
                if (assemblyEntry.Length > AxolotlPackageFormat.MaximumAssemblyBytes)
                    throw new InvalidDataException($"Module assembly '{assemblyEntry.Name}' is unreasonably large.");
                loadContext = new ModuleLoadContext(package, ResolveDependencyAssembly);
                using var assemblyStream = package.OpenEntry(package.Manifest.Assembly);
                assembly = loadContext.LoadFromStream(assemblyStream);
                context = new AxolotlModuleContext(package, loaders, modules, prefabs, services);
                InitializeKnownType<IAxolotlModuleRegistration>(assembly, package.Manifest.RegistrationType, context);
                InitializeKnownType<IAxolotlModule>(assembly, package.Manifest.Entrypoint, context);
                context.Commit();
            }

            var result = new MountedAxolotlPackage(
                package, assembly, loadContext, modules, loaders, prefabs, context?.RegisteredAssetTypes ?? []);
            mounted.Add(package.Manifest.Id, result);
            return ValueTask.FromResult(result);
        }
        catch
        {
            context?.Cancel();
            loadContext?.Unload();
            package.Dispose();
            throw;
        }
    }

    /// <summary>Gets an explicitly mounted package by ID.</summary>
    public MountedAxolotlPackage Get(string packageId) => mounted.TryGetValue(packageId, out var package)
        ? package
        : throw new KeyNotFoundException($"Package '{packageId}' is not mounted.");

    private void ValidateDependencies(AxolotlPackageManifest manifest)
    {
        foreach (var dependency in manifest.Dependencies)
        {
            if (!mounted.TryGetValue(dependency.Id, out var package))
                throw new InvalidOperationException($"Package '{manifest.Id}' requires explicitly mounted package '{dependency.Id}' version {dependency.Version}.");
            if (!string.Equals(package.Manifest.Version, dependency.Version, StringComparison.Ordinal))
                throw new InvalidOperationException($"Package '{manifest.Id}' requires '{dependency.Id}' version {dependency.Version}, but {package.Manifest.Version} is mounted.");
        }
    }

    private Assembly? ResolveDependencyAssembly(AssemblyName name)
    {
        foreach (var package in mounted.Values)
            if (package.Assembly is not null && AssemblyName.ReferenceMatchesDefinition(package.Assembly.GetName(), name))
                return package.Assembly;
        return null;
    }

    private static void InitializeKnownType<T>(Assembly assembly, string? typeName, AxolotlModuleContext context)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return;
        var type = assembly.GetType(typeName, throwOnError: true, ignoreCase: false)!;
        if (Activator.CreateInstance(type) is not T instance)
            throw new InvalidOperationException($"Module type '{typeName}' does not implement {typeof(T).Name}.");
        switch (instance)
        {
            case IAxolotlModuleRegistration registration: registration.Register(context); break;
            case IAxolotlModule module: module.Initialize(context); break;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var package in mounted.Values) package.Dispose();
        mounted.Clear();
    }
}

/// <summary>A mounted package and its optional trusted module assembly.</summary>
public sealed class MountedAxolotlPackage : IDisposable
{
    private readonly ModuleLoadContext? loadContext;
    private readonly AxolotlModuleRegistry modules;
    private readonly AssetLoaderRegistry loaders;
    private readonly PrefabComponentRegistry prefabs;
    private readonly IReadOnlyList<Type> registeredAssetTypes;
    private bool disposed;
    internal MountedAxolotlPackage(AxolotlPackage package, Assembly? assembly, ModuleLoadContext? loadContext,
        AxolotlModuleRegistry modules, AssetLoaderRegistry loaders, PrefabComponentRegistry prefabs,
        IReadOnlyList<Type> registeredAssetTypes)
    {
        Package = package;
        Assembly = assembly;
        this.loadContext = loadContext;
        this.modules = modules;
        this.loaders = loaders;
        this.prefabs = prefabs;
        this.registeredAssetTypes = registeredAssetTypes;
    }
    /// <summary>The validated package index.</summary>
    public AxolotlPackage Package { get; }
    /// <summary>The package manifest.</summary>
    public AxolotlPackageManifest Manifest => Package.Manifest;
    /// <summary>The loaded module assembly, or null under content-only policy.</summary>
    public Assembly? Assembly { get; }
    /// <summary>Opens a bounded stream for a package-local asset.</summary>
    public Stream OpenAsset(string name)
    {
        var asset = Manifest.Assets.FirstOrDefault(asset => asset.Name == name)
            ?? throw new KeyNotFoundException($"Package '{Manifest.Id}' does not contain asset '{name}'.");
        return Package.OpenEntry(asset.Entry);
    }
    /// <summary>Gets package-local asset metadata.</summary>
    public AxolotlPackageAsset GetAsset(string name) => Manifest.Assets.FirstOrDefault(asset => asset.Name == name)
        ?? throw new KeyNotFoundException($"Package '{Manifest.Id}' does not contain asset '{name}'.");
    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        modules.RemovePackage(Manifest.Id);
        prefabs.RemovePackage(Manifest.Id);
        loaders.Unregister(registeredAssetTypes);
        loadContext?.Unload();
        Package.Dispose();
    }
}

internal sealed class ModuleLoadContext(AxolotlPackage package, Func<AssemblyName, Assembly?> dependencyResolver)
    : AssemblyLoadContext($"Axolotl:{package.Manifest.Id}", isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var defaultAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate =>
            AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), assemblyName));
        if (defaultAssembly is not null) return defaultAssembly;
        var dependency = dependencyResolver(assemblyName);
        if (dependency is not null) return dependency;
        var entryName = $"$module/{assemblyName.Name}.dll";
        if (!package.TryGetEntry(entryName, out var entry)) return null;
        if (entry!.Length > AxolotlPackageFormat.MaximumAssemblyBytes)
            throw new InvalidDataException($"Module dependency assembly '{entryName}' is unreasonably large.");
        using var stream = package.OpenEntry(entryName);
        return LoadFromStream(stream);
    }
}
