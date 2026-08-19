using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

[DefaultScene]
public sealed class ExampleScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    ILogger<ExampleScene> logger) : BaseScene
{
    private IKeyboard keyboard = null!;
    private FontAsset font = null!;
    private Sprite logoSprite = null!;
    private bool escapeWasDown;
    private bool spawnWasDown;
    private bool destroyWasDown;
    private int spawnedCount;

    public override void Load()
    {
        Game.Title = "GameObjects, batching, camera, and text";
        Game.ClearColor = Color.FromHTML("#17213A");
        keyboard = Game.GetKeyboard()!;
        font = assets.Get<FontAsset>("ui-font");
        logoSprite = new Sprite(assets.Get<Texture2D>("logo"));

        for (var index = 0; index < 6; index++)
            SpawnAxolotl();

        logger.LogInformation("Scene created {Count} DI-composed GameObjects", GameObjects.Count);
    }

    public override void Draw(double frameDelta, double frameRate) =>
        textRenderer.Draw(spriteBatch, font,
            "WASD pan | Q/E zoom | Space spawn | Backspace destroy | Escape scene",
            24, new Vector2(24, 24), Color.White, CoordinateSpace.Screen, depth: 10);

    public override void Update(double frameDelta)
    {
        var movement = Vector2.Zero;
        if (keyboard.IsKeyPressed(Key.A)) movement.X--;
        if (keyboard.IsKeyPressed(Key.D)) movement.X++;
        if (keyboard.IsKeyPressed(Key.W)) movement.Y--;
        if (keyboard.IsKeyPressed(Key.S)) movement.Y++;
        camera.Pan(movement * (float)frameDelta * 350f / camera.Zoom);

        var viewportCenter = Game.Viewport / 2f;
        if (keyboard.IsKeyPressed(Key.Q)) camera.ZoomAt(MathF.Pow(0.45f, (float)frameDelta), viewportCenter);
        if (keyboard.IsKeyPressed(Key.E)) camera.ZoomAt(MathF.Pow(2.2f, (float)frameDelta), viewportCenter);

        var spawnIsDown = keyboard.IsKeyPressed(Key.Space);
        if (spawnIsDown && !spawnWasDown)
            SpawnAxolotl();
        spawnWasDown = spawnIsDown;

        var destroyIsDown = keyboard.IsKeyPressed(Key.Backspace);
        if (destroyIsDown && !destroyWasDown)
            GameObjects.LastOrDefault(gameObject => gameObject.Name.StartsWith("Axolotl", StringComparison.Ordinal))?.Destroy();
        destroyWasDown = destroyIsDown;

        var escapeIsDown = keyboard.IsKeyPressed(Key.Escape);
        if (escapeIsDown && !escapeWasDown)
            SceneGameHost.ChangeScene<ExampleScene2>();
        escapeWasDown = escapeIsDown;
    }

    private void SpawnAxolotl()
    {
        var index = spawnedCount++;
        var gameObject = Instantiate($"Axolotl {index + 1}");
        gameObject.Transform.LocalPosition = new Vector2((index % 3 - 1) * 270, (index % 6 / 3 - 0.5f) * 230);
        gameObject.Transform.LocalScale = new Vector2(0.28f);
        gameObject.AddComponent<SpriteRenderer>().Sprite = logoSprite;
        gameObject.AddComponent<Spinner>().Speed = 0.15f + index * 0.07f;
    }
}

public sealed class Spinner(GameObject gameObject, ILogger<Spinner> logger) : Component(gameObject)
{
    public float Speed { get; set; } = 1f;
    public override void Start() => logger.LogDebug("Started {GameObject}", GameObject.Name);
    public override void FixedUpdate(double fixedDeltaTime) => Transform.Rotate(Speed * (float)fixedDeltaTime);
    public override void OnDestroy() => logger.LogDebug("Destroyed {GameObject}", GameObject.Name);
}
