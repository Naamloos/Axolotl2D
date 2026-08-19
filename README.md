<p align="center">
    <img width="250" src="docs/images/logo.png"/>
    <h1 align="center">Axolotl2D</h1>
</p>

Axolotl2D is a small 2D game framework built on Silk.NET and Microsoft.Extensions.Hosting. You compose games from DI-created scenes, GameObjects, and components while the framework handles the window, render loop, assets, graphics, and audio.

## Framework model

- A Generic Host owns the game and engine services.
- `SceneGameHost` creates one DI scope for each active scene and disposes it during a transition.
- Each scene owns runtime-instantiated GameObjects. A GameObject owns a hierarchical `Transform` and DI-created components.
- `AssetManager` selects `IAssetLoader<T>` implementations by asset type and caches each result by key.
- `SpriteBatch` groups sprites that share a texture and shader. `PrimitiveBatch` supplies rectangles, lines, and circles. `Camera2D` maps world coordinates to top-left screen coordinates.
- Scene scopes own input maps, custom shader libraries, and Box2D worlds. `TimeService` supplies scaled frame and fixed-step time.

The built-in loaders support textures, RIFF/WAVE PCM sounds, and scalable fonts. Rendering includes texture regions, sprite sheets, animation, particles, retained UI with textured or primitive visuals, text, custom GLSL programs, tinting, depth order, and world or screen coordinate spaces. Box2D.NET supplies rigid-body simulation, collision events, and optional debug drawing. A host flag enables in-game scene, lifecycle, rendering, asset, timing, and physics inspection.

Components support `Awake`, enable and disable notifications, `Start`, fixed and variable updates, late updates, rendering, and destruction. A scene defers GameObject disposal until the current update or render phase ends, so components can destroy objects during callbacks.

## Minimal host

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.UseSceneManagerGameHost<MyGame>();
        services.AddScene<MainScene>();
    })
    .Build();

await host.RunAsync();
```

`UseSceneManagerGameHost<T>()` registers the standard Axolotl2D services. Pass `enableDebugOverlay: true` during development for in-game runtime inspection. Override `Game.InitializeAsync` to await asset loading before the window and first scene start. Register your own `IAssetLoader<T>`, component dependencies, or other game services before building the host; constructors receive them through normal Microsoft DI.

See the [documentation](docs/index.md), [getting-started guide](docs/articles/getting-started.md), and [example project](Axolotl2D.Example) for assets, GameObjects, input actions, time control, rendering, shaders, animation, particles, UI, audio, physics, and inspection. [Bletris](Axolotl2D.Example.Bletris) is a playable falling-block example with a retained-UI menu, music, row-clear particles, and custom-shader blocks.
