using Axolotl2D.Animation;
using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class ExampleScene2(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input) : BaseScene
{
    private FontAsset font = null!;
    private InputAction changeScene = null!;

    public override void Load()
    {
        Game.Title = "Sprite sheets and animation";
        Game.ClearColor = Color.FromHTML("#3A1734");
        font = assets.Get<FontAsset>("ui-font");
        changeScene = input.BindButton("Change scene", Key.Escape);
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;

        // Two 100x100 cells in the example texture demonstrate atlas slicing.
        var sheet = new SpriteSheet(assets.Get<Texture2D>("run"), 255, 255);
        var gameObject = Instantiate("Animated atlas sprite");
        gameObject.Transform.LocalScale = new Vector2(0.7f);
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.Sprite = sheet[0];
        var animator = gameObject.AddComponent<SpriteAnimator>();
        animator.Add("run", new SpriteAnimation(sheet.Sprites, 20f));
        animator.Play("run");
    }

    public override void Draw(double frameDelta, double frameRate) =>
        textRenderer.Draw(spriteBatch, font, "SpriteSheet + SpriteAnimator | Escape to return",
            14, new Vector2(24, 24), Color.White, CoordinateSpace.Screen, depth: 10);

    public override void Update(double frameDelta)
    {
        if (changeScene.WasPressedThisFrame)
            SceneGameHost.ChangeScene<ExampleScene>();
    }
}
