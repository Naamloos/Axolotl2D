# Scenes and Scene Scopes

A scene is a runtime container for GameObjects and a dependency injection scope. `SceneGameHost` owns a stack of active scenes so overlays can have isolated objects and services while drawing over another scene.

## Register and select scenes

Register every scene with `AddScene<T>()` and mark exactly one scene in the entry assembly with `[DefaultScene]`:

```csharp
services.UseSceneManagerGameHost<MyGame>();
services.AddScene<MainMenuScene>();
services.AddScene<GameplayScene>();

[DefaultScene]
public sealed class MainMenuScene : BaseScene
{
}
```

The scene host creates a new `IServiceScope`, resolves the scene from it, loads the scene, and starts forwarding game events. A transition unloads and disposes the current scene before the old scope is disposed.

## Change scenes

Use the protected `SceneGameHost` property from a scene to request a transition:

```csharp
public sealed class MainMenuScene : BaseScene
{
    public void StartGame() => SceneGameHost.ChangeScene<GameplayScene>();
}
```

The destination scene receives a fresh scope. Scoped objects from the previous scene are not reused. Singleton services, such as `AssetManager`, remain available across the transition.

## Layer scenes

Push an overlay for pause menus, minimaps, dialogs, and other independently scoped layers:

```csharp
SceneGameHost.PushScene<PauseMenuScene>();
SceneGameHost.PopScene();
```

The default overlay draws lower scenes but blocks their updates. Use `SceneLayerOptions.Additive` when lower scenes should keep updating, or `SceneLayerOptions.Opaque` when lower scenes should be neither updated nor drawn:

```csharp
SceneGameHost.PushScene<HudScene>(SceneLayerOptions.Additive);
SceneGameHost.PushScene<LoadingScene>(SceneLayerOptions.Opaque);
```

Each pushed scene owns a separate DI scope. Layers update and draw from bottom to top. `ChangeScene` remains a full transition: it clears every layer before activating the destination scene. `CurrentScene` is the top layer, and `SceneCount` reports the stack size. The base scene cannot be popped; use `ChangeScene` to replace it.

## Scene callbacks

Override only the callbacks the scene needs:

```csharp
public sealed class GameplayScene : BaseScene
{
    public override void Load() { }
    public override void FixedUpdate(double fixedDeltaTime) { }
    public override void Update(double deltaTime) { }
    public override void LateUpdate(double deltaTime) { }
    public override void Draw(double frameDelta, double frameRate) { }
    public override void Resize(Vector2 size) { }
    public override void Unload() { }
}
```

The scene invokes matching component callbacks around its own callbacks. `SpriteBatch` is already open during component `Render` and `BaseScene.Draw`, so scene drawing code should submit draws without calling `Begin` or `End`.

## Fixed updates

`FixedTimeStep` defaults to 1/60 second. `MaximumFixedStepsPerFrame` defaults to 8 and limits catch-up work after a slow frame:

```csharp
public sealed class GameplayScene : BaseScene
{
    public GameplayScene()
    {
        FixedTimeStep = 1d / 120d;
        MaximumFixedStepsPerFrame = 6;
    }
}
```

Use fixed updates for forces and simulation input. The scoped `PhysicsWorld` advances after component and scene fixed callbacks. Use `Update` for input actions and animation. Use `LateUpdate` for work that must observe the final transforms from the current frame, such as a following camera.

`TimeService.TimeScale` controls how fast the accumulator fills. Paused scenes still receive `Update(0)` and render, which lets input actions resume the game.

## Scene-owned objects

Create objects through `Instantiate` so the scene can schedule lifecycle callbacks and disposal:

```csharp
var enemy = Instantiate("Enemy");
enemy.AddComponent<EnemyController>();

Destroy(enemy);
```

`GameObjects` exposes the current scene-owned objects as a read-only list. Scene unload destroys any objects that remain.

See [Runtime GameObjects](runtime-gameobjects.md) for safe creation and destruction and [Component Lifecycle](component-lifecycle.md) for callback order.
