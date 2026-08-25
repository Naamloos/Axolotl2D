# Box2D Physics

Axolotl2D uses [Box2D.NET 3.1.654](https://www.nuget.org/packages/Box2D.NET/3.1.654), a pure C# port that follows the Box2D 3 API. The framework creates one `PhysicsWorld` per scene scope and advances it during the scene fixed-update phase.

## Configure the scene world

Inject `PhysicsWorld` into the scene constructor or `Load`:

```csharp
public sealed class GameplayScene : BaseScene
{
    public GameplayScene(PhysicsWorld physics)
    {
        physics.PixelsPerMeter = 100f;
        physics.Gravity = new Vector2(0, 9.81f);
        physics.SubStepCount = 4;
    }
}
```

Axolotl2D world coordinates use positive Y downward, so the default physics gravity is `(0, 9.81)`. Box2D measures simulation values in meters. `PixelsPerMeter` converts render-world pixels to physics meters and must be set before any body starts.

The framework defaults to 100 pixels per meter and four solver substeps.

## Create a dynamic body

Add `PhysicsBody`, configure it, and add at least one shape before `Start`:

```csharp
var crate = Instantiate("Crate");
crate.Transform.LocalPosition = new Vector2(200, 40);
crate.AddComponent<SpriteRenderer>().Sprite = crateSprite;

var body = crate.AddComponent<PhysicsBody>();
body.Type = B2BodyType.b2_dynamicBody;
body.LinearDamping = 0.1f;
body.AddBox(
    size: new Vector2(64, 64),
    density: 1f,
    friction: 0.6f,
    restitution: 0.1f);
```

`AddCircle` creates a centered circular shape:

```csharp
body.AddCircle(radius: 24f, restitution: 0.5f);
```

Box and circle dimensions use render-world pixels. Density, friction, restitution, forces, and impulses use Box2D units.

Capsules, convex polygons, and two-sided segments use local pixel coordinates:

```csharp
body.AddCapsule(new Vector2(0, -24), new Vector2(0, 24), radius: 16f);
body.AddPolygon([
    new Vector2(-32, 24),
    new Vector2(0, -28),
    new Vector2(32, 24)
]);
body.AddSegment(new Vector2(-100, 0), new Vector2(100, 0));
```

Polygons accept three to eight finite points. Box2D computes their convex hull; inputs that cannot produce a valid hull are rejected. Segment shapes have no density and are normally attached to static geometry.

`AddBox`, `AddCircle`, `AddCapsule`, `AddPolygon`, and `AddSegment` support compact body setup. Use collider components when shapes need sensors, offsets, collision layers, runtime material changes, prefab component data, or individual enable/disable behavior:

```csharp
var body = crate.AddComponent<PhysicsBody>();
var collider = crate.AddComponent<BoxCollider>();
collider.Size = new Vector2(64, 64);
collider.Offset = new Vector2(0, 4);
collider.Density = 1f;
collider.Friction = 0.6f;
collider.Restitution = 0.1f;
```

`BoxCollider`, `CircleCollider`, `CapsuleCollider`, `PolygonCollider`, and `SegmentCollider` require a `PhysicsBody` on the same GameObject. They attach regardless of component declaration order, participate in normal component enable/disable and destruction, and expose their `B2ShapeId` through `ShapeId`.

## Create static geometry

```csharp
var floor = Instantiate("Floor");
floor.Transform.LocalPosition = new Vector2(0, 360);

var floorBody = floor.AddComponent<PhysicsBody>();
floorBody.Type = B2BodyType.b2_staticBody;
floorBody.AddBox(new Vector2(1200, 40));
```

Static bodies need no renderer. Add one when the geometry should be visible.

## Control a body

Apply force during `FixedUpdate`:

```csharp
public sealed class CharacterMotor(GameObject gameObject, InputActionMap input)
    : Component(gameObject)
{
    private PhysicsBody body = null!;

    public override void Start() =>
        body = GameObject.GetComponent<PhysicsBody>()
            ?? throw new InvalidOperationException("CharacterMotor requires PhysicsBody.");

    public override void FixedUpdate(double fixedDeltaTime)
    {
        var move = input.Get("Move").Scalar;
        body.ApplyForce(new Vector2(move * 20f, 0));
    }
}
```

`ApplyLinearImpulse` changes velocity at once. `LinearVelocity` converts between pixels per second and Box2D meters per second. `Teleport` accepts render-world pixels and radians.

The physics world updates GameObject position and rotation after each step. Parent transforms remain supported because `PhysicsBody` converts the Box2D world pose into local transform values.

## Receive collisions

```csharp
body.CollisionEntered += other =>
    logger.LogInformation("{Self} hit {Other}", body.GameObject.Name, other.GameObject.Name);

body.CollisionExited += other =>
    logger.LogInformation("{Self} left {Other}", body.GameObject.Name, other.GameObject.Name);
```

`PhysicsWorld.ContactBegan` and `ContactEnded` provide scene-wide `PhysicsContact` events. Framework shapes enable Box2D contact events when created.

## Filter collision layers

Collider filters use Box2D's 64-bit category and mask fields:

```csharp
const ulong Player = 1ul << 0;
const ulong Enemy = 1ul << 1;
const ulong World = 1ul << 2;

collider.CategoryBits = Player;
collider.MaskBits = Enemy | World;
collider.GroupIndex = 0;
```

Category, mask, material, and group values can change after the shape starts. `IsSensor` and geometry must be configured before shape creation.

## Create sensors

Sensors report overlap without producing a collision response:

```csharp
var trigger = area.AddComponent<BoxCollider>();
trigger.Size = new Vector2(200, 80);
trigger.IsSensor = true;
trigger.CategoryBits = TriggerLayer;
trigger.MaskBits = PlayerLayer;

trigger.SensorEntered += hit =>
    logger.LogInformation("Entered by {Body}", hit.Body?.GameObject.Name);
trigger.SensorExited += hit =>
    logger.LogInformation("Exited by {Body}", hit.Body?.GameObject.Name);
```

`PhysicsWorld.SensorBegan` and `SensorEnded` expose scene-wide `PhysicsSensorContact` values. Sensor events are separate from `CollisionEntered` and `CollisionExited`.

## Query and cast

Queries accept pixel-based world coordinates and an optional `PhysicsQueryFilter`:

```csharp
var filter = new PhysicsQueryFilter(
    CategoryBits: ulong.MaxValue,
    MaskBits: PlayerLayer | EnemyLayer);

PhysicsCastHit? ray = physics.RayCast(origin, translation, filter);
PhysicsCastHit? sweep = physics.CircleCast(origin, radius: 12f, translation, filter);
IReadOnlyList<PhysicsShapeHit> nearby =
    physics.OverlapBox(center, new Vector2(200, 120), filter);
```

`RayCast` and `CircleCast` return the closest hit with world point, normal, and zero-to-one fraction. `OverlapBox` is axis-aligned and returns every overlapping shape. A hit includes `ShapeId`, plus its framework `PhysicsBody` and `PhysicsCollider` when registered. Body and collider are nullable because advanced projects can create raw Box2D shapes through `WorldId`.

## Connect bodies with joints

Attach a joint to the first body's GameObject and assign the second body. Joint creation waits until both bodies have started, so references resolved by prefabs and runtime object construction are safe:

```csharp
var joint = bob.AddComponent<DistanceJoint>();
joint.ConnectedBody = anchorBody;
joint.LocalAnchorA = Vector2.Zero;
joint.LocalAnchorB = Vector2.Zero;
joint.Length = 180f;
joint.MaximumLength = 180f;
joint.EnableSpring = true;
joint.Hertz = 4f;
joint.DampingRatio = 0.7f;
```

`RevoluteJoint` provides body-local anchors, angular limits, spring tuning, and motor settings. Both components expose `JointId` for direct Box2D tuning after creation. Distance lengths, local anchors, and linear motor speed use pixel-based units; forces, torques, hertz, damping ratios, angles, and angular speed use Box2D units.

## Use the Box2D.NET API

`PhysicsWorld.WorldId` exposes the scoped `B2WorldId`. Use Box2D.NET static APIs for advanced shapes, additional joint types, and specialized queries:

```csharp
var worldId = physics.WorldId;
var events = B2Worlds.b2World_GetContactEvents(worldId);
```

The framework destroys component bodies before it disposes the scoped world. Do not retain body, shape, or world IDs after a scene transition.

The integration wraps body motion, box, circle, capsule, convex-polygon and segment colliders, filters, sensors, ray and circle casts, box overlaps, distance and revolute joints, transform synchronization, contact events, and host-enabled debug drawing. Chain shapes and other Box2D joint types remain available through raw IDs. See the `PHYSICS` example screen, [Debug Overlay and Runtime Inspection](debug-overlay.md), and the [Box2D simulation guide](https://github.com/erincatto/box2d/blob/main/docs/simulation.md).
