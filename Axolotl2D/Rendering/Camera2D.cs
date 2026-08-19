using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>A top-left-screen-space 2D camera for panning and zooming a world.</summary>
public sealed class Camera2D
{
    private const float MinimumZoom = 0.01f;
    private float zoom = 1f;

    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public Vector2 ViewportSize { get; private set; }

    public float Zoom
    {
        get => zoom;
        set => zoom = Math.Max(MinimumZoom, value);
    }

    public Camera2D(Game game)
    {
        ViewportSize = game.Viewport;
        game.OnResize += size => ViewportSize = size;
    }

    /// <summary>Moves the camera by a world-space amount.</summary>
    public void Pan(Vector2 worldDelta) => Position += worldDelta;

    /// <summary>Changes zoom while keeping the world point beneath a screen position fixed.</summary>
    public void ZoomAt(float factor, Vector2 screenPosition)
    {
        if (factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor));
        var anchoredWorldPosition = ScreenToWorld(screenPosition);
        Zoom *= factor;
        Position += anchoredWorldPosition - ScreenToWorld(screenPosition);
    }

    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        var relative = worldPosition - Position;
        var rotated = Vector2.Transform(relative, Matrix3x2.CreateRotation(-Rotation));
        return rotated * Zoom + ViewportSize / 2f;
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        var relative = (screenPosition - ViewportSize / 2f) / Zoom;
        return Vector2.Transform(relative, Matrix3x2.CreateRotation(Rotation)) + Position;
    }
}
