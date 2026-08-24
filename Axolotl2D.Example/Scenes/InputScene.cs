using Axolotl2D.Assets;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class InputScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input) : ExampleSceneBase(assets)
{
    private InputAction move = null!;
    private InputAction gamepadButton = null!;
    private InputAction trigger = null!;
    private InputAction cycleBinding = null!;
    private int binding;

    public override void Load()
    {
        LoadExample("Gamepad input and rebinding", "#17243A");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        move = input.BindVector2("Rebindable movement", Key.A, Key.D, Key.W, Key.S);
        gamepadButton = input.BindButton("Gamepad confirm", ButtonName.A);
        trigger = input.BindAxis("Gamepad throttle", GamepadAxis.RightTrigger, deadZone: 0.1f);
        cycleBinding = input.BindButton("Cycle movement binding", Key.R);
    }

    protected override void UpdateExample(double deltaTime)
    {
        if (!cycleBinding.WasPressedThisFrame) return;
        binding = (binding + 1) % 3;
        switch (binding)
        {
            case 0:
                input.RebindVector2(move.Name, Key.A, Key.D, Key.W, Key.S);
                break;
            case 1:
                input.RebindVector2(move.Name, Key.Left, Key.Right, Key.Up, Key.Down);
                break;
            default:
                input.RebindVector2(move.Name, GamepadStick.Left, deadZone: 0.2f);
                break;
        }
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        DrawText(spriteBatch, textRenderer, "R cycles the same InputAction through WASD, arrows, and left stick.",
            new Vector2(24f, 70f), 15f);
        DrawText(spriteBatch, textRenderer,
            $"Movement binding: {BindingName} | value: ({move.Value.X:0.00}, {move.Value.Y:0.00})",
            new Vector2(24f, 98f), 15f);
        DrawText(spriteBatch, textRenderer,
            $"Gamepad A: {(gamepadButton.IsPressed ? "pressed" : "released")} | right trigger after dead zone: {trigger.Scalar:0.00}",
            new Vector2(24f, 126f), 15f, Color.LightGray);
        DrawText(spriteBatch, textRenderer,
            "The movement action reference is preserved when its binding changes.",
            new Vector2(24f, 154f), 14f, Color.LightGray);
    }

    private string BindingName => binding switch
    {
        0 => "WASD",
        1 => "arrow keys",
        _ => "gamepad left stick (20% radial dead zone)"
    };
}
