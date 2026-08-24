using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;
using System.Text.Json;
using Axolotl2D.Prefabs;

namespace Axolotl2D.Example.Scenes;

[DefaultScene]
public sealed class SpriteScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input) : ExampleSceneBase(assets)
{
    private Sprite logo = null!;
    private InputAction spawn = null!;
    private InputAction destroy = null!;
    private int count;

    public override void Load()
    {
        LoadExample("Sprites and transforms", "#17213A");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        logo = new Sprite(assets.Get<Texture2D>("logo"));
        spawn = input.BindButton("Spawn sprite", Key.Space);
        destroy = input.BindButton("Destroy sprite", Key.Backspace);

        var parent = AddSprite("Parent", new Vector2(-220f, 20f), 0.32f);
        var child = AddSprite("Child", new Vector2(190f, 0f), 0.55f);
        child.Transform.SetParent(parent.Transform, worldPositionStays: false);
        parent.AddComponent<Spinner>().Speed = 0.7f;
        child.AddComponent<Spinner>().Speed = -1.5f;

        for (var index = 0; index < 4; index++)
            AddLooseSprite();
    }

    public override void Draw(double frameDelta, double frameRate) =>
        DrawText(spriteBatch, textRenderer,
            "Transform hierarchy, components, runtime creation and deferred destruction | Space add | Backspace remove",
            new Vector2(24f, 70f), 15f);

    protected override void UpdateExample(double deltaTime)
    {
        if (spawn.WasPressedThisFrame) AddLooseSprite();
        if (destroy.WasPressedThisFrame)
            GameObjects.LastOrDefault(gameObject => gameObject.HasTag("loose"))?.Destroy();
    }

    private GameObject AddSprite(string name, Vector2 position, float scale)
    {
        var gameObject = Instantiate(name);
        gameObject.Transform.LocalPosition = position;
        gameObject.Transform.LocalScale = new Vector2(scale);
        gameObject.AddComponent<SpriteRenderer>().Sprite = logo;
        return gameObject;
    }

    private void AddLooseSprite()
    {
        var index = count++;
        var gameObject = AddSprite($"Loose sprite {index + 1}",
            new Vector2((index % 4 - 1.5f) * 190f, 170f + index / 4 * 100f), 0.18f);
        gameObject.AddTag("loose");
        gameObject.AddComponent<Spinner>().Speed = 0.25f + index * 0.08f;
    }
}

public sealed class Spinner(GameObject gameObject, ILogger<Spinner> logger) : Component(gameObject), IPrefabDataReceiver
{
    public float Speed { get; set; } = 1f;
    public override void Start() => logger.LogDebug("Started {GameObject}", GameObject.Name);
    public override void FixedUpdate(double fixedDeltaTime) => Transform.Rotate(Speed * (float)fixedDeltaTime);
    public override void OnDestroy() => logger.LogDebug("Destroyed {GameObject}", GameObject.Name);
    public void LoadPrefabData(JsonElement data, PrefabLoadContext context) =>
        Speed = context.Deserialize<SpinnerPrefabData>(data).Speed;
}

public sealed record SpinnerPrefabData(float Speed = 1f);
