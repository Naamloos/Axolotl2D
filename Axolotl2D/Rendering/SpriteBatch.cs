using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>Queues sprites and submits them in texture batches at the end of a scene frame.</summary>
public sealed class SpriteBatch(Rendering rendering, Camera2D defaultCamera)
{
    private readonly List<SpriteDrawCommand> commands = [];
    private Camera2D? camera;

    public bool IsBegun { get; private set; }

    public void Begin(Camera2D? camera = null)
    {
        if (IsBegun)
            throw new InvalidOperationException("SpriteBatch.Begin cannot be called twice without End.");
        commands.Clear();
        this.camera = camera ?? defaultCamera;
        IsBegun = true;
    }

    public void Draw(Sprite sprite, Matrix3x2 transform, Color? tint = null, CoordinateSpace space = CoordinateSpace.World, float depth = 0f)
    {
        EnsureBegun();
        commands.Add(new SpriteDrawCommand(sprite, transform, tint ?? Color.White, space, depth, commands.Count));
    }

    public void Draw(Sprite sprite, Vector2 position, Vector2? size = null, float rotation = 0f, Color? tint = null,
        CoordinateSpace space = CoordinateSpace.World, float depth = 0f)
    {
        var drawSize = size ?? sprite.Size;
        var scale = drawSize / sprite.Size;
        var transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position);
        Draw(sprite, transform, tint, space, depth);
    }

    public void Draw(Texture2D texture, Vector2 position, Vector2? size = null, Color? tint = null,
        CoordinateSpace space = CoordinateSpace.World, float depth = 0f) =>
        Draw(new Sprite(texture), position, size, 0f, tint, space, depth);

    public void End()
    {
        EnsureBegun();
        var activeCamera = camera!;
        IsBegun = false;
        camera = null;
        rendering.Draw(commands, activeCamera);
    }

    private void EnsureBegun()
    {
        if (!IsBegun)
            throw new InvalidOperationException("Call SpriteBatch.Begin before drawing.");
    }
}

internal readonly record struct SpriteDrawCommand(
    Sprite Sprite,
    Matrix3x2 Transform,
    Color Tint,
    CoordinateSpace Space,
    float Depth,
    int Order);
