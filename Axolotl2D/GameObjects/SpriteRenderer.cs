using Axolotl2D.Rendering;

namespace Axolotl2D.GameObjects;

/// <summary>Renders a sprite using its GameObject's world transform.</summary>
public sealed class SpriteRenderer(GameObject gameObject, SpriteBatch spriteBatch) : Component(gameObject)
{
    public Sprite? Sprite { get; set; }
    public Color Tint { get; set; } = Color.White;
    public CoordinateSpace Space { get; set; } = CoordinateSpace.World;
    public float Depth { get; set; }

    public override void Render()
    {
        if (Sprite is not null)
            spriteBatch.Draw(Sprite, Transform.WorldMatrix, Tint, Space, Depth);
    }
}
