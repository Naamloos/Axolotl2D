using System.Numerics;

namespace Axolotl2D.GameObjects;

/// <summary>A hierarchical 2D position, rotation, and scale.</summary>
public sealed class Transform
{
    private Transform? parent;
    private readonly List<Transform> children = [];

    public Vector2 LocalPosition { get; set; }
    public float LocalRotation { get; set; }
    public Vector2 LocalScale { get; set; } = Vector2.One;
    public IReadOnlyList<Transform> Children => children;

    public Transform? Parent
    {
        get => parent;
        set => SetParent(value);
    }

    public Matrix3x2 LocalMatrix =>
        Matrix3x2.CreateScale(LocalScale)
        * Matrix3x2.CreateRotation(LocalRotation)
        * Matrix3x2.CreateTranslation(LocalPosition);

    public Matrix3x2 WorldMatrix => parent is null ? LocalMatrix : LocalMatrix * parent.WorldMatrix;
    public Vector2 Position => Vector2.Transform(Vector2.Zero, WorldMatrix);
    public float Rotation => LocalRotation + (parent?.Rotation ?? 0f);
    public Vector2 LossyScale => LocalScale * (parent?.LossyScale ?? Vector2.One);
    public Vector2 Right => new(MathF.Cos(Rotation), MathF.Sin(Rotation));
    public Vector2 Up => new(MathF.Sin(Rotation), -MathF.Cos(Rotation));

    public void Translate(Vector2 amount, bool localSpace = false) =>
        LocalPosition += localSpace ? Right * amount.X - Up * amount.Y : amount;

    public void Rotate(float radians) => LocalRotation += radians;

    public void LookAt(Vector2 worldTarget) =>
        LocalRotation = MathF.Atan2(worldTarget.Y - Position.Y, worldTarget.X - Position.X) - (parent?.Rotation ?? 0f);

    public Vector2 TransformPoint(Vector2 localPoint) => Vector2.Transform(localPoint, WorldMatrix);

    public Vector2 InverseTransformPoint(Vector2 worldPoint)
    {
        if (!Matrix3x2.Invert(WorldMatrix, out var inverse))
            throw new InvalidOperationException("A transform with a zero scale cannot transform world points to local space.");
        return Vector2.Transform(worldPoint, inverse);
    }

    public void SetParent(Transform? newParent, bool worldPositionStays = true)
    {
        if (ReferenceEquals(newParent, this) || IsDescendantOf(newParent))
            throw new InvalidOperationException("A transform cannot be parented to itself or one of its descendants.");
        if (ReferenceEquals(parent, newParent))
            return;

        var worldPosition = Position;
        var worldRotation = Rotation;
        var worldScale = LossyScale;
        parent?.children.Remove(this);
        parent = newParent;
        parent?.children.Add(this);

        if (!worldPositionStays)
            return;
        LocalPosition = parent?.InverseTransformPoint(worldPosition) ?? worldPosition;
        LocalRotation = worldRotation - (parent?.Rotation ?? 0f);
        LocalScale = worldScale / (parent?.LossyScale ?? Vector2.One);
    }

    private bool IsDescendantOf(Transform? candidate)
    {
        for (var current = candidate; current is not null; current = current.parent)
            if (ReferenceEquals(current, this))
                return true;
        return false;
    }

    internal void DetachHierarchy()
    {
        SetParent(null);
        foreach (var child in children.ToArray())
            child.SetParent(null);
    }
}
