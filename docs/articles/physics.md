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

`PhysicsWorld.ContactBegan` and `ContactEnded` provide scene-wide `PhysicsContact` events. Box and circle shapes enable Box2D contact events when created.

## Use the Box2D.NET API

`PhysicsWorld.WorldId` exposes the scoped `B2WorldId`. Use Box2D.NET static APIs for joints, casts, advanced shapes, filters, sensors, and queries:

```csharp
var worldId = physics.WorldId;
var events = B2Worlds.b2World_GetContactEvents(worldId);
```

The framework destroys component bodies before it disposes the scoped world. Do not retain body, shape, or world IDs after a scene transition.

The integration wraps boxes, circles, forces, impulses, velocity, transform synchronization, contact begin/end events, and host-enabled debug drawing with collision bounds. Joint components, query helpers, filters, and sensor components remain direct Box2D.NET work for now. See [Debug Overlay and Runtime Inspection](debug-overlay.md) for visualization and the [Box2D simulation guide](https://github.com/erincatto/box2d/blob/main/docs/simulation.md) for the underlying model.
