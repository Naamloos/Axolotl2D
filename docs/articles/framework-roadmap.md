# Framework Roadmap

Axolotl2D now has the core runtime loop required for a small 2D game: scoped scenes, DI-created components, assets, input actions, scaled time, rendering, custom shaders, audio, animation, and Box2D physics.

The next additions should reduce repeated game code and expose runtime state during development.

## 1. Prefabs and scene serialization

Runtime `Instantiate` calls build each object in C#. A prefab format should describe a GameObject, its transform, component types, and component data. Scene files can then reference prefab assets and overrides.

This would provide the largest workflow improvement because it separates content data from construction code. Start with JSON and explicit component serializers. Avoid arbitrary reflection-based object graphs until the format has versioning and migration rules.

## 2. Debug overlay and inspection

Add an in-game overlay that lists scenes, GameObjects, components, transforms, frame timing, draw submissions, loaded assets, and physics bodies. Include Box2D debug drawing and collision bounds.

The overlay would expose lifecycle, scope, rendering, and physics problems without an editor. Ship it as a development package.

## 3. Materials and render targets

`ShaderProgram` provides program selection and shared uniforms. A `Material` should pair a shader with uniform values and textures, then snapshot that state into each draw command. Render targets would enable lighting, post-processing, minimaps, and pixel-art scaling.

Add this after real games need different uniform values on sprites that share one shader. The current shader scope covers scene-wide and batch-wide effects.

## 4. Full input rebinding

Extend action bindings with gamepads, dead zones, chords, control schemes, and JSON persistence. Add conflict detection and display names for rebinding screens.

The current keyboard and mouse map covers desktop prototypes. Saved profiles matter once a game exposes player settings.

## 5. Physics tooling

Add collider components, sensors, collision layers, query helpers, and joint components over Box2D.NET. Keep `WorldId` and `BodyId` available so advanced projects can call the Box2D API.

Debug drawing should come first. It gives immediate feedback for pixel-to-meter scale, body origins, sleeping, and contact shapes.

## 6. Asset lifetime and hot reload

Add reference-counted asset handles or explicit content scopes before games begin streaming levels. Watch shader, texture, and scene files in development builds and reload them without restarting the game.

`AssetManager` suits preload-and-keep workflows. Streaming needs ownership rules so one scene cannot unload content still used by another.

## 7. Automated runtime checks

Add tests for scene transitions, lifecycle order, action edges, time scaling, transform parenting, asset caching, shader-scope restoration, and physics cleanup. A headless simulation mode would let most component and physics tests run without creating an OpenGL window.

The headless path would also make server-side simulation and deterministic replay easier to evaluate.
