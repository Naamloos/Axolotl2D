using Axolotl2D.Lighting;
using Axolotl2D.Shaders;
using Axolotl2D.UI;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Axolotl2D.Rendering;

public interface IRendering : IDisposable
{
    RenderStatistics Statistics { get; }
    void Initialize();
    void BeginFrame();
    void EndFrame();
}

public readonly record struct RenderStatistics(int DrawCommands, int DrawSubmissions, int Triangles, int UploadedTextures);

/// <summary>Owns shared GPU buffers and texture uploads for sprite rendering.</summary>
public sealed unsafe class Rendering(Game game) : IRendering
{
    private const int VertexFloatCount = 17;
    private readonly HashSet<Texture2D> uploadedTextures = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Camera2D, CameraRenderTargets> cameraTargets = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<RenderTexture> renderTextures = new(ReferenceEqualityComparer.Instance);
    private readonly Texture2D flatNormal = new(1, 1, [128, 128, 255, 255]);
    private GL openGL = null!;
    private uint vertexArray;
    private uint vertexBuffer;
    private uint indexBuffer;
    private bool initialized;
    private int frameCommands;
    private int frameSubmissions;
    private int frameTriangles;
    private int disposed;

    public RenderStatistics Statistics { get; private set; }

    /// <summary>Creates a fixed-size GPU texture suitable for a camera render target.</summary>
    public RenderTexture CreateRenderTexture(int width, int height,
        RenderTextureFilter filter = RenderTextureFilter.Linear)
    {
        ValidateTargetSize(width, height);
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        if (!initialized) Initialize();
        var target = CreateRenderTarget(width, height);
        var texture = new RenderTexture(this, width, height, target.Framebuffer, target.Texture, filter);
        renderTextures.Add(texture);
        SetFilter(texture, filter);
        RestoreWindowTarget();
        return texture;
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (initialized) return;
        openGL = game.openGL ?? throw new InvalidOperationException("Rendering can only initialize after the game window loads.");
        vertexArray = openGL.GenVertexArray();
        vertexBuffer = openGL.GenBuffer();
        indexBuffer = openGL.GenBuffer();
        openGL.BindVertexArray(vertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);

        const uint stride = VertexFloatCount * sizeof(float);
        Attribute(0, 3, 0, stride);
        Attribute(1, 2, 3, stride);
        Attribute(2, 4, 5, stride);
        Attribute(3, 2, 9, stride);
        Attribute(4, 2, 11, stride);
        Attribute(5, 2, 13, stride);
        Attribute(6, 2, 15, stride);
        openGL.BindVertexArray(0);
        initialized = true;
    }

    internal void Draw(IReadOnlyList<SpriteDrawCommand> commands, Camera2D camera, LightingSnapshot lighting,
        bool includeWorld, bool includeScreen)
    {
        var world = commands.Where(command => includeWorld && command.Space == CoordinateSpace.World &&
            (command.LightingLayer & camera.CullingMask) != 0).ToArray();
        var screen = commands.Where(command => includeScreen && command.Space == CoordinateSpace.Screen).ToArray();
        var effects = camera.PostProcessEffects.Where(effect => effect.Enabled && !effect.IsDisposed).ToArray();
        var output = GetOutput(camera);

        if (effects.Length == 0)
        {
            ReleaseTargets(camera);
            if (output is not null) Clear(output);
            DrawInternal([.. world, .. screen], camera, lighting, output);
            if (output is not null) RestoreWindowTarget();
            return;
        }

        DrawPostProcessed(world, camera, lighting, effects, output);
        DrawInternal(screen, null, new(false, Vector3.One, [], []));
    }

    internal void DrawScreen(IReadOnlyList<SpriteDrawCommand> commands)
    {
        var selected = commands.Where(command => command.Space == CoordinateSpace.Screen).ToArray();
        DrawInternal(selected, null, new(false, Vector3.One, [], []));
    }

