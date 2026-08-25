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
    private Vector2 pivot;
    private Vector2 anchoredPosition;
    private Vector2 size = new(100f, 30f);
    private Vector2 offsetMin;
    private Vector2 offsetMax;
    private Vector2 minSize;
    private Vector2 maxSize = new(float.PositiveInfinity);
    private UIRect cachedContainer;
    private UIRect cachedRect;
    private bool dirty = true;
    private int layoutVersion;
    private int childrenVersion;

    /// <summary>Normalized attachment point inside the parent rectangle.</summary>
    public Vector2 Anchor
    {
        get => (anchorMin + anchorMax) / 2f;
        set
        {
            if (anchorMin == value && anchorMax == value) return;
            anchorMin = anchorMax = value;
            MarkDirty();
        }
    }

    public Vector2 AnchorMin { get => anchorMin; set => Set(ref anchorMin, value); }
    public Vector2 AnchorMax { get => anchorMax; set => Set(ref anchorMax, value); }

    /// <summary>Normalized point on this rectangle placed at the anchor.</summary>
    public Vector2 Pivot { get => pivot; set => Set(ref pivot, value); }

    /// <summary>Pixel offset from the anchor.</summary>
    public Vector2 AnchoredPosition { get => anchoredPosition; set => Set(ref anchoredPosition, value); }

    /// <summary>Element size in screen pixels.</summary>
    public Vector2 Size { get => size; set => Set(ref size, value); }
    public Vector2 OffsetMin { get => offsetMin; set => Set(ref offsetMin, value); }
    public Vector2 OffsetMax { get => offsetMax; set => Set(ref offsetMax, value); }
    public Vector2 MinSize { get => minSize; set => Set(ref minSize, value); }
    public Vector2 MaxSize { get => maxSize; set => Set(ref maxSize, value); }

    public UITransform? Parent
    {
        get => parent;
        set => SetParent(value);
    }

    public IReadOnlyList<UITransform> Children => children;
    internal int ChildrenVersion => childrenVersion;
    internal int LayoutVersion { get { _ = Rect; return layoutVersion; } }

    public UIRect Rect
    {
        get
        {
            var container = parent?.Rect ?? new UIRect(Vector2.Zero, game.Viewport);
            if (!dirty && cachedContainer == container) return cachedRect;
            cachedContainer = container;
            cachedRect = CalculateRect(container);
            dirty = false;
            layoutVersion++;
            return cachedRect;
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

    /// <summary>Changes the layout parent and optionally preserves the element's screen position.</summary>
    public void SetParent(UITransform? newParent, bool screenPositionStays = true)
    {
        if (ReferenceEquals(newParent, this) || IsDescendantOf(newParent))
            throw new InvalidOperationException("A UITransform cannot be parented to itself or one of its descendants.");
        if (ReferenceEquals(parent, newParent)) return;

        var screenPosition = Rect.Position;
        if (parent is not null)
        {
            parent.children.Remove(this);
            parent.childrenVersion++;
        }
        parent = newParent;
        if (parent is not null)
        {
            parent.children.Add(this);
            parent.childrenVersion++;
        }
        MarkDirty(notifyParent: false);

        if (screenPositionStays)
        {
            var container = parent?.Rect ?? new UIRect(Vector2.Zero, game.Viewport);
            AnchoredPosition = screenPosition + Size * Pivot - container.Position - container.Size * Anchor;
        }
    }

    public override void OnDestroy()
    {
        if (parent is not null)
        {
            parent.children.Remove(this);
            parent.childrenVersion++;
            parent = null;
        }
        foreach (var child in children.ToArray())
            child.SetParent(null);
        children.Clear();
        childrenVersion++;
    }

    private UIRect CalculateRect(UIRect container)
    {
        if (anchorMin != anchorMax)
        {
            var minimum = container.Position + container.Size * anchorMin + offsetMin;
            var maximum = container.Position + container.Size * anchorMax + offsetMax;
            return new(minimum, ClampSize(Vector2.Max(Vector2.Zero, maximum - minimum)));
        }
        var fixedSize = ClampSize(size);
        var position = container.Position + container.Size * Anchor + anchoredPosition - fixedSize * pivot;
        return new(position, fixedSize);
    }

    private Vector2 ClampSize(Vector2 value) => Vector2.Min(Vector2.Max(value, minSize), maxSize);

    private void Set(ref Vector2 field, Vector2 value)
    {
        if (field == value) return;
        field = value;
        MarkDirty();
    }

    private void MarkDirty(bool notifyParent = true)
    {
        if (notifyParent && parent is not null) parent.childrenVersion++;
        if (dirty) return;
        dirty = true;
        foreach (var child in children)
            child.MarkDirty(notifyParent: false);
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
        return new(min, Vector2.Max(Vector2.Zero, max - min));
    }
}
