using Axolotl2D.GameObjects;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Marks a UI rectangle as a clipping boundary for descendant visuals and text.</summary>
public sealed class UIClip(GameObject gameObject) : Component(gameObject);

public enum UILayoutDirection { Horizontal, Vertical }
public enum UILayoutAlignment { Start, Center, End }

/// <summary>Arranges direct UITransform children in a horizontal or vertical row.</summary>
public sealed class UILayoutGroup(GameObject gameObject) : Component(gameObject)
{
    private UITransform transform = null!;
    private UILayoutDirection direction = UILayoutDirection.Vertical;
    private UILayoutAlignment alignment;
    private Vector4 padding;
    private float spacing;
    private bool expandChildren;
    private bool dirty = true;
    private int lastLayoutVersion = -1;
    private int lastChildrenVersion = -1;

    public UILayoutDirection Direction { get => direction; set => Set(ref direction, value); }
    public UILayoutAlignment Alignment { get => alignment; set => Set(ref alignment, value); }
    public Vector4 Padding { get => padding; set => Set(ref padding, value); }
    public float Spacing { get => spacing; set => Set(ref spacing, value); }
    public bool ExpandChildren { get => expandChildren; set => Set(ref expandChildren, value); }

    public override void Start() => transform = GameObject.GetComponent<UITransform>()
        ?? throw new InvalidOperationException("UILayoutGroup requires a UITransform.");

    public override void LateUpdate(double deltaTime)
    {
        var children = transform.Children;
        var layoutVersion = transform.LayoutVersion;
        if (!dirty && lastLayoutVersion == layoutVersion && lastChildrenVersion == transform.ChildrenVersion)
            return;
        if (children.Count == 0)
        {
            dirty = false;
            lastLayoutVersion = layoutVersion;
            lastChildrenVersion = transform.ChildrenVersion;
            return;
        }
        var rect = transform.Rect;
        var horizontal = direction == UILayoutDirection.Horizontal;
        var available = (horizontal ? rect.Size.X - padding.X - padding.Z : rect.Size.Y - padding.Y - padding.W)
            - spacing * (children.Count - 1);
        var total = 0f;
        for (var index = 0; index < children.Count; index++)
            total += horizontal ? children[index].Size.X : children[index].Size.Y;
        var cursor = alignment switch
        {
            UILayoutAlignment.Start => horizontal ? padding.X : padding.Y,
            UILayoutAlignment.Center => ((horizontal ? rect.Size.X : rect.Size.Y) - total - spacing * (children.Count - 1)) / 2f,
            UILayoutAlignment.End => (horizontal ? rect.Size.X - padding.Z : rect.Size.Y - padding.W) - total - spacing * (children.Count - 1),
            _ => throw new ArgumentOutOfRangeException()
        };
        var expanded = Math.Max(0f, available / children.Count);
        foreach (var child in children)
        {
            child.Anchor = Vector2.Zero;
            child.Pivot = Vector2.Zero;
            if (horizontal)
            {
                if (expandChildren) child.Size = new(expanded, child.Size.Y);
                child.AnchoredPosition = new(cursor, padding.Y);
                cursor += child.Size.X + spacing;
            }
            else
            {
                if (expandChildren) child.Size = new(child.Size.X, expanded);
                child.AnchoredPosition = new(padding.X, cursor);
                cursor += child.Size.Y + spacing;
            }
        }
        dirty = false;
        lastLayoutVersion = transform.LayoutVersion;
        lastChildrenVersion = transform.ChildrenVersion;
    }

    private void Set<T>(ref T field, T value) where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        dirty = true;
    }
}