    private void DrawInternal(IReadOnlyList<SpriteDrawCommand> commands, Camera2D? camera, LightingSnapshot lighting,
        RenderTarget? target = null)
    {
        if (!initialized) Initialize();
        if (commands.Count == 0) return;
        frameCommands += commands.Count;

        var ordered = commands.OrderBy(command => command.Depth).ThenBy(command => command.Order);
        var batch = new List<SpriteDrawCommand>();
        SpriteDrawCommand? previous = null;
        foreach (var command in ordered)
        {
            if (previous is { } value && !CanBatch(value, command))
            {
                Flush(batch, camera, lighting, target);
                batch.Clear();
            }
            batch.Add(command);
            previous = command;
        }
        if (batch.Count > 0) Flush(batch, camera, lighting, target);
        openGL.Disable(EnableCap.ScissorTest);
    }

    private void Flush(IReadOnlyList<SpriteDrawCommand> commands, Camera2D? camera, LightingSnapshot lighting,
        RenderTarget? target)
    {
        frameSubmissions++;
        frameTriangles += commands.Count * 2;
        var firstCommand = commands[0];
        var texture = firstCommand.Sprite.Texture;
        var normalMap = firstCommand.Sprite.NormalMap ?? flatNormal;
        var viewport = firstCommand.Space == CoordinateSpace.World && camera is not null
            ? camera.PixelViewport
            : new UIRect(Vector2.Zero, game.Viewport);
        if (target is null)
            SetViewport(viewport, game.Viewport.Y);
        else
            openGL.Viewport(0, 0, (uint)target.Width, (uint)target.Height);
        var vertices = new float[commands.Count * 4 * VertexFloatCount];
        var indices = new uint[commands.Count * 6];
        var vertexOffset = 0;

        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            var sprite = command.Sprite;
            var size = sprite.Size;
            var origin = sprite.Origin * size;
            Span<Vector2> corners =
            [
                new(-origin.X, -origin.Y), new(size.X - origin.X, -origin.Y),
                new(size.X - origin.X, size.Y - origin.Y), new(-origin.X, size.Y - origin.Y)
            ];
            var colorUvs = TextureCoordinates(sprite.Source, texture);
            var normalUvs = TextureCoordinates(sprite.Source, normalMap);
            var tangent = Vector2.TransformNormal(Vector2.UnitX, command.Transform);
            var bitangent = Vector2.TransformNormal(Vector2.UnitY, command.Transform);
            tangent = tangent.LengthSquared() > 0f ? Vector2.Normalize(tangent) : Vector2.UnitX;
            bitangent = bitangent.LengthSquared() > 0f ? Vector2.Normalize(bitangent) : Vector2.UnitY;

            for (var corner = 0; corner < 4; corner++)
            {
                var world = Vector2.Transform(corners[corner], command.Transform);
                var screen = command.Space == CoordinateSpace.World ? camera!.WorldToScreen(world) : world;
                var ndc = Coordinates.ScreenToNormalizedDevice(screen - viewport.Position, viewport.Size);
                Write(ref vertexOffset, vertices,
                    ndc.X, ndc.Y, 0f,
                    colorUvs[corner].X, colorUvs[corner].Y,
                    command.Tint.R, command.Tint.G, command.Tint.B, command.Tint.A,
                    world.X, world.Y,
                    tangent.X, tangent.Y,
                    bitangent.X, bitangent.Y,
                    normalUvs[corner].X, normalUvs[corner].Y);
            }

            var first = (uint)i * 4;
            var index = i * 6;
            indices[index] = first; indices[index + 1] = first + 1; indices[index + 2] = first + 2;
            indices[index + 3] = first; indices[index + 4] = first + 2; indices[index + 5] = first + 3;
        }

