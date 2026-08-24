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
    private readonly List<CaptureRegistration> captures = [];
    private HashSet<InputControl> previousPressed = [];

    internal void Register(InputActionMap map) => maps.Add(map);
    internal void Unregister(InputActionMap map) => maps.Remove(map);
    internal bool IsPressed(Key key) => game.GetKeyboard()?.IsKeyPressed(key) == true;
    internal bool IsPressed(MouseButton button) => game.GetMouse()?.IsButtonPressed(button) == true;
    internal bool IsPressed(int gamepadIndex, ButtonName button) =>
        game.GetGamepad(gamepadIndex)?.Buttons.FirstOrDefault(value => value.Name == button).Pressed == true;

    internal bool IsPressed(InputControl control)
    {
        control.Validate(buttonOnly: true);
        return control.Kind switch
        {
            InputControlKind.Key => IsPressed(Enum.Parse<Key>(control.Name)),
            InputControlKind.MouseButton => IsPressed(Enum.Parse<MouseButton>(control.Name)),
            InputControlKind.GamepadButton => IsPressed(control.GamepadIndex, Enum.Parse<ButtonName>(control.Name)),
            _ => false
        };
    }

    internal float ReadAxis(InputControl control, float deadZone)
    {
        if (control.Kind != InputControlKind.GamepadAxis)
            throw new ArgumentException("The control must be a gamepad axis.", nameof(control));
        return ReadAxis(control.GamepadIndex, Enum.Parse<GamepadAxis>(control.Name), deadZone);
    }

    internal Vector2 ReadStick(InputControl control, float deadZone)
    {
        if (control.Kind != InputControlKind.GamepadStick)
            throw new ArgumentException("The control must be a gamepad stick.", nameof(control));
        return ReadStick(control.GamepadIndex, Enum.Parse<GamepadStick>(control.Name), deadZone);
    }

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

    internal void RegisterCapture(InputCapture capture, Func<InputControl, InputBinding> complete)
    {
        if (captures.Count == 0) previousPressed = ReadPressedControls();
        captures.Add(new(capture, complete));
    }

    internal void CancelCapture(InputCapture capture) =>
        captures.RemoveAll(registration => ReferenceEquals(registration.Capture, capture));

    internal void Update()
    {
        if (captures.Count > 0)
        {
            var pressed = ReadPressedControls();
            var newPress = pressed.FirstOrDefault(control => !previousPressed.Contains(control));
            if (newPress != default)
            {
                foreach (var registration in captures.ToArray())
                {
                    captures.Remove(registration);
                    registration.Capture.Complete(registration.Complete(newPress));
                }
            }
            previousPressed = pressed;
        }

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

    private HashSet<InputControl> ReadPressedControls()
    {
        var pressed = new HashSet<InputControl>();
        var keyboard = game.GetKeyboard();
        if (keyboard is not null)
            foreach (var key in Enum.GetValues<Key>().Distinct())
                if (key != Key.Unknown && keyboard.IsKeyPressed(key))
                    pressed.Add(InputControl.From(key));

        var mouse = game.GetMouse();
        if (mouse is not null)
            foreach (var button in Enum.GetValues<MouseButton>().Distinct())
                if (button != MouseButton.Unknown && mouse.IsButtonPressed(button))
                    pressed.Add(InputControl.From(button));

        if (game.input is not null)
            foreach (var gamepad in game.input.Gamepads)
                foreach (var button in gamepad.Buttons)
                    if (button.Name != ButtonName.Unknown && button.Pressed)
                        pressed.Add(InputControl.From(button.Name, gamepad.Index));
        return pressed;
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

    private sealed record CaptureRegistration(InputCapture Capture, Func<InputControl, InputBinding> Complete);
}

/// <summary>A named input value with current and frame-transition state.</summary>
public sealed class InputAction
{
    private Func<Vector2> read;

    public InputAction(string name, Func<Vector2> initialRead)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(initialRead);
        Name = name;
        read = initialRead;
    }

    internal InputAction(string name, InputBinding binding, InputActionSystem input)
        : this(name, () => binding.Read(input)) => Binding = binding;

    public string Name { get; }
    public InputBinding? Binding { get; private set; }
    public string? BindingDescription => Binding?.Description;
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

    internal void Rebind(InputBinding binding, InputActionSystem input)
    {
        Binding = binding;
        read = () => binding.Read(input);
    }
}

