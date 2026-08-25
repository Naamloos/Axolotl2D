<p align="center">
    <img width="250" src="docs/images/logo.png"/>
    <h1 align="center">Axolotl2D</h1>
</p>

Axolotl2D is a small 2D game framework built on Silk.NET and Microsoft.Extensions.Hosting. You compose games from DI-created scenes, GameObjects, and components while the framework handles the window, render loop, assets, graphics, and audio. Versioned `.axpkg` modules package reusable assets and managed code for base content, DLC, and mods.

## Framework model

- A Generic Host owns the game and engine services.
- `SceneGameHost` creates one DI scope per scene, supports stacked overlay scenes, and disposes scopes during pops and transitions.
- Each scene owns runtime-instantiated GameObjects. A GameObject owns a hierarchical `Transform` and DI-created components.
- Versioned `.axprefab` JSON assets create reusable GameObject hierarchies through the same scene DI and lifecycle paths.
- `AssetManager` selects `IAssetLoader<T>` implementations by asset type and caches each result by key.
- `AxolotlPackageManager` validates and mounts explicitly selected `.axpkg` modules. Executable modules can register scenes, GameObject factories, asset loaders, and game-defined extensions.
- `SpriteBatch` groups sprites that share a texture and shader. `PrimitiveBatch` supplies rectangles, lines, and circles. `Camera2D` maps world coordinates to top-left screen coordinates.
- `Lighting2D` supplies normal-mapped point and spot lights with polygon shadows. `CameraManager` handles follow targets, bounds, shake, multiple viewports, and public render-texture destinations.
- Scene scopes own keyboard, mouse and gamepad input maps with capture and JSON profiles, custom shader libraries, and Box2D worlds with collider, sensor, query, and joint tooling. `TimeService` supplies scaled frame and fixed-step time.
- Scene-scoped tweens and coroutines drive sequences and transitions. `SaveGameManager` stores typed, versioned JSON slots with atomic replacement.

The built-in loaders support textures, RIFF/WAVE PCM sounds, scalable fonts, and validated data prefabs. Rendering includes texture regions, timed sprite animation, particles, normal-mapped lighting, custom GLSL programs, per-camera post-processing, render textures, and multiple cameras. Audio includes controllable one-shots, stereo pan, and spatial 2D sources. Retained UI provides stretch or grouped layout, clipping, scrolling, routed input, focus navigation, and common controls. Box2D.NET supplies rigid bodies, box, circle, capsule, polygon and segment colliders, filters, sensors, casts, queries, joints, collision events, and optional debug drawing. A host flag enables in-game scene, lifecycle, rendering, asset, timing, and physics inspection.

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

## Modules and `.axpkg`

An Axolotl module is a normal .NET class-library project. A build produces a versioned `.axpkg` containing its manifest, compiled assets, DLL, dependencies, and optional ECDSA signature. The MSBuild content pipeline imports PNG, TTF, and WAV files and supports custom build-time importers.

Games choose each package and trust policy:

```csharp
var policy = PackageTrustPolicy.RequireTrustedSignature(publisherKeys);

await packages.LoadAsync("Content/Base.axpkg", policy, cancellationToken);
var logo = await assets.LoadPackageAsync<Texture2D>(
    "ui/logo", "my.game.base", "sprites/logo", cancellationToken);
```

Trusted executable modules can extend the game without assembly scanning:

```csharp
public void Initialize(AxolotlModuleContext context)
{
    context.RegisterScene<ChallengeScene>("my.dlc/challenge");
    context.RegisterGameObject("my.dlc/enemy", static (_, objects, name) =>
    {
        var enemy = objects.Create(name);
        enemy.AddComponent<ChallengeEnemy>();
        return enemy;
    });
}
```

The game can enter that scene with `SceneGameHost.ChangeScene("my.dlc/challenge")`. An existing scene can spawn the registered object with `InstantiateRegistered("my.dlc/enemy")`. Modules can also implement game-owned contracts through `RegisterExtension<TContract>` or reuse code and assets from mounted package dependencies.

Axolotl2D loads packages only when the game supplies a path. Signed policies protect official content, while content-only policies accept assets without executing the package DLL. Enabling unsigned executable modules gives mod code the same operating-system permissions as the game.

Read [Modules and Packages](docs/articles/modules-and-packages.md), [Package Use Cases](docs/articles/package-use-cases.md), and the [`.axpkg` File Format](docs/articles/axpkg-format.md).

Read [Data Prefabs](docs/articles/prefabs.md) for authoring reusable JSON GameObject hierarchies, registering custom component data, and packaging `.axprefab` assets.

See the [documentation](docs/index.md), [getting-started guide](docs/articles/getting-started.md), and [example project](Axolotl2D.Example) for assets, GameObjects, packaged data prefabs, input profiles and capture, time control, render textures, shaders, camera post-processing, animation, particles, UI, audio, physics tooling, and inspection.

## Bletris benchmark

[Bletris](Axolotl2D.Example.Bletris) is a small playable benchmark rather than a feature catalogue. Its menu exercises responsive retained UI controls, sprite animation and markers, tweens, packaged prefabs, several collider shapes, and a physics joint. Gameplay uses keyboard and gamepad actions, a carry slot, a layered pause menu, scalable world-space rendering, camera shake, post-processing, particles, coroutines, time control, persistent versioned settings, generated spatial sound effects, and looped music.

## Stress test

[Axolotl2D.Example.Stress](Axolotl2D.Example.Stress) provides adjustable sprite, culling, instancing, atlas/texture-array/raw batching, SDF text, spatial-index, camera, and retained-UI workloads with FPS and renderer timings enabled.

## AI assistance

I used AI-assisted tools for parts of Axolotl2D's code, documentation, and design work. I remain responsible for the project's direction and the code that ships. I include this note because I choose to be transparent about how I develop the engine.
