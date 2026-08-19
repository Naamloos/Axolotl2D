using Silk.NET.OpenGL;
using System.Numerics;

namespace Axolotl2D.Shaders;

/// <summary>A linked custom GLSL program owned by a scene shader library.</summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly GL openGL;
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

    private int GetLocation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var location = openGL.GetUniformLocation(Handle, name);
        return location >= 0
            ? location
            : throw new KeyNotFoundException($"Shader uniform '{name}' is not active in this program.");
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        openGL.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }
}

/// <summary>Creates and disposes the custom shaders owned by one scene scope.</summary>
public sealed class ShaderLibrary(Game game) : IDisposable
{
    private readonly List<ShaderProgram> programs = [];
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

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        for (var index = programs.Count - 1; index >= 0; index--)
            programs[index].Dispose();
        programs.Clear();
        GC.SuppressFinalize(this);
    }
}
