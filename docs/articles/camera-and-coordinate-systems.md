# Camera and Coordinate Systems

Axolotl2D exposes world, screen, and normalized-device coordinates explicitly. `Camera2D` converts between the game world and the top-left screen coordinate system used by input and UI.

## Coordinate conventions

| Space | Origin and range | Typical use |
| --- | --- | --- |
| World | Defined by your scene | Gameplay objects and maps |
| Screen | `(0, 0)` at top-left, pixels increase right and down | Mouse input and UI |
| Normalized device | `(-1, 1)` at top-left, `(1, -1)` at bottom-right | Low-level rendering |

`CoordinateSpace.World` tells `SpriteBatch` to apply the active camera. `CoordinateSpace.Screen` submits pixel coordinates without the camera.

## Camera position, pan, and zoom

`Camera2D.Position` is the world point shown at the viewport center. `ViewportSize` reflects the camera's part of the current game window.

```csharp
public sealed class CameraController(
    GameObject gameObject,
    Camera2D camera,
    Game game) : Component(gameObject)
{
    public override void Update(double deltaTime)
    {
        var keyboard = game.GetKeyboard();
        if (keyboard is null)
        {
            return;
        }

        var distance = 300f * (float)deltaTime / camera.Zoom;
        if (keyboard.IsKeyPressed(Key.A)) camera.Pan(new Vector2(-distance, 0));
        if (keyboard.IsKeyPressed(Key.D)) camera.Pan(new Vector2(distance, 0));
        if (keyboard.IsKeyPressed(Key.W)) camera.Pan(new Vector2(0, -distance));
        if (keyboard.IsKeyPressed(Key.S)) camera.Pan(new Vector2(0, distance));
    }
}
```

Assign `Zoom` directly or zoom around a screen position:

```csharp
camera.Zoom = 2f;
camera.ZoomAt(1.1f, mouse.Position);
camera.ZoomAt(1f / 1.1f, mouse.Position);
```

`ZoomAt` keeps the world point under the supplied screen position fixed. Its factor must be positive. Direct `Zoom` assignments are clamped to a minimum of `0.01`.

`Camera2D.Rotation` uses radians. Positive values rotate the camera view consistently with the framework's screen coordinate convention.

## Convert input to world coordinates

Mouse positions arrive in screen pixels. Convert them before world hit testing or placement:

```csharp
Vector2 screenMouse = mouse.Position;
Vector2 worldMouse = Coordinates.ScreenToWorld(screenMouse, camera);

projectile.Transform.LocalPosition = worldMouse;
```

Convert a world position to screen space when placing a UI marker over an object:

```csharp
Vector2 markerPosition = Coordinates.WorldToScreen(
    target.Transform.Position,
    camera);
```

## Normalized device conversions

Low-level rendering code can use:

```csharp
Vector2 ndc = Coordinates.ScreenToNormalizedDevice(screenPoint, game.Viewport);
Vector2 screen = Coordinates.NormalizedDeviceToScreen(ndc, game.Viewport);
```

Most game code should remain in world or screen coordinates. Keep conversions at system boundaries, such as input, UI projection, and custom rendering.

## Use a different camera for a batch

`SpriteBatch.Begin(camera)` accepts a camera override. Scene rendering uses the DI-registered default camera. A manually managed batch can select another camera for a minimap or alternate view.

## Follow a target and constrain movement

```csharp
camera.FollowTarget = player.Transform;
camera.FollowOffset = new Vector2(0, -40);
camera.FollowSmoothing = 7f;
camera.DeadZone = new Vector2(120, 70);
camera.Bounds = new CameraBounds(
    new Vector2(0, 0),
    new Vector2(4096, 2048));
```

The camera updates after scene components finish their frame update. A smoothing value of zero snaps to the target. Positive values use frame-rate-independent exponential smoothing. The dead zone lets the target move around the current center before the camera follows. Bounds clamp the visible rectangle inside the supplied world area.

Start a decaying shake without changing the tracked camera position:

```csharp
camera.Shake(amplitude: 18f, duration: 0.35f, frequency: 28f);
```

## Render multiple cameras

Inject `CameraManager` and create a split-screen or inset camera:

```csharp
var minimap = cameras.Create("Minimap");
minimap.Viewport = new CameraViewport(0.72f, 0.04f, 0.25f, 0.28f);
minimap.Zoom = 0.25f;
minimap.Priority = 10;
```

The scene records its draw commands once. Axolotl2D submits world-space commands through each enabled camera in priority order, then submits screen-space commands once over the full window. `CullingMask` selects world commands by their `lightingLayer` or `SpriteRenderer.LightingLayer` bit.

Remove scene-owned cameras during unload:

```csharp
public override void Unload() => cameras.Remove(minimap);
```
