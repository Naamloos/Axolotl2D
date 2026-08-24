using Axolotl2D.GameObjects;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Positions a fixed-size UI element relative to the viewport or another UI element.</summary>
public sealed class UITransform(GameObject gameObject, Game game) : Component(gameObject)
{
    private readonly List<UITransform> children = [];
    private UITransform? parent;
    private Vector2 anchorMin;
    private Vector2 anchorMax;

    /// <summary>Normalized attachment point inside the parent rectangle.</summary>
    public Vector2 Anchor
    {
        get => (anchorMin + anchorMax) / 2f;
        set => anchorMin = anchorMax = value;
    }

    public Vector2 AnchorMin { get => anchorMin; set => anchorMin = value; }
    public Vector2 AnchorMax { get => anchorMax; set => anchorMax = value; }

    /// <summary>Normalized point on this rectangle placed at the anchor.</summary>
    public Vector2 Pivot { get; set; }

    /// <summary>Pixel offset from the anchor.</summary>
    public Vector2 AnchoredPosition { get; set; }

    /// <summary>Element size in screen pixels.</summary>
    public Vector2 Size { get; set; } = new(100f, 30f);
    public Vector2 OffsetMin { get; set; }
    public Vector2 OffsetMax { get; set; }
    public Vector2 MinSize { get; set; }
    public Vector2 MaxSize { get; set; } = new(float.PositiveInfinity);

    public UITransform? Parent
    {
        get => parent;
        set => SetParent(value);
    }

    public IReadOnlyList<UITransform> Children => children;

    public UIRect Rect
    {
        get
        {
            var container = parent?.Rect ?? new UIRect(Vector2.Zero, game.Viewport);
            if (anchorMin != anchorMax)
            {
                var min = container.Position + container.Size * anchorMin + OffsetMin;
                var max = container.Position + container.Size * anchorMax + OffsetMax;
                var size = ClampSize(Vector2.Max(Vector2.Zero, max - min));
                return new UIRect(min, size);
            }
            var fixedSize = ClampSize(Size);
            var position = container.Position + container.Size * Anchor + AnchoredPosition - fixedSize * Pivot;
            return new UIRect(position, fixedSize);
        }
    }

    internal UIRect? ResolveClip()
    {
        UIRect? result = null;
        for (var current = this; current is not null; current = current.parent)
            if (current.GameObject.GetComponent<UIClip>() is not null)
                result = result is null ? current.Rect : UIRect.Intersect(result.Value, current.Rect);
        return result;
    }

    private Vector2 ClampSize(Vector2 value) => Vector2.Min(Vector2.Max(value, MinSize), MaxSize);

    /// <summary>Changes the layout parent and optionally preserves the element's screen position.</summary>
    public void SetParent(UITransform? newParent, bool screenPositionStays = true)
    {
        if (ReferenceEquals(newParent, this) || IsDescendantOf(newParent))
            throw new InvalidOperationException("A UITransform cannot be parented to itself or one of its descendants.");
        if (ReferenceEquals(parent, newParent))
            return;

        var screenPosition = Rect.Position;
        parent?.children.Remove(this);
        parent = newParent;
        parent?.children.Add(this);

        if (screenPositionStays)
        {
            var container = parent?.Rect ?? new UIRect(Vector2.Zero, game.Viewport);
            AnchoredPosition = screenPosition + Size * Pivot - container.Position - container.Size * Anchor;
        }
    }

    public override void OnDestroy()
    {
        parent?.children.Remove(this);
        parent = null;
        foreach (var child in children.ToArray())
            child.SetParent(null);
        children.Clear();
    }

    private bool IsDescendantOf(UITransform? candidate)
    {
        for (var current = candidate; current is not null; current = current.parent)
            if (ReferenceEquals(current, this))
                return true;
        return false;
    }
}

/// <summary>An axis-aligned rectangle in top-left screen coordinates.</summary>
public readonly record struct UIRect(Vector2 Position, Vector2 Size)
{
    public Vector2 Center => Position + Size / 2f;
    public bool Contains(Vector2 point) =>
        point.X >= Position.X && point.Y >= Position.Y &&
        point.X <= Position.X + Size.X && point.Y <= Position.Y + Size.Y;

    public static UIRect Intersect(UIRect left, UIRect right)
    {
        var min = Vector2.Max(left.Position, right.Position);
        var max = Vector2.Min(left.Position + left.Size, right.Position + right.Size);
        return new UIRect(min, Vector2.Max(Vector2.Zero, max - min));
    }
}
