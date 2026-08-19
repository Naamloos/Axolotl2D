# Input Actions

`InputActionMap` turns keyboard and mouse state into named button, axis, and two-dimensional actions. Axolotl2D registers one map per scene scope, so bindings disappear with the scene that owns them.

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

Axis bindings return `-1`, `0`, or `1` in `Scalar`. Vector bindings return raw X and Y components, so diagonal input has both components set and is not normalized.

## Disable a map

```csharp
input.Enabled = false;
```

Disabling the map releases active actions on the next frame. This suits pause menus and modal UI. Re-enabling the map lets the current device state drive its actions again.

`InputActionSystem` updates maps before scene and component `Update` callbacks. Game code does not need to poll `Game.GetKeyboard()` or track previous key state.

The current action system supports keyboard and mouse bindings. Gamepads, saved rebinding profiles, control schemes, and input consumption remain future additions. See [Framework Roadmap](framework-roadmap.md).
