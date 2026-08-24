# Framework Roadmap

Axolotl2D now has the core runtime loop required for a small 2D game: scoped scenes, DI-created components, assets, data-authored prefabs, keyboard, mouse and gamepad input actions with runtime rebinding, scaled time, rendering, camera post-processing, custom shaders, audio, animation, particles, retained UI, Box2D physics, and runtime inspection.

The next additions should reduce repeated game code and expose runtime state during development.

Versioned `.axprefab` assets now cover reusable GameObject hierarchies through explicit, stable component registrations. Full scene serialization and editor tooling remain optional future authoring features rather than runtime requirements.

## 1. Automated runtime checks

Add tests for scene transitions, lifecycle order, action edges and rebinding, dead zones, time scaling, transform parenting, asset caching, shader-scope restoration, and physics cleanup. A headless simulation mode would let most component and physics tests run without creating an OpenGL window.

The headless path would also make server-side simulation and deterministic replay easier to evaluate.

## 2. Asset lifetime and hot reload

Add reference-counted asset handles or explicit content scopes before games begin streaming levels. Watch shader, texture, prefab, and scene files in development builds and reload them without restarting the game.

`AssetManager` suits preload-and-keep workflows. Streaming needs ownership rules so one scene cannot unload content still used by another.

## 3. Physics tooling

Add collider components, sensors, collision layers, query helpers, and joint components over Box2D.NET. Keep `WorldId` and `BodyId` available so advanced projects can call the Box2D API.

`PhysicsBody` already wraps boxes, circles, collision events, motion, and transform synchronization. Add joints, filters, sensors, casts, and query helpers when games need more physics without direct Box2D.NET calls.

## 4. Input profiles and capture

Add interactive "press any control" capture, binding descriptions, conflict detection, chords, control schemes, and JSON profile persistence.

The current API supports gamepad buttons, sticks, triggers, per-binding dead zones, multiple device indices, and runtime programmatic rebinding. Persisted profiles matter once a game exposes a player settings screen.

## 5. Public render targets

Expose render textures only when games need minimaps, security cameras, portals, or fixed-resolution pixel-art composition. Camera post-processing already owns resize-aware internal render targets and does not require a material system.

Keep `ShaderProgram` as the direct shader-and-uniform API. Introduce a material abstraction only if real per-sprite uniform-state duplication makes it necessary.
