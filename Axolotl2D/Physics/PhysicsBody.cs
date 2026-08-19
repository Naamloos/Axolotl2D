using Axolotl2D.GameObjects;
using Box2D.NET;
using System.Numerics;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Ids;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace Axolotl2D.Physics;

/// <summary>Creates a Box2D body and keeps its GameObject transform synchronized.</summary>
public sealed class PhysicsBody(GameObject gameObject, PhysicsWorld world) : Component(gameObject)
{
    private readonly List<ShapeDefinition> shapes = [];
    private readonly List<B2ShapeId> shapeIds = [];

    public B2BodyType Type { get; set; } = B2BodyType.b2_dynamicBody;
    public float LinearDamping { get; set; }
    public float AngularDamping { get; set; }
    public float GravityScale { get; set; } = 1f;
    public bool IsBullet { get; set; }
    public B2BodyId BodyId { get; private set; } = b2_nullBodyId;
    public bool HasBody => B2_IS_NON_NULL(BodyId);

    public Vector2 LinearVelocity
    {
        get
        {
            EnsureBody();
            return world.ToWorldVector(b2Body_GetLinearVelocity(BodyId));
        }
        set
        {
            EnsureBody();
            b2Body_SetLinearVelocity(BodyId, world.ToPhysicsVector(value));
        }
    }

    public event Action<PhysicsBody>? CollisionEntered;
    public event Action<PhysicsBody>? CollisionExited;

    public void AddBox(Vector2 size, float density = 1f, float friction = 0.6f, float restitution = 0f)
    {
        EnsureConfigurable();
        if (size.X <= 0f || size.Y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Box size must be positive.");
        ValidateMaterial(density, friction, restitution);
        shapes.Add(new BoxDefinition(size, density, friction, restitution));
    }

    public void AddCircle(float radius, float density = 1f, float friction = 0.6f, float restitution = 0f)
    {
        EnsureConfigurable();
        if (radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        ValidateMaterial(density, friction, restitution);
        shapes.Add(new CircleDefinition(radius, density, friction, restitution));
    }

    public override void Start()
    {
        if (shapes.Count == 0)
            throw new InvalidOperationException("PhysicsBody requires at least one box or circle shape before Start.");

        var definition = b2DefaultBodyDef();
        definition.type = Type;
        var position = world.ToPhysics(Transform.Position);
        definition.position = new B2Vec2(position.X, position.Y);
        definition.rotation = b2MakeRot(Transform.Rotation);
        definition.linearDamping = LinearDamping;
        definition.angularDamping = AngularDamping;
        definition.gravityScale = GravityScale;
        definition.isBullet = IsBullet;
        BodyId = b2CreateBody(world.WorldId, definition);
        world.Register(this);

        foreach (var shape in shapes)
        {
            var shapeId = shape.Create(BodyId, world);
            shapeIds.Add(shapeId);
            world.RegisterShape(shapeId, this);
        }
    }

    public override void OnEnable()
    {
        if (HasBody)
            b2Body_Enable(BodyId);
    }

    public override void OnDisable()
    {
        if (HasBody)
            b2Body_Disable(BodyId);
    }

    public void ApplyForce(Vector2 force)
    {
        EnsureBody();
        b2Body_ApplyForceToCenter(BodyId, new B2Vec2(force.X, force.Y), true);
    }

    public void ApplyLinearImpulse(Vector2 impulse)
    {
        EnsureBody();
        b2Body_ApplyLinearImpulseToCenter(BodyId, new B2Vec2(impulse.X, impulse.Y), true);
    }

    public void Teleport(Vector2 worldPosition, float rotation)
    {
        EnsureBody();
        var position = world.ToPhysics(worldPosition);
        b2Body_SetTransform(BodyId, new B2Vec2(position.X, position.Y), b2MakeRot(rotation));
        SyncTransform();
    }

    internal void SyncTransform()
    {
        if (!HasBody)
            return;
        var position = world.ToWorld(b2Body_GetPosition(BodyId));
        Transform.LocalPosition = Transform.Parent?.InverseTransformPoint(position) ?? position;
        Transform.LocalRotation = b2Rot_GetAngle(b2Body_GetRotation(BodyId)) - (Transform.Parent?.Rotation ?? 0f);
    }

    internal void RaiseContact(PhysicsBody other, bool entering)
    {
        if (entering)
            CollisionEntered?.Invoke(other);
        else
            CollisionExited?.Invoke(other);
    }

    public override void OnDestroy()
    {
        if (!HasBody)
            return;
        world.Unregister(this, shapeIds);
        b2DestroyBody(BodyId);
        BodyId = b2_nullBodyId;
        shapeIds.Clear();
    }

    private void EnsureConfigurable()
    {
        if (HasBody)
            throw new InvalidOperationException("Add physics shapes before the component starts.");
    }

    private void EnsureBody()
    {
        if (!HasBody)
            throw new InvalidOperationException("The physics body has not started.");
    }

    private static void ValidateMaterial(float density, float friction, float restitution)
    {
        if (!float.IsFinite(density) || density < 0f)
            throw new ArgumentOutOfRangeException(nameof(density));
        if (!float.IsFinite(friction) || friction < 0f)
            throw new ArgumentOutOfRangeException(nameof(friction));
        if (!float.IsFinite(restitution) || restitution < 0f)
            throw new ArgumentOutOfRangeException(nameof(restitution));
    }

    private abstract record ShapeDefinition(float Density, float Friction, float Restitution)
    {
        public abstract B2ShapeId Create(B2BodyId bodyId, PhysicsWorld world);

        protected B2ShapeDef CreateDefinition()
        {
            var definition = b2DefaultShapeDef();
            definition.density = Density;
            definition.material.friction = Friction;
            definition.material.restitution = Restitution;
            definition.enableContactEvents = true;
            return definition;
        }
    }

    private sealed record BoxDefinition(Vector2 Size, float Density, float Friction, float Restitution)
        : ShapeDefinition(Density, Friction, Restitution)
    {
        public override B2ShapeId Create(B2BodyId bodyId, PhysicsWorld world)
        {
            var size = world.ToPhysics(Size) / 2f;
            var box = b2MakeBox(size.X, size.Y);
            return b2CreatePolygonShape(bodyId, CreateDefinition(), box);
        }
    }

    private sealed record CircleDefinition(float Radius, float Density, float Friction, float Restitution)
        : ShapeDefinition(Density, Friction, Restitution)
    {
        public override B2ShapeId Create(B2BodyId bodyId, PhysicsWorld world)
        {
            var circle = new B2Circle(new B2Vec2(0f, 0f), Radius / world.PixelsPerMeter);
            return b2CreateCircleShape(bodyId, CreateDefinition(), circle);
        }
    }
}
