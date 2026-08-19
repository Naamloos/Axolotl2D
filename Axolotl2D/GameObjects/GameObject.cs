using Axolotl2D.Scenes;
using Microsoft.Extensions.DependencyInjection;

namespace Axolotl2D.GameObjects;

/// <summary>A scene-owned object composed from DI-created components.</summary>
public class GameObject : IDisposable
{
    private readonly IServiceProvider services;
    private readonly List<Component> components = [];
    private BaseScene? scene;
    private bool active = true;
    private bool disposed;

    public string Name { get; set; }
    public Transform Transform { get; } = new();
    public IReadOnlyList<Component> Components => components;
    public BaseScene Scene => scene ?? throw new InvalidOperationException("The GameObject does not belong to a scene.");
    public bool IsDestroyed => disposed;
    internal bool HasStarted { get; private set; }

    public bool Active
    {
        get => active;
        set
        {
            if (active == value || disposed)
                return;
            active = value;
            foreach (var component in components.ToArray())
                component.RefreshActivation();
        }
    }

    public GameObject(IServiceProvider services, string name = "GameObject")
    {
        this.services = services;
        Name = name;
    }

    public T AddComponent<T>() where T : Component
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (GetComponent<T>() is not null)
            throw new InvalidOperationException($"{Name} already has a {typeof(T).Name} component.");

        var component = ActivatorUtilities.CreateInstance<T>(services, this);
        components.Add(component);
        try
        {
            component.Attach();
            return component;
        }
        catch
        {
            components.Remove(component);
            component.Dispose();
            throw;
        }
    }

    public T? GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault();

    public bool RemoveComponent<T>() where T : Component
    {
        var component = GetComponent<T>();
        if (component is null)
            return false;
        components.Remove(component);
        component.Dispose();
        return true;
    }

    /// <summary>Marks this object for removal at the end of the current scene phase.</summary>
    public void Destroy() => Scene.Destroy(this);

    internal void AssignTo(BaseScene owner)
    {
        if (scene is not null && !ReferenceEquals(scene, owner))
            throw new InvalidOperationException("A GameObject cannot belong to two scenes.");
        scene = owner;
    }

    internal void Start()
    {
        if (HasStarted || disposed)
            return;
        HasStarted = true;
        foreach (var component in components.ToArray())
            component.StartIfNeeded();
    }

    internal void FixedUpdate(double fixedDeltaTime)
    {
        if (!active || disposed)
            return;
        foreach (var component in components.ToArray())
            component.TickFixed(fixedDeltaTime);
    }

    internal void Update(double deltaTime)
    {
        if (!active || disposed)
            return;
        foreach (var component in components.ToArray())
            component.Tick(deltaTime);
    }

    internal void LateUpdate(double deltaTime)
    {
        if (!active || disposed)
            return;
        foreach (var component in components.ToArray())
            component.TickLate(deltaTime);
    }

    internal void Render()
    {
        if (!active || disposed)
            return;
        foreach (var component in components.ToArray())
            component.Draw();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        active = false;
        foreach (var component in components.ToArray())
            component.RefreshActivation();
        for (var index = components.Count - 1; index >= 0; index--)
            components[index].Dispose();
        components.Clear();
        Transform.DetachHierarchy();
        scene = null;
        GC.SuppressFinalize(this);
    }
}

public interface IGameObjectFactory
{
    GameObject Create(string name = "GameObject");
    T Create<T>(string name) where T : GameObject;
}

internal sealed class GameObjectFactory(IServiceProvider services) : IGameObjectFactory
{
    public GameObject Create(string name = "GameObject") => new(services, name);
    public T Create<T>(string name) where T : GameObject => ActivatorUtilities.CreateInstance<T>(services, name);
}
