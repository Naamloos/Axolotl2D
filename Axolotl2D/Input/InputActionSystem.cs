using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Input;

/// <summary>Updates scene-scoped action maps from the current keyboard and mouse.</summary>
public sealed class InputActionSystem(Game game)
{
    private readonly List<InputActionMap> maps = [];

    internal void Register(InputActionMap map) => maps.Add(map);
    internal void Unregister(InputActionMap map) => maps.Remove(map);
    internal bool IsPressed(Key key) => game.GetKeyboard()?.IsKeyPressed(key) == true;
    internal bool IsPressed(MouseButton button) => game.GetMouse()?.IsButtonPressed(button) == true;

    internal void Update()
    {
        foreach (var map in maps.ToArray())
            map.Update();
    }
}

/// <summary>A named input value with current and frame-transition state.</summary>
public sealed class InputAction(string name, Func<Vector2> read)
{
    public string Name { get; } = name;
    public Vector2 Value { get; private set; }
    public float Scalar => Value.X;
    public bool IsPressed => Value != Vector2.Zero;
    public bool WasPressedThisFrame { get; private set; }
    public bool WasReleasedThisFrame { get; private set; }

    internal void Update(bool enabled)
    {
        var wasPressed = IsPressed;
        Value = enabled ? read() : Vector2.Zero;
        WasPressedThisFrame = !wasPressed && IsPressed;
        WasReleasedThisFrame = wasPressed && !IsPressed;
    }
}

/// <summary>A scene-scoped collection of gameplay actions.</summary>
public sealed class InputActionMap : IDisposable
{
    private readonly InputActionSystem input;
    private readonly Dictionary<string, InputAction> actions = new(StringComparer.Ordinal);
    private bool disposed;

    public bool Enabled { get; set; } = true;
    public IReadOnlyCollection<InputAction> Actions => actions.Values;

    public InputActionMap(InputActionSystem input)
    {
        this.input = input;
        input.Register(this);
    }

    public InputAction BindButton(string name, Key key, params Key[] alternatives)
    {
        var keys = alternatives.Prepend(key).ToArray();
        return Add(name, () => keys.Any(input.IsPressed) ? Vector2.UnitX : Vector2.Zero);
    }

    public InputAction BindButton(string name, MouseButton button, params MouseButton[] alternatives)
    {
        var buttons = alternatives.Prepend(button).ToArray();
        return Add(name, () => buttons.Any(input.IsPressed) ? Vector2.UnitX : Vector2.Zero);
    }

    public InputAction BindAxis(string name, Key negative, Key positive) =>
        Add(name, () => new Vector2((input.IsPressed(positive) ? 1f : 0f) - (input.IsPressed(negative) ? 1f : 0f), 0f));

    public InputAction BindVector2(string name, Key left, Key right, Key up, Key down) =>
        Add(name, () => new Vector2(
            (input.IsPressed(right) ? 1f : 0f) - (input.IsPressed(left) ? 1f : 0f),
            (input.IsPressed(down) ? 1f : 0f) - (input.IsPressed(up) ? 1f : 0f)));

    public InputAction Get(string name) =>
        actions.TryGetValue(name, out var action)
            ? action
            : throw new KeyNotFoundException($"Input action '{name}' is not bound.");

    public bool TryGet(string name, out InputAction? action) => actions.TryGetValue(name, out action);
    public bool Remove(string name) => actions.Remove(name);

    private InputAction Add(string name, Func<Vector2> read)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var action = new InputAction(name, read);
        actions.Add(name, action);
        return action;
    }

    internal void Update()
    {
        foreach (var action in actions.Values)
            action.Update(Enabled);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        input.Unregister(this);
        actions.Clear();
        GC.SuppressFinalize(this);
    }
}
