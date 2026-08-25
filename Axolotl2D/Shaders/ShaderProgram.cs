using Silk.NET.OpenGL;
using System.Numerics;
using Axolotl2D.Rendering;

namespace Axolotl2D.Shaders;

/// <summary>A linked custom GLSL program owned by a scene shader library.</summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly GL openGL;
    private readonly Dictionary<string, int> uniformLocations = new(StringComparer.Ordinal);
    private bool disposed;

    internal uint Handle { get; }

    internal ShaderProgram(GL openGL, string vertexSource, string fragmentSource)
    {
        this.openGL = openGL;
        ArgumentException.ThrowIfNullOrWhiteSpace(vertexSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentSource);

        uint vertex = 0;
        uint fragment = 0;
        uint program = 0;
        try
        {
            vertex = Compile(ShaderType.VertexShader, vertexSource);
            fragment = Compile(ShaderType.FragmentShader, fragmentSource);
            program = openGL.CreateProgram();
            openGL.AttachShader(program, vertex);
            openGL.AttachShader(program, fragment);
            openGL.LinkProgram(program);
            openGL.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
            if (status != (int)GLEnum.True)
                throw new InvalidOperationException("Shader program failed to link: " + openGL.GetProgramInfoLog(program));
            Handle = program;
        }
        catch
        {
            if (program != 0)
                openGL.DeleteProgram(program);
            throw;
        }
        finally
        {
            if (vertex != 0)
                openGL.DeleteShader(vertex);
            if (fragment != 0)
                openGL.DeleteShader(fragment);
        }
    }

    public void SetInt(string name, int value)
    {
        Use();
        openGL.Uniform1(GetLocation(name), value);
    }

    public void SetFloat(string name, float value)
    {
        Use();
        openGL.Uniform1(GetLocation(name), value);
    }

    public void SetVector2(string name, Vector2 value)
    {
        Use();
        openGL.Uniform2(GetLocation(name), value.X, value.Y);
    }

    public void SetVector4(string name, Vector4 value)
    {
        Use();
        openGL.Uniform4(GetLocation(name), value.X, value.Y, value.Z, value.W);
    }

    internal void Use()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        openGL.UseProgram(Handle);
    }

    private uint Compile(ShaderType type, string source)
    {
        var shader = openGL.CreateShader(type);
        openGL.ShaderSource(shader, source);
        openGL.CompileShader(shader);
        openGL.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == (int)GLEnum.True)
            return shader;

        var log = openGL.GetShaderInfoLog(shader);
        openGL.DeleteShader(shader);
        throw new InvalidOperationException($"{type} failed to compile: {log}");
    }

    internal int FindLocation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (uniformLocations.TryGetValue(name, out var cached))
            return cached;
        var location = openGL.GetUniformLocation(Handle, name);
        uniformLocations.Add(name, location);
        return location;
    }

    private int GetLocation(string name)
    {
        var location = FindLocation(name);
        return location >= 0
            ? location
            : throw new KeyNotFoundException($"Shader uniform '{name}' is not active in this program.");
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        uniformLocations.Clear();
        openGL.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }
}

/// <summary>Creates and disposes the custom shaders owned by one scene scope.</summary>
public sealed class ShaderLibrary(Game game) : IDisposable
{
    private readonly List<ShaderProgram> programs = [];
    private readonly List<PostProcessEffect> postProcessEffects = [];
    private bool disposed;

    public ShaderProgram Create(string vertexSource, string fragmentSource)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var openGL = game.openGL
            ?? throw new InvalidOperationException("Create custom shaders after the game window has loaded.");
        var program = new ShaderProgram(openGL, vertexSource, fragmentSource);
        programs.Add(program);
        return program;
    }

    /// <summary>Creates and attaches an ordered full-screen fragment pass to a camera.</summary>
    public PostProcessEffect CreatePostProcess(Camera2D camera, string fragmentSource)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        var program = Create(PostProcessVertexShader, fragmentSource);
        var effect = camera.AddPostProcess(program);
        postProcessEffects.Add(effect);
        return effect;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        for (var index = postProcessEffects.Count - 1; index >= 0; index--)
            postProcessEffects[index].Dispose();
        postProcessEffects.Clear();
        for (var index = programs.Count - 1; index >= 0; index--)
            programs[index].Dispose();
        programs.Clear();
        GC.SuppressFinalize(this);
    }

    private const string PostProcessVertexShader = """
        #version 330 core
        out vec2 frag_texCoords;
        void main() {
            vec2 positions[3] = vec2[](vec2(-1.0, -1.0), vec2(3.0, -1.0), vec2(-1.0, 3.0));
            vec2 coordinates[3] = vec2[](vec2(0.0, 0.0), vec2(2.0, 0.0), vec2(0.0, 2.0));
            gl_Position = vec4(positions[gl_VertexID], 0.0, 1.0);
            frag_texCoords = coordinates[gl_VertexID];
        }
        """;
}
