using Axolotl2D.Shaders;

namespace Axolotl2D.Rendering;

/// <summary>A scene-owned full-screen shader pass attached to one camera.</summary>
public sealed class PostProcessEffect : IDisposable
{
    private Action? detach;

    internal PostProcessEffect(Camera2D camera, ShaderProgram shader, Action detach)
    {
        Camera = camera;
        Shader = shader;
        this.detach = detach;
    }

    public Camera2D Camera { get; }
    public ShaderProgram Shader { get; }
    public bool Enabled { get; set; } = true;
    public bool IsDisposed => detach is null;

    /// <summary>Detaches the pass. Its scene shader remains owned by ShaderLibrary.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref detach, null)?.Invoke();
        GC.SuppressFinalize(this);
    }
}
