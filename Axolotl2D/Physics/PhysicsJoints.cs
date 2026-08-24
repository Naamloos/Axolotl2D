using Axolotl2D.GameObjects;
using Box2D.NET;
using System.Numerics;
using static Box2D.NET.B2Ids;
using static Box2D.NET.B2Joints;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Worlds;

namespace Axolotl2D.Physics;

/// <summary>A scene-owned Box2D joint between a local PhysicsBody and a connected body.</summary>
public abstract class PhysicsJoint(GameObject gameObject, PhysicsWorld world) : Component(gameObject)
{
    private PhysicsBody? connectedBody;
    private bool collideConnected;

    protected PhysicsBody Body { get; private set; } = null!;
    public B2JointId JointId { get; private set; } = b2_nullJointId;
    public bool HasJoint => B2_IS_NON_NULL(JointId) && b2Joint_IsValid(JointId);

    public PhysicsBody? ConnectedBody
    {
        get => connectedBody;
        set { EnsureConfigurable(); connectedBody = value; }
    }

    public bool CollideConnected
    {
        get => collideConnected;
        set { EnsureConfigurable(); collideConnected = value; }
    }

    public override void Start()
    {
        Body = GameObject.GetComponent<PhysicsBody>()
            ?? throw new InvalidOperationException($"{GetType().Name} requires PhysicsBody on the same GameObject.");
        if (connectedBody is null)
            throw new InvalidOperationException($"{GetType().Name} requires ConnectedBody before Start.");
        if (ReferenceEquals(Body, connectedBody))
            throw new InvalidOperationException("A physics joint cannot connect a body to itself.");
        if (!ReferenceEquals(Body.World, world) || !ReferenceEquals(connectedBody.World, world))
            throw new InvalidOperationException("Both joint bodies must belong to the same PhysicsWorld.");
        Validate();
    }

    public override void FixedUpdate(double fixedDeltaTime)
    {
        if (!HasJoint && Body.HasBody && ConnectedBody?.HasBody == true)
            JointId = CreateJoint();
    }

    public override void OnDisable() => DestroyJoint();
    public override void OnDestroy() => DestroyJoint();

    protected abstract B2JointId CreateJoint();
    protected virtual void Validate() { }

    protected void Configure(ref B2JointDef definition, Vector2 localAnchorA, Vector2 localAnchorB)
    {
        definition.bodyIdA = Body.BodyId;
        definition.bodyIdB = ConnectedBody!.BodyId;
        definition.localFrameA = LocalFrame(localAnchorA);
        definition.localFrameB = LocalFrame(localAnchorB);
        definition.collideConnected = collideConnected;
    }

    protected void EnsureConfigurable()
    {
        if (B2_IS_NON_NULL(JointId))
            throw new InvalidOperationException("Change joint configuration before the joint is created.");
    }

    protected static void ValidateFinite(float value, string name)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(name);
    }

    private B2Transform LocalFrame(Vector2 anchor) => new()
        {
            p = world.ToPhysicsVector(anchor),
            q = b2MakeRot(0f)
        };

    private void DestroyJoint()
    {
        if (HasJoint) b2DestroyJoint(JointId, true);
        JointId = b2_nullJointId;
    }
}

public sealed class DistanceJoint(GameObject gameObject, PhysicsWorld world) : PhysicsJoint(gameObject, world)
{
    public Vector2 LocalAnchorA { get; set; }
    public Vector2 LocalAnchorB { get; set; }
    public float Length { get; set; } = 100f;
    public bool EnableSpring { get; set; }
    public float Hertz { get; set; } = 4f;
    public float DampingRatio { get; set; } = 0.7f;
    public bool EnableLimit { get; set; }
    public float MinimumLength { get; set; }
    public float MaximumLength { get; set; } = 100f;
    public bool EnableMotor { get; set; }
    public float MaximumMotorForce { get; set; }
    public float MotorSpeed { get; set; }

