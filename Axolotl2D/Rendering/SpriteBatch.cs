using Axolotl2D.Shaders;
using Axolotl2D.Lighting;
using Axolotl2D.UI;
using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>Queues sprites and submits them in texture batches at the end of a scene frame.</summary>
public sealed class SpriteBatch(Rendering rendering, CameraManager cameras, Lighting2D lighting)
{
    private static readonly Color DefaultTint = Color.White;
    private readonly List<SpriteDrawCommand> commands = [];
    private Camera2D? camera;
    private ShaderProgram? shader;
    private UIRect? clip;
    private bool explicitCamera;

    public bool IsBegun { get; private set; }

    public void Begin(Camera2D? camera = null)
    {
        if (IsBegun)
            throw new InvalidOperationException("SpriteBatch.Begin cannot be called twice without End.");
        commands.Clear();
        this.camera = camera;
        explicitCamera = camera is not null;
        shader = null;
        clip = null;
        IsBegun = true;
    }

    /// <summary>Clips subsequent draw commands to a top-left screen rectangle.</summary>
    public IDisposable PushClip(UIRect rectangle)
    {
        EnsureBegun();
        var previous = clip;
        clip = previous is null ? rectangle : UIRect.Intersect(previous.Value, rectangle);
        return new ShaderScope(() => clip = previous);
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

    public void Draw(Sprite sprite, Matrix3x2 transform, Color? tint = null, CoordinateSpace space = CoordinateSpace.World,
        float depth = 0f, uint lightingLayer = 1)
    {
        EnsureBegun();
        commands.Add(new SpriteDrawCommand(sprite, transform, tint ?? DefaultTint, space, depth, commands.Count,
            shader, lightingLayer, clip));
    }

    public void Draw(Sprite sprite, Vector2 position, Vector2? size = null, float rotation = 0f, Color? tint = null,
        CoordinateSpace space = CoordinateSpace.World, float depth = 0f, uint lightingLayer = 1)
    {
        var transform = size is null && rotation == 0f
            ? Matrix3x2.CreateTranslation(position)
            : Matrix3x2.CreateScale((size ?? sprite.Size) / sprite.Size) *
                Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position);
        Draw(sprite, transform, tint, space, depth, lightingLayer);
    }

    public void Draw(Texture2D texture, Vector2 position, Vector2? size = null, Color? tint = null,
        CoordinateSpace space = CoordinateSpace.World, float depth = 0f, uint lightingLayer = 1) =>
        Draw(new Sprite(texture), position, size, 0f, tint, space, depth, lightingLayer);

    public void End()
    {
        EnsureBegun();
        IsBegun = false;
        if (explicitCamera)
            rendering.Draw(commands, camera!, lighting.Snapshot(), includeWorld: true, includeScreen: true);
        else
        {
            var hasWorldCommands = false;
            for (var index = 0; index < commands.Count; index++)
                if (commands[index].Space == CoordinateSpace.World)
                {
                    hasWorldCommands = true;
                    break;
            }
            if (hasWorldCommands)
            {
                var snapshot = lighting.Snapshot();
                foreach (var activeCamera in cameras.ActiveCameras)
                    rendering.Draw(commands, activeCamera, snapshot, includeWorld: true, includeScreen: false);
            }
            rendering.DrawScreen(commands);
        }
        camera = null;
        explicitCamera = false;
        shader = null;
        clip = null;
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
    ShaderProgram? Shader,
    uint LightingLayer,
    UIRect? Clip);
