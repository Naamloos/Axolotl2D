using Axolotl2D.GameObjects;
using Box2D.NET;
using System.Numerics;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Ids;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace Axolotl2D.Physics;

/// <summary>A configurable shape attached to a PhysicsBody on the same GameObject.</summary>
public abstract class PhysicsCollider(GameObject gameObject, PhysicsWorld world) : Component(gameObject)
{
    private float density = 1f;
    private float friction = 0.6f;
    private float restitution;
    private bool isSensor;
    private ulong categoryBits = 1;
    private ulong maskBits = ulong.MaxValue;
    private int groupIndex;
    private PhysicsBody? body;

    public B2ShapeId ShapeId { get; private set; } = b2_nullShapeId;
    public bool HasShape => B2_IS_NON_NULL(ShapeId);

    public float Density
    {
        get => density;
        set
        {
            ValidateNonNegative(value, nameof(Density));
            density = value;
            if (HasShape) b2Shape_SetDensity(ShapeId, value, true);
        }
    }

    public float Friction
    {
        get => friction;
        set
        {
            ValidateNonNegative(value, nameof(Friction));
            friction = value;
            if (HasShape) b2Shape_SetFriction(ShapeId, value);
        }
    }

    public float Restitution
    {
        get => restitution;
        set
        {
            ValidateNonNegative(value, nameof(Restitution));
            restitution = value;
            if (HasShape) b2Shape_SetRestitution(ShapeId, value);
        }
    }

    public bool IsSensor
    {
        get => isSensor;
        set
        {
            EnsureShapeConfigurable();
            isSensor = value;
        }
    }

    public ulong CategoryBits
    {
        get => categoryBits;
        set { categoryBits = value; UpdateFilter(); }
    }

    public ulong MaskBits
    {
        get => maskBits;
        set { maskBits = value; UpdateFilter(); }
    }

    public int GroupIndex
    {
        get => groupIndex;
        set { groupIndex = value; UpdateFilter(); }
    }

    public event Action<PhysicsShapeHit>? SensorEntered;
    public event Action<PhysicsShapeHit>? SensorExited;

    public override void Start()
    {
        body = GameObject.GetComponent<PhysicsBody>()
            ?? throw new InvalidOperationException($"{GetType().Name} requires PhysicsBody on the same GameObject.");
        if (body.HasBody && IsActiveAndEnabled)
            body.AttachCollider(this);
    }

    public override void OnEnable()
    {
        if (GameObject.HasStarted && body?.HasBody == true)
            body.AttachCollider(this);
    }

    public override void OnDisable()
    {
        if (HasShape)
            body?.DetachCollider(this);
    }

    public override void OnDestroy()
    {
        if (HasShape)
            body?.DetachCollider(this);
    }

    internal B2ShapeId Create(PhysicsBody owner)
    {
        if (HasShape) return ShapeId;
        body = owner;
        var definition = b2DefaultShapeDef();
        definition.density = density;
        definition.material.friction = friction;
        definition.material.restitution = restitution;
        definition.filter = Filter();
        definition.isSensor = isSensor;
        definition.enableSensorEvents = isSensor;
        definition.enableContactEvents = !isSensor;
        ShapeId = CreateShape(owner.BodyId, definition, world);
        return ShapeId;
    }

    internal void ReleaseShape()
    {
        ShapeId = b2_nullShapeId;
    }

    internal void RaiseSensor(PhysicsShapeHit visitor, bool entering)
    {
        if (entering) SensorEntered?.Invoke(visitor);
        else SensorExited?.Invoke(visitor);
    }

    protected abstract B2ShapeId CreateShape(B2BodyId bodyId, B2ShapeDef definition, PhysicsWorld world);

    protected void EnsureShapeConfigurable()
    {
        if (HasShape)
            throw new InvalidOperationException("Change collider geometry and sensor state before the shape is created.");
    }

    private B2Filter Filter() => new()
    {
        categoryBits = categoryBits,
        maskBits = maskBits,
        groupIndex = groupIndex
    };

    private void UpdateFilter()
    {
        if (!HasShape) return;
        var filter = Filter();
        b2Shape_SetFilter(ShapeId, filter);
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(name);
    }
}

public sealed class BoxCollider(GameObject gameObject, PhysicsWorld world) : PhysicsCollider(gameObject, world)
{
    private Vector2 size = Vector2.One;
    private Vector2 offset;

    public Vector2 Size
    {
        get => size;
        set
        {
            EnsureShapeConfigurable();
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X <= 0f || value.Y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(Size));
            size = value;
        }
    }

    public Vector2 Offset
    {
        get => offset;
        set
        {
            EnsureShapeConfigurable();
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(Offset));
            offset = value;
        }
    }

    protected override B2ShapeId CreateShape(B2BodyId bodyId, B2ShapeDef definition, PhysicsWorld world)
    {
        var halfSize = world.ToPhysics(size) / 2f;
        var center = world.ToPhysics(offset);
        var box = b2MakeOffsetBox(halfSize.X, halfSize.Y, new B2Vec2(center.X, center.Y), b2MakeRot(0f));
        return b2CreatePolygonShape(bodyId, definition, box);
    }
}

public sealed class CircleCollider(GameObject gameObject, PhysicsWorld world) : PhysicsCollider(gameObject, world)
{
    private float radius = 0.5f;
    private Vector2 offset;

    public float Radius
    {
        get => radius;
        set
        {
            EnsureShapeConfigurable();
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(Radius));
            radius = value;
        }
    }

    public Vector2 Offset
    {
        get => offset;
        set
        {
            EnsureShapeConfigurable();
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(Offset));
            offset = value;
        }
    }

    protected override B2ShapeId CreateShape(B2BodyId bodyId, B2ShapeDef definition, PhysicsWorld world)
    {
        var center = world.ToPhysics(offset);
        var circle = new B2Circle(new B2Vec2(center.X, center.Y), radius / world.PixelsPerMeter);
        return b2CreateCircleShape(bodyId, definition, circle);
    }
}
