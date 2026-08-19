using Box2D.NET;
using System.Numerics;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace Axolotl2D.Physics;

/// <summary>A Box2D world owned by one scene DI scope.</summary>
public sealed class PhysicsWorld : IDisposable
{
    private readonly Dictionary<B2ShapeId, PhysicsBody> shapeOwners = [];
    private readonly HashSet<PhysicsBody> bodies = [];
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

    public PhysicsWorld()
    {
        var definition = b2DefaultWorldDef();
        definition.gravity = new B2Vec2(0f, 9.81f);
        WorldId = b2CreateWorld(definition);
    }

    public Vector2 ToPhysics(Vector2 worldPixels) => worldPixels / PixelsPerMeter;
    public Vector2 ToWorld(B2Vec2 physicsPosition) => new(physicsPosition.X * PixelsPerMeter, physicsPosition.Y * PixelsPerMeter);

    internal B2Vec2 ToPhysicsVector(Vector2 value) => new(value.X / PixelsPerMeter, value.Y / PixelsPerMeter);
    internal Vector2 ToWorldVector(B2Vec2 value) => new(value.X * PixelsPerMeter, value.Y * PixelsPerMeter);

    internal void Register(PhysicsBody body) => bodies.Add(body);

    internal void RegisterShape(B2ShapeId shapeId, PhysicsBody owner) => shapeOwners.Add(shapeId, owner);

    internal void Unregister(PhysicsBody body, IEnumerable<B2ShapeId> shapeIds)
    {
        bodies.Remove(body);
        foreach (var shapeId in shapeIds)
            shapeOwners.Remove(shapeId);
    }

    internal void Step(float fixedDeltaTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (SubStepCount <= 0)
            throw new InvalidOperationException("SubStepCount must be greater than zero.");

        b2World_Step(WorldId, fixedDeltaTime, SubStepCount);
        foreach (var body in bodies.ToArray())
            body.SyncTransform();
        DispatchContacts(b2World_GetContactEvents(WorldId));
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

    private void RaiseContact(B2ShapeId shapeA, B2ShapeId shapeB, Action<PhysicsContact>? worldEvent, bool entering)
    {
        if (!shapeOwners.TryGetValue(shapeA, out var bodyA) ||
            !shapeOwners.TryGetValue(shapeB, out var bodyB) ||
            ReferenceEquals(bodyA, bodyB))
            return;

        var contact = new PhysicsContact(bodyA, bodyB);
        worldEvent?.Invoke(contact);
        bodyA.RaiseContact(bodyB, entering);
        bodyB.RaiseContact(bodyA, entering);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        bodies.Clear();
        shapeOwners.Clear();
        b2DestroyWorld(WorldId);
        GC.SuppressFinalize(this);
    }
}

public readonly record struct PhysicsContact(PhysicsBody BodyA, PhysicsBody BodyB);
