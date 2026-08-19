# Component Lifecycle

Component lifecycle callbacks separate initialization, fixed simulation, frame updates, rendering, activation, and cleanup.

## Callback order

For an active component, the lifecycle is:

1. `Awake` when the component attaches.
2. `OnEnable` when the component becomes active and enabled.
3. `Start` before its first fixed update, update, or render.
4. `FixedUpdate` zero or more times for each game update.
5. `Update` once for the game update.
6. `LateUpdate` after regular updates.
7. `Render` during drawing.
8. `OnDisable` when the component or its GameObject becomes inactive, or before destruction.
9. `OnDestroy` once when the component is removed or its GameObject is destroyed.

`Awake`, `Start`, and `OnDestroy` run once. `OnEnable` and `OnDisable` can run more than once as activation changes.

## Choose the right callback

| Callback | Use it for |
| --- | --- |
| `Awake` | Internal initialization that does not depend on later component configuration |
| `OnEnable` | Subscribing to events or starting work that must pause while disabled |
| `Start` | Initialization that reads other components or values assigned after `AddComponent` |
| `FixedUpdate` | Fixed-rate simulation |
| `Update` | Input, timers, and frame-rate-aware behavior |
| `LateUpdate` | Camera following and final transform adjustments |
| `Render` | Submitting draw commands to the active sprite batch |
| `OnDisable` | Unsubscribing or pausing active work |
| `OnDestroy` | Final component cleanup |

## Configuration before Start

`Start` is intentionally deferred, so callers can configure a component after adding it:

```csharp
var follower = cameraObject.AddComponent<CameraFollower>();
follower.Target = player.Transform;
follower.Smoothing = 8f;
```

```csharp
public sealed class CameraFollower(GameObject gameObject, Camera2D camera)
    : Component(gameObject)
{
    public Transform? Target { get; set; }
    public float Smoothing { get; set; }

    public override void Start()
    {
        ArgumentNullException.ThrowIfNull(Target);
    }

    public override void LateUpdate(double deltaTime)
    {
        var amount = 1f - MathF.Exp(-Smoothing * (float)deltaTime);
        camera.Position = Vector2.Lerp(camera.Position, Target!.Position, amount);
    }
}
```

## Activation and cleanup

Set `Enabled` when one behavior should pause. Set `GameObject.Active` when every component on the object should pause. In both cases, inactive components skip fixed updates, updates, late updates, and rendering.

If a component subscribes in `OnEnable`, unsubscribe in `OnDisable`. Reserve `OnDestroy` for cleanup that happens only once. Scene unload invokes the destruction path for every remaining object before its scoped services are disposed.
