using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Input;

/// <summary>Identifies a standard gamepad analog axis.</summary>
public enum GamepadAxis
{
    LeftStickX,
    LeftStickY,
    RightStickX,
    RightStickY,
    LeftTrigger,
    RightTrigger
}

/// <summary>Identifies a standard two-dimensional gamepad stick.</summary>
public enum GamepadStick
{
    Left,
    Right
}

/// <summary>Updates scene-scoped action maps from current input devices.</summary>
public sealed class InputActionSystem(Game game)
{
    private readonly List<InputActionMap> maps = [];

    internal void Register(InputActionMap map) => maps.Add(map);
    internal void Unregister(InputActionMap map) => maps.Remove(map);
    internal bool IsPressed(Key key) => game.GetKeyboard()?.IsKeyPressed(key) == true;
    internal bool IsPressed(MouseButton button) => game.GetMouse()?.IsButtonPressed(button) == true;
    internal bool IsPressed(int gamepadIndex, ButtonName button) =>
        game.GetGamepad(gamepadIndex)?.Buttons.FirstOrDefault(value => value.Name == button).Pressed == true;

    internal float ReadAxis(int gamepadIndex, GamepadAxis axis, float deadZone)
    {
        var gamepad = game.GetGamepad(gamepadIndex);
        if (gamepad is null) return 0f;
        var value = axis switch
        {
            GamepadAxis.LeftStickX => ReadStick(gamepad, 0).X,
            GamepadAxis.LeftStickY => ReadStick(gamepad, 0).Y,
            GamepadAxis.RightStickX => ReadStick(gamepad, 1).X,
            GamepadAxis.RightStickY => ReadStick(gamepad, 1).Y,
            GamepadAxis.LeftTrigger => ReadTrigger(gamepad, 0),
            GamepadAxis.RightTrigger => ReadTrigger(gamepad, 1),
            _ => 0f
        };
        return ApplyDeadZone(value, deadZone);
    }

    internal Vector2 ReadStick(int gamepadIndex, GamepadStick stick, float deadZone)
    {
        var gamepad = game.GetGamepad(gamepadIndex);
        if (gamepad is null) return Vector2.Zero;
        var value = ReadStick(gamepad, stick == GamepadStick.Left ? 0 : 1);
        value.Y = -value.Y;
        var length = value.Length();
        if (length <= deadZone) return Vector2.Zero;
        var magnitude = Math.Min(1f, (length - deadZone) / (1f - deadZone));
        return value / length * magnitude;
    }

    internal void Update()
    {
        foreach (var map in maps.ToArray())
            map.Update();
    }

    internal static void ValidateDeadZone(float deadZone)
    {
        if (!float.IsFinite(deadZone) || deadZone < 0f || deadZone >= 1f)
            throw new ArgumentOutOfRangeException(nameof(deadZone), "A dead zone must be at least zero and less than one.");
    }

    internal static void ValidateGamepadIndex(int gamepadIndex)
    {
        if (gamepadIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(gamepadIndex), "A gamepad index cannot be negative.");
    }

    private static Vector2 ReadStick(IGamepad gamepad, int index) =>
        index < gamepad.Thumbsticks.Count
            ? new Vector2(gamepad.Thumbsticks[index].X, gamepad.Thumbsticks[index].Y)
            : Vector2.Zero;

    private static float ReadTrigger(IGamepad gamepad, int index) =>
        index < gamepad.Triggers.Count ? gamepad.Triggers[index].Position : 0f;

    private static float ApplyDeadZone(float value, float deadZone) =>
        MathF.Abs(value) <= deadZone
            ? 0f
            : MathF.CopySign((MathF.Abs(value) - deadZone) / (1f - deadZone), value);
}

/// <summary>A named input value with current and frame-transition state.</summary>
public sealed class InputAction(string name, Func<Vector2> initialRead)
{
    private Func<Vector2> read = initialRead;

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

    internal void Rebind(Func<Vector2> replacement) => read = replacement;
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

    public InputAction BindButton(string name, ButtonName button, params ButtonName[] alternatives) =>
        BindButton(name, 0, button, alternatives);