/// <summary>A scene-scoped collection of gameplay actions.</summary>
public sealed class InputActionMap : IDisposable
{
    private readonly InputActionSystem input;
    private readonly Dictionary<string, InputAction> actions = new(StringComparer.Ordinal);
    private readonly List<InputCapture> captures = [];
    private bool disposed;

    public bool Enabled { get; set; } = true;
    public IReadOnlyCollection<InputAction> Actions => actions.Values;
    public InputProfile? ActiveProfile { get; private set; }
    public string? ActiveControlScheme { get; private set; }

    public InputActionMap(InputActionSystem input)
    {
        this.input = input;
        input.Register(this);
    }

    public InputAction Bind(string name, InputBinding binding) => Add(name, binding);

    public InputAction BindButton(string name, Key key, params Key[] alternatives) =>
        Add(name, InputBinding.Button(InputControl.From(key), alternatives.Select(InputControl.From).ToArray()));

    public InputAction BindButton(string name, MouseButton button, params MouseButton[] alternatives) =>
        Add(name, InputBinding.Button(InputControl.From(button), alternatives.Select(InputControl.From).ToArray()));

    public InputAction BindButton(string name, ButtonName button, params ButtonName[] alternatives) =>
        BindButton(name, 0, button, alternatives);

    public InputAction BindButton(string name, int gamepadIndex, ButtonName button,
        params ButtonName[] alternatives)
    {
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        return Add(name, InputBinding.Button(InputControl.From(button, gamepadIndex),
            alternatives.Select(value => InputControl.From(value, gamepadIndex)).ToArray()));
    }

    public InputAction BindChord(string name, InputControl first, InputControl second,
        params InputControl[] additional) => Add(name, InputBinding.Chord(first, second, additional));

    public InputAction BindAxis(string name, Key negative, Key positive) =>
        Add(name, InputBinding.Axis(InputControl.From(negative), InputControl.From(positive)));

    public InputAction BindVector2(string name, Key left, Key right, Key up, Key down) =>
        Add(name, InputBinding.Vector(InputControl.From(left), InputControl.From(right),
            InputControl.From(up), InputControl.From(down)));

    public InputAction BindAxis(string name, GamepadAxis axis, float deadZone = 0.15f,
        int gamepadIndex = 0) => Add(name, InputBinding.AnalogAxis(axis, deadZone, gamepadIndex));

    public InputAction BindVector2(string name, GamepadStick stick, float deadZone = 0.15f,
        int gamepadIndex = 0) => Add(name, InputBinding.Stick(stick, deadZone, gamepadIndex));

    public InputAction Rebind(string name, InputBinding binding) => RebindCore(name, binding);

    public InputAction RebindButton(string name, Key key, params Key[] alternatives) =>
        RebindCore(name, InputBinding.Button(InputControl.From(key), alternatives.Select(InputControl.From).ToArray()));

    public InputAction RebindButton(string name, MouseButton button, params MouseButton[] alternatives) =>
        RebindCore(name, InputBinding.Button(InputControl.From(button), alternatives.Select(InputControl.From).ToArray()));

    public InputAction RebindButton(string name, ButtonName button, params ButtonName[] alternatives) =>
        RebindButton(name, 0, button, alternatives);

    public InputAction RebindButton(string name, int gamepadIndex, ButtonName button,
        params ButtonName[] alternatives)
    {
        InputActionSystem.ValidateGamepadIndex(gamepadIndex);
        return RebindCore(name, InputBinding.Button(InputControl.From(button, gamepadIndex),
            alternatives.Select(value => InputControl.From(value, gamepadIndex)).ToArray()));
    }

