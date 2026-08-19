# Architecture and Dependency Injection

Axolotl2D is a framework rather than an editor-driven engine. Your application owns startup, service registration, scenes, and components. The .NET Generic Host is the composition root, and Microsoft.Extensions.DependencyInjection supplies services throughout the framework.

## Configure the host

Use `UseSceneManagerGameHost<TGame>()` for a scene-based game:

```csharp
using Axolotl2D;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.UseSceneManagerGameHost<MyGame>();
        services.AddScene<MainMenuScene>();
        services.AddScene<GameplayScene>();

        services.AddSingleton<GameSettings>();
        services.AddScoped<LevelSession>();
        services.AddTransient<EnemyBrain>();
    })
    .Build();

await host.RunAsync();
```

`UseSceneManagerGameHost<TGame>()` calls `AddAxolotl2D()` and registers the game and scene host. `AddAxolotl2D()` provides assets, audio, input actions, time, rendering, scoped shaders, physics, and the GameObject factory.

The game host awaits `Game.InitializeAsync` before it starts the window and loads the default scene. Override that method for asset loading and pass its cancellation token to each loader.

`UseSimpleGameHost<TGame>()` remains available for applications that want to work directly with the `Game` events and do not need scenes.

## Service lifetimes

Choose lifetimes according to ownership:

| Lifetime | Good fit | Ends when |
| --- | --- | --- |
| Singleton | Configuration, save data, `TimeService`, `InputActionSystem` | The application stops |
| Scoped | Level state, `InputActionMap`, `ShaderLibrary`, `PhysicsWorld` | The active scene changes |
| Transient | Small stateless collaborators | The consumer releases them |

Axolotl2D registers scenes, action maps, shader libraries, physics worlds, and `IGameObjectFactory` as scoped services. Rendering, assets, audio, time, the input device system, the default camera, sprite batching, and text rendering are singleton services.

## Constructor injection

Games, scenes, and components can request registered services through their constructors:

```csharp
public sealed class GameplayScene(
    LevelSession session,
    AssetManager assets,
    Camera2D camera) : BaseScene
{
    public override void Load()
    {
        session.Begin();
        camera.Position = Vector2.Zero;

        var player = Instantiate("Player");
        player.AddComponent<PlayerController>();
    }
}

public sealed class PlayerController(
    GameObject gameObject,
    LevelSession session,
    ILogger<PlayerController> logger) : Component(gameObject)
{
    public override void Start() =>
        logger.LogInformation("Player entered level {Level}", session.LevelName);
}
```

`GameObject.AddComponent<T>()` uses `ActivatorUtilities` with the active scene provider. The current `GameObject` is supplied automatically, and the remaining constructor arguments come from DI. Components in the same scene therefore receive the same scoped `LevelSession` instance.

## Ownership boundaries

The framework follows a clear ownership chain:

```text
Generic Host
  Game and application-wide singleton services
    Scene DI scope
      Scene and scoped services
        GameObjects
          Components
```

Changing scenes disposes the old GameObjects and their components before disposing the old scene scope. This makes scene-scoped services safe to use from `OnDestroy`.

Avoid resolving services from the root provider inside gameplay code. Constructor injection keeps dependencies visible and ensures scoped services come from the correct scene.

## Design patterns in the framework

Axolotl2D combines a small set of established patterns:

| Pattern | Where it appears | Benefit |
| --- | --- | --- |
| Composition | GameObjects gain behavior from components | Features stay reusable without deep inheritance trees |
| Dependency injection | Host, scenes, custom services, and components | Dependencies are explicit and replaceable |
| Lifetime scope | One service scope per active scene | Scene-local state has a clear lifetime and cleanup boundary |
| Factory | `IGameObjectFactory` and `Instantiate` | Runtime objects are created with the correct scene provider |
| Template method | `BaseScene` and `Component` callbacks | The framework controls ordering while game code supplies behavior |
| Command batching | `SpriteBatch` queues draw commands | Rendering can order work and reduce texture submissions |
| Strategy | `IAssetLoader<TAsset>` | New asset formats plug in without changing `AssetManager` |
| Adapter | `PhysicsWorld` and `PhysicsBody` wrap Box2D.NET | GameObjects use Box2D without losing access to raw handles |

These patterns reinforce the same ownership model. A component declares what it needs, the current scene scope supplies it, the scene owns the resulting object, and the framework disposes the graph at a predictable boundary. That improves testability, makes scene transitions less prone to leaked state, and keeps rendering and asset code separate from gameplay behavior.

## Replace or extend services

Register custom services before building the host. The built-in registrations use `TryAdd`, so an earlier registration can replace a default implementation where the service type permits it. Asset formats are extended by registering another closed `IAssetLoader<TAsset>`:

```csharp
services.AddSingleton<IAssetLoader<TileMap>, TileMapAssetLoader>();
```

See [Asset Management](asset-management.md) for a complete loader example and [Scenes and Scene Scopes](scenes-and-scene-scopes.md) for scope behavior during transitions.
