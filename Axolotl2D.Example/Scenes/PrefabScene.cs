using Axolotl2D.Assets;
using Axolotl2D.Input;
using Axolotl2D.Prefabs;
using Axolotl2D.Rendering;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class PrefabScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input) : ExampleSceneBase(assets)
{
    private readonly Random random = new();
    private PrefabAsset prefab = null!;
    private InputAction spawn = null!;
    private int count;

    public override void Load()
    {
        LoadExample("Data prefabs", "#182330");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        prefab = assets.Get<PrefabAsset>("axolotl-cluster");
        spawn = input.BindButton("Instantiate prefab", Key.Space);
        for (var index = 0; index < 5; index++) Spawn();
    }

    public override void Draw(double frameDelta, double frameRate) =>
        DrawText(spriteBatch, textRenderer,
            "Packaged .axprefab hierarchy with built-in and custom component data | Space instantiate",
            new Vector2(24f, 70f), 15f);

    protected override void UpdateExample(double deltaTime)
    {
        if (spawn.WasPressedThisFrame) Spawn();
    }

    private void Spawn()
    {
        var gameObject = Instantiate(prefab, $"Prefab instance {++count}");
        gameObject.Transform.LocalPosition = new Vector2(random.NextSingle() * 760f - 380f,
            random.NextSingle() * 400f - 160f);
        gameObject.Transform.LocalRotation = random.NextSingle() * MathF.Tau;
    }
}
