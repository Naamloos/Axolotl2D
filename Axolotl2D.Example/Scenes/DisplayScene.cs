using Axolotl2D.Assets;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class DisplayScene(
    AssetManager assets,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input) : ExampleSceneBase(assets)
{
    private InputAction cycleMode = null!;
    private InputAction toggleVSync = null!;
    private InputAction toggleTitleFps = null!;
    private InputAction cycleRate = null!;
    private InputAction cycleResolution = null!;
    private int resolution;

    public override void Load()
    {
        LoadExample("Window and display settings", "#1B263B");
        cycleMode = input.BindButton("Cycle window mode", Key.F);
        toggleVSync = input.BindButton("Toggle VSync", Key.V);
        toggleTitleFps = input.BindButton("Toggle title FPS", Key.T);
        cycleRate = input.BindButton("Cycle draw rate", Key.R);
        cycleResolution = input.BindButton("Cycle resolution", Key.S);
    }

    protected override void UpdateExample(double deltaTime)
    {
        if (cycleMode.WasPressedThisFrame)
            Game.WindowMode = Game.WindowMode switch
            {
                GameWindowMode.Windowed => GameWindowMode.BorderlessFullscreen,
                GameWindowMode.BorderlessFullscreen => GameWindowMode.Fullscreen,
                _ => GameWindowMode.Windowed
            };
        if (toggleVSync.WasPressedThisFrame) Game.VSync = !Game.VSync;
        if (toggleTitleFps.WasPressedThisFrame) Game.ShowFramerateInTitle = !Game.ShowFramerateInTitle;
        if (cycleRate.WasPressedThisFrame)
            Game.MaximumDrawRate = Game.MaximumDrawRate >= 240d ? 60d : Game.MaximumDrawRate + 60d;
        if (cycleResolution.WasPressedThisFrame && Game.WindowMode == GameWindowMode.Windowed)
        {
            Vector2[] sizes = [new(960f, 540f), new(1280f, 720f), new(1600f, 900f)];
            Game.Viewport = sizes[resolution++ % sizes.Length];
        }
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        DrawText(spriteBatch, textRenderer, "F mode | V VSync | T title FPS | R draw limit | S window resolution",
            new Vector2(24f, 80f), 17f);
        DrawText(spriteBatch, textRenderer,
            $"Mode: {Game.WindowMode} | viewport: {Game.Viewport.X:0} x {Game.Viewport.Y:0}",
            new Vector2(24f, 116f), 16f, Color.LightGray);
        DrawText(spriteBatch, textRenderer,
            $"VSync: {Game.VSync} | draw limit: {Game.MaximumDrawRate:0} | update limit: {Game.MaximumUpdateRate:0} | title FPS: {Game.ShowFramerateInTitle}",
            new Vector2(24f, 146f), 16f, Color.LightGray);
    }
}
