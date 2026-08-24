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
    private InputAction confirm = null!;
    private InputAction chord = null!;
    private InputAction trigger = null!;
    private InputAction switchScheme = null!;
    private InputAction beginCapture = null!;
    private InputProfile profile = null!;
    private InputCapture? capture;
    private string scheme = "Keyboard & Mouse";
    private int jsonLength;

    public override void Load()
    {
        LoadExample("Input profiles and capture", "#17243A");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        move = input.BindVector2("Move", Key.A, Key.D, Key.W, Key.S);
        confirm = input.BindButton("Confirm", Key.Enter);
        input.BindButton("Menu confirm", Key.Enter);
        chord = input.BindChord("Developer chord", InputControl.From(Key.ControlLeft), InputControl.From(Key.K));
        trigger = input.BindAxis("Gamepad throttle", GamepadAxis.RightTrigger, deadZone: 0.1f);
        switchScheme = input.BindButton("Switch scheme", Key.R);
        beginCapture = input.BindButton("Capture confirm", Key.C);

        profile = input.CreateProfile(scheme);
        profile.SetBinding("Gamepad", move.Name, InputBinding.Stick(GamepadStick.Left, 0.2f));
        profile.SetBinding("Gamepad", confirm.Name,
            InputBinding.Button(InputControl.From(ButtonName.A)));
        var json = profile.ToJson();
        jsonLength = json.Length;
        profile = InputProfile.FromJson(json);
        input.ApplyProfile(profile, scheme);
    }

    protected override void UpdateExample(double deltaTime)
    {
        if (switchScheme.WasPressedThisFrame)
        {
            scheme = scheme == "Keyboard & Mouse" ? "Gamepad" : "Keyboard & Mouse";
            input.SwitchControlScheme(scheme);
        }
        if (beginCapture.WasPressedThisFrame && capture?.IsPending != true)
            capture = input.CaptureButton(confirm.Name);
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        DrawText(spriteBatch, textRenderer, "R switches named schemes. C captures the next key, mouse, or gamepad button for Confirm.",
            new Vector2(24f, 70f), 15f);
        DrawText(spriteBatch, textRenderer,
            $"Scheme: {scheme} | Move: {move.BindingDescription} | value: ({move.Value.X:0.00}, {move.Value.Y:0.00})",
            new Vector2(24f, 98f), 15f);
        DrawText(spriteBatch, textRenderer,
            $"Confirm: {confirm.BindingDescription} ({CaptureState}) | right trigger: {trigger.Scalar:0.00}",
            new Vector2(24f, 126f), 15f, Color.LightGray);
        DrawText(spriteBatch, textRenderer,
            $"Ctrl+K chord: {(chord.IsPressed ? "pressed" : "released")} | deliberate conflicts: {profile.FindConflicts("Keyboard & Mouse").Count}",
            new Vector2(24f, 154f), 14f, Color.LightGray);
        DrawText(spriteBatch, textRenderer,
            $"Profile JSON round-tripped in memory ({jsonLength} characters); InputAction references remain stable.",
            new Vector2(24f, 180f), 14f, Color.LightGray);
    }

    private string CaptureState => capture?.IsPending == true ? "waiting" : "ready";
}
