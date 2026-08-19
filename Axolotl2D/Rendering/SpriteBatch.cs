using Axolotl2D.Shaders;
using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>Queues sprites and submits them in texture batches at the end of a scene frame.</summary>
public sealed class SpriteBatch(Rendering rendering, Camera2D defaultCamera)
{
    private readonly List<SpriteDrawCommand> commands = [];
    private Camera2D? camera;
    private ShaderProgram? shader;

    public bool IsBegun { get; private set; }

    public void Begin(Camera2D? camera = null)
    {
        if (IsBegun)
            throw new InvalidOperationException("SpriteBatch.Begin cannot be called twice without End.");
        commands.Clear();
        this.camera = camera ?? defaultCamera;
        shader = null;
        IsBegun = true;
    }

    /// <summary>Selects a custom shader for draws submitted inside the returned scope.</summary>
    public IDisposable UseShader(ShaderProgram program)
    {
        EnsureBegun();
        ArgumentNullException.ThrowIfNull(program);
        var previous = shader;
        shader = program;
        return new ShaderScope(() => shader = previous);
    }

    public void Draw(Sprite sprite, Matrix3x2 transform, Color? tint = null, CoordinateSpace space = CoordinateSpace.World, float depth = 0f)
    {
        EnsureBegun();
        commands.Add(new SpriteDrawCommand(sprite, transform, tint ?? Color.White, space, depth, commands.Count, shader));
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
        shader = null;
        rendering.Draw(commands, activeCamera);
    }

    private void EnsureBegun()
    {
        if (!IsBegun)
            throw new InvalidOperationException("Call SpriteBatch.Begin before drawing.");
    }

    private sealed class ShaderScope(Action dispose) : IDisposable
    {
        private Action? action = dispose;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}

// ponytail: uniform state is program-wide for one batch; add material snapshots when per-draw uniforms are required.
internal readonly record struct SpriteDrawCommand(
    Sprite Sprite,
    Matrix3x2 Transform,
    Color Tint,
    CoordinateSpace Space,
    float Depth,
    int Order,
    ShaderProgram? Shader);
