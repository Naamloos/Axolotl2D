# Window and Display Settings

Pass `GameWindowOptions` to the `Game` base constructor to choose startup settings:

```csharp
public sealed class MyGame(IServiceProvider services) : Game(services, new GameWindowOptions
{
    Title = "My Game",
    Size = new Vector2(1280, 720),
    Mode = GameWindowMode.Windowed,
    Resizable = true,
    VSync = true,
    MaximumDrawRate = 120,
    MaximumUpdateRate = 120,
    ShowFramerateInTitle = false
})
{
    protected override void Cleanup() { }
}
```

The original `Game(IServiceProvider, maxDrawRate, maxUpdateRate)` constructor remains available.

## Change settings at runtime

`Game` exposes the settings commonly used by an options screen:

```csharp
Game.Viewport = new Vector2(1920, 1080);
Game.VSync = true;
Game.MaximumDrawRate = 144;
Game.MaximumUpdateRate = 120;
Game.ShowFramerateInTitle = false;
Game.WindowMode = GameWindowMode.BorderlessFullscreen;
```

The available modes are `Windowed`, `BorderlessFullscreen`, and `Fullscreen`. Switching away from windowed mode remembers the window's size and position and restores both when returning. Borderless fullscreen covers the window's current monitor.

Rates and viewport dimensions must be finite and greater than zero. VSync and the draw-rate limit are both forwarded to Silk.NET; the platform window backend determines their exact scheduling behavior.
