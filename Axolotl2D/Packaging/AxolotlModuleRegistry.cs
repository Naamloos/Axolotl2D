using Axolotl2D.GameObjects;
using Axolotl2D.Scenes;

namespace Axolotl2D.Packages;

/// <summary>Creates a scene-owned object from the active scene scope.</summary>
public delegate GameObject AxolotlGameObjectFactory(
    IServiceProvider services,
    IGameObjectFactory objects,
    string name);

/// <summary>A scene registered by an executable package.</summary>
public sealed record AxolotlSceneRegistration(string Id, string PackageId, Type SceneType);

/// <summary>A GameObject factory registered by an executable package.</summary>
public sealed record AxolotlGameObjectRegistration(
    string Id,
    string PackageId,
    AxolotlGameObjectFactory Factory);

/// <summary>
/// Stores runtime extensions contributed by explicitly loaded executable packages.
/// Registrations use stable IDs so the game does not need compile-time access to package types.
/// </summary>
public sealed class AxolotlModuleRegistry
{
    private readonly Dictionary<string, AxolotlSceneRegistration> scenes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AxolotlGameObjectRegistration> gameObjects = new(StringComparer.Ordinal);
    private readonly Dictionary<(Type Contract, string Id), ExtensionRegistration> extensions = [];
    private readonly object gate = new();

    /// <summary>Gets a snapshot of registered package scenes.</summary>
    public IReadOnlyCollection<AxolotlSceneRegistration> Scenes
    {
        get { lock (gate) return scenes.Values.ToArray(); }
    }

    /// <summary>Gets a snapshot of registered package GameObject factories.</summary>
    public IReadOnlyCollection<AxolotlGameObjectRegistration> GameObjects
    {
        get { lock (gate) return gameObjects.Values.ToArray(); }
    }

    /// <summary>Gets a package scene by its stable ID.</summary>
    public AxolotlSceneRegistration GetScene(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (gate)
            return scenes.TryGetValue(id, out var registration)
                ? registration
                : throw new KeyNotFoundException($"No package scene is registered as '{id}'.");
    }

    /// <summary>Gets a package GameObject factory by its stable ID.</summary>
    public AxolotlGameObjectRegistration GetGameObject(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (gate)
            return gameObjects.TryGetValue(id, out var registration)
                ? registration
                : throw new KeyNotFoundException($"No package GameObject is registered as '{id}'.");
    }

    /// <summary>Gets a game-defined extension by contract and stable ID.</summary>
    public TContract GetExtension<TContract>(string id) where TContract : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (gate)
            return extensions.TryGetValue((typeof(TContract), id), out var registration)
                ? (TContract)registration.Extension
                : throw new KeyNotFoundException($"No package extension for '{typeof(TContract).FullName}' is registered as '{id}'.");
    }

    /// <summary>Gets all extensions registered for a game-defined contract.</summary>
    public IReadOnlyDictionary<string, TContract> GetExtensions<TContract>() where TContract : class
    {
        lock (gate)
            return extensions
                .Where(pair => pair.Key.Contract == typeof(TContract))
                .ToDictionary(pair => pair.Key.Id, pair => (TContract)pair.Value.Extension, StringComparer.Ordinal);
    }

    internal void Register(
        string packageId,
        IReadOnlyList<(string Id, Type SceneType)> sceneRegistrations,
        IReadOnlyList<(string Id, AxolotlGameObjectFactory Factory)> gameObjectRegistrations,
        IReadOnlyList<(Type Contract, string Id, object Extension)> extensionRegistrations)
    {
        lock (gate)
        {
            ValidateUniqueIds(sceneRegistrations.Select(item => item.Id), scenes.Keys, "scene");
            ValidateUniqueIds(gameObjectRegistrations.Select(item => item.Id), gameObjects.Keys, "GameObject");

            var extensionKeys = new HashSet<(Type Contract, string Id)>();
            foreach (var registration in extensionRegistrations)
            {
                var key = (registration.Contract, registration.Id);
                if (!extensionKeys.Add(key) || extensions.ContainsKey(key))
                    throw new InvalidOperationException(
                        $"A package extension for '{registration.Contract.FullName}' is already registered as '{registration.Id}'.");
            }

            foreach (var registration in sceneRegistrations)
                scenes.Add(registration.Id, new(registration.Id, packageId, registration.SceneType));
            foreach (var registration in gameObjectRegistrations)
                gameObjects.Add(registration.Id, new(registration.Id, packageId, registration.Factory));
            foreach (var registration in extensionRegistrations)
                extensions.Add((registration.Contract, registration.Id), new(packageId, registration.Extension));
        }
    }

    internal void RemovePackage(string packageId)
    {
        object[] removedExtensions;
        lock (gate)
        {
            foreach (var id in scenes.Where(pair => pair.Value.PackageId == packageId).Select(pair => pair.Key).ToArray())
                scenes.Remove(id);
            foreach (var id in gameObjects.Where(pair => pair.Value.PackageId == packageId).Select(pair => pair.Key).ToArray())
                gameObjects.Remove(id);
            var extensionKeys = extensions.Where(pair => pair.Value.PackageId == packageId).Select(pair => pair.Key).ToArray();
            removedExtensions = extensionKeys.Select(key => extensions[key].Extension).ToArray();
            foreach (var key in extensionKeys)
                extensions.Remove(key);
        }
        foreach (var disposable in removedExtensions.OfType<IDisposable>())
            disposable.Dispose();
    }

    private static void ValidateUniqueIds(IEnumerable<string> pending, IEnumerable<string> existing, string kind)
    {
        var ids = new HashSet<string>(existing, StringComparer.Ordinal);
        foreach (var id in pending)
            if (!ids.Add(id))
                throw new InvalidOperationException($"A package {kind} is already registered as '{id}'.");
    }

    private sealed record ExtensionRegistration(string PackageId, object Extension);
}
