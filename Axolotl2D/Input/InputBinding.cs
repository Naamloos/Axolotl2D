using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Input;

public enum InputControlKind
{
    Key,
    MouseButton,
    GamepadButton,
    GamepadAxis,
    GamepadStick
}

/// <summary>A serializable physical control.</summary>
public readonly record struct InputControl(InputControlKind Kind, string Name, int GamepadIndex = 0)
{
    public static InputControl From(Key key) => new(InputControlKind.Key, key.ToString());
    public static InputControl From(MouseButton button) => new(InputControlKind.MouseButton, button.ToString());
    public static InputControl From(ButtonName button, int gamepadIndex = 0) =>
        new(InputControlKind.GamepadButton, button.ToString(), gamepadIndex);
    public static InputControl From(GamepadAxis axis, int gamepadIndex = 0) =>
        new(InputControlKind.GamepadAxis, axis.ToString(), gamepadIndex);
    public static InputControl From(GamepadStick stick, int gamepadIndex = 0) =>
        new(InputControlKind.GamepadStick, stick.ToString(), gamepadIndex);

    public string Description => Kind switch
    {
        InputControlKind.Key => Name,
        InputControlKind.MouseButton => $"Mouse {Name}",
        InputControlKind.GamepadButton => $"Gamepad {GamepadIndex + 1} {Name}",
        InputControlKind.GamepadAxis => $"Gamepad {GamepadIndex + 1} {Name}",
        InputControlKind.GamepadStick => $"Gamepad {GamepadIndex + 1} {Name} stick",
        _ => Name
    };

    internal void Validate(bool buttonOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (GamepadIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(GamepadIndex));
        if ((Kind is InputControlKind.Key or InputControlKind.MouseButton) && GamepadIndex != 0)
            throw new ArgumentOutOfRangeException(nameof(GamepadIndex), "Keyboard and mouse controls do not use a gamepad index.");
        var valid = Kind switch
        {
            InputControlKind.Key => IsEnumValue<Key>(Name),
            InputControlKind.MouseButton => IsEnumValue<MouseButton>(Name),
            InputControlKind.GamepadButton => IsEnumValue<ButtonName>(Name),
            InputControlKind.GamepadAxis => !buttonOnly && IsEnumValue<GamepadAxis>(Name),
            InputControlKind.GamepadStick => !buttonOnly && IsEnumValue<GamepadStick>(Name),
            _ => false
        };
        if (!valid)
            throw new ArgumentException($"'{Name}' is not a valid {Kind} control.", nameof(Name));
    }

    private static bool IsEnumValue<T>(string name) where T : struct, Enum =>
        Enum.TryParse<T>(name, out var value) && Enum.IsDefined(value);
}

public enum InputBindingKind
{
    Button,
    Chord,
    Axis,
    Vector2,
    AnalogAxis,
    Stick
}

/// <summary>A serializable button, chord, axis, or two-dimensional binding.</summary>
public sealed record InputBinding(InputBindingKind Kind, IReadOnlyList<InputControl> Controls, float DeadZone = 0f)
{
    public string Description => Kind switch
    {
        InputBindingKind.Button => string.Join(" / ", Controls.Select(control => control.Description)),
        InputBindingKind.Chord => string.Join(" + ", Controls.Select(control => control.Description)),
        InputBindingKind.Axis => $"{Controls[0].Description} / {Controls[1].Description}",
        InputBindingKind.Vector2 => string.Join(" / ", Controls.Select(control => control.Description)),
        _ => Controls[0].Description
    };

    public static InputBinding Button(InputControl control, params InputControl[] alternatives) =>
        new(InputBindingKind.Button, alternatives.Prepend(control).ToArray());

    public static InputBinding Chord(InputControl first, InputControl second, params InputControl[] additional) =>
        new(InputBindingKind.Chord, additional.Prepend(second).Prepend(first).ToArray());

    public static InputBinding Axis(InputControl negative, InputControl positive) =>
        new(InputBindingKind.Axis, [negative, positive]);

    public static InputBinding Vector(InputControl left, InputControl right, InputControl up, InputControl down) =>
        new(InputBindingKind.Vector2, [left, right, up, down]);

    public static InputBinding AnalogAxis(GamepadAxis axis, float deadZone = 0.15f, int gamepadIndex = 0) =>
        new(InputBindingKind.AnalogAxis, [InputControl.From(axis, gamepadIndex)], deadZone);

    public static InputBinding Stick(GamepadStick stick, float deadZone = 0.15f, int gamepadIndex = 0) =>
        new(InputBindingKind.Stick, [InputControl.From(stick, gamepadIndex)], deadZone);

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Controls);
        var expected = Kind switch
        {
            InputBindingKind.Button => Controls.Count >= 1,
            InputBindingKind.Chord => Controls.Count >= 2,
            InputBindingKind.Axis => Controls.Count == 2,
            InputBindingKind.Vector2 => Controls.Count == 4,
            InputBindingKind.AnalogAxis or InputBindingKind.Stick => Controls.Count == 1,
            _ => false
        };
        if (!expected)
            throw new ArgumentException($"{Kind} has an invalid number of controls.", nameof(Controls));

        foreach (var control in Controls)
            control.Validate(Kind is InputBindingKind.Button or InputBindingKind.Chord or InputBindingKind.Axis or InputBindingKind.Vector2);

        if (Kind == InputBindingKind.AnalogAxis && Controls[0].Kind != InputControlKind.GamepadAxis)
            throw new ArgumentException("An analog-axis binding requires a gamepad axis.", nameof(Controls));
        if (Kind == InputBindingKind.Stick && Controls[0].Kind != InputControlKind.GamepadStick)
            throw new ArgumentException("A stick binding requires a gamepad stick.", nameof(Controls));
        if (Kind is InputBindingKind.AnalogAxis or InputBindingKind.Stick)
            InputActionSystem.ValidateDeadZone(DeadZone);
        else if (DeadZone != 0f)
            throw new ArgumentOutOfRangeException(nameof(DeadZone), "Digital bindings do not use a dead zone.");
    }

    internal Vector2 Read(InputActionSystem input) => Kind switch
    {
        InputBindingKind.Button => Controls.Any(input.IsPressed) ? Vector2.UnitX : Vector2.Zero,
        InputBindingKind.Chord => Controls.All(input.IsPressed) ? Vector2.UnitX : Vector2.Zero,
        InputBindingKind.Axis => new Vector2(
            (input.IsPressed(Controls[1]) ? 1f : 0f) - (input.IsPressed(Controls[0]) ? 1f : 0f), 0f),
        InputBindingKind.Vector2 => new Vector2(
            (input.IsPressed(Controls[1]) ? 1f : 0f) - (input.IsPressed(Controls[0]) ? 1f : 0f),
            (input.IsPressed(Controls[3]) ? 1f : 0f) - (input.IsPressed(Controls[2]) ? 1f : 0f)),
        InputBindingKind.AnalogAxis => new Vector2(input.ReadAxis(Controls[0], DeadZone), 0f),
        InputBindingKind.Stick => input.ReadStick(Controls[0], DeadZone),
        _ => Vector2.Zero
    };
}

/// <summary>Two actions that use the same physical control in one scheme.</summary>
public readonly record struct InputBindingConflict(string FirstAction, string SecondAction, InputControl Control);
