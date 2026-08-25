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
    private int markers;
    private int completions;

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
            var frames = sheet.Sprites.Select((sprite, frame) => new SpriteAnimationFrame(
                sprite,
                frame % 2 == 0 ? 0.08d : 0.14d,
                frame == 1 ? "footstep" : null));
            var playback = index switch
            {
                1 => SpriteAnimationPlayback.PingPong,
                2 => SpriteAnimationPlayback.Once,
                _ => SpriteAnimationPlayback.Loop
            };
            animator.Add("run", new SpriteAnimation(frames, playback));
            animator.PlaybackSpeed = 0.75f + index * 0.5f;
            animator.MarkerReached += _ => markers++;
            animator.Completed += () => completions++;
            animator.Play("run");
        }
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        DrawText(spriteBatch, textRenderer,
            "Timed frames: loop, ping-pong, and once | variable durations and footstep markers",
            new Vector2(24f, 70f), 15f);
        DrawText(spriteBatch, textRenderer, $"Markers: {markers} | completed once animations: {completions}",
            new Vector2(24f, 96f), 14f, Color.LightGray);
    }
}
