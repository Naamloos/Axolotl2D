using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Physics;
using Axolotl2D.Rendering;
using Box2D.NET;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class PhysicsScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    PrimitiveBatch primitives,
    InputActionMap input) : ExampleSceneBase(assets)
{
    private readonly Random random = new();
    private Sprite logo = null!;
    private InputAction spawn = null!;
    private int spawned;
    private int collisions;

    public override void Load()
    {
        LoadExample("Box2D physics", "#182D27");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        logo = new Sprite(assets.Get<Texture2D>("logo"));
        spawn = input.BindButton("Spawn physics body", Key.Space);

        var ground = Instantiate("Ground");
        ground.Transform.LocalPosition = new Vector2(0f, 290f);
        var groundBody = ground.AddComponent<PhysicsBody>();
        groundBody.Type = B2BodyType.b2_staticBody;
        groundBody.AddBox(new Vector2(900f, 36f));

        for (var index = 0; index < 14; index++)
            SpawnBody();
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        primitives.FillRectangle(new Vector2(-450f, 272f), new Vector2(900f, 36f),
            Color.FromHTML("#4F6F52"), CoordinateSpace.World);
        DrawText(spriteBatch, textRenderer,
            $"Dynamic boxes and circles with collision events | Space spawn | Collisions: {collisions}",
            new Vector2(24f, 70f), 15f);
    }

    protected override void UpdateExample(double deltaTime)
    {
        if (spawn.WasPressedThisFrame) SpawnBody();
    }

    private void SpawnBody()
    {
        var index = spawned++;
        var gameObject = Instantiate($"Physics body {index + 1}");
        gameObject.Transform.LocalPosition = new Vector2(
            random.NextSingle() * 760f - 380f,
            random.NextSingle() * 230f - 260f);
        gameObject.Transform.LocalRotation = random.NextSingle() * MathF.Tau - MathF.PI;
        gameObject.Transform.LocalScale = new Vector2(0.11f);
        gameObject.AddComponent<SpriteRenderer>().Sprite = logo;
        var body = gameObject.AddComponent<PhysicsBody>();
        if (index % 3 == 0)
            body.AddCircle(48f, restitution: 0.55f);
        else
            body.AddBox(new Vector2(92f, 62f), restitution: 0.3f);
        body.CollisionEntered += _ => collisions++;
    }
}
