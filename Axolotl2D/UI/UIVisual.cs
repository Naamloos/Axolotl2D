using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Draws a texture or a primitive fallback inside a <see cref="UITransform"/>.</summary>
public sealed class UIVisual(
    GameObject gameObject,
    SpriteBatch spriteBatch,
    PrimitiveBatch primitives) : Component(gameObject)
{
    private UITransform transform = null!;

    public Sprite? Sprite { get; set; }
    public UIPrimitive Primitive { get; set; } = UIPrimitive.Rectangle;
    public Color Color { get; set; } = Color.White;
    public float Thickness { get; set; } = 1f;
    public float Depth { get; set; }

    public override void Start() => transform = GameObject.GetComponent<UITransform>()
        ?? throw new InvalidOperationException("UIVisual requires a UITransform on the same GameObject.");

    public override void Render()
    {
        var rect = transform.Rect;
        if (Sprite is not null)
        {
            var position = rect.Position + rect.Size * Sprite.Origin;
            spriteBatch.Draw(Sprite, position, rect.Size, tint: Color, space: CoordinateSpace.Screen, depth: Depth);
            return;
        }

        var radius = MathF.Min(rect.Size.X, rect.Size.Y) / 2f;
        switch (Primitive)
        {
            case UIPrimitive.Rectangle:
                primitives.FillRectangle(rect.Position, rect.Size, Color, CoordinateSpace.Screen, Depth);
                break;
            case UIPrimitive.RectangleOutline:
                primitives.DrawRectangle(rect.Position, rect.Size, Color, Thickness, CoordinateSpace.Screen, Depth);
                break;
            case UIPrimitive.Circle:
                primitives.FillCircle(rect.Center, radius, Color, CoordinateSpace.Screen, Depth);
                break;
            case UIPrimitive.CircleOutline:
                primitives.DrawCircle(rect.Center, radius, Color, Thickness, space: CoordinateSpace.Screen, depth: Depth);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Primitive));
        }
    }
}

/// <summary>Primitive fallback shapes supported by <see cref="UIVisual"/>.</summary>
public enum UIPrimitive
{
    Rectangle,
    RectangleOutline,
    Circle,
    CircleOutline
}
