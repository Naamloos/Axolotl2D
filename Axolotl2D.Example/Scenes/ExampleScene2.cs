using Axolotl2D.Animation;
using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class ExampleScene2(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer) : BaseScene
{
    private IKeyboard keyboard = null!;
    private FontAsset font = null!;
    private bool escapeWasDown;

    public override void Load()
    {
        Game.Title = "Sprite sheets and animation";
        Game.ClearColor = Color.FromHTML("#3A1734");
        keyboard = Game.GetKeyboard()!;
        font = assets.Get<FontAsset>("ui-font");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;

        // Two 384x512 cells in the example texture demonstrate atlas slicing.
        var sheet = new SpriteSheet(assets.Get<Texture2D>("logo"), 384, 512);
        var gameObject = Instantiate("Animated atlas sprite");
        gameObject.Transform.LocalScale = new Vector2(0.7f);
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.Sprite = sheet[0];
        var animator = gameObject.AddComponent<SpriteAnimator>();
        animator.Add("blink", new SpriteAnimation(sheet.Sprites, 2f));
        animator.Play("blink");
    }

    public override void Draw(double frameDelta, double frameRate) =>
        textRenderer.Draw(spriteBatch, font, "SpriteSheet + SpriteAnimator  |  Escape to return",
            24, new Vector2(24, 24), Color.White, CoordinateSpace.Screen, depth: 10);

    public override void Update(double frameDelta)
    {
        var escapeIsDown = keyboard.IsKeyPressed(Key.Escape);
        if (escapeIsDown && !escapeWasDown)
            SceneGameHost.ChangeScene<ExampleScene>();
        escapeWasDown = escapeIsDown;
    }
}
