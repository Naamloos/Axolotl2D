using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Draws aligned text inside a <see cref="UITransform"/>.</summary>
public sealed class UIText(
    GameObject gameObject,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer) : Component(gameObject)
{
    private UITransform transform = null!;

    public FontAsset? Font { get; set; }
    public string Text { get; set; } = string.Empty;
    public float FontSize { get; set; } = 16f;
    public Color Color { get; set; } = Color.White;
    public UIHorizontalAlignment HorizontalAlignment { get; set; }
    public UIVerticalAlignment VerticalAlignment { get; set; }
    public float Depth { get; set; }

    public override void Start()
    {
        transform = GameObject.GetComponent<UITransform>()
            ?? throw new InvalidOperationException("UIText requires a UITransform on the same GameObject.");
        ArgumentNullException.ThrowIfNull(Font);
    }

    public override void Render()
    {
        if (string.IsNullOrEmpty(Text))
            return;

        using var clip = transform.ResolveClip() is { } rectangle ? spriteBatch.PushClip(rectangle) : null;
        var texture = textRenderer.Render(Font!, Text, FontSize);
        var rect = transform.Rect;
        var position = rect.Position;
        position.X += HorizontalAlignment switch
        {
            UIHorizontalAlignment.Left => 0f,
            UIHorizontalAlignment.Center => (rect.Size.X - texture.Width) / 2f,
            UIHorizontalAlignment.Right => rect.Size.X - texture.Width,
            _ => throw new ArgumentOutOfRangeException(nameof(HorizontalAlignment))
        };
        position.Y += VerticalAlignment switch
        {
            UIVerticalAlignment.Top => 0f,
            UIVerticalAlignment.Center => (rect.Size.Y - texture.Height) / 2f,
            UIVerticalAlignment.Bottom => rect.Size.Y - texture.Height,
            _ => throw new ArgumentOutOfRangeException(nameof(VerticalAlignment))
        };
        spriteBatch.Draw(new Sprite(texture) { Origin = Vector2.Zero }, position,
            tint: Color, space: CoordinateSpace.Screen, depth: Depth);
    }
}

public enum UIHorizontalAlignment
{
    Left,
    Center,
    Right
}

public enum UIVerticalAlignment
{
    Top,
    Center,
    Bottom
}
