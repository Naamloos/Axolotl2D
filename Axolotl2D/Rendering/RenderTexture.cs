namespace Axolotl2D.Rendering;

public enum RenderTextureFilter
{
    Linear,
    Nearest
}

/// <summary>A GPU-backed texture that can receive a camera's rendered output.</summary>
public sealed class RenderTexture : IDisposable
{
    private readonly Rendering owner;
    private RenderTextureFilter filter;

    internal RenderTexture(Rendering owner, int width, int height, uint framebuffer, uint texture,
        RenderTextureFilter filter)
    {
        this.owner = owner;
        this.filter = filter;
        Framebuffer = framebuffer;
        Texture = new Texture2D(width, height, texture);
    }

    /// <summary>The texture sampled by sprites and shaders.</summary>
    public Texture2D Texture { get; }
    public int Width => Texture.Width;
    public int Height => Texture.Height;
    public bool IsDisposed { get; private set; }
    internal uint Framebuffer { get; private set; }

    public RenderTextureFilter Filter
    {
        get => filter;
        set
        {
            owner.SetFilter(this, value);
            filter = value;
        }
    }

    /// <summary>Recreates the GPU storage while preserving the public texture reference.</summary>
    public void Resize(int width, int height) => owner.Resize(this, width, height);

    internal void Replace(int width, int height, uint framebuffer, uint texture)
    {
        Framebuffer = framebuffer;
        Texture.Width = width;
        Texture.Height = height;
        Texture.Handle = texture;
    }

    internal void MarkDisposed()
    {
        IsDisposed = true;
        Framebuffer = 0;
        Texture.Handle = 0;
    }

    public void Dispose()
    {
        owner.Release(this);
        GC.SuppressFinalize(this);
    }
}
