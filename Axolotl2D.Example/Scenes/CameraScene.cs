using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Axolotl2D.Timing;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class CameraScene(
    AssetManager assets,
    Camera2D camera,
    CameraManager cameras,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input,
    TimeService time,
    TweenService tweens,
    CoroutineService coroutines) : ExampleSceneBase(assets)
{
    private InputAction move = null!;
    private InputAction zoomOut = null!;
    private InputAction zoomIn = null!;
    private InputAction shake = null!;
    private InputAction pause = null!;
    private GameObject target = null!;
    private Camera2D? inset;

    public override void Load()
    {
        LoadExample("Cameras and timing", "#12243A");
        move = input.BindVector2("Move camera target", Key.A, Key.D, Key.W, Key.S);
        zoomOut = input.BindButton("Zoom out", Key.Q);
        zoomIn = input.BindButton("Zoom in", Key.E);
        shake = input.BindButton("Shake", Key.Space);
        pause = input.BindButton("Pause", Key.P);

        var logo = new Sprite(assets.Get<Texture2D>("logo"));
        for (var index = 0; index < 15; index++)
        {
            var marker = Instantiate($"World marker {index + 1}");
            marker.Transform.LocalPosition = new Vector2((index % 5 - 2) * 320f, (index / 5 - 1) * 260f);
            marker.Transform.LocalScale = new Vector2(0.13f);
            marker.AddComponent<SpriteRenderer>().Sprite = logo;
        }

        target = Instantiate("Camera follow target");
        target.Transform.LocalScale = new Vector2(0.22f);
        target.AddComponent<SpriteRenderer>().Sprite = logo;

        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        camera.FollowTarget = target.Transform;
        camera.FollowSmoothing = 6f;
        camera.DeadZone = new Vector2(80f, 50f);
        camera.Bounds = new CameraBounds(new(-900f, -520f), new(1800f, 1040f));

        inset = cameras.Create("Example inset");
        inset.Viewport = new CameraViewport(0.72f, 0.14f, 0.25f, 0.28f);
        inset.Zoom = 0.28f;
        inset.Priority = 1;

        tweens.To(-0.2f, 0.2f, 2d, value => target.Transform.LocalRotation = value,
            new TweenOptions(Ease.InOutQuad, RepeatCount: -1, Yoyo: true));
        coroutines.Start(AutomaticShake());
    }

    public override void Draw(double frameDelta, double frameRate) =>
        DrawText(spriteBatch, textRenderer,
            $"Follow, bounds, inset viewport, zoom, shake, tween and coroutine | WASD | Q/E | Space | P pause ({time.IsPaused})",
            new Vector2(24f, 70f), 14f);

    protected override void UpdateExample(double deltaTime)
    {
        target.Transform.Translate(move.Value * (float)deltaTime * 350f / camera.Zoom);
        var center = Game.Viewport / 2f;
        if (zoomOut.IsPressed) camera.ZoomAt(MathF.Pow(0.45f, (float)deltaTime), center);
        if (zoomIn.IsPressed) camera.ZoomAt(MathF.Pow(2.2f, (float)deltaTime), center);
        if (shake.WasPressedThisFrame) camera.Shake(18f, 0.35f);
        if (pause.WasPressedThisFrame) time.IsPaused = !time.IsPaused;
    }

    public override void Unload()
    {
        time.IsPaused = false;
        camera.FollowTarget = null;
        camera.Bounds = null;
        camera.DeadZone = Vector2.Zero;
        if (inset is not null) cameras.Remove(inset);
    }

    private IEnumerable<CoroutineYield?> AutomaticShake()
    {
        while (true)
        {
            yield return new WaitForSeconds(5d, UnscaledTime: true);
            camera.Shake(8f, 0.25f);
        }
    }
}
