using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using Axolotl2D.Timing;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class RenderTargetScene(
    AssetManager assets,
    Camera2D camera,
    CameraManager cameras,
    Axolotl2D.Rendering.Rendering rendering,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    TimeService time) : ExampleSceneBase(assets)
{
    private const uint CaptureLayer = 1u << 1;
    private RenderTexture target = null!;
    private Camera2D captureCamera = null!;
    private Transform subject = null!;

    public override void Load()
    {
        LoadExample("Public render textures", "#101722");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        camera.CullingMask = 1;

        target = rendering.CreateRenderTexture(320, 180, RenderTextureFilter.Nearest);
        captureCamera = cameras.Create("Render texture capture");
        captureCamera.RenderTarget = target;
        captureCamera.CullingMask = CaptureLayer;
        captureCamera.Priority = -1;
        captureCamera.Zoom = 0.48f;

        var logo = new Sprite(assets.Get<Texture2D>("logo"));
        for (var index = 0; index < 11; index++)
        {
            var item = Instantiate($"Captured item {index + 1}");
            item.Transform.LocalPosition = new Vector2((index % 4 - 1.5f) * 250f, (index / 4 - 1) * 210f);
            item.Transform.LocalScale = new Vector2(0.13f + index % 3 * 0.025f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.Sprite = logo;
            renderer.LightingLayer = CaptureLayer;
            if (index == 5) subject = item.Transform;
        }
    }

    protected override void UpdateExample(double deltaTime)
    {
        subject.LocalRotation = (float)time.TotalTime;
        captureCamera.Position = new Vector2(MathF.Sin((float)time.TotalTime * 0.5f) * 180f, 0f);
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        spriteBatch.Draw(target.Texture, new Vector2(540f, 390f), new Vector2(640f, 360f),
            space: CoordinateSpace.Screen, depth: 10f);
        DrawText(spriteBatch, textRenderer,
            "A 320x180 nearest-filtered camera target, sampled as a normal screen-space texture.",
            new Vector2(24f, 70f), 15f);
        DrawText(spriteBatch, textRenderer,
            "The capture camera moves independently; the default camera never renders layer 2 directly.",
            new Vector2(24f, 94f), 14f, Color.LightGray);
    }

    public override void Unload()
    {
        camera.CullingMask = uint.MaxValue;
        captureCamera.RenderTarget = null;
        cameras.Remove(captureCamera);
        target.Dispose();
    }
}
