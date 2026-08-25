using Axolotl2D.GameObjects;
using Axolotl2D.Assets;
using Axolotl2D.Physics;
using Axolotl2D.Rendering;
using Axolotl2D.Timing;
using Axolotl2D.Debugging;
using Axolotl2D.Packages;
using Axolotl2D.UI;
using Axolotl2D.Prefabs;
using System.Numerics;

namespace Axolotl2D.Scenes;

/// <summary>A scoped scene that owns GameObjects and their components.</summary>
public abstract class BaseScene
{
    private readonly List<GameObject> gameObjects = [];
    private GameObject[] gameObjectSnapshot = [];
    private readonly HashSet<GameObject> pendingDestruction = [];
    private readonly Dictionary<string, List<GameObject>> objectsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GameObject>> objectsByTag = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, List<Component>> componentsByType = [];
    private IGameObjectFactory objectFactory = null!;
    private AxolotlModuleRegistry moduleRegistry = null!;
    private SpriteBatch spriteBatch = null!;
    private PhysicsWorld physics = null!;
    private TimeService time = null!;
    private TweenService tweens = null!;
    private CoroutineService coroutines = null!;
    private UIEventSystem uiEvents = null!;
    private PrefabComponentRegistry prefabComponents = null!;
    private AssetManager assets = null!;
    private DebugOverlay? debugOverlay;
    private double fixedAccumulator;
    private bool acceptsObjects;

    protected SceneGameHost SceneGameHost => sceneGameHost!;
    protected Game Game => game!;
    protected IServiceProvider Services { get; private set; } = null!;
    internal IServiceProvider ScopeServices => Services;
    public IReadOnlyList<GameObject> GameObjects => gameObjects;
    public bool IsLoaded { get; private set; }
    public double FixedTimeStep { get; set; } = 1d / 60d;
    public int MaximumFixedStepsPerFrame { get; set; } = 8;

    internal SceneGameHost? sceneGameHost;
    internal Game? game;