    public InputAction BindButton(string name, int gamepadIndex, ButtonName button,
        params ButtonName[] alternatives)
    {
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        var buttons = alternatives.Prepend(button).ToArray();
        return Add(name, ButtonReader(gamepadIndex, buttons));
    }

    public InputAction BindAxis(string name, Key negative, Key positive) =>
        Add(name, () => new Vector2((input.IsPressed(positive) ? 1f : 0f) - (input.IsPressed(negative) ? 1f : 0f), 0f));

    public InputAction BindVector2(string name, Key left, Key right, Key up, Key down) =>
        Add(name, () => new Vector2(
            (input.IsPressed(right) ? 1f : 0f) - (input.IsPressed(left) ? 1f : 0f),
            (input.IsPressed(down) ? 1f : 0f) - (input.IsPressed(up) ? 1f : 0f)));

    public InputAction BindAxis(string name, GamepadAxis axis, float deadZone = 0.15f,
        int gamepadIndex = 0)
    {
        InputActionSystem.ValidateDeadZone(deadZone);
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        return Add(name, () => new Vector2(input.ReadAxis(gamepadIndex, axis, deadZone), 0f));
    }

    public InputAction BindVector2(string name, GamepadStick stick, float deadZone = 0.15f,
        int gamepadIndex = 0)
    {
        InputActionSystem.ValidateDeadZone(deadZone);
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        return Add(name, () => input.ReadStick(gamepadIndex, stick, deadZone));
    }

    public InputAction RebindButton(string name, Key key, params Key[] alternatives)
    {
        var keys = alternatives.Prepend(key).ToArray();
        return Rebind(name, () => keys.Any(input.IsPressed) ? Vector2.UnitX : Vector2.Zero);
    }

    public InputAction RebindButton(string name, MouseButton button, params MouseButton[] alternatives)
    {
        var buttons = alternatives.Prepend(button).ToArray();
        return Rebind(name, () => buttons.Any(input.IsPressed) ? Vector2.UnitX : Vector2.Zero);
    }

    public InputAction RebindButton(string name, ButtonName button, params ButtonName[] alternatives) =>
        RebindButton(name, 0, button, alternatives);

    public InputAction RebindButton(string name, int gamepadIndex, ButtonName button,
        params ButtonName[] alternatives)
    {
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        var buttons = alternatives.Prepend(button).ToArray();
        return Rebind(name, ButtonReader(gamepadIndex, buttons));
    }

    public InputAction RebindAxis(string name, Key negative, Key positive) =>
        Rebind(name, () => new Vector2(
            (input.IsPressed(positive) ? 1f : 0f) - (input.IsPressed(negative) ? 1f : 0f), 0f));

    public InputAction RebindAxis(string name, GamepadAxis axis, float deadZone = 0.15f,
        int gamepadIndex = 0)
    {
        InputActionSystem.ValidateDeadZone(deadZone);
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        return Rebind(name, () => new Vector2(input.ReadAxis(gamepadIndex, axis, deadZone), 0f));
    }

    public InputAction RebindVector2(string name, Key left, Key right, Key up, Key down) =>
        Rebind(name, () => new Vector2(
            (input.IsPressed(right) ? 1f : 0f) - (input.IsPressed(left) ? 1f : 0f),
            (input.IsPressed(down) ? 1f : 0f) - (input.IsPressed(up) ? 1f : 0f)));

    public InputAction RebindVector2(string name, GamepadStick stick, float deadZone = 0.15f,
        int gamepadIndex = 0)
    {
        InputActionSystem.ValidateDeadZone(deadZone);
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        return Rebind(name, () => input.ReadStick(gamepadIndex, stick, deadZone));
    }

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

    private InputAction Rebind(string name, Func<Vector2> read)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var action = Get(name);
        action.Rebind(read);
        return action;
    }

    private Func<Vector2> ButtonReader(int gamepadIndex, IReadOnlyCollection<ButtonName> buttons) =>
        () => buttons.Any(button => input.IsPressed(gamepadIndex, button)) ? Vector2.UnitX : Vector2.Zero;

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
