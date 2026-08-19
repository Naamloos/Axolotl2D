using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using System.Numerics;

namespace Axolotl2D.Scenes;

/// <summary>A scoped scene that owns GameObjects and their components.</summary>
public abstract class BaseScene
{
    private readonly List<GameObject> gameObjects = [];
    private readonly HashSet<GameObject> pendingDestruction = [];
    private IGameObjectFactory objectFactory = null!;
    private SpriteBatch spriteBatch = null!;
    private double fixedAccumulator;
    private bool acceptsObjects;

    protected SceneGameHost SceneGameHost => sceneGameHost!;
    protected Game Game => game!;
    protected IServiceProvider Services { get; private set; } = null!;
    public IReadOnlyList<GameObject> GameObjects => gameObjects;
    public bool IsLoaded { get; private set; }
    public double FixedTimeStep { get; set; } = 1d / 60d;
    public int MaximumFixedStepsPerFrame { get; set; } = 8;

    internal SceneGameHost? sceneGameHost;
    internal Game? game;

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

    /// <summary>Disables an object and schedules its disposal after the current scene phase.</summary>
    public bool Destroy(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        if (!gameObjects.Contains(gameObject) || !pendingDestruction.Add(gameObject))
            return false;
        gameObject.Active = false;
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
        spriteBatch = (SpriteBatch)(services.GetService(typeof(SpriteBatch))
            ?? throw new InvalidOperationException("SpriteBatch is not registered."));
    }

    internal void LoadScene()
    {
        Load();
        FlushDestroyed();
        IsLoaded = true;
        foreach (var gameObject in gameObjects.ToArray())
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
            foreach (var gameObject in gameObjects.ToArray())
                gameObject.FixedUpdate(FixedTimeStep);
            FixedUpdate(FixedTimeStep);
            FlushDestroyed();
            fixedAccumulator -= FixedTimeStep;
        }

        if (!IsLoaded)
            return;
        foreach (var gameObject in gameObjects.ToArray())
            gameObject.Update(deltaTime);
        Update(deltaTime);
        FlushDestroyed();

        if (!IsLoaded)
            return;
        foreach (var gameObject in gameObjects.ToArray())
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
            foreach (var gameObject in gameObjects.ToArray())
                gameObject.Render();
            Draw(deltaTime, frameRate);
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
        pendingDestruction.Clear();
        fixedAccumulator = 0;
    }

    private T Add<T>(T gameObject) where T : GameObject
    {
        gameObject.AssignTo(this);
        gameObjects.Add(gameObject);
        if (IsLoaded)
            gameObject.Start();
        return gameObject;
    }

    private void FlushDestroyed()
    {
        while (pendingDestruction.Count > 0)
        {
            var batch = pendingDestruction.ToArray();
            pendingDestruction.Clear();
            foreach (var gameObject in batch)
            {
                gameObjects.Remove(gameObject);
                gameObject.Dispose();
            }
        }
    }

    private void EnsureAcceptsObjects()
    {
        if (!acceptsObjects)
            throw new InvalidOperationException("GameObjects can only be instantiated while the scene is loading or active.");
    }
}
