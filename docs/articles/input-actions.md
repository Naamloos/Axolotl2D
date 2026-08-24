# Input Actions

`InputActionMap` turns keyboard, mouse, and gamepad state into named button, axis, and two-dimensional actions. Axolotl2D registers one map per scene scope, so bindings disappear with the scene that owns them.

## Bind scene actions

Inject the scoped map into a scene and bind actions before adding components that consume them:

```csharp
public sealed class GameplayScene(InputActionMap input) : BaseScene
{
    public override void Load()
    {
        input.BindVector2("Move", Key.A, Key.D, Key.W, Key.S);
        input.BindButton("Jump", Key.Space);
        input.BindButton("Fire", MouseButton.Left);

        Instantiate("Player").AddComponent<PlayerController>();
    }
}
```

`BindButton` accepts alternative keys or mouse buttons:

```csharp
input.BindButton("Confirm", Key.Enter, Key.Space);
```

An action name must be unique inside its map. Use `Remove` before replacing a binding.

## Read actions from components

Components receive the same scoped map as their scene:

```csharp
public sealed class PlayerController(GameObject gameObject, InputActionMap input)
    : Component(gameObject)
{
    private InputAction move = null!;
    private InputAction jump = null!;

    public override void Start()
    {
        move = input.Get("Move");
        jump = input.Get("Jump");
    }

    public override void Update(double deltaTime)
    {
        Transform.Translate(move.Value * 220f * (float)deltaTime);

        if (jump.WasPressedThisFrame)
        {
            // Start a jump.
        }
    }
}
```

Each action exposes:

| Property | Meaning |
| --- | --- |
| `Value` | Current `Vector2` value |
| `Scalar` | Current X value for a button or axis |
| `IsPressed` | The current value is non-zero |
| `WasPressedThisFrame` | The action changed from zero to non-zero this frame |
| `WasReleasedThisFrame` | The action changed from non-zero to zero this frame |

Keyboard axis bindings return `-1`, `0`, or `1` in `Scalar`; gamepad axes return continuous values. Keyboard vector bindings are not normalized, so diagonal input has both components set. Gamepad stick vectors use radial normalization and remain within unit length.

## Bind gamepad controls

Gamepad buttons use Silk.NET's standard `ButtonName` values:

```csharp
var jump = input.BindButton("Jump", ButtonName.A);
var pause = input.BindButton("Pause", ButtonName.Start);
```

Bind individual sticks or triggers as scalar axes, or bind a complete stick as a two-dimensional action:

```csharp
var lookX = input.BindAxis("Look X", GamepadAxis.RightStickX, deadZone: 0.12f);
var throttle = input.BindAxis("Throttle", GamepadAxis.RightTrigger, deadZone: 0.05f);
var move = input.BindVector2("Move", GamepadStick.Left, deadZone: 0.2f);
```

Scalar stick axes preserve the gamepad's native direction. Two-dimensional stick bindings invert Y to match Axolotl2D screen and keyboard-vector coordinates: up is negative Y and down is positive Y.

The default gamepad index is zero. Pass `gamepadIndex` to an analog binding, or use the indexed button overload for local multiplayer:

```csharp
var playerTwoJump = input.BindButton("Player 2 Jump", 1, ButtonName.A);
var playerTwoMove = input.BindVector2("Player 2 Move", GamepadStick.Left,
    deadZone: 0.18f, gamepadIndex: 1);
```

An unavailable gamepad, stick, or trigger reads as zero. Dead zones must be at least zero and less than one. Scalar axes use an axial dead zone; complete sticks use a radial dead zone. Values outside the dead zone are rescaled to retain the full zero-to-one range.

## Rebind an action

The `RebindButton`, `RebindAxis`, and `RebindVector2` overloads replace an action's reader without replacing its `InputAction` object. Components can safely retain the reference returned during initial binding:

```csharp
var move = input.BindVector2("Move", Key.A, Key.D, Key.W, Key.S);

input.RebindVector2("Move", Key.Left, Key.Right, Key.Up, Key.Down);
input.RebindVector2("Move", GamepadStick.Left, deadZone: 0.2f);

// Still the same object, now reading the left stick.
var value = move.Value;
```

Button and scalar-axis actions can likewise move between keyboard, mouse, and gamepad overloads. Rebinding takes effect on the next input update and retains normal pressed/released edge tracking.

## Disable a map

```csharp
input.Enabled = false;
```

Disabling the map releases active actions on the next frame. This suits pause menus and modal UI. Re-enabling the map lets the current device state drive its actions again.

`InputActionSystem` updates maps before scene and component `Update` callbacks. Game code does not need to poll `Game.GetKeyboard()` or track previous key state.

The current action system supports keyboard, mouse, gamepad buttons, standard sticks and triggers, per-binding dead zones, and runtime rebinding. Interactive "press any control" capture, saved binding profiles, control schemes, conflict detection, and input consumption remain future additions. See the `INPUT` screen in `Axolotl2D.Example` and the [Framework Roadmap](framework-roadmap.md).
