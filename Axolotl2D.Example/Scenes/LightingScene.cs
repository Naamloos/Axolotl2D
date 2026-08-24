using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Lighting;
using Axolotl2D.Rendering;
using Axolotl2D.Timing;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class LightingScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    Lighting2D lighting,
    TweenService tweens) : ExampleSceneBase(assets)
{
    public override void Load()
    {
        LoadExample("2D lighting", "#101522");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        lighting.AmbientColor = Color.FromHTML("#263553");
        lighting.AmbientIntensity = 0.22f;

        var logo = new Sprite(assets.Get<Texture2D>("logo"));
        logo.NormalMap = CreateNormalMap(logo.Texture.Width, logo.Texture.Height);
        foreach (var x in new[] { -280f, 0f, 280f })
        {
            var gameObject = Instantiate("Normal-mapped sprite");
            gameObject.Transform.LocalPosition = new Vector2(x, 80f);
            gameObject.Transform.LocalScale = new Vector2(0.24f);
            gameObject.AddComponent<SpriteRenderer>().Sprite = logo;
        }

        var pointObject = Instantiate("Moving point light");
        pointObject.Transform.LocalPosition = new Vector2(-380f, -100f);
        var point = pointObject.AddComponent<Light2D>();
        point.Color = Color.FromHTML("#FFB45C");
        point.Intensity = 1.8f;
        point.Radius = 520f;
        tweens.To(new Vector2(-380f, -100f), new Vector2(380f, -100f), 3d,
            value => pointObject.Transform.LocalPosition = value,
            new TweenOptions(Ease.InOutQuad, RepeatCount: -1, Yoyo: true));

        var spotObject = Instantiate("Rotating spot light");
        spotObject.Transform.LocalPosition = new Vector2(0f, -250f);
        spotObject.AddComponent<Spinner>().Speed = 0.7f;
        var spot = spotObject.AddComponent<Light2D>();
        spot.Kind = LightKind2D.Spot;
        spot.Color = Color.FromHTML("#69D2FF");
        spot.Intensity = 2f;
        spot.Radius = 650f;
        spot.SpotAngle = 0.65f;

        var caster = Instantiate("Shadow caster");
        caster.Transform.LocalPosition = new Vector2(0f, 220f);
        caster.AddComponent<ShadowCaster2D>().SetPolygon(
            new(-180f, -35f), new(180f, -35f), new(180f, 35f), new(-180f, 35f));
    }

    public override void Draw(double frameDelta, double frameRate) =>
        DrawText(spriteBatch, textRenderer, "Normal maps, animated point light, rotating spotlight and polygon shadows",
            new Vector2(24f, 70f), 15f);

    private static Texture2D CreateNormalMap(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var center = new Vector2(width, height) / 2f;
        var radius = Math.Max(1f, Math.Min(width, height) / 2f);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var planar = Vector2.Clamp((new Vector2(x, y) - center) / radius, new(-1f), new(1f));
                var z = MathF.Sqrt(MathF.Max(0f, 1f - planar.LengthSquared()));
                var offset = (y * width + x) * 4;
                pixels[offset] = (byte)((planar.X * 0.5f + 0.5f) * 255f);
                pixels[offset + 1] = (byte)((planar.Y * 0.5f + 0.5f) * 255f);
                pixels[offset + 2] = (byte)((z * 0.5f + 0.5f) * 255f);
                pixels[offset + 3] = 255;
            }
        return new Texture2D(width, height, pixels);
    }
}