    protected override B2JointId CreateJoint()
    {
        var definition = b2DefaultDistanceJointDef();
        Configure(ref definition.@base, LocalAnchorA, LocalAnchorB);
        definition.length = Length / world.PixelsPerMeter;
        definition.enableSpring = EnableSpring;
        definition.hertz = Hertz;
        definition.dampingRatio = DampingRatio;
        definition.enableLimit = EnableLimit;
        definition.minLength = MinimumLength / world.PixelsPerMeter;
        definition.maxLength = MaximumLength / world.PixelsPerMeter;
        definition.enableMotor = EnableMotor;
        definition.maxMotorForce = MaximumMotorForce;
        definition.motorSpeed = MotorSpeed / world.PixelsPerMeter;
        return b2CreateDistanceJoint(world.WorldId, definition);
    }

    protected override void Validate()
    {
        ValidateVector(LocalAnchorA, nameof(LocalAnchorA));
        ValidateVector(LocalAnchorB, nameof(LocalAnchorB));
        ValidateFinite(Length, nameof(Length));
        ValidateFinite(Hertz, nameof(Hertz));
        ValidateFinite(DampingRatio, nameof(DampingRatio));
        ValidateFinite(MinimumLength, nameof(MinimumLength));
        ValidateFinite(MaximumLength, nameof(MaximumLength));
        ValidateFinite(MaximumMotorForce, nameof(MaximumMotorForce));
        ValidateFinite(MotorSpeed, nameof(MotorSpeed));
        if (Length < 0f || Hertz < 0f || DampingRatio < 0f || MinimumLength < 0f ||
            MaximumLength < MinimumLength || MaximumMotorForce < 0f)
            throw new ArgumentOutOfRangeException(nameof(Length), "Distance joint lengths and tuning values must be non-negative and ordered.");
    }

    private static void ValidateVector(Vector2 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(name);
    }
}

public sealed class RevoluteJoint(GameObject gameObject, PhysicsWorld world) : PhysicsJoint(gameObject, world)
{
    public Vector2 LocalAnchorA { get; set; }
    public Vector2 LocalAnchorB { get; set; }
    public bool EnableSpring { get; set; }
    public float Hertz { get; set; } = 4f;
    public float DampingRatio { get; set; } = 0.7f;
    public bool EnableLimit { get; set; }
    public float LowerAngle { get; set; }
    public float UpperAngle { get; set; }
    public bool EnableMotor { get; set; }
    public float MaximumMotorTorque { get; set; }
    public float MotorSpeed { get; set; }

    protected override B2JointId CreateJoint()
    {
        var definition = b2DefaultRevoluteJointDef();
        Configure(ref definition.@base, LocalAnchorA, LocalAnchorB);
        definition.enableSpring = EnableSpring;
        definition.hertz = Hertz;
        definition.dampingRatio = DampingRatio;
        definition.enableLimit = EnableLimit;
        definition.lowerAngle = LowerAngle;
        definition.upperAngle = UpperAngle;
        definition.enableMotor = EnableMotor;
        definition.maxMotorTorque = MaximumMotorTorque;
        definition.motorSpeed = MotorSpeed;
        return b2CreateRevoluteJoint(world.WorldId, definition);
    }

    protected override void Validate()
    {
        if (!float.IsFinite(LocalAnchorA.X) || !float.IsFinite(LocalAnchorA.Y) ||
            !float.IsFinite(LocalAnchorB.X) || !float.IsFinite(LocalAnchorB.Y))
            throw new ArgumentOutOfRangeException(nameof(LocalAnchorA));
        ValidateFinite(Hertz, nameof(Hertz));
        ValidateFinite(DampingRatio, nameof(DampingRatio));
        ValidateFinite(LowerAngle, nameof(LowerAngle));
        ValidateFinite(UpperAngle, nameof(UpperAngle));
        ValidateFinite(MaximumMotorTorque, nameof(MaximumMotorTorque));
        ValidateFinite(MotorSpeed, nameof(MotorSpeed));
        if (Hertz < 0f || DampingRatio < 0f || LowerAngle > UpperAngle || MaximumMotorTorque < 0f)
            throw new ArgumentOutOfRangeException(nameof(LowerAngle), "Revolute joint limits and tuning values are invalid.");
    }
}
