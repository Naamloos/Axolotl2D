# Getting Started with Axolotl2D

Axolotl2D targets .NET 9 and uses a .NET Generic Host as its composition root. Reference the `Axolotl2D` project and add the `Microsoft.Extensions.Hosting` package to your game project, then register the game and scenes in `Program.cs`:

```csharp
using Axolotl2D;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.UseSceneManagerGameHost<MyGame>();
        services.AddScene<MainScene>();
    })
    .Build();

await host.RunAsync();
```

`UseSceneManagerGameHost<T>()` installs assets, audio, rendering, `Camera2D`, `SpriteBatch`, `PrimitiveBatch`, text, UI and particle dependencies, optional runtime inspection, and the GameObject factory. The host creates a DI scope for the active scene. Scenes and their components share scoped services until a scene transition disposes the scope. Pass `enableDebugOverlay: true` to inspect a development build in-game.

## Load assets

Load CPU assets in `Game.InitializeAsync`. The game host awaits this method before it starts the window and loads the default scene. The manager chooses the registered `IAssetLoader<T>` and caches the result by type and key.

```csharp
public sealed class MyGame : Game
{
    private readonly AssetManager assets;

    public MyGame(IServiceProvider services, AssetManager assets) : base(services)
    {
        this.assets = assets;
    }

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await assets.LoadFileAsync<Texture2D>(
            "player",
            "Assets/player.png",
            cancellationToken);
    }

    protected override void Cleanup() { }
}
```

Use `LoadEmbeddedAsync<T>` for assembly resources. `SoundAsset` accepts PCM WAVE streams, and `FontAsset` accepts TrueType, OpenType, WOFF, and WOFF2 streams. You can add another asset format by registering an `IAssetLoader<YourAsset>` before the host starts.

## Build a scene from GameObjects

Mark one registered scene with `[DefaultScene]`. The scene owns each object created through `Instantiate`; scene unload disposes those objects and their components.

```csharp
[DefaultScene]
public sealed class MainScene(AssetManager assets) : BaseScene
{
    public override void Load()
    {
        var player = Instantiate("Player");
        player.Transform.LocalPosition = new Vector2(100, 80);

        var renderer = player.AddComponent<SpriteRenderer>();
        renderer.Sprite = new Sprite(assets.Get<Texture2D>("player"));
        player.AddComponent<PlayerController>();
    }
}

public sealed class PlayerController(GameObject gameObject, InputSettings settings)
    : Component(gameObject)
{
    public override void Update(double deltaTime) =>
        Transform.Translate(Vector2.UnitX * settings.Speed * (float)deltaTime);
}
```

`AddComponent<T>()` uses the scene's scoped service provider, so component constructors can request application services. `Transform` supplies parenting, local and world matrices, translation, rotation, point conversion, direction vectors, and `LookAt`.

Register scene-owned dependencies with `AddScoped`. Components resolved in one scene receive the same scoped instance:

```csharp
services.AddScoped<CombatSession>();
```

## Runtime objects and lifecycle

Scenes can create and destroy GameObjects during fixed updates, variable updates, late updates, or rendering:

```csharp
GameObject projectile = Instantiate("Projectile");
projectile.AddComponent<ProjectileRenderer>();
projectile.AddComponent<ProjectileController>();

projectile.Destroy();
// Equivalent: Destroy(projectile);
```

`Destroy` disables the object at once. The scene removes and disposes it after the current lifecycle phase, which keeps callback iteration safe.

Components receive callbacks in this order:

1. `Awake` runs when the component attaches.
2. `OnEnable` runs when both the component and GameObject become active.
3. `Start` runs before the first fixed update, update, or render.
4. `FixedUpdate`, `Update`, `LateUpdate`, and `Render` run while the component remains active.
5. `OnDisable` and `OnDestroy` run during removal or scene unload.

Setting `Component.Enabled` or `GameObject.Active` invokes the enable and disable callbacks. `BaseScene.FixedTimeStep` defaults to 1/60 second and caps catch-up work through `MaximumFixedStepsPerFrame`.

## Camera and coordinates

Axolotl2D treats screen coordinates as pixels measured from the top-left. World coordinates pass through `Camera2D`; the camera position identifies the world point at the viewport center.

```csharp
camera.Pan(new Vector2(20, 0));
camera.ZoomAt(1.1f, mouse.Position);

Vector2 worldMouse = Coordinates.ScreenToWorld(mouse.Position, camera);
Vector2 screenLabel = Coordinates.WorldToScreen(enemy.Transform.Position, camera);
```

Sprite renderers use `CoordinateSpace.World`. Draw UI with `CoordinateSpace.Screen` so camera motion leaves it fixed.

## Continue learning

- [Architecture and Dependency Injection](architecture-and-dependency-injection.md) explains service lifetimes and component injection.
- [Scenes and Scene Scopes](scenes-and-scene-scopes.md) covers transitions and fixed updates.
- [GameObjects and Components](gameobjects-and-components.md) introduces the composition model.
- [Asset Management](asset-management.md) covers files, embedded resources, caching, and custom loaders.
- [Sprites and Sprite Batching](sprites-and-sprite-batching.md) covers draw submission and ordering.
- [Camera and Coordinate Systems](camera-and-coordinate-systems.md) covers panning, zooming, and conversions.
- [Sprite Sheets and Animation](sprite-sheets-and-animation.md), [Text Rendering](text-rendering.md), and [Audio Playback](audio.md) cover the remaining content systems.
- [Input Actions](input-actions.md) and [Time and Fixed Updates](time-and-fixed-update.md) cover gameplay input and timing.
- [Custom Shaders](custom-shaders.md) and [Box2D Physics](physics.md) cover programmable rendering and simulation.
