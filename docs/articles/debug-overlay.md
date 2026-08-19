# Debug Overlay and Runtime Inspection

The optional debug overlay inspects a running game without an editor. It lists available and active scenes, the current scene scope, GameObjects, components and lifecycle state, world transforms, frame timing, draw commands and GPU submissions, loaded assets, and Box2D bodies and counters.

## Enable the overlay

Pass the development flag to the Generic Host extension:

```csharp
services.UseSceneManagerGameHost<MyGame>(enableDebugOverlay: true);
```

`UseSimpleGameHost<TGame>` accepts the same flag. A simple-host overlay reports timing, rendering, and assets but has no scene scope or scoped physics world to inspect.

The flag defaults to `false`. Enable it only in a development configuration, for example:

```csharp
services.UseSceneManagerGameHost<MyGame>(
    enableDebugOverlay: context.HostingEnvironment.IsDevelopment());
```

The overlay ships inside the main assembly and creates its own small diagnostic glyph atlas. It does not require a game font or add changing timing strings to the `TextRenderer` cache.

## What it shows

The left scene column includes:

- every concrete scene in the entry assembly, with the active scene marked;
- the active scope identity and scene load state;
- every scene-owned GameObject and whether it is active;
- world position, rotation, and lossy scale;
- component type, enabled/active state, and whether `Start` ran.

The right runtime column includes:

- frame interval, callback update/draw time, FPS, frame counters, and time scale;
- sprite commands, GPU draw submissions, triangles, and uploaded textures from the previous completed frame;
- every asset cache key, type, and load state;
- Box2D body, shape, contact, and joint counts plus each component body.

Long lists are clipped to the available window rows and report how many lines remain. Compact diagnostic text renders over normal scene content without opaque backdrops; the scene column is left-aligned and every runtime line is aligned to the right window edge.

## Box2D debug drawing

In scene mode the overlay also calls Box2D's world debug-draw API. Shape outlines, joints, body transforms, contact points and normals, and collision AABBs are converted from physics meters to Axolotl2D world pixels and rendered through `PrimitiveBatch`. Drawing is culled to the camera's visible world bounds.

To customize physics visualization while leaving inspection enabled, register options before the host:

```csharp
services.AddSingleton(new DebugOverlayOptions(true)
{
    DrawPhysics = true,
    DrawCollisionBounds = false
});
services.UseSceneManagerGameHost<MyGame>();
```

The host's `enableDebugOverlay: true` flag installs default enabled options. A pre-registered options instance is preserved when the flag is omitted.
