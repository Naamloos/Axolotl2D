# Time and Fixed Updates

`TimeService` provides scaled and unscaled frame time, fixed-step time, totals, frame counters, and pause state. Axolotl2D registers it as a singleton so every scene and component reads the same clock.

## Read frame time

Inject `TimeService` into a component:

```csharp
public sealed class Lifetime(GameObject gameObject, TimeService time)
    : Component(gameObject)
{
    public override void Update(double deltaTime)
    {
        if (time.TotalTime > 30)
            GameObject.Destroy();
    }
}
```

The callback `deltaTime` matches `TimeService.DeltaTime`. The service also exposes `UnscaledDeltaTime` for UI, pause menus, and effects that should ignore game speed.

## Scale or pause time

```csharp
time.TimeScale = 0.5;
time.IsPaused = true;
```

`TimeScale` must be finite and at least zero. Pausing sets scaled `DeltaTime` to zero while `UnscaledDeltaTime`, input actions, rendering, and the unscaled clock continue. Set `IsPaused` to `false` from an input action to resume play.

| Property | Contents |
| --- | --- |
| `DeltaTime` | Current frame duration after pause and time scale |
| `UnscaledDeltaTime` | Current wall-clock frame duration |
| `FixedDeltaTime` | Duration of the current fixed step |
| `TotalTime` | Accumulated scaled game time |
| `UnscaledTotalTime` | Accumulated wall-clock game time |
| `FrameCount` | Number of update frames |
| `FixedFrameCount` | Number of fixed steps |

## Configure fixed updates

Each scene owns its fixed-step settings:

```csharp
public sealed class GameplayScene : BaseScene
{
    public GameplayScene()
    {
        FixedTimeStep = 1d / 60d;
        MaximumFixedStepsPerFrame = 8;
    }
}
```

Axolotl2D adds scaled frame time to the scene accumulator. A paused scene performs no fixed steps. Slow motion reduces the frequency of fixed steps in wall-clock time while each step still receives `FixedTimeStep`.

Each fixed phase runs in this order:

1. `TimeService` records the fixed step.
2. Active components receive `FixedUpdate`.
3. The scene receives `FixedUpdate`.
4. The scene removes objects queued for destruction.
5. `PhysicsWorld` advances and synchronizes transforms.
6. The scene dispatches physics contacts and removes objects destroyed by contact handlers.

Use `FixedUpdate` to apply forces and impulses. Read the resulting transform in `Update` or `LateUpdate`.
