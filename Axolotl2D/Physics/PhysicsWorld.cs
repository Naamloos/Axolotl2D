using Box2D.NET;
using System.Numerics;
using static Box2D.NET.B2Distances;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace Axolotl2D.Physics;

/// <summary>A Box2D world owned by one scene DI scope.</summary>
public sealed class PhysicsWorld : IDisposable
{
    private readonly Dictionary<B2ShapeId, ShapeOwner> shapeOwners = [];
    private readonly HashSet<PhysicsBody> bodies = [];
    private PhysicsBody[] bodySnapshot = [];
    private float pixelsPerMeter = 100f;
    private bool disposed;

    public B2WorldId WorldId { get; }
    public int SubStepCount { get; set; } = 4;
    public IReadOnlyCollection<PhysicsBody> Bodies => bodies;

    /// <summary>Returns Box2D's current world counters.</summary>
    public B2Counters Counters => b2World_GetCounters(WorldId);

    public float PixelsPerMeter
    {
        get => pixelsPerMeter;
        set
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "PixelsPerMeter must be finite and greater than zero.");
            if (bodies.Count > 0)
                throw new InvalidOperationException("Set PixelsPerMeter before creating physics bodies.");
            pixelsPerMeter = value;
        }
    }

    public Vector2 Gravity
    {
        get
        {
            var value = b2World_GetGravity(WorldId);
            return new Vector2(value.X, value.Y);
        }
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(value), "Gravity must contain finite values.");
            b2World_SetGravity(WorldId, new B2Vec2(value.X, value.Y));
        }
    }

    public event Action<PhysicsContact>? ContactBegan;
    public event Action<PhysicsContact>? ContactEnded;
    public event Action<PhysicsSensorContact>? SensorBegan;
    public event Action<PhysicsSensorContact>? SensorEnded;

    public PhysicsWorld()
    {
        var definition = b2DefaultWorldDef();
        definition.gravity = new B2Vec2(0f, 9.81f);
        WorldId = b2CreateWorld(definition);
    }

    public Vector2 ToPhysics(Vector2 worldPixels) => worldPixels / PixelsPerMeter;
    public Vector2 ToWorld(B2Vec2 physicsPosition) => new(physicsPosition.X * PixelsPerMeter, physicsPosition.Y * PixelsPerMeter);

    /// <summary>Returns the closest shape hit by a ray in pixel-based world coordinates.</summary>
    public PhysicsCastHit? RayCast(Vector2 origin, Vector2 translation, PhysicsQueryFilter? filter = null)
    {
        EnsureQuery(origin, translation);
        var query = ToQueryFilter(filter);
        var result = b2World_CastRayClosest(WorldId, ToPhysicsVector(origin), ToPhysicsVector(translation), query);
        return result.hit
            ? CastHit(result.shapeId, result.point, result.normal, result.fraction)
            : null;
    }

    /// <summary>Sweeps a circle through the world and returns the closest shape hit.</summary>
    public PhysicsCastHit? CircleCast(Vector2 origin, float radius, Vector2 translation,
        PhysicsQueryFilter? filter = null)
    {
        EnsureQuery(origin, translation);
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var proxy = b2MakeProxy(ToPhysicsVector(origin), 1, radius / PixelsPerMeter);
        var query = ToQueryFilter(filter);
        var context = new CastContext(this);
        b2World_CastShape(WorldId, ref proxy, ToPhysicsVector(translation), query, CollectCast, context);
        return context.Hit;
    }

    /// <summary>Returns shapes whose bounds overlap an axis-aligned pixel-based box.</summary>
    public IReadOnlyList<PhysicsShapeHit> OverlapBox(Vector2 center, Vector2 size,
        PhysicsQueryFilter? filter = null)
    {
        EnsureQuery(center, size);
        if (size.X <= 0f || size.Y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size));
        var half = ToPhysics(size) / 2f;
        var physicsCenter = ToPhysics(center);
        var bounds = new B2AABB(
            new B2Vec2(physicsCenter.X - half.X, physicsCenter.Y - half.Y),
            new B2Vec2(physicsCenter.X + half.X, physicsCenter.Y + half.Y));
        var query = ToQueryFilter(filter);
        var context = new OverlapContext(this);
        b2World_OverlapAABB(WorldId, bounds, query, CollectOverlap, context);
        return context.Hits;
    }

    internal B2Vec2 ToPhysicsVector(Vector2 value) => new(value.X / PixelsPerMeter, value.Y / PixelsPerMeter);
    internal Vector2 ToWorldVector(B2Vec2 value) => new(value.X * PixelsPerMeter, value.Y * PixelsPerMeter);

    internal void Register(PhysicsBody body)
    {
        if (bodies.Add(body)) bodySnapshot = bodies.ToArray();
    }

    internal void RegisterShape(B2ShapeId shapeId, PhysicsBody body, PhysicsCollider? collider) =>
        shapeOwners.Add(shapeId, new(body, collider));

    internal void UnregisterShape(B2ShapeId shapeId) => shapeOwners.Remove(shapeId);

    internal void Unregister(PhysicsBody body, IEnumerable<B2ShapeId> shapeIds)
    {
        if (bodies.Remove(body)) bodySnapshot = bodies.ToArray();
        foreach (var shapeId in shapeIds)
            shapeOwners.Remove(shapeId);
    }

    internal void Step(float fixedDeltaTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (SubStepCount <= 0)
            throw new InvalidOperationException("SubStepCount must be greater than zero.");

        b2World_Step(WorldId, fixedDeltaTime, SubStepCount);
        var activeBodies = bodySnapshot;
        foreach (var body in activeBodies)
            body.SyncTransform();
        DispatchContacts(b2World_GetContactEvents(WorldId));
        DispatchSensors(b2World_GetSensorEvents(WorldId));
    }

    private void DispatchContacts(B2ContactEvents events)
    {
        for (var index = 0; index < events.beginCount; index++)
        {
            var contact = events.beginEvents[index];
            RaiseContact(contact.shapeIdA, contact.shapeIdB, ContactBegan, entering: true);
        }

        for (var index = 0; index < events.endCount; index++)
        {
            var contact = events.endEvents[index];
            RaiseContact(contact.shapeIdA, contact.shapeIdB, ContactEnded, entering: false);
        }
    }

    private void DispatchSensors(B2SensorEvents events)
    {
        for (var index = 0; index < events.beginCount; index++)
        {
            var contact = events.beginEvents[index];
            RaiseSensor(contact.sensorShapeId, contact.visitorShapeId, SensorBegan, entering: true);
        }

        for (var index = 0; index < events.endCount; index++)
        {
            var contact = events.endEvents[index];
            RaiseSensor(contact.sensorShapeId, contact.visitorShapeId, SensorEnded, entering: false);
        }
    }

    private void RaiseContact(B2ShapeId shapeA, B2ShapeId shapeB, Action<PhysicsContact>? worldEvent, bool entering)
    {
        if (!shapeOwners.TryGetValue(shapeA, out var bodyA) ||
            !shapeOwners.TryGetValue(shapeB, out var bodyB) ||
            ReferenceEquals(bodyA.Body, bodyB.Body))
            return;

        var contact = new PhysicsContact(bodyA.Body, bodyB.Body);
        worldEvent?.Invoke(contact);
        bodyA.Body.RaiseContact(bodyB.Body, entering);
        bodyB.Body.RaiseContact(bodyA.Body, entering);
    }

    private void RaiseSensor(B2ShapeId sensorId, B2ShapeId visitorId,
        Action<PhysicsSensorContact>? worldEvent, bool entering)
    {
        if (!shapeOwners.TryGetValue(sensorId, out var sensorOwner) || sensorOwner.Collider is not { } sensor)
            return;
        var contact = new PhysicsSensorContact(sensor, ShapeHit(visitorId));
        worldEvent?.Invoke(contact);
        sensor.RaiseSensor(contact.Visitor, entering);
    }

    private PhysicsShapeHit ShapeHit(B2ShapeId shapeId) =>
        shapeOwners.TryGetValue(shapeId, out var owner)
            ? new(owner.Body, owner.Collider, shapeId)
            : new(null, null, shapeId);

    private PhysicsCastHit CastHit(B2ShapeId shapeId, B2Vec2 point, B2Vec2 normal, float fraction)
    {
        var shape = ShapeHit(shapeId);
        return new(shape.Body, shape.Collider, shapeId, ToWorld(point), new Vector2(normal.X, normal.Y), fraction);
    }

    private static B2QueryFilter ToQueryFilter(PhysicsQueryFilter? filter)
    {
        var value = filter ?? PhysicsQueryFilter.All;
        return new B2QueryFilter { categoryBits = value.CategoryBits, maskBits = value.MaskBits };
    }

    private void EnsureQuery(Vector2 first, Vector2 second)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!float.IsFinite(first.X) || !float.IsFinite(first.Y) ||
            !float.IsFinite(second.X) || !float.IsFinite(second.Y))
            throw new ArgumentOutOfRangeException(nameof(first), "Physics query vectors must be finite.");
    }

    private static bool CollectOverlap(B2ShapeId shapeId, object value)
    {
        var context = (OverlapContext)value;
        context.Hits.Add(context.World.ShapeHit(shapeId));
        return true;
    }

    private static float CollectCast(B2ShapeId shapeId, B2Vec2 point, B2Vec2 normal, float fraction, object value)
    {
        var context = (CastContext)value;
        context.Hit = context.World.CastHit(shapeId, point, normal, fraction);
        return fraction;
    }

    private sealed record ShapeOwner(PhysicsBody Body, PhysicsCollider? Collider);
    private sealed record OverlapContext(PhysicsWorld World)
    {
        public List<PhysicsShapeHit> Hits { get; } = [];
    }
    private sealed record CastContext(PhysicsWorld World)
    {
        public PhysicsCastHit? Hit { get; set; }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        bodies.Clear();
        bodySnapshot = [];
        shapeOwners.Clear();
        b2DestroyWorld(WorldId);
        GC.SuppressFinalize(this);
    }
}

public readonly record struct PhysicsContact(PhysicsBody BodyA, PhysicsBody BodyB);
public readonly record struct PhysicsQueryFilter(ulong CategoryBits, ulong MaskBits)
{
    public static PhysicsQueryFilter All => new(ulong.MaxValue, ulong.MaxValue);
}
public readonly record struct PhysicsShapeHit(PhysicsBody? Body, PhysicsCollider? Collider, B2ShapeId ShapeId);
public readonly record struct PhysicsCastHit(PhysicsBody? Body, PhysicsCollider? Collider, B2ShapeId ShapeId,
    Vector2 Point, Vector2 Normal, float Fraction);
public readonly record struct PhysicsSensorContact(PhysicsCollider Sensor, PhysicsShapeHit Visitor);
