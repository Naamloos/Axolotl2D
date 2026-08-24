using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Draws a normalized horizontal fill inside its UITransform.</summary>
public sealed class UIProgressBar(GameObject gameObject, PrimitiveBatch primitives) : Component(gameObject)
{
    private UITransform transform = null!;
    public float Value { get; set; }
    public Color BackgroundColor { get; set; } = Color.DarkGray;
    public Color FillColor { get; set; } = Color.Green;
    public float Depth { get; set; }
    public override void Start() => transform = GameObject.GetComponent<UITransform>()
        ?? throw new InvalidOperationException("UIProgressBar requires a UITransform.");
    public override void Render()
    {
        var rect = transform.Rect;
        primitives.FillRectangle(rect.Position, rect.Size, BackgroundColor, depth: Depth);
        primitives.FillRectangle(rect.Position, new(rect.Size.X * Math.Clamp(Value, 0f, 1f), rect.Size.Y), FillColor, depth: Depth + 0.001f);
    }
}

/// <summary>Clips and offsets a larger content transform inside a viewport.</summary>
public sealed class UIScrollView(GameObject gameObject, Game game) : Component(gameObject)
{
    private UITransform viewport = null!;
    private Vector2 offset;
    public UITransform Content { get; set; } = null!;
    public Vector2 ContentSize { get; set; }
    public float WheelSpeed { get; set; } = 36f;
    public bool Horizontal { get; set; }
    public bool Vertical { get; set; } = true;
    public Vector2 Offset { get => offset; set { offset = Clamp(value); Apply(); } }

    public override void Awake()
    {
        viewport = GameObject.GetComponent<UITransform>()
            ?? throw new InvalidOperationException("UIScrollView requires a UITransform.");
        if (GameObject.GetComponent<UIClip>() is null) GameObject.AddComponent<UIClip>();
    }

    public override void Update(double deltaTime)
    {
        var mouse = game.GetMouse();
        if (mouse is null || !viewport.Rect.Contains(mouse.Position) || mouse.ScrollWheels.Count == 0) return;
        var wheel = mouse.ScrollWheels[0];
        ScrollBy(new Vector2(Horizontal ? -wheel.X * WheelSpeed : 0f, Vertical ? -wheel.Y * WheelSpeed : 0f));
    }

    public void ScrollBy(Vector2 amount) => Offset += amount;
    private Vector2 Clamp(Vector2 value) => Vector2.Clamp(value, Vector2.Zero, Vector2.Max(Vector2.Zero, ContentSize - viewport.Rect.Size));
    private void Apply() { if (Content is not null) Content.AnchoredPosition = -offset; }
}
