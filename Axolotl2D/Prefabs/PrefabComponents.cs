using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axolotl2D.Prefabs;

/// <summary>Lets a component own the schema and application of its prefab data.</summary>
public interface IPrefabDataReceiver
{
    void LoadPrefabData(JsonElement data, PrefabLoadContext context);
}

/// <summary>Services and hierarchy references available while component prefab data is applied.</summary>
public sealed class PrefabLoadContext
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    private readonly IReadOnlyDictionary<string, GameObject> objects;
    private readonly List<Action> deferred = [];
    private bool completed;

    internal PrefabLoadContext(AssetManager assets, IReadOnlyDictionary<string, GameObject> objects)
    {
        Assets = assets;
        this.objects = objects;
    }

    public AssetManager Assets { get; }

    public TData Deserialize<TData>(JsonElement data)
        => data.Deserialize<TData>(JsonOptions)
            ?? throw new InvalidDataException($"Prefab data for {typeof(TData).Name} cannot be null.");

    public TAsset GetAsset<TAsset>(string key) where TAsset : class => Assets.Get<TAsset>(key);

    public GameObject GetObject(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return objects.TryGetValue(id, out var gameObject)
            ? gameObject
            : throw new KeyNotFoundException($"The prefab does not contain an object with ID '{id}'.");
    }

    public TComponent GetComponent<TComponent>(string objectId) where TComponent : Component =>
        GetObject(objectId).GetComponent<TComponent>()
        ?? throw new InvalidDataException($"Prefab object '{objectId}' does not have a {typeof(TComponent).Name} component.");

    /// <summary>Runs reference binding after every component in the prefab has been created.</summary>
    public void Defer(Action bind)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        ArgumentNullException.ThrowIfNull(bind);
        deferred.Add(bind);
    }

    internal void Complete()
    {
        foreach (var bind in deferred) bind();
        deferred.Clear();
        completed = true;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public delegate void PrefabComponentLoader(Component component, JsonElement data, PrefabLoadContext context);

/// <summary>Maps a stable prefab component ID to a DI-created component type and data loader.</summary>
public sealed class PrefabComponentRegistration
{
    public string Id { get; }
    public Type ComponentType { get; }
    internal PrefabComponentLoader Loader { get; }

    private PrefabComponentRegistration(string id, Type componentType, PrefabComponentLoader loader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length > 256) throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(loader);
        if (!typeof(Component).IsAssignableFrom(componentType) || componentType.IsAbstract)
            throw new ArgumentException($"{componentType.FullName} must be a concrete Component type.", nameof(componentType));
        Id = id;
        ComponentType = componentType;
        Loader = loader;
    }

    public static PrefabComponentRegistration Create<TComponent>(string id)
        where TComponent : Component, IPrefabDataReceiver =>
        new(id, typeof(TComponent), static (component, data, context) =>
            ((TComponent)component).LoadPrefabData(data, context));

    public static PrefabComponentRegistration Create<TComponent, TData>(string id,
        Action<TComponent, TData, PrefabLoadContext> load)
        where TComponent : Component
    {
        ArgumentNullException.ThrowIfNull(load);
        return new(id, typeof(TComponent), (component, data, context) =>
            load((TComponent)component, context.Deserialize<TData>(data), context));
    }
}

/// <summary>Resolves stable prefab component IDs from the game and trusted modules.</summary>
public sealed class PrefabComponentRegistry
{
    private readonly Dictionary<string, RegisteredComponent> registrations = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public PrefabComponentRegistry(IEnumerable<PrefabComponentRegistration> gameRegistrations)
    {
        Register(null, BuiltInPrefabComponents.Registrations);
        Register(null, gameRegistrations.ToArray());
    }

    public IReadOnlyCollection<string> ComponentIds
    {
        get { lock (gate) return registrations.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray(); }
    }

    internal PrefabComponentRegistration Get(string id)
    {
        lock (gate)
            return registrations.TryGetValue(id, out var registration)
                ? registration.Registration
                : throw new KeyNotFoundException($"No prefab component is registered as '{id}'.");
    }

    internal void Register(string? packageId, IReadOnlyList<PrefabComponentRegistration> pending)
    {
        lock (gate)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var registration in pending)
                if (!ids.Add(registration.Id) || registrations.ContainsKey(registration.Id))
                    throw new InvalidOperationException($"A prefab component is already registered as '{registration.Id}'.");
            foreach (var registration in pending)
                registrations.Add(registration.Id, new(packageId, registration));
        }
    }

    internal void RemovePackage(string packageId)
    {
        lock (gate)
            foreach (var id in registrations.Where(pair => pair.Value.PackageId == packageId).Select(pair => pair.Key).ToArray())
                registrations.Remove(id);
    }

    private sealed record RegisteredComponent(string? PackageId, PrefabComponentRegistration Registration);
}
