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
    public UILayoutDirection Direction { get; set; } = UILayoutDirection.Vertical;
    public UILayoutAlignment Alignment { get; set; }
    public Vector4 Padding { get; set; }
    public float Spacing { get; set; }
    public bool ExpandChildren { get; set; }

    public override void Start() => transform = GameObject.GetComponent<UITransform>()
        ?? throw new InvalidOperationException("UILayoutGroup requires a UITransform.");

    public override void LateUpdate(double deltaTime)
    {
        var children = transform.Children;
        if (children.Count == 0) return;
        var rect = transform.Rect;
        var horizontal = Direction == UILayoutDirection.Horizontal;
        var available = (horizontal ? rect.Size.X - Padding.X - Padding.Z : rect.Size.Y - Padding.Y - Padding.W)
            - Spacing * (children.Count - 1);
        var total = children.Sum(child => horizontal ? child.Size.X : child.Size.Y);
        var cursor = Alignment switch
        {
            UILayoutAlignment.Start => horizontal ? Padding.X : Padding.Y,
            UILayoutAlignment.Center => ((horizontal ? rect.Size.X : rect.Size.Y) - total - Spacing * (children.Count - 1)) / 2f,
            UILayoutAlignment.End => (horizontal ? rect.Size.X - Padding.Z : rect.Size.Y - Padding.W) - total - Spacing * (children.Count - 1),
            _ => throw new ArgumentOutOfRangeException()
        };
        var expanded = Math.Max(0f, available / children.Count);
        foreach (var child in children)
        {
            child.Anchor = Vector2.Zero;
            child.Pivot = Vector2.Zero;
            if (horizontal)
            {
                if (ExpandChildren) child.Size = new(expanded, child.Size.Y);
                child.AnchoredPosition = new(cursor, Padding.Y);
                cursor += child.Size.X + Spacing;
            }
            else
            {
                if (ExpandChildren) child.Size = new(child.Size.X, expanded);
                child.AnchoredPosition = new(Padding.X, cursor);
                cursor += child.Size.Y + Spacing;
            }
        }
    }
}
