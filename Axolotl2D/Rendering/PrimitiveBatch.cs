using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>Draws simple colored shapes through the active <see cref="SpriteBatch"/>.</summary>
public sealed class PrimitiveBatch(SpriteBatch spriteBatch)
{
    private readonly Sprite pixel = new(new Texture2D(1, 1, [255, 255, 255, 255]))
    {
        Origin = Vector2.Zero
    };

    /// <summary>Draws a filled axis-aligned rectangle.</summary>
    public void FillRectangle(Vector2 position, Vector2 size, Color color,
        CoordinateSpace space = CoordinateSpace.Screen, float depth = 0f)
    {
        if (size.X <= 0f || size.Y <= 0f)
            return;
        spriteBatch.Draw(pixel, position, size, tint: color, space: space, depth: depth);
    }

    /// <summary>Draws the outline of an axis-aligned rectangle.</summary>
    public void DrawRectangle(Vector2 position, Vector2 size, Color color, float thickness = 1f,
        CoordinateSpace space = CoordinateSpace.Screen, float depth = 0f)
    {
        if (size.X <= 0f || size.Y <= 0f || thickness <= 0f)
            return;
        DrawLine(position, position + new Vector2(size.X, 0f), color, thickness, space, depth);
        DrawLine(position + new Vector2(size.X, 0f), position + size, color, thickness, space, depth);
        DrawLine(position + size, position + new Vector2(0f, size.Y), color, thickness, space, depth);
        DrawLine(position + new Vector2(0f, size.Y), position, color, thickness, space, depth);
    }

    /// <summary>Draws a line with pixel thickness.</summary>
    public void DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 1f,
        CoordinateSpace space = CoordinateSpace.Screen, float depth = 0f)
    {
        if (thickness <= 0f)
            return;
        var delta = end - start;
        var length = delta.Length();
        if (length <= float.Epsilon)
            return;

        var transform = Matrix3x2.CreateScale(length, thickness)
            * Matrix3x2.CreateRotation(MathF.Atan2(delta.Y, delta.X))
            * Matrix3x2.CreateTranslation(start - Vector2.Normalize(new(-delta.Y, delta.X)) * thickness / 2f);
        spriteBatch.Draw(pixel, transform, color, space, depth);
    }

    /// <summary>Draws a circle outline using line segments.</summary>
    public void DrawCircle(Vector2 center, float radius, Color color, float thickness = 1f, int segments = 24,
        CoordinateSpace space = CoordinateSpace.Screen, float depth = 0f)
    {
        if (radius <= 0f || thickness <= 0f)
            return;
        if (segments < 3)
            throw new ArgumentOutOfRangeException(nameof(segments));

        var previous = center + new Vector2(radius, 0f);
        for (var index = 1; index <= segments; index++)
        {
            var angle = MathF.Tau * index / segments;
            var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawLine(previous, next, color, thickness, space, depth);
            previous = next;
        }
    }

    /// <summary>Draws a filled circle as horizontal primitive spans.</summary>
    public void FillCircle(Vector2 center, float radius, Color color,
        CoordinateSpace space = CoordinateSpace.Screen, float depth = 0f)
    {
        if (radius <= 0f)
            return;

        var rows = Math.Max(1, (int)MathF.Ceiling(radius * 2f));
        var rowHeight = radius * 2f / rows;
        for (var row = 0; row < rows; row++)
        {
            var y = -radius + (row + 0.5f) * rowHeight;
            var halfWidth = MathF.Sqrt(MathF.Max(0f, radius * radius - y * y));
            FillRectangle(center + new Vector2(-halfWidth, y - rowHeight / 2f),
                new Vector2(halfWidth * 2f, rowHeight), color, space, depth);
        }
    }
}
