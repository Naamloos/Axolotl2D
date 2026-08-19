using Axolotl2D.GameObjects;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Positions a fixed-size UI element relative to the viewport or another UI element.</summary>
public sealed class UITransform(GameObject gameObject, Game game) : Component(gameObject)
{
    private readonly List<UITransform> children = [];
    private UITransform? parent;

    /// <summary>Normalized attachment point inside the parent rectangle.</summary>
    public Vector2 Anchor { get; set; }

    /// <summary>Normalized point on this rectangle placed at the anchor.</summary>
    public Vector2 Pivot { get; set; }

    /// <summary>Pixel offset from the anchor.</summary>
    public Vector2 AnchoredPosition { get; set; }

    /// <summary>Element size in screen pixels.</summary>
    public Vector2 Size { get; set; } = new(100f, 30f);

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
            var position = container.Position + container.Size * Anchor + AnchoredPosition - Size * Pivot;
            return new UIRect(position, Size);
        }
    }

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
}
