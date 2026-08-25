using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Physics;
using Axolotl2D.Prefabs;
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
    InputActionMap input,
    PhysicsWorld physics) : ExampleSceneBase(assets)
{
    private const ulong DynamicLayer = 1ul << 0;
    private const ulong EnvironmentLayer = 1ul << 1;
    private const ulong SensorLayer = 1ul << 2;
    private readonly Random random = new();
    private Sprite logo = null!;
    private InputAction spawn = null!;
    private int spawned;
    private int collisions;
    private int sensorEntries;
    private PhysicsCastHit? rayHit;
    private PhysicsCastHit? circleHit;
    private int overlapCount;

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
        ground.AddComponent<BoxCollider>(collider =>
        {
            collider.Size = new Vector2(900f, 36f);
            collider.CategoryBits = EnvironmentLayer;
            collider.MaskBits = DynamicLayer;
        });

        var sensor = Instantiate("Sensor zone");
        sensor.Transform.LocalPosition = new Vector2(0f, 110f);
        sensor.AddComponent<PhysicsBody>().Type = B2BodyType.b2_staticBody;
        var sensorCollider = sensor.AddComponent<BoxCollider>(collider =>
        {
            collider.Size = new Vector2(260f, 100f);
            collider.IsSensor = true;
            collider.CategoryBits = SensorLayer;
            collider.MaskBits = DynamicLayer;
        });
        sensorCollider.SensorEntered += hit =>
        {
            if (hit.Body is not null) sensorEntries++;
        };

        var ramp = Instantiate("Segment ramp");
        ramp.Transform.LocalPosition = new Vector2(190f, 210f);
        ramp.AddComponent<PhysicsBody>().Type = B2BodyType.b2_staticBody;
        ramp.AddComponent<SegmentCollider>(collider =>
        {
            collider.Point1 = new Vector2(-130f, -35f);
            collider.Point2 = new Vector2(130f, 35f);
            collider.CategoryBits = EnvironmentLayer;
            collider.MaskBits = DynamicLayer;
        });

        CreateJointExamples();

        var packaged = Instantiate(assets.Get<PrefabAsset>("physics-tooling"), "Packaged physics collider");
        packaged.Transform.LocalPosition = new Vector2(0f, -230f);
        packaged.GetComponent<PhysicsBody>()!.CollisionEntered += _ => collisions++;

        for (var index = 0; index < 14; index++)
            SpawnBody();
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        primitives.FillRectangle(new Vector2(-450f, 272f), new Vector2(900f, 36f),
            Color.FromHTML("#4F6F52"), CoordinateSpace.World);
        primitives.FillRectangle(new Vector2(-130f, 60f), new Vector2(260f, 100f),
            new Color(0.2f, 0.8f, 0.7f, 0.18f), CoordinateSpace.World);
        primitives.DrawLine(new Vector2(60f, 175f), new Vector2(320f, 245f), Color.Orange,
            3f, CoordinateSpace.World);
        primitives.DrawLine(new Vector2(-430f, 0f), new Vector2(430f, 0f), Color.Yellow,
            2f, CoordinateSpace.World);
        if (rayHit is { } ray)
            primitives.FillCircle(ray.Point, 7f, Color.Red, CoordinateSpace.World);
        if (circleHit is { } circle)
            primitives.DrawCircle(circle.Point, 10f, Color.Cyan, 2f, space: CoordinateSpace.World);
        DrawText(spriteBatch, textRenderer,
            $"Collider components, layers, sensor events and joints | Space spawn | Collisions: {collisions}",
            new Vector2(24f, 70f), 15f);
        DrawText(spriteBatch, textRenderer,
            $"Ray: {(rayHit is null ? "miss" : "hit")} | circle cast: {(circleHit is null ? "miss" : "hit")} | overlap box: {overlapCount} | sensor entries: {sensorEntries}",
            new Vector2(24f, 94f), 14f, Color.LightGray);
    }

    protected override void UpdateExample(double deltaTime)
    {
        if (spawn.WasPressedThisFrame) SpawnBody();
        var filter = new PhysicsQueryFilter(ulong.MaxValue, DynamicLayer);
        rayHit = physics.RayCast(new Vector2(-430f, 0f), new Vector2(860f, 0f), filter);
        circleHit = physics.CircleCast(new Vector2(-430f, -140f), 20f, new Vector2(860f, 0f), filter);
        overlapCount = physics.OverlapBox(Vector2.Zero, new Vector2(300f, 220f), filter).Count;
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
        switch (index % 4)
        {
            case 0:
                gameObject.AddComponent<CircleCollider>(collider =>
                {
                    collider.Radius = 48f;
                    collider.Restitution = 0.55f;
                    ConfigureDynamic(collider);
                });
                break;
            case 1:
                gameObject.AddComponent<BoxCollider>(collider =>
                {
                    collider.Size = new Vector2(92f, 62f);
                    collider.Restitution = 0.3f;
                    ConfigureDynamic(collider);
                });
                break;
            case 2:
                gameObject.AddComponent<CapsuleCollider>(collider =>
                {
                    collider.Point1 = new Vector2(0f, -34f);
                    collider.Point2 = new Vector2(0f, 34f);
                    collider.Radius = 24f;
                    ConfigureDynamic(collider);
                });
                break;
            default:
                gameObject.AddComponent<PolygonCollider>(collider =>
                {
                    collider.Vertices =
                    [
                        new Vector2(-48f, 30f),
                        new Vector2(0f, -48f),
                        new Vector2(48f, 30f)
                    ];
                    ConfigureDynamic(collider);
                });
                break;
        }
        body.CollisionEntered += _ => collisions++;
    }

    private void CreateJointExamples()
    {
        var anchor = Instantiate("Distance anchor");
        anchor.Transform.LocalPosition = new Vector2(-280f, -170f);
        var anchorBody = anchor.AddComponent<PhysicsBody>();
        anchorBody.Type = B2BodyType.b2_staticBody;
        anchor.AddComponent<CircleCollider>(collider =>
        {
            collider.Radius = 10f;
            collider.CategoryBits = EnvironmentLayer;
            collider.MaskBits = DynamicLayer;
        });

        var distanceBob = Instantiate("Distance joint bob");
        distanceBob.Transform.LocalPosition = new Vector2(-280f, 10f);
        distanceBob.Transform.LocalScale = new Vector2(0.09f);
        distanceBob.AddComponent<SpriteRenderer>().Sprite = logo;
        distanceBob.AddComponent<PhysicsBody>();
        distanceBob.AddComponent<CircleCollider>(collider =>
        {
            collider.Radius = 38f;
            ConfigureDynamic(collider);
        });
        distanceBob.AddComponent<DistanceJoint>(joint =>
        {
            joint.ConnectedBody = anchorBody;
            joint.Length = 180f;
            joint.MaximumLength = 180f;
            joint.EnableSpring = true;
        });

        var hinge = Instantiate("Revolute anchor");
        hinge.Transform.LocalPosition = new Vector2(280f, -170f);
        var hingeBody = hinge.AddComponent<PhysicsBody>();
        hingeBody.Type = B2BodyType.b2_staticBody;
        hinge.AddComponent<CircleCollider>(collider =>
        {
            collider.Radius = 10f;
            collider.CategoryBits = EnvironmentLayer;
            collider.MaskBits = DynamicLayer;
        });

        var bar = Instantiate("Revolute joint bar");
        bar.Transform.LocalPosition = new Vector2(280f, -70f);
        bar.AddComponent<PhysicsBody>();
        bar.AddComponent<BoxCollider>(collider =>
        {
            collider.Size = new Vector2(36f, 200f);
            ConfigureDynamic(collider);
        });
        bar.AddComponent<RevoluteJoint>(joint =>
        {
            joint.ConnectedBody = hingeBody;
            joint.LocalAnchorA = new Vector2(0f, -100f);
            joint.EnableLimit = true;
            joint.LowerAngle = -0.9f;
            joint.UpperAngle = 0.9f;
        });
    }

    private static void ConfigureDynamic(PhysicsCollider collider)
    {
        collider.CategoryBits = DynamicLayer;
        collider.MaskBits = DynamicLayer | EnvironmentLayer | SensorLayer;
    }
}