    /// <summary>Finds the first live GameObject with an exact, case-sensitive name.</summary>
    public GameObject? FindByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return objectsByName.TryGetValue(name, out var objects) ? objects[0] : null;
    }

    /// <summary>Finds all live GameObjects with an exact, case-sensitive name.</summary>
    public IReadOnlyList<GameObject> FindAllByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return objectsByName.TryGetValue(name, out var objects) ? objects.ToArray() : [];
    }

    /// <summary>Finds the first live GameObject with a case-sensitive tag.</summary>
    public GameObject? FindWithTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return objectsByTag.TryGetValue(tag, out var objects) ? objects[0] : null;
    }

    /// <summary>Finds all live GameObjects with a case-sensitive tag.</summary>
    public IReadOnlyList<GameObject> FindAllWithTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return objectsByTag.TryGetValue(tag, out var objects) ? objects.ToArray() : [];
    }

    /// <summary>Finds the first live component assignable to a type.</summary>
    public T? FindComponent<T>() where T : Component
        => componentsByType.TryGetValue(typeof(T), out var components) ? (T)components[0] : null;

    /// <summary>Finds all live components assignable to a type.</summary>
    public IReadOnlyList<T> FindComponents<T>() where T : Component
        => componentsByType.TryGetValue(typeof(T), out var components) ? components.Cast<T>().ToArray() : [];

    /// <summary>Creates a scene-owned GameObject. Runtime objects start after their components attach.</summary>
    public GameObject Instantiate(string name = "GameObject")
    {
        EnsureAcceptsObjects();
        return Add(objectFactory.Create(name));
    }

    /// <summary>Creates a scene-owned GameObject subtype through the scene's DI scope.</summary>
    public T Instantiate<T>(string name = "GameObject") where T : GameObject
    {
        EnsureAcceptsObjects();
        return Add(objectFactory.Create<T>(name));
    }

    /// <summary>Creates a scene-owned GameObject subtype discovered at runtime.</summary>
    public GameObject Instantiate(Type gameObjectType, string name = "GameObject")
    {
        EnsureAcceptsObjects();
        return Add(objectFactory.Create(gameObjectType, name));
    }

    /// <summary>Creates a scene-owned GameObject from a package registration.</summary>
    public GameObject InstantiateRegistered(string id, string? name = null)
    {
        EnsureAcceptsObjects();
        var registration = moduleRegistry.GetGameObject(id);
        var gameObject = registration.Factory(Services, objectFactory, name ?? id)
            ?? throw new InvalidOperationException($"Package GameObject factory '{id}' returned null.");
        return Add(gameObject);
    }

    /// <summary>Instantiates a data-authored prefab hierarchy through this scene's DI scope.</summary>
    public GameObject Instantiate(PrefabAsset prefab, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(prefab);
        var created = new List<(GameObject GameObject, PrefabObject Definition)>();
        var objectsById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        try
        {
            var root = CreatePrefabObjects(prefab.Root, null, name, created, objectsById);
            var context = new PrefabLoadContext(assets, objectsById);
            foreach (var (gameObject, definition) in created)
                foreach (var componentDefinition in definition.Components)
                {
                    var registration = prefabComponents.Get(componentDefinition.Type);
                    gameObject.AddComponent(registration.ComponentType, component =>
                    {
                        component.Enabled = componentDefinition.Enabled;
                        registration.Loader(component, componentDefinition.Data, context);
                    });
                }
            context.Complete();
            foreach (var (gameObject, definition) in created)
                gameObject.Active = definition.Active;
            return root;
        }
        catch
        {
            foreach (var (gameObject, _) in created)
                if (!gameObject.IsDestroyed) Destroy(gameObject);
            throw;
        }
    }

    /// <summary>Disables an object and schedules its disposal after the current scene phase.</summary>
    public bool Destroy(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        if (!gameObjects.Contains(gameObject) || !pendingDestruction.Add(gameObject))
            return false;
        gameObject.Active = false;
        UnindexObject(gameObject);
        return true;
    }

    [Obsolete("Use Instantiate instead.")]
    protected GameObject CreateGameObject(string name = "GameObject") => Instantiate(name);

    [Obsolete("Use Instantiate<T> instead.")]
    protected T CreateGameObject<T>(string name) where T : GameObject => Instantiate<T>(name);

    public virtual void Load() { }
    public virtual void FixedUpdate(double fixedDeltaTime) { }
    public virtual void Update(double deltaTime) { }
    public virtual void LateUpdate(double deltaTime) { }
    public virtual void Draw(double frameDelta, double frameRate) { }
    public virtual void Resize(Vector2 size) { }
    public virtual void Unload() { }

    internal void Initialize(IServiceProvider services)
    {
        Services = services;
        acceptsObjects = true;
        objectFactory = (IGameObjectFactory)(services.GetService(typeof(IGameObjectFactory))
            ?? throw new InvalidOperationException("IGameObjectFactory is not registered."));
        moduleRegistry = (AxolotlModuleRegistry)(services.GetService(typeof(AxolotlModuleRegistry))
            ?? throw new InvalidOperationException("AxolotlModuleRegistry is not registered."));
        spriteBatch = (SpriteBatch)(services.GetService(typeof(SpriteBatch))
            ?? throw new InvalidOperationException("SpriteBatch is not registered."));
        physics = (PhysicsWorld)(services.GetService(typeof(PhysicsWorld))
            ?? throw new InvalidOperationException("PhysicsWorld is not registered."));
        time = (TimeService)(services.GetService(typeof(TimeService))
            ?? throw new InvalidOperationException("TimeService is not registered."));
        tweens = (TweenService)(services.GetService(typeof(TweenService))
            ?? throw new InvalidOperationException("TweenService is not registered."));
        coroutines = (CoroutineService)(services.GetService(typeof(CoroutineService))
            ?? throw new InvalidOperationException("CoroutineService is not registered."));
        uiEvents = (UIEventSystem)(services.GetService(typeof(UIEventSystem))
            ?? throw new InvalidOperationException("UIEventSystem is not registered."));
        prefabComponents = (PrefabComponentRegistry)(services.GetService(typeof(PrefabComponentRegistry))
            ?? throw new InvalidOperationException("PrefabComponentRegistry is not registered."));
        assets = (AssetManager)(services.GetService(typeof(AssetManager))
            ?? throw new InvalidOperationException("AssetManager is not registered."));
        var debugOptions = services.GetService(typeof(DebugOverlayOptions)) as DebugOverlayOptions;
        if (debugOptions?.Enabled == true)
            debugOverlay = (DebugOverlay)(services.GetService(typeof(DebugOverlay))
                ?? throw new InvalidOperationException("DebugOverlay is not registered."));
    }

    internal void LoadScene()
    {
        Load();
        FlushDestroyed();
        IsLoaded = true;
        var snapshot = gameObjectSnapshot;
        foreach (var gameObject in snapshot)
            gameObject.Start();
    }

    internal void Tick(double deltaTime)
    {
        if (!IsLoaded)
            return;
        if (FixedTimeStep <= 0 || MaximumFixedStepsPerFrame <= 0)
            throw new InvalidOperationException("FixedTimeStep and MaximumFixedStepsPerFrame must be greater than zero.");

        fixedAccumulator += Math.Min(deltaTime, FixedTimeStep * MaximumFixedStepsPerFrame);
        var fixedSteps = 0;
        while (fixedAccumulator >= FixedTimeStep && fixedSteps++ < MaximumFixedStepsPerFrame && IsLoaded)
        {
            time.BeginFixedStep(FixedTimeStep);
            var snapshot = gameObjectSnapshot;
            foreach (var gameObject in snapshot)
                gameObject.FixedUpdate(FixedTimeStep);
            FixedUpdate(FixedTimeStep);
            FlushDestroyed();
            if (IsLoaded)
                physics.Step((float)FixedTimeStep);
            FlushDestroyed();
            fixedAccumulator -= FixedTimeStep;
        }

        if (!IsLoaded)
            return;
        tweens.Update(deltaTime, time.UnscaledDeltaTime);
        coroutines.Update(deltaTime, time.UnscaledDeltaTime);
        uiEvents.Update();
        var updateSnapshot = gameObjectSnapshot;
        foreach (var gameObject in updateSnapshot)
            gameObject.Update(deltaTime);
        Update(deltaTime);
        FlushDestroyed();

        if (!IsLoaded)
            return;
        var lateUpdateSnapshot = gameObjectSnapshot;
        foreach (var gameObject in lateUpdateSnapshot)
            gameObject.LateUpdate(deltaTime);
        LateUpdate(deltaTime);
        FlushDestroyed();
    }

    internal void Render(double deltaTime, double frameRate)
    {
        if (!IsLoaded)
            return;
        spriteBatch.Begin();
        try
        {
            var snapshot = gameObjectSnapshot;
            foreach (var gameObject in snapshot)
                gameObject.Render();
            Draw(deltaTime, frameRate);
            debugOverlay?.Render(this, physics, deltaTime, frameRate);
        }
        finally
        {
            try
            {
                spriteBatch.End();
            }
            finally
            {
                FlushDestroyed();
            }
        }
    }

    internal void UnloadScene()
    {
        acceptsObjects = false;
        IsLoaded = false;
        Unload();
        for (var index = gameObjects.Count - 1; index >= 0; index--)
            gameObjects[index].Dispose();
        gameObjects.Clear();
        gameObjectSnapshot = [];
        pendingDestruction.Clear();
        objectsByName.Clear();
        objectsByTag.Clear();
        componentsByType.Clear();
        fixedAccumulator = 0;
        debugOverlay = null;
    }

    private T Add<T>(T gameObject) where T : GameObject
    {
        gameObject.AssignTo(this);
        gameObjects.Add(gameObject);
        gameObjectSnapshot = gameObjects.ToArray();
        IndexObject(gameObject);
        if (IsLoaded)
            gameObject.Start();
        return gameObject;
    }

    private void FlushDestroyed()
    {
        while (pendingDestruction.Count > 0)
        {
            using var enumerator = pendingDestruction.GetEnumerator();
            enumerator.MoveNext();
            var gameObject = enumerator.Current;
            pendingDestruction.Remove(gameObject);
            gameObject.Dispose();
        }
    }

    internal void OnNameChanged(GameObject gameObject, string previous)
    {
        Remove(objectsByName, previous, gameObject);
        if (IsIndexed(gameObject)) Add(objectsByName, gameObject.Name, gameObject);
    }

    internal void OnTagAdded(GameObject gameObject, string tag)
    {
        if (IsIndexed(gameObject)) Add(objectsByTag, tag, gameObject);
    }

    internal void OnTagRemoved(GameObject gameObject, string tag) => Remove(objectsByTag, tag, gameObject);

    internal void OnComponentAdded(GameObject gameObject, Component component)
    {
        if (IsIndexed(gameObject)) IndexComponent(component);
    }

    internal void OnComponentRemoved(Component component) => UnindexComponent(component);

    internal void OnObjectDisposed(GameObject gameObject)
    {
        UnindexObject(gameObject);
        pendingDestruction.Remove(gameObject);
        gameObjects.Remove(gameObject);
        gameObjectSnapshot = gameObjects.ToArray();
    }

    private bool IsIndexed(GameObject gameObject)
        => gameObjects.Contains(gameObject) && !pendingDestruction.Contains(gameObject);

    private void IndexObject(GameObject gameObject)
    {
        Add(objectsByName, gameObject.Name, gameObject);
        foreach (var tag in gameObject.Tags) Add(objectsByTag, tag, gameObject);
        foreach (var component in gameObject.Components) IndexComponent(component);
    }

    private void UnindexObject(GameObject gameObject)
    {
        Remove(objectsByName, gameObject.Name, gameObject);
        foreach (var tag in gameObject.Tags) Remove(objectsByTag, tag, gameObject);
        foreach (var component in gameObject.Components) UnindexComponent(component);
    }

    private void IndexComponent(Component component)
    {
        for (var type = component.GetType(); type is not null && type.IsAssignableTo(typeof(Component)); type = type.BaseType)
            Add(componentsByType, type, component);
    }

    private void UnindexComponent(Component component)
    {
        for (var type = component.GetType(); type is not null && type.IsAssignableTo(typeof(Component)); type = type.BaseType)
            Remove(componentsByType, type, component);
    }

    private static void Add<TKey, TValue>(Dictionary<TKey, List<TValue>> index, TKey key, TValue value)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var values)) index.Add(key, values = []);
        values.Add(value);
    }

    private static void Remove<TKey, TValue>(Dictionary<TKey, List<TValue>> index, TKey key, TValue value)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var values)) return;
        values.Remove(value);
        if (values.Count == 0) index.Remove(key);
    }

    private void EnsureAcceptsObjects()
    {
        if (!acceptsObjects)
            throw new InvalidOperationException("GameObjects can only be instantiated while the scene is loading or active.");
    }

    private GameObject CreatePrefabObjects(PrefabObject definition, Transform? parent, string? rootName,
        List<(GameObject GameObject, PrefabObject Definition)> created,
        Dictionary<string, GameObject> objectsById)
    {
        var gameObject = Instantiate(rootName ?? definition.Name);
        created.Add((gameObject, definition));
        if (definition.Id is not null) objectsById.Add(definition.Id, gameObject);
        gameObject.Active = false;
        foreach (var tag in definition.Tags) gameObject.AddTag(tag);
        gameObject.Transform.SetParent(parent, worldPositionStays: false);
        gameObject.Transform.LocalPosition = definition.Transform.Position;
        gameObject.Transform.LocalRotation = definition.Transform.Rotation;
        gameObject.Transform.LocalScale = definition.Transform.Scale;
        foreach (var child in definition.Children)
            CreatePrefabObjects(child, gameObject.Transform, null, created, objectsById);
        return gameObject;
    }
}
