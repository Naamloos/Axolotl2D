# Framework Roadmap

Axolotl2D has the core runtime required for a small 2D game: scoped scenes, DI-created components, assets, data-authored prefabs, input actions and persisted profiles, scaled time, rendering and public render textures, camera post-processing, custom shaders, audio, animation, particles, retained UI, Box2D physics tooling, and runtime inspection.

Versioned `.axprefab` assets cover reusable GameObject hierarchies through explicit, stable component registrations. Collider and joint components use the same GameObject, component lifecycle, prefab, and `.axpkg` paths as other built-in components. Full scene serialization and editor tooling remain optional authoring features rather than runtime requirements.

The next additions should improve verification and content iteration.

## 1. Automated runtime checks

Add tests for scene transitions, lifecycle order, action edges and rebinding, profile round trips, dead zones, time scaling, transform parenting, asset caching, render-target restoration, shader scopes, and physics cleanup. A headless simulation mode would let most component and physics tests run without creating an OpenGL window.

The headless path would also make server-side simulation and deterministic replay easier to evaluate.

## 2. Asset lifetime and hot reload

Add reference-counted asset handles or explicit content scopes before games begin streaming levels. Watch shader, texture, prefab, and scene files in development builds and reload them without restarting the game.

`AssetManager` suits preload-and-keep workflows. Streaming needs ownership rules so one scene cannot unload content still used by another.

## Delivered foundations

- Physics tooling includes box and circle collider components, sensors, 64-bit collision filters, ray and circle casts, box overlap queries, distance and revolute joints, and raw Box2D IDs for specialized work.
- Input tooling includes binding descriptions, chords, keyboard/mouse/gamepad button capture, conflict detection, named control schemes, and versioned JSON profiles.
- Public render textures support camera destinations, sprite sampling, resizing, nearest or linear filtering, and camera post-processing output.

Material state remains deliberately direct: keep `ShaderProgram` as the shader-and-uniform API and introduce a material abstraction only when real per-sprite uniform duplication makes it necessary.
