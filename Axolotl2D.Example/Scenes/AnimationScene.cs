using Axolotl2D.Animation;
using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class AnimationScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer) : ExampleSceneBase(assets)
{
    public override void Load()
    {
        LoadExample("Sprite sheets and animation", "#3A1734");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        var sheet = new SpriteSheet(assets.Get<Texture2D>("run"), 255, 255);

        for (var index = 0; index < 3; index++)
        {
            var gameObject = Instantiate($"Animated sprite {index + 1}");
            gameObject.Transform.LocalPosition = new Vector2((index - 1) * 280f, 70f);
            gameObject.Transform.LocalScale = new Vector2(0.65f);
            gameObject.AddComponent<SpriteRenderer>().Sprite = sheet[0];
            var animator = gameObject.AddComponent<SpriteAnimator>();
            animator.Add("run", new SpriteAnimation(sheet.Sprites, 5f + index * 7f));
            animator.Play("run");
        }
    }

    public override void Draw(double frameDelta, double frameRate) =>
        DrawText(spriteBatch, textRenderer, "SpriteSheet atlas slicing and SpriteAnimator at three playback speeds",
            new Vector2(24f, 70f), 15f);
}
