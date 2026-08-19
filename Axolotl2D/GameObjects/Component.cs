namespace Axolotl2D.GameObjects;

/// <summary>Behavior attached to a <see cref="GameObject"/> and created through DI.</summary>
public abstract class Component : IDisposable
{
    private bool enabled = true;
    private bool awakened;
    private bool started;
    private bool active;
    private bool disposed;

    public GameObject GameObject { get; }
    public Transform Transform => GameObject.Transform;
    public bool IsActiveAndEnabled => active;
    public bool HasStarted => started;

    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value || disposed)
                return;
            enabled = value;
            RefreshActivation();
        }
    }

    protected Component(GameObject gameObject) => GameObject = gameObject;

    public virtual void Awake() { }
    public virtual void OnEnable() { }
    public virtual void Start() { }
    public virtual void FixedUpdate(double fixedDeltaTime) { }
    public virtual void Update(double deltaTime) { }
    public virtual void LateUpdate(double deltaTime) { }
    public virtual void Render() { }
    public virtual void OnDisable() { }
    public virtual void OnDestroy() { }

    internal void Attach()
    {
        if (awakened)
            return;
        awakened = true;
        Awake();
        RefreshActivation();
    }

    internal void RefreshActivation()
    {
        if (!awakened || disposed)
            return;

        var shouldBeActive = enabled && GameObject.Active;
        if (shouldBeActive && !active)
        {
            active = true;
            OnEnable();
        }
        else if (!shouldBeActive && active)
        {
            active = false;
            OnDisable();
        }

    }

    internal void StartIfNeeded()
    {
        if (active && GameObject.HasStarted && !started)
        {
            started = true;
            Start();
        }
    }

    internal void TickFixed(double fixedDeltaTime)
    {
        StartIfNeeded();
        if (active)
            FixedUpdate(fixedDeltaTime);
    }

    internal void Tick(double deltaTime)
    {
        StartIfNeeded();
        if (active)
            Update(deltaTime);
    }

    internal void TickLate(double deltaTime)
    {
        StartIfNeeded();
        if (active)
            LateUpdate(deltaTime);
    }

    internal void Draw()
    {
        StartIfNeeded();
        if (active)
            Render();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try
        {
            if (active)
            {
                active = false;
                OnDisable();
            }
        }
        finally
        {
            OnDestroy();
            GC.SuppressFinalize(this);
        }
    }
}