    public InputAction RebindChord(string name, InputControl first, InputControl second,
        params InputControl[] additional) => RebindCore(name, InputBinding.Chord(first, second, additional));

    public InputAction RebindAxis(string name, Key negative, Key positive) =>
        RebindCore(name, InputBinding.Axis(InputControl.From(negative), InputControl.From(positive)));

    public InputAction RebindAxis(string name, GamepadAxis axis, float deadZone = 0.15f,
        int gamepadIndex = 0) => RebindCore(name, InputBinding.AnalogAxis(axis, deadZone, gamepadIndex));

    public InputAction RebindVector2(string name, Key left, Key right, Key up, Key down) =>
        RebindCore(name, InputBinding.Vector(InputControl.From(left), InputControl.From(right),
            InputControl.From(up), InputControl.From(down)));

    public InputAction RebindVector2(string name, GamepadStick stick, float deadZone = 0.15f,
        int gamepadIndex = 0) => RebindCore(name, InputBinding.Stick(stick, deadZone, gamepadIndex));

    /// <summary>Applies matching action bindings from a named profile scheme.</summary>
    public void ApplyProfile(InputProfile profile, string scheme)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(profile);
        var bindings = profile.GetScheme(scheme);
        foreach (var (name, binding) in bindings)
            if (actions.TryGetValue(name, out var action))
                action.Rebind(binding, input);
        ActiveProfile = profile;
        ActiveControlScheme = scheme;
    }

    public void SwitchControlScheme(string scheme)
    {
        if (ActiveProfile is null)
            throw new InvalidOperationException("Apply an input profile before switching control schemes.");
        ApplyProfile(ActiveProfile, scheme);
    }

    public InputProfile CreateProfile(string scheme = "Default")
    {
        var profile = new InputProfile();
        foreach (var action in actions.Values)
            if (action.Binding is { } binding)
                profile.SetBinding(scheme, action.Name, binding);
        return profile;
    }

    public IReadOnlyList<InputBindingConflict> FindConflicts() => InputProfile.FindConflicts(
        actions.Values.Where(action => action.Binding is not null)
            .Select(action => KeyValuePair.Create(action.Name, action.Binding!)));

    /// <summary>Captures the next keyboard, mouse, or gamepad button and rebinds the action.</summary>
    public InputCapture CaptureButton(string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Get(name);
        InputCapture? capture = null;
        capture = new(name, value =>
        {
            input.CancelCapture(value);
            captures.Remove(value);
        });
        capture.Completed += _ => captures.Remove(capture);
        captures.Add(capture);
        input.RegisterCapture(capture, control =>
        {
            var binding = InputBinding.Button(control);
            RebindCore(name, binding);
            return binding;
        });
        return capture;
    }

    public InputAction Get(string name) =>
        actions.TryGetValue(name, out var action)
            ? action
            : throw new KeyNotFoundException($"Input action '{name}' is not bound.");

    public bool TryGet(string name, out InputAction? action) => actions.TryGetValue(name, out action);

    public bool Remove(string name)
    {
        foreach (var capture in captures.Where(value => value.ActionName == name).ToArray())
            capture.Cancel();
        return actions.Remove(name);
    }

    private InputAction Add(string name, InputBinding binding)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        var action = new InputAction(name, binding, input);
        actions.Add(name, action);
        return action;
    }

    private InputAction RebindCore(string name, InputBinding binding)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        var action = Get(name);
        action.Rebind(binding, input);
        if (ActiveProfile is not null && ActiveControlScheme is not null)
            ActiveProfile.SetBinding(ActiveControlScheme, name, binding);
        return action;
    }

    internal void Update()
    {
        foreach (var action in actions.Values)
            action.Update(Enabled);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var capture in captures.ToArray()) capture.Cancel();
        input.Unregister(this);
        actions.Clear();
        GC.SuppressFinalize(this);
    }
}