        var program = firstCommand.Shader?.Handle ?? game.shaderProgramPointer;
        if (firstCommand.Shader is null) openGL.UseProgram(program); else firstCommand.Shader.Use();
        if (firstCommand.Shader is null)
            ConfigureLighting(program, camera is not null && firstCommand.Space == CoordinateSpace.World,
                lighting, firstCommand.LightingLayer);

        SetTexture(program, "uTexture", TextureUnit.Texture0, texture, 0);
        if (firstCommand.Shader is null) SetTexture(program, "uNormalMap", TextureUnit.Texture1, normalMap, 1);
        var clip = firstCommand.Clip is { } requestedClip ? UIRect.Intersect(viewport, requestedClip) : viewport;
        if (target is not null)
            clip = new UIRect(clip.Position - viewport.Position, clip.Size);
        SetScissor(clip, target is null ? game.Viewport.Y : target.Height);

        openGL.BindVertexArray(vertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        openGL.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)),
            ref MemoryMarshal.GetReference(vertices.AsSpan()), BufferUsageARB.DynamicDraw);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        openGL.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)),
            ref MemoryMarshal.GetReference(indices.AsSpan()), BufferUsageARB.DynamicDraw);
        openGL.DrawElements(PrimitiveType.Triangles, (uint)indices.Length, DrawElementsType.UnsignedInt, (void*)0);
        openGL.BindVertexArray(0);
        openGL.ActiveTexture(TextureUnit.Texture0);
    }

    private void ConfigureLighting(uint program, bool world, LightingSnapshot lighting, uint layer)
    {
        Uniform(program, "uUseLighting", world && lighting.Enabled ? 1 : 0);
        if (!world || !lighting.Enabled) return;
        Uniform(program, "uAmbient", lighting.Ambient);
        Uniform(program, "uLightingLayer", unchecked((int)layer));
        Uniform(program, "uLightCount", lighting.Lights.Count);
        for (var index = 0; index < lighting.Lights.Count; index++)
        {
            var light = lighting.Lights[index];
            Uniform(program, $"uLights[{index}].position", light.Position);
            Uniform(program, $"uLights[{index}].direction", light.Direction);
            Uniform(program, $"uLights[{index}].color", light.Color);
            Uniform(program, $"uLights[{index}].intensity", light.Intensity);
            Uniform(program, $"uLights[{index}].radius", light.Radius);
            Uniform(program, $"uLights[{index}].height", light.Height);
            Uniform(program, $"uLights[{index}].falloff", light.Falloff);
            Uniform(program, $"uLights[{index}].kind", (int)light.Kind);
            Uniform(program, $"uLights[{index}].spotCos", MathF.Cos(light.SpotAngle / 2f));
            Uniform(program, $"uLights[{index}].layerMask", unchecked((int)light.LayerMask));
            Uniform(program, $"uLights[{index}].castsShadows", light.CastShadows ? 1 : 0);
        }
        Uniform(program, "uShadowEdgeCount", lighting.ShadowEdges.Count);
        for (var index = 0; index < lighting.ShadowEdges.Count; index++)
        {
            var edge = lighting.ShadowEdges[index];
            Uniform(program, $"uShadowEdges[{index}]", new Vector4(
                edge.Start.X, edge.Start.Y, edge.End.X, edge.End.Y));
            Uniform(program, $"uShadowMasks[{index}]", unchecked((int)edge.LayerMask));
        }
    }

    public void BeginFrame() { frameCommands = 0; frameSubmissions = 0; frameTriangles = 0; }
    public void EndFrame() => Statistics = new(frameCommands, frameSubmissions, frameTriangles, uploadedTextures.Count);

    private void DrawPostProcessed(IReadOnlyList<SpriteDrawCommand> commands, Camera2D camera,
        LightingSnapshot lighting, IReadOnlyList<PostProcessEffect> effects, RenderTarget? output)
    {
        if (!initialized) Initialize();
        var width = output?.Width ?? Math.Max(1, (int)camera.PixelViewport.Size.X);
        var height = output?.Height ?? Math.Max(1, (int)camera.PixelViewport.Size.Y);
        var targets = GetTargets(camera, width, height);

        openGL.BindFramebuffer(FramebufferTarget.Framebuffer, targets.First.Framebuffer);
        openGL.Viewport(0, 0, (uint)width, (uint)height);
        openGL.Disable(EnableCap.ScissorTest);
        openGL.ClearColor(0f, 0f, 0f, 0f);
        openGL.Clear(ClearBufferMask.ColorBufferBit);
        openGL.Enable(EnableCap.Blend);
        openGL.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
            BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        DrawInternal(commands, camera, lighting, targets.First);
        if (output is not null) Clear(output);

        var source = targets.First;
        for (var index = 0; index < effects.Count; index++)
        {
            var last = index == effects.Count - 1;
            var destination = ReferenceEquals(source, targets.First) ? targets.Second : targets.First;
            openGL.BindFramebuffer(FramebufferTarget.Framebuffer, last ? output?.Framebuffer ?? 0 : destination.Framebuffer);
            if (last)
            {
                if (output is null)
                    SetViewport(camera.PixelViewport, game.Viewport.Y);
                else
                    openGL.Viewport(0, 0, (uint)width, (uint)height);
            }
            else
                openGL.Viewport(0, 0, (uint)width, (uint)height);

            openGL.Disable(EnableCap.ScissorTest);
            if (last && output is null)
            {
                openGL.Enable(EnableCap.Blend);
                openGL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            }
            else
            {
                openGL.Disable(EnableCap.Blend);
            }

            var program = effects[index].Shader;
            program.Use();
            Uniform(program.Handle, "uTexture", 0);
            Uniform(program.Handle, "uResolution", new Vector2(width, height));
            Uniform(program.Handle, "uTexelSize", new Vector2(1f / width, 1f / height));
            openGL.ActiveTexture(TextureUnit.Texture0);
            openGL.BindTexture(TextureTarget.Texture2D, source.Texture);
            openGL.BindVertexArray(vertexArray);
            openGL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            openGL.BindVertexArray(0);
            frameSubmissions++;
            frameTriangles++;
            source = destination;
        }

        RestoreWindowTarget();
    }

    private CameraRenderTargets GetTargets(Camera2D camera, int width, int height)
    {
        if (cameraTargets.TryGetValue(camera, out var targets) &&
            targets.First.Width == width && targets.First.Height == height)
            return targets;

        ReleaseTargets(camera);
        var first = CreateRenderTarget(width, height);
        try
        {
            targets = new(first, CreateRenderTarget(width, height));
        }
        catch
        {
            Delete(first);
            throw;
        }
        cameraTargets.Add(camera, targets);
        return targets;
    }

    private RenderTarget CreateRenderTarget(int width, int height)
    {
        var texture = openGL.GenTexture();
        openGL.BindTexture(TextureTarget.Texture2D, texture);
        openGL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        var framebuffer = openGL.GenFramebuffer();
        openGL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        openGL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, texture, 0);
        if (openGL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            openGL.DeleteFramebuffer(framebuffer);
            openGL.DeleteTexture(texture);
            throw new InvalidOperationException("Failed to create a framebuffer for camera post-processing.");
        }
        return new(framebuffer, texture, width, height);
    }

    private void ReleaseTargets(Camera2D camera)
    {
        if (!cameraTargets.Remove(camera, out var targets)) return;
        Delete(targets.First);
        Delete(targets.Second);
    }

    private void Delete(RenderTarget target)
    {
        openGL.DeleteFramebuffer(target.Framebuffer);
        openGL.DeleteTexture(target.Texture);
    }

    internal void Resize(RenderTexture texture, int width, int height)
    {
        ObjectDisposedException.ThrowIf(texture.IsDisposed, texture);
        ValidateTargetSize(width, height);
        if (!renderTextures.Contains(texture))
            throw new InvalidOperationException("The render texture belongs to a different renderer.");
        if (texture.Width == width && texture.Height == height) return;

        var replacement = CreateRenderTarget(width, height);
        var previous = new RenderTarget(texture.Framebuffer, texture.Texture.Handle, texture.Width, texture.Height);
        texture.Replace(width, height, replacement.Framebuffer, replacement.Texture);
        ApplyFilter(texture.Texture.Handle, texture.Filter);
        Delete(previous);
        RestoreWindowTarget();
    }

    internal void SetFilter(RenderTexture texture, RenderTextureFilter filter)
    {
        ObjectDisposedException.ThrowIf(texture.IsDisposed, texture);
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        if (!renderTextures.Contains(texture))
            throw new InvalidOperationException("The render texture belongs to a different renderer.");
        ApplyFilter(texture.Texture.Handle, filter);
    }

    internal void Release(RenderTexture texture)
    {
        if (!renderTextures.Remove(texture)) return;
        Delete(new RenderTarget(texture.Framebuffer, texture.Texture.Handle, texture.Width, texture.Height));
        texture.MarkDisposed();
    }

    private RenderTarget? GetOutput(Camera2D camera)
    {
        if (camera.RenderTarget is not { } texture) return null;
        ObjectDisposedException.ThrowIf(texture.IsDisposed, texture);
        if (!renderTextures.Contains(texture))
            throw new InvalidOperationException("The camera render target belongs to a different renderer.");
        return new(texture.Framebuffer, texture.Texture.Handle, texture.Width, texture.Height);
    }

    private void Clear(RenderTarget target)
    {
        openGL.BindFramebuffer(FramebufferTarget.Framebuffer, target.Framebuffer);
        openGL.Viewport(0, 0, (uint)target.Width, (uint)target.Height);
        openGL.Disable(EnableCap.ScissorTest);
        openGL.ClearColor(0f, 0f, 0f, 0f);
        openGL.Clear(ClearBufferMask.ColorBufferBit);
    }

    private void RestoreWindowTarget()
    {
        openGL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        openGL.Enable(EnableCap.Blend);
        openGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        openGL.ClearColor(game.ClearColor.R, game.ClearColor.G, game.ClearColor.B, game.ClearColor.A);
    }

    private static void ValidateTargetSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Render texture dimensions must be positive.");
    }

    private void ApplyFilter(uint texture, RenderTextureFilter filter)
    {
        openGL.BindTexture(TextureTarget.Texture2D, texture);
        var min = filter == RenderTextureFilter.Nearest ? TextureMinFilter.Nearest : TextureMinFilter.Linear;
        var mag = filter == RenderTextureFilter.Nearest ? TextureMagFilter.Nearest : TextureMagFilter.Linear;
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)mag);
    }

    private uint GetTextureHandle(Texture2D texture)
    {
        if (texture.Handle != 0) return texture.Handle;
        var pixels = texture.Pixels ?? throw new ObjectDisposedException(nameof(Texture2D),
            "The render texture backing this texture has been disposed.");
        texture.Handle = openGL.GenTexture();
        openGL.BindTexture(TextureTarget.Texture2D, texture.Handle);
        openGL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)texture.Width, (uint)texture.Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, ref pixels[0]);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        uploadedTextures.Add(texture);
        return texture.Handle;
    }

    private void Attribute(uint index, int count, int floatOffset, uint stride)
    {
        openGL.EnableVertexAttribArray(index);
        openGL.VertexAttribPointer(index, count, VertexAttribPointerType.Float, false, stride, (void*)(floatOffset * sizeof(float)));
    }

    private void SetTexture(uint program, string uniform, TextureUnit unit, Texture2D texture, int slot)
    {
        openGL.Uniform1(openGL.GetUniformLocation(program, uniform), slot);
        openGL.ActiveTexture(unit);
        openGL.BindTexture(TextureTarget.Texture2D, GetTextureHandle(texture));
    }

    private void SetViewport(UIRect rectangle, float framebufferHeight)
    {
        var x = (int)rectangle.Position.X;
        var y = (int)(framebufferHeight - rectangle.Position.Y - rectangle.Size.Y);
        openGL.Viewport(x, y, (uint)Math.Max(0, (int)rectangle.Size.X), (uint)Math.Max(0, (int)rectangle.Size.Y));
    }

    private void SetScissor(UIRect rectangle, float framebufferHeight)
    {
        openGL.Enable(EnableCap.ScissorTest);
        var x = (int)rectangle.Position.X;
        var y = (int)(framebufferHeight - rectangle.Position.Y - rectangle.Size.Y);
        openGL.Scissor(x, y, (uint)Math.Max(0, (int)rectangle.Size.X), (uint)Math.Max(0, (int)rectangle.Size.Y));
    }

    private static bool CanBatch(SpriteDrawCommand left, SpriteDrawCommand right) =>
        ReferenceEquals(left.Sprite.Texture, right.Sprite.Texture) &&
        ReferenceEquals(left.Sprite.NormalMap, right.Sprite.NormalMap) &&
        ReferenceEquals(left.Shader, right.Shader) &&
        left.Space == right.Space && left.LightingLayer == right.LightingLayer && left.Clip == right.Clip;

    private static Vector2[] TextureCoordinates(TextureRegion source, Texture2D texture)
    {
        var left = (float)source.X / texture.Width;
        var top = (float)source.Y / texture.Height;
        var right = (float)(source.X + source.Width) / texture.Width;
        var bottom = (float)(source.Y + source.Height) / texture.Height;
        return [new(left, top), new(right, top), new(right, bottom), new(left, bottom)];
    }

    private static void Write(ref int offset, float[] target, params float[] values)
    {
        values.CopyTo(target, offset);
        offset += values.Length;
    }

    private void Uniform(uint program, string name, int value) { var location = openGL.GetUniformLocation(program, name); if (location >= 0) openGL.Uniform1(location, value); }
    private void Uniform(uint program, string name, float value) { var location = openGL.GetUniformLocation(program, name); if (location >= 0) openGL.Uniform1(location, value); }
    private void Uniform(uint program, string name, Vector2 value) { var location = openGL.GetUniformLocation(program, name); if (location >= 0) openGL.Uniform2(location, value.X, value.Y); }
    private void Uniform(uint program, string name, Vector3 value) { var location = openGL.GetUniformLocation(program, name); if (location >= 0) openGL.Uniform3(location, value.X, value.Y, value.Z); }
    private void Uniform(uint program, string name, Vector4 value) { var location = openGL.GetUniformLocation(program, name); if (location >= 0) openGL.Uniform4(location, value.X, value.Y, value.Z, value.W); }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0 || !initialized) return;
        initialized = false;
        if (game.window!.GLContext?.IsCurrent == true)
        {
            foreach (var targets in cameraTargets.Values)
            {
                Delete(targets.First);
                Delete(targets.Second);
            }
            foreach (var texture in renderTextures)
                Delete(new RenderTarget(texture.Framebuffer, texture.Texture.Handle, texture.Width, texture.Height));
            foreach (var texture in uploadedTextures) openGL.DeleteTexture(texture.Handle);
            openGL.DeleteBuffer(vertexBuffer); openGL.DeleteBuffer(indexBuffer); openGL.DeleteVertexArray(vertexArray);
        }
        foreach (var texture in renderTextures) texture.MarkDisposed();
        foreach (var texture in uploadedTextures) texture.Handle = 0;
        renderTextures.Clear();
        uploadedTextures.Clear();
        cameraTargets.Clear();
    }

    private sealed record RenderTarget(uint Framebuffer, uint Texture, int Width, int Height);
    private sealed record CameraRenderTargets(RenderTarget First, RenderTarget Second);
}
