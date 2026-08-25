using Axolotl2D.Lighting;
using Axolotl2D.Shaders;
using Axolotl2D.UI;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using Silk.NET.OpenGL;
using System.Diagnostics;
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

public readonly record struct RenderStatistics(int DrawCommands, int DrawSubmissions, int Triangles, int UploadedTextures)
{
    public int CulledCommands { get; init; }
    public long UploadedVertexBytes { get; init; }
    public double CpuCullingMilliseconds { get; init; }
    public double CpuSortingMilliseconds { get; init; }
    public double CpuVertexBuildMilliseconds { get; init; }
    public double CpuSubmissionMilliseconds { get; init; }
    public double GpuMilliseconds { get; init; }
}

/// <summary>Owns shared GPU buffers and texture uploads for sprite rendering.</summary>
public sealed unsafe class Rendering(Game game) : IRendering
{
    private const int VertexFloatCount = 17;
    private const int InstanceFloatCount = 23;
    private const int MaximumSpritesPerBatch = 16_384;
    private const int StreamSegmentCount = 3;
    private const int VertexStride = VertexFloatCount * sizeof(float);
    private const int StreamSegmentBytes = MaximumSpritesPerBatch * 4 * VertexStride;
    private readonly HashSet<Texture2D> uploadedTextures = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<TextureArray2D> uploadedTextureArrays = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Camera2D, CameraRenderTargets> cameraTargets = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<RenderTexture> renderTextures = new(ReferenceEqualityComparer.Instance);
    private readonly List<SpriteDrawCommand> worldCommands = [];
    private readonly List<SpriteDrawCommand> screenCommands = [];
    private readonly List<SpriteDrawCommand> selectedCommands = [];
    private readonly List<SpriteDrawCommand> orderedCommands = [];
    private readonly List<PostProcessEffect> activeEffects = [];
    private readonly Texture2D flatNormal = new(1, 1, [128, 128, 255, 255]);
    private float[] vertices = [];
    private float[] instances = [];
    private readonly uint[] indices = new uint[MaximumSpritesPerBatch * 6];
    private readonly nint[] streamFences = new nint[StreamSegmentCount];
    private readonly uint[] gpuQueries = new uint[4];
    private readonly bool[] gpuQueryPending = new bool[4];
    private GL openGL = null!;
    private uint vertexArray;
    private uint vertexBuffer;
    private uint indexBuffer;
    private uint instanceVertexArray;
    private uint instanceQuadBuffer;
    private uint instanceBuffer;
    private uint instanceIndexBuffer;
    private ShaderProgram instancedProgram = null!;
    private ShaderProgram instancedArrayProgram = null!;
    private ShaderProgram sdfProgram = null!;
    private BasicUniformLocations basicUniforms = null!;
    private BasicUniformLocations instancedUniforms = null!;
    private BasicUniformLocations instancedArrayUniforms = null!;
    private BasicUniformLocations sdfUniforms = null!;
    private BasicUniformLocations activeUniforms = null!;
    private void* mappedVertices;
    private bool persistentStreaming;
    private int streamSegment;
    private int activeStreamSegment = -1;
    private int activeStreamSpriteOffset;
    private bool initialized;
    private int frameCommands;
    private int frameSubmissions;
    private int frameTriangles;
    private int frameCulledCommands;
    private long frameUploadedVertexBytes;
    private long frameCullingTicks;
    private long frameSortingTicks;
    private long frameVertexBuildTicks;
    private long frameSubmissionTicks;
    private int activeGpuQuery = -1;
    private double gpuMilliseconds;
    private uint boundProgram;
    private uint boundTexture0;
    private uint boundTexture1;
    private bool lightingStateValid;
    private bool boundLightingEnabled;
    private uint boundLightingLayer;
    private bool basicSamplersConfigured;
    private TextureUnit activeTexture = TextureUnit.Texture0;
    private (int X, int Y, uint Width, uint Height)? boundViewport;
    private (int X, int Y, uint Width, uint Height)? boundScissor;
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
        for (var index = 0; index < gpuQueries.Length; index++)
            gpuQueries[index] = openGL.GenQuery();
        openGL.BindVertexArray(vertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);

        for (var sprite = 0; sprite < MaximumSpritesPerBatch; sprite++)
        {
            var first = (uint)sprite * 4;
            var offset = sprite * 6;
            indices[offset] = first;
            indices[offset + 1] = first + 1;
            indices[offset + 2] = first + 2;
            indices[offset + 3] = first;
            indices[offset + 4] = first + 2;
            indices[offset + 5] = first + 3;
        }
        openGL.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)),
            ref indices[0], BufferUsageARB.StaticDraw);

        openGL.GetInteger(GetPName.MajorVersion, out var major);
        openGL.GetInteger(GetPName.MinorVersion, out var minor);
        persistentStreaming = major > 4 || major == 4 && minor >= 4;
        if (persistentStreaming)
        {
            var bytes = (nuint)(StreamSegmentBytes * StreamSegmentCount);
            var storageFlags = BufferStorageMask.MapWriteBit | BufferStorageMask.MapPersistentBit |
                BufferStorageMask.MapCoherentBit;
            openGL.BufferStorage(BufferStorageTarget.ArrayBuffer, bytes, null, storageFlags);
            mappedVertices = openGL.MapBufferRange(BufferTargetARB.ArrayBuffer, 0, bytes,
                MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapPersistentBit |
                MapBufferAccessMask.MapCoherentBit);
            if (mappedVertices == null)
                throw new InvalidOperationException("OpenGL failed to map the persistent sprite vertex buffer.");
        }

        const uint stride = VertexStride;
        Attribute(0, 3, 0, stride);
        Attribute(1, 2, 3, stride);
        Attribute(2, 4, 5, stride);
        Attribute(3, 2, 9, stride);
        Attribute(4, 2, 11, stride);
        Attribute(5, 2, 13, stride);
        Attribute(6, 2, 15, stride);
        openGL.BindVertexArray(0);
        basicUniforms = new(openGL, game.shaderProgramPointer);
        InitializeInstancing();
        initialized = true;
    }

    private void InitializeInstancing()
    {
        var fragmentSource = ReadEmbeddedShader("Axolotl2D.Shaders.BasicFragment.glsl");
        instancedProgram = new ShaderProgram(openGL, InstancedVertexShader, fragmentSource);
        var arrayFragment = fragmentSource
            .Replace("uniform sampler2D uTexture;", "uniform sampler2DArray uTexture;")
            .Replace("uniform sampler2D uNormalMap;", "uniform sampler2DArray uNormalMap;")
            .Replace("in vec2 frag_texCoords;", "in vec2 frag_texCoords;\nflat in float frag_textureLayer;")
            .Replace("texture(uTexture, frag_texCoords)", "texture(uTexture, vec3(frag_texCoords, frag_textureLayer))")
            .Replace("texture(uNormalMap, frag_normalTexCoords)", "texture(uNormalMap, vec3(frag_normalTexCoords, frag_textureLayer))");
        instancedArrayProgram = new ShaderProgram(openGL, InstancedVertexShader, arrayFragment);
        sdfProgram = new ShaderProgram(openGL, InstancedVertexShader, SdfFragmentShader);
        instancedUniforms = new(openGL, instancedProgram.Handle);
        instancedArrayUniforms = new(openGL, instancedArrayProgram.Handle);
        sdfUniforms = new(openGL, sdfProgram.Handle);

        instanceVertexArray = openGL.GenVertexArray();
        instanceQuadBuffer = openGL.GenBuffer();
        instanceBuffer = openGL.GenBuffer();
        instanceIndexBuffer = openGL.GenBuffer();
        openGL.BindVertexArray(instanceVertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, instanceQuadBuffer);
        float[] quad = [0f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 1f, 1f, 1f, 1f, 0f, 1f, 0f, 1f];
        openGL.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), ref quad[0], BufferUsageARB.StaticDraw);
        Attribute(0, 4, 0, 4 * sizeof(float));

        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, instanceBuffer);
        const uint stride = InstanceFloatCount * sizeof(float);
        InstanceAttribute(1, 4, 0, stride);
        InstanceAttribute(2, 2, 4, stride);
        InstanceAttribute(3, 2, 6, stride);
        InstanceAttribute(4, 2, 8, stride);
        InstanceAttribute(5, 4, 10, stride);
        InstanceAttribute(6, 4, 14, stride);
        InstanceAttribute(7, 4, 18, stride);
        InstanceAttribute(8, 1, 22, stride);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, instanceIndexBuffer);
        uint[] quadIndices = [0, 1, 2, 0, 2, 3];
        openGL.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(quadIndices.Length * sizeof(uint)),
            ref quadIndices[0], BufferUsageARB.StaticDraw);
        openGL.BindVertexArray(0);
    }

    internal void Draw(IReadOnlyList<SpriteDrawCommand> commands, Camera2D camera, LightingSnapshot lighting,
        bool includeWorld, bool includeScreen)
    {
        worldCommands.Clear();
        screenCommands.Clear();
        activeEffects.Clear();
        var cullingStarted = Stopwatch.GetTimestamp();
        var visible = camera.VisibleWorldBounds;
        var visibleMinimum = visible.Min;
        var visibleMaximum = visible.Max;
        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            if (includeWorld && command.Space == CoordinateSpace.World &&
                (command.LightingLayer & camera.CullingMask) != 0)
            {
                if (IsVisible(command, visibleMinimum, visibleMaximum))
                    worldCommands.Add(command);
                else
                    frameCulledCommands++;
            }
            else if (includeScreen && command.Space == CoordinateSpace.Screen)
                screenCommands.Add(command);
        }
        frameCullingTicks += Stopwatch.GetTimestamp() - cullingStarted;
        foreach (var effect in camera.PostProcessEffects)
            if (effect.Enabled && !effect.IsDisposed)
                activeEffects.Add(effect);
        var output = GetOutput(camera);

        if (activeEffects.Count == 0)
        {
            ReleaseTargets(camera);
            if (output is not null) Clear(output);
            if (screenCommands.Count == 0)
                DrawInternal(worldCommands, camera, lighting, output);
            else if (worldCommands.Count == 0)
                DrawInternal(screenCommands, camera, lighting, output);
            else
            {
                selectedCommands.Clear();
                selectedCommands.AddRange(worldCommands);
                selectedCommands.AddRange(screenCommands);
                DrawInternal(selectedCommands, camera, lighting, output);
            }
            if (output is not null) RestoreWindowTarget();
            return;
        }

        DrawPostProcessed(worldCommands, camera, lighting, activeEffects, output);
        DrawInternal(screenCommands, null, new(false, Vector3.One, [], []));
    }

    internal void DrawScreen(IReadOnlyList<SpriteDrawCommand> commands)
    {
        selectedCommands.Clear();
        for (var index = 0; index < commands.Count; index++)
            if (commands[index].Space == CoordinateSpace.Screen)
                selectedCommands.Add(commands[index]);
        DrawInternal(selectedCommands, null, new(false, Vector3.One, [], []));
    }

    private void DrawInternal(IReadOnlyList<SpriteDrawCommand> commands, Camera2D? camera, LightingSnapshot lighting,
        RenderTarget? target = null)
    {
        if (!initialized) Initialize();
        if (commands.Count == 0) return;
        frameCommands += commands.Count;

        var sortingStarted = Stopwatch.GetTimestamp();
        orderedCommands.Clear();
        var drawCommands = commands;
        if (!IsOrdered(commands))
        {
            orderedCommands.AddRange(commands);
            orderedCommands.Sort(SpriteDrawCommandComparer.Instance);
            drawCommands = orderedCommands;
        }
        frameSortingTicks += Stopwatch.GetTimestamp() - sortingStarted;
        ResetCachedState();
        openGL.BindVertexArray(vertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        openGL.Enable(EnableCap.ScissorTest);
        try
        {
            var batchStart = 0;
            for (var index = 1; index < drawCommands.Count; index++)
            {
                if (!CanBatch(drawCommands[index - 1], drawCommands[index]))
                {
                    FlushBatch(drawCommands, batchStart, index - batchStart, camera, lighting, target);
                    batchStart = index;
                }
            }
            FlushBatch(drawCommands, batchStart, drawCommands.Count - batchStart, camera, lighting, target);
        }
        finally
        {
            openGL.BindVertexArray(0);
            ActivateTexture(TextureUnit.Texture0);
            openGL.Disable(EnableCap.ScissorTest);
        }
    }

    private void FlushBatch(IReadOnlyList<SpriteDrawCommand> commands, int start, int count,
        Camera2D? camera, LightingSnapshot lighting, RenderTarget? target)
    {
        while (count > 0)
        {
            var chunk = Math.Min(count, MaximumSpritesPerBatch);
            Flush(commands, start, chunk, camera, lighting, target);
            start += chunk;
            count -= chunk;
        }
    }

    private void Flush(IReadOnlyList<SpriteDrawCommand> commands, int start, int count,
        Camera2D? camera, LightingSnapshot lighting, RenderTarget? target)
    {
        if (commands[start].Shader is null)
        {
            FlushInstanced(commands, start, count, camera, lighting, target);
            return;
        }
        frameSubmissions++;
        frameTriangles += count * 2;
        var firstCommand = commands[start];
        var texture = firstCommand.Sprite.Texture;
        var normalMap = firstCommand.Sprite.NormalMap ?? flatNormal;
        var viewport = firstCommand.Space == CoordinateSpace.World && camera is not null
            ? camera.PixelViewport
            : new UIRect(Vector2.Zero, game.Viewport);
        if (target is null)
            SetViewport(viewport, game.Viewport.Y);
        else
            SetViewport(0, 0, (uint)target.Width, (uint)target.Height);

        var vertexByteCount = count * 4 * VertexStride;
        Span<float> vertexData;
        var usedStreamSegment = -1;
        var streamSpriteOffset = 0;
        if (persistentStreaming)
        {
            ReserveStream(count, out usedStreamSegment, out streamSpriteOffset);
            vertexData = new Span<float>((byte*)mappedVertices + usedStreamSegment * StreamSegmentBytes +
                streamSpriteOffset * 4 * VertexStride,
                count * 4 * VertexFloatCount);
        }
        else
        {
            EnsureBatchCapacity(count);
            vertexData = vertices.AsSpan(0, count * 4 * VertexFloatCount);
        }
        var vertexBuildStarted = Stopwatch.GetTimestamp();
        var vertexOffset = 0;
        var worldToScreen = camera?.WorldToScreenMatrix ?? Matrix3x2.Identity;
        var ndcScale = new Vector2(2f / viewport.Size.X, 2f / viewport.Size.Y);

        for (var i = 0; i < count; i++)
        {
            var command = commands[start + i];
            var sprite = command.Sprite;
            var size = sprite.Size;
            var origin = sprite.Origin * size;
            var colorUvs = TextureCoordinates(sprite.Source, texture);
            var normalUvs = TextureCoordinates(sprite.Source, normalMap);
            var tangent = Vector2.TransformNormal(Vector2.UnitX, command.Transform);
            var bitangent = Vector2.TransformNormal(Vector2.UnitY, command.Transform);
            tangent = tangent.LengthSquared() > 0f ? Vector2.Normalize(tangent) : Vector2.UnitX;
            bitangent = bitangent.LengthSquared() > 0f ? Vector2.Normalize(bitangent) : Vector2.UnitY;

            for (var corner = 0; corner < 4; corner++)
            {
                var local = corner switch
                {
                    0 => new Vector2(-origin.X, -origin.Y),
                    1 => new Vector2(size.X - origin.X, -origin.Y),
                    2 => new Vector2(size.X - origin.X, size.Y - origin.Y),
                    _ => new Vector2(-origin.X, size.Y - origin.Y)
                };
                var world = Vector2.Transform(local, command.Transform);
                var screen = command.Space == CoordinateSpace.World
                    ? Vector2.Transform(world, worldToScreen)
                    : world;
                var relative = screen - viewport.Position;
                var ndc = new Vector2(relative.X * ndcScale.X - 1f, 1f - relative.Y * ndcScale.Y);
                Write(vertexData, ref vertexOffset,
                    ndc.X, ndc.Y, 0f,
                    corner is 0 or 3 ? colorUvs.Left : colorUvs.Right,
                    corner < 2 ? colorUvs.Top : colorUvs.Bottom,
                    command.Tint.R, command.Tint.G, command.Tint.B, command.Tint.A,
                    world.X, world.Y,
                    tangent.X, tangent.Y,
                    bitangent.X, bitangent.Y,
                    corner is 0 or 3 ? normalUvs.Left : normalUvs.Right,
                    corner < 2 ? normalUvs.Top : normalUvs.Bottom);
            }

        }
        frameVertexBuildTicks += Stopwatch.GetTimestamp() - vertexBuildStarted;

        var submissionStarted = Stopwatch.GetTimestamp();
        openGL.BindVertexArray(vertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        var program = firstCommand.Shader?.Handle ?? game.shaderProgramPointer;
        UseProgram(program, firstCommand.Shader);
        activeUniforms = basicUniforms;
        if (firstCommand.Shader is null)
        {
            var lightingEnabled = camera is not null && firstCommand.Space == CoordinateSpace.World && lighting.Enabled;
            if (!lightingStateValid || boundLightingEnabled != lightingEnabled ||
                lightingEnabled && boundLightingLayer != firstCommand.LightingLayer)
            {
                ConfigureLighting(camera is not null && firstCommand.Space == CoordinateSpace.World,
                    lighting, firstCommand.LightingLayer);
                lightingStateValid = true;
                boundLightingEnabled = lightingEnabled;
                boundLightingLayer = firstCommand.LightingLayer;
            }
        }

        if (firstCommand.Shader is null)
        {
            if (!basicSamplersConfigured)
            {
                Uniform(basicUniforms.Texture, 0);
                Uniform(basicUniforms.NormalMap, 1);
                basicSamplersConfigured = true;
            }
            SetTexture(TextureUnit.Texture0, texture);
            SetTexture(TextureUnit.Texture1, normalMap);
        }
        else
            SetTexture(firstCommand.Shader.FindLocation("uTexture"), TextureUnit.Texture0, texture, 0);
        var clip = firstCommand.Clip is { } requestedClip ? UIRect.Intersect(viewport, requestedClip) : viewport;
        if (target is not null)
            clip = new UIRect(clip.Position - viewport.Position, clip.Size);
        SetScissor(clip, target is null ? game.Viewport.Y : target.Height);

        if (persistentStreaming)
        {
            openGL.DrawElementsBaseVertex(PrimitiveType.Triangles, (uint)(count * 6),
                DrawElementsType.UnsignedInt, (void*)0,
                (usedStreamSegment * MaximumSpritesPerBatch + streamSpriteOffset) * 4);
            activeStreamSpriteOffset += count;
        }
        else
        {
            openGL.BufferData(BufferTargetARB.ArrayBuffer, (nuint)vertexByteCount, null, BufferUsageARB.StreamDraw);
            openGL.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)vertexByteCount,
                ref MemoryMarshal.GetReference(vertexData));
            openGL.DrawElements(PrimitiveType.Triangles, (uint)(count * 6), DrawElementsType.UnsignedInt, (void*)0);
        }
        frameUploadedVertexBytes += vertexByteCount;
        frameSubmissionTicks += Stopwatch.GetTimestamp() - submissionStarted;
    }

    private void FlushInstanced(IReadOnlyList<SpriteDrawCommand> commands, int start, int count,
        Camera2D? camera, LightingSnapshot lighting, RenderTarget? target)
    {
        frameSubmissions++;
        frameTriangles += count * 2;
        var first = commands[start];
        var array = first.Sprite.TextureArray;
        var viewport = first.Space == CoordinateSpace.World && camera is not null
            ? camera.PixelViewport
            : new UIRect(Vector2.Zero, game.Viewport);
        if (target is null) SetViewport(viewport, game.Viewport.Y);
        else SetViewport(0, 0, (uint)target.Width, (uint)target.Height);

        EnsureInstanceCapacity(count);
        var data = instances.AsSpan(0, count * InstanceFloatCount);
        var buildStarted = Stopwatch.GetTimestamp();
        var offset = 0;
        for (var index = 0; index < count; index++)
        {
            var command = commands[start + index];
            var sprite = command.Sprite;
            var transform = command.Transform;
            var colorUvs = TextureCoordinates(sprite.Source, array?.Width ?? sprite.Texture.Width,
                array?.Height ?? sprite.Texture.Height);
            var normal = sprite.NormalMap ?? flatNormal;
            var normalUvs = array is null
                ? TextureCoordinates(sprite.Source, normal.Width, normal.Height)
                : colorUvs;
            WriteInstance(data, ref offset, sprite.Size, sprite.Origin,
                transform, command.Tint, colorUvs, normalUvs, sprite.TextureLayer);
        }
        frameVertexBuildTicks += Stopwatch.GetTimestamp() - buildStarted;

        var submitStarted = Stopwatch.GetTimestamp();
        var program = first.Sprite.IsSdf ? sdfProgram : array is null ? instancedProgram : instancedArrayProgram;
        activeUniforms = first.Sprite.IsSdf ? sdfUniforms : array is null ? instancedUniforms : instancedArrayUniforms;
        UseProgram(program.Handle, program);
        var worldToScreen = first.Space == CoordinateSpace.World && camera is not null
            ? camera.WorldToScreenMatrix : Matrix3x2.Identity;
        Uniform(activeUniforms.CameraX, new Vector2(worldToScreen.M11, worldToScreen.M12));
        Uniform(activeUniforms.CameraY, new Vector2(worldToScreen.M21, worldToScreen.M22));
        Uniform(activeUniforms.CameraTranslation, new Vector2(worldToScreen.M31, worldToScreen.M32));
        Uniform(activeUniforms.ViewportPosition, viewport.Position);
        Uniform(activeUniforms.ViewportSize, viewport.Size);
        Uniform(activeUniforms.Texture, 0);
        Uniform(activeUniforms.NormalMap, 1);

        var lightingEnabled = camera is not null && first.Space == CoordinateSpace.World && lighting.Enabled;
        if (!lightingStateValid || boundLightingEnabled != lightingEnabled ||
            lightingEnabled && boundLightingLayer != first.LightingLayer)
        {
            ConfigureLighting(camera is not null && first.Space == CoordinateSpace.World, lighting, first.LightingLayer);
            lightingStateValid = true;
            boundLightingEnabled = lightingEnabled;
            boundLightingLayer = first.LightingLayer;
        }

        if (array is null)
        {
            SetTexture(TextureUnit.Texture0, first.Sprite.Texture);
            SetTexture(TextureUnit.Texture1, first.Sprite.NormalMap ?? flatNormal);
        }
        else
            SetTextureArray(array);

        var clip = first.Clip is { } requestedClip ? UIRect.Intersect(viewport, requestedClip) : viewport;
        if (target is not null) clip = new UIRect(clip.Position - viewport.Position, clip.Size);
        SetScissor(clip, target is null ? game.Viewport.Y : target.Height);
        openGL.BindVertexArray(instanceVertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, instanceBuffer);
        var byteCount = count * InstanceFloatCount * sizeof(float);
        openGL.BufferData(BufferTargetARB.ArrayBuffer, (nuint)byteCount, null, BufferUsageARB.StreamDraw);
        openGL.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)byteCount, ref MemoryMarshal.GetReference(data));
        openGL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
        frameUploadedVertexBytes += byteCount;
        frameSubmissionTicks += Stopwatch.GetTimestamp() - submitStarted;
    }

    private void ConfigureLighting(bool world, LightingSnapshot lighting, uint layer)
    {
        var uniforms = activeUniforms;
        Uniform(uniforms.UseLighting, world && lighting.Enabled ? 1 : 0);
        if (!world || !lighting.Enabled) return;
        Uniform(uniforms.Ambient, lighting.Ambient);
        Uniform(uniforms.LightingLayer, unchecked((int)layer));
        Uniform(uniforms.LightCount, lighting.Lights.Count);
        for (var index = 0; index < lighting.Lights.Count; index++)
        {
            var light = lighting.Lights[index];
            var locations = uniforms.Lights[index];
            Uniform(locations.Position, light.Position);
            Uniform(locations.Direction, light.Direction);
            Uniform(locations.Color, light.Color);
            Uniform(locations.Intensity, light.Intensity);
            Uniform(locations.Radius, light.Radius);
            Uniform(locations.Height, light.Height);
            Uniform(locations.Falloff, light.Falloff);
            Uniform(locations.Kind, (int)light.Kind);
            Uniform(locations.SpotCos, MathF.Cos(light.SpotAngle / 2f));
            Uniform(locations.LayerMask, unchecked((int)light.LayerMask));
            Uniform(locations.CastsShadows, light.CastShadows ? 1 : 0);
        }
        Uniform(uniforms.ShadowEdgeCount, lighting.ShadowEdges.Count);
        for (var index = 0; index < lighting.ShadowEdges.Count; index++)
        {
            var edge = lighting.ShadowEdges[index];
            Uniform(uniforms.ShadowEdges[index], new Vector4(
                edge.Start.X, edge.Start.Y, edge.End.X, edge.End.Y));
            Uniform(uniforms.ShadowMasks[index], unchecked((int)edge.LayerMask));
        }
    }

    public void BeginFrame()
    {
        PollGpuQueries();
        for (var index = 0; index < gpuQueries.Length; index++)
            if (!gpuQueryPending[index])
            {
                activeGpuQuery = index;
                openGL.BeginQuery(QueryTarget.TimeElapsed, gpuQueries[index]);
                break;
            }
        frameCommands = 0;
        frameSubmissions = 0;
        frameTriangles = 0;
        frameCulledCommands = 0;
        frameUploadedVertexBytes = 0;
        frameCullingTicks = 0;
        frameSortingTicks = 0;
        frameVertexBuildTicks = 0;
        frameSubmissionTicks = 0;
    }

    public void EndFrame()
    {
        if (activeGpuQuery >= 0)
        {
            openGL.EndQuery(QueryTarget.TimeElapsed);
            gpuQueryPending[activeGpuQuery] = true;
            activeGpuQuery = -1;
        }
        if (persistentStreaming) FenceActiveStreamSegment();
        Statistics = new(frameCommands, frameSubmissions, frameTriangles,
            uploadedTextures.Count + uploadedTextureArrays.Count * 2)
        {
            CulledCommands = frameCulledCommands,
            UploadedVertexBytes = frameUploadedVertexBytes,
            CpuCullingMilliseconds = Milliseconds(frameCullingTicks),
            CpuSortingMilliseconds = Milliseconds(frameSortingTicks),
            CpuVertexBuildMilliseconds = Milliseconds(frameVertexBuildTicks),
            CpuSubmissionMilliseconds = Milliseconds(frameSubmissionTicks),
            GpuMilliseconds = gpuMilliseconds
        };
    }

    private void PollGpuQueries()
    {
        for (var index = 0; index < gpuQueries.Length; index++)
        {
            if (!gpuQueryPending[index]) continue;
            openGL.GetQueryObject(gpuQueries[index], QueryObjectParameterName.QueryResultAvailable, out int available);
            if (available == 0) continue;
            var nanoseconds = openGL.GetQueryObject(gpuQueries[index], QueryObjectParameterName.QueryResult);
            gpuMilliseconds = nanoseconds / 1_000_000d;
            gpuQueryPending[index] = false;
        }
    }

    private static double Milliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;

    private void DrawPostProcessed(IReadOnlyList<SpriteDrawCommand> commands, Camera2D camera,
        LightingSnapshot lighting, IReadOnlyList<PostProcessEffect> effects, RenderTarget? output)
    {
        if (!initialized) Initialize();
        var width = output?.Width ?? Math.Max(1, (int)camera.PixelViewport.Size.X);
        var height = output?.Height ?? Math.Max(1, (int)camera.PixelViewport.Size.Y);
        var targets = GetTargets(camera, width, height);

        openGL.BindFramebuffer(FramebufferTarget.Framebuffer, targets.First.Framebuffer);
        SetViewport(0, 0, (uint)width, (uint)height);
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
                    SetViewport(0, 0, (uint)width, (uint)height);
            }
            else
                SetViewport(0, 0, (uint)width, (uint)height);

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
            UseProgram(program.Handle, program);
            Uniform(program.FindLocation("uTexture"), 0);
            Uniform(program.FindLocation("uResolution"), new Vector2(width, height));
            Uniform(program.FindLocation("uTexelSize"), new Vector2(1f / width, 1f / height));
            BindTexture(TextureUnit.Texture0, source.Texture);
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
        SetViewport(0, 0, (uint)target.Width, (uint)target.Height);
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
        var pixels = texture.PixelSpan;
        if (pixels.IsEmpty)
            throw new ObjectDisposedException(nameof(Texture2D),
                "The render texture backing this texture has been disposed.");
        texture.Handle = openGL.GenTexture();
        openGL.BindTexture(TextureTarget.Texture2D, texture.Handle);
        if (activeTexture == TextureUnit.Texture0) boundTexture0 = texture.Handle;
        else boundTexture1 = texture.Handle;
        openGL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)texture.Width, (uint)texture.Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, ref MemoryMarshal.GetReference(pixels));
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        uploadedTextures.Add(texture);
        texture.ReleaseOwnedPixels();
        return texture.Handle;
    }

    private void SetTextureArray(TextureArray2D texture)
    {
        if (texture.Handle == 0)
        {
            texture.Handle = UploadTextureArray(texture.Pixels, texture.Width, texture.Height, texture.Layers,
                TextureUnit.Texture0);
            texture.NormalHandle = UploadTextureArray(texture.NormalPixels, texture.Width, texture.Height,
                texture.Layers, TextureUnit.Texture1);
            uploadedTextureArrays.Add(texture);
            texture.ReleasePixels();
        }
        ActivateTexture(TextureUnit.Texture0);
        openGL.BindTexture(TextureTarget.Texture2DArray, texture.Handle);
        ActivateTexture(TextureUnit.Texture1);
        openGL.BindTexture(TextureTarget.Texture2DArray, texture.NormalHandle);
        boundTexture0 = 0;
        boundTexture1 = 0;
    }

    private uint UploadTextureArray(ReadOnlySpan<byte> pixels, int width, int height, int layers, TextureUnit unit)
    {
        if (pixels.IsEmpty) throw new ObjectDisposedException(nameof(TextureArray2D));
        ActivateTexture(unit);
        var handle = openGL.GenTexture();
        openGL.BindTexture(TextureTarget.Texture2DArray, handle);
        openGL.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba, (uint)width, (uint)height,
            (uint)layers, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref MemoryMarshal.GetReference(pixels));
        openGL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        openGL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        return handle;
    }

    private void UploadDirtyTexture(Texture2D texture, TextureUnit unit)
    {
        if (!texture.TryTakeDirtyRegion(out var dirty)) return;
        ActivateTexture(unit);
        var pixels = texture.PixelSpan;
        if (pixels.IsEmpty)
            throw new InvalidOperationException("A mutable texture must retain its CPU pixel data.");
        try
        {
            using var upload = MemoryOwner<byte>.Allocate(checked(dirty.Width * dirty.Height * 4));
            var source = pixels.AsSpan2D(texture.Height, texture.Width * 4)
                .Slice(dirty.Y, dirty.X * 4, dirty.Height, dirty.Width * 4);
            source.CopyTo(upload.Span.AsSpan2D(dirty.Height, dirty.Width * 4));
            openGL.TexSubImage2D(TextureTarget.Texture2D, 0, dirty.X, dirty.Y, (uint)dirty.Width, (uint)dirty.Height,
                PixelFormat.Rgba, PixelType.UnsignedByte, ref upload.Span[0]);
        }
        catch
        {
            texture.MarkDirty(dirty);
            throw;
        }
    }

    private void Attribute(uint index, int count, int floatOffset, uint stride)
    {
        openGL.EnableVertexAttribArray(index);
        openGL.VertexAttribPointer(index, count, VertexAttribPointerType.Float, false, stride, (void*)(floatOffset * sizeof(float)));
    }

    private void InstanceAttribute(uint index, int count, int floatOffset, uint stride)
    {
        Attribute(index, count, floatOffset, stride);
        openGL.VertexAttribDivisor(index, 1);
    }

    private void SetTexture(int location, TextureUnit unit, Texture2D texture, int slot)
    {
        if (location >= 0) openGL.Uniform1(location, slot);
        SetTexture(unit, texture);
    }

    private void SetTexture(TextureUnit unit, Texture2D texture)
    {
        if (texture.Handle == 0) ActivateTexture(unit);
        BindTexture(unit, GetTextureHandle(texture));
        UploadDirtyTexture(texture, unit);
    }

    private void SetViewport(UIRect rectangle, float framebufferHeight)
    {
        var x = (int)rectangle.Position.X;
        var y = (int)(framebufferHeight - rectangle.Position.Y - rectangle.Size.Y);
        SetViewport(x, y, (uint)Math.Max(0, (int)rectangle.Size.X), (uint)Math.Max(0, (int)rectangle.Size.Y));
    }

    private void SetViewport(int x, int y, uint width, uint height)
    {
        var state = (x, y, width, height);
        if (boundViewport == state) return;
        openGL.Viewport(x, y, width, height);
        boundViewport = state;
    }

    private void SetScissor(UIRect rectangle, float framebufferHeight)
    {
        var x = (int)rectangle.Position.X;
        var y = (int)(framebufferHeight - rectangle.Position.Y - rectangle.Size.Y);
        var state = (X: x, Y: y, Width: (uint)Math.Max(0, (int)rectangle.Size.X),
            Height: (uint)Math.Max(0, (int)rectangle.Size.Y));
        if (boundScissor == state) return;
        openGL.Scissor(state.X, state.Y, state.Width, state.Height);
        boundScissor = state;
    }

    private static bool CanBatch(SpriteDrawCommand left, SpriteDrawCommand right) =>
        left.Sprite.IsSdf == right.Sprite.IsSdf &&
        ReferenceEquals(left.Sprite.TextureArray, right.Sprite.TextureArray) &&
        (left.Shader is null && left.Sprite.TextureArray is not null ||
            ReferenceEquals(left.Sprite.Texture, right.Sprite.Texture)) &&
        (left.Shader is null && left.Sprite.TextureArray is not null ||
            ReferenceEquals(left.Sprite.NormalMap, right.Sprite.NormalMap)) &&
        ReferenceEquals(left.Shader, right.Shader) &&
        left.Space == right.Space && left.LightingLayer == right.LightingLayer && left.Clip == right.Clip;

    private void EnsureBatchCapacity(int spriteCount)
    {
        var vertexCount = spriteCount * 4 * VertexFloatCount;
        if (vertices.Length < vertexCount)
            Array.Resize(ref vertices, Math.Max(vertexCount, Math.Max(256, vertices.Length * 2)));
    }

    private void EnsureInstanceCapacity(int spriteCount)
    {
        var count = spriteCount * InstanceFloatCount;
        if (instances.Length < count)
            Array.Resize(ref instances, Math.Max(count, Math.Max(256, instances.Length * 2)));
    }

    private static bool IsVisible(SpriteDrawCommand command, Vector2 visibleMinimum, Vector2 visibleMaximum)
    {
        var size = command.Sprite.Size;
        var origin = command.Sprite.Origin * size;
        var localExtent = size / 2f;
        var transform = command.Transform;
        var center = Vector2.Transform(localExtent - origin, transform);
        var worldExtent = new Vector2(
            MathF.Abs(transform.M11) * localExtent.X + MathF.Abs(transform.M21) * localExtent.Y,
            MathF.Abs(transform.M12) * localExtent.X + MathF.Abs(transform.M22) * localExtent.Y);
        var minimum = center - worldExtent;
        var maximum = center + worldExtent;
        return maximum.X >= visibleMinimum.X && minimum.X <= visibleMaximum.X &&
            maximum.Y >= visibleMinimum.Y && minimum.Y <= visibleMaximum.Y;
    }

    private static bool IsOrdered(IReadOnlyList<SpriteDrawCommand> commands)
    {
        for (var index = 1; index < commands.Count; index++)
            if (SpriteDrawCommandComparer.Instance.Compare(commands[index - 1], commands[index]) > 0)
                return false;
        return true;
    }

    private static TextureUv TextureCoordinates(TextureRegion source, Texture2D texture)
        => TextureCoordinates(source, texture.Width, texture.Height);

    private static TextureUv TextureCoordinates(TextureRegion source, int width, int height)
    {
        var left = (float)source.X / width;
        var top = (float)source.Y / height;
        var right = (float)(source.X + source.Width) / width;
        var bottom = (float)(source.Y + source.Height) / height;
        return new(left, top, right, bottom);
    }

    private static void WriteInstance(Span<float> target, ref int offset, Vector2 size, Vector2 origin,
        Matrix3x2 transform, Color tint, TextureUv colorUv, TextureUv normalUv, int layer)
    {
        target[offset++] = size.X; target[offset++] = size.Y;
        target[offset++] = origin.X; target[offset++] = origin.Y;
        target[offset++] = transform.M11; target[offset++] = transform.M12;
        target[offset++] = transform.M21; target[offset++] = transform.M22;
        target[offset++] = transform.M31; target[offset++] = transform.M32;
        target[offset++] = tint.R; target[offset++] = tint.G; target[offset++] = tint.B; target[offset++] = tint.A;
        target[offset++] = colorUv.Left; target[offset++] = colorUv.Top;
        target[offset++] = colorUv.Right; target[offset++] = colorUv.Bottom;
        target[offset++] = normalUv.Left; target[offset++] = normalUv.Top;
        target[offset++] = normalUv.Right; target[offset++] = normalUv.Bottom;
        target[offset++] = layer;
    }

    private static void Write(Span<float> target, ref int offset,
        float x, float y, float z, float u, float v,
        float r, float g, float b, float a,
        float worldX, float worldY,
        float tangentX, float tangentY,
        float bitangentX, float bitangentY,
        float normalU, float normalV)
    {
        target[offset++] = x;
        target[offset++] = y;
        target[offset++] = z;
        target[offset++] = u;
        target[offset++] = v;
        target[offset++] = r;
        target[offset++] = g;
        target[offset++] = b;
        target[offset++] = a;
        target[offset++] = worldX;
        target[offset++] = worldY;
        target[offset++] = tangentX;
        target[offset++] = tangentY;
        target[offset++] = bitangentX;
        target[offset++] = bitangentY;
        target[offset++] = normalU;
        target[offset++] = normalV;
    }

    private int AcquireStreamSegment()
    {
        var fence = streamFences[streamSegment];
        if (fence == 0) return streamSegment;

        GLEnum result;
        do
        {
            result = openGL.ClientWaitSync(fence, SyncObjectMask.SyncFlushCommandsBit, 1_000_000_000);
        }
        while (result == GLEnum.TimeoutExpired);
        openGL.DeleteSync(fence);
        streamFences[streamSegment] = 0;
        if (result == GLEnum.WaitFailed)
            throw new InvalidOperationException("OpenGL failed while waiting for the sprite streaming buffer.");
        return streamSegment;
    }

    private void ReserveStream(int spriteCount, out int segment, out int spriteOffset)
    {
        if (activeStreamSegment < 0)
            activeStreamSegment = AcquireStreamSegment();
        if (activeStreamSpriteOffset + spriteCount > MaximumSpritesPerBatch)
        {
            FenceActiveStreamSegment();
            activeStreamSegment = AcquireStreamSegment();
        }
        segment = activeStreamSegment;
        spriteOffset = activeStreamSpriteOffset;
    }

    private void FenceActiveStreamSegment()
    {
        if (activeStreamSegment < 0) return;
        if (activeStreamSpriteOffset > 0)
        {
            var fence = openGL.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
            if (fence == 0)
                throw new InvalidOperationException("OpenGL failed to fence the sprite streaming buffer.");
            streamFences[activeStreamSegment] = fence;
            streamSegment = (activeStreamSegment + 1) % StreamSegmentCount;
        }
        activeStreamSegment = -1;
        activeStreamSpriteOffset = 0;
    }

    private void UseProgram(uint handle, ShaderProgram? program)
    {
        if (boundProgram == handle) return;
        if (program is null) openGL.UseProgram(handle); else program.Use();
        boundProgram = handle;
        lightingStateValid = false;
    }

    private void BindTexture(TextureUnit unit, uint texture)
    {
        ref var bound = ref (unit == TextureUnit.Texture0 ? ref boundTexture0 : ref boundTexture1);
        if (bound == texture) return;
        ActivateTexture(unit);
        openGL.BindTexture(TextureTarget.Texture2D, texture);
        bound = texture;
    }

    private void ActivateTexture(TextureUnit unit)
    {
        if (activeTexture == unit) return;
        openGL.ActiveTexture(unit);
        activeTexture = unit;
    }

    private void ResetCachedState()
    {
        boundProgram = 0;
        boundTexture0 = 0;
        boundTexture1 = 0;
        lightingStateValid = false;
        boundViewport = null;
        boundScissor = null;
        activeTexture = TextureUnit.Texture0;
        openGL.ActiveTexture(TextureUnit.Texture0);
    }

    private static string ReadEmbeddedShader(string name)
    {
        using var stream = typeof(Rendering).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded shader '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void Uniform(int location, int value) { if (location >= 0) openGL.Uniform1(location, value); }
    private void Uniform(int location, float value) { if (location >= 0) openGL.Uniform1(location, value); }
    private void Uniform(int location, Vector2 value) { if (location >= 0) openGL.Uniform2(location, value.X, value.Y); }
    private void Uniform(int location, Vector3 value) { if (location >= 0) openGL.Uniform3(location, value.X, value.Y, value.Z); }
    private void Uniform(int location, Vector4 value) { if (location >= 0) openGL.Uniform4(location, value.X, value.Y, value.Z, value.W); }

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
            foreach (var texture in uploadedTextureArrays)
            {
                openGL.DeleteTexture(texture.Handle);
                openGL.DeleteTexture(texture.NormalHandle);
                texture.Handle = 0;
                texture.NormalHandle = 0;
            }
            if (persistentStreaming)
            {
                foreach (var fence in streamFences)
                    if (fence != 0)
                        openGL.DeleteSync(fence);
                openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
                openGL.UnmapBuffer(BufferTargetARB.ArrayBuffer);
                mappedVertices = null;
            }
            openGL.DeleteBuffer(vertexBuffer); openGL.DeleteBuffer(indexBuffer); openGL.DeleteVertexArray(vertexArray);
            openGL.DeleteBuffer(instanceQuadBuffer);
            openGL.DeleteBuffer(instanceBuffer);
            openGL.DeleteBuffer(instanceIndexBuffer);
            openGL.DeleteVertexArray(instanceVertexArray);
            instancedProgram.Dispose();
            instancedArrayProgram.Dispose();
            sdfProgram.Dispose();
            foreach (var query in gpuQueries)
                if (query != 0)
                    openGL.DeleteQuery(query);
        }
        foreach (var texture in renderTextures) texture.MarkDisposed();
        foreach (var texture in uploadedTextures) texture.Handle = 0;
        renderTextures.Clear();
        uploadedTextures.Clear();
        uploadedTextureArrays.Clear();
        cameraTargets.Clear();
    }

    private sealed record RenderTarget(uint Framebuffer, uint Texture, int Width, int Height);
    private sealed record CameraRenderTargets(RenderTarget First, RenderTarget Second);
    private readonly record struct TextureUv(float Left, float Top, float Right, float Bottom);

    private sealed class BasicUniformLocations
    {
        public int Texture { get; }
        public int NormalMap { get; }
        public int UseLighting { get; }
        public int Ambient { get; }
        public int LightingLayer { get; }
        public int LightCount { get; }
        public int ShadowEdgeCount { get; }
        public int CameraX { get; }
        public int CameraY { get; }
        public int CameraTranslation { get; }
        public int ViewportPosition { get; }
        public int ViewportSize { get; }
        public LightUniformLocations[] Lights { get; } = new LightUniformLocations[Lighting2D.MaximumLights];
        public int[] ShadowEdges { get; } = new int[Lighting2D.MaximumShadowEdges];
        public int[] ShadowMasks { get; } = new int[Lighting2D.MaximumShadowEdges];

        public BasicUniformLocations(GL openGL, uint program)
        {
            Texture = openGL.GetUniformLocation(program, "uTexture");
            NormalMap = openGL.GetUniformLocation(program, "uNormalMap");
            UseLighting = openGL.GetUniformLocation(program, "uUseLighting");
            Ambient = openGL.GetUniformLocation(program, "uAmbient");
            LightingLayer = openGL.GetUniformLocation(program, "uLightingLayer");
            LightCount = openGL.GetUniformLocation(program, "uLightCount");
            ShadowEdgeCount = openGL.GetUniformLocation(program, "uShadowEdgeCount");
            CameraX = openGL.GetUniformLocation(program, "uCameraX");
            CameraY = openGL.GetUniformLocation(program, "uCameraY");
            CameraTranslation = openGL.GetUniformLocation(program, "uCameraTranslation");
            ViewportPosition = openGL.GetUniformLocation(program, "uViewportPosition");
            ViewportSize = openGL.GetUniformLocation(program, "uViewportSize");
            for (var index = 0; index < Lights.Length; index++)
                Lights[index] = new(
                    openGL.GetUniformLocation(program, $"uLights[{index}].position"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].direction"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].color"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].intensity"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].radius"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].height"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].falloff"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].kind"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].spotCos"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].layerMask"),
                    openGL.GetUniformLocation(program, $"uLights[{index}].castsShadows"));
            for (var index = 0; index < ShadowEdges.Length; index++)
            {
                ShadowEdges[index] = openGL.GetUniformLocation(program, $"uShadowEdges[{index}]");
                ShadowMasks[index] = openGL.GetUniformLocation(program, $"uShadowMasks[{index}]");
            }
        }
    }

    private readonly record struct LightUniformLocations(
        int Position, int Direction, int Color, int Intensity, int Radius, int Height,
        int Falloff, int Kind, int SpotCos, int LayerMask, int CastsShadows);

    private sealed class SpriteDrawCommandComparer : IComparer<SpriteDrawCommand>
    {
        public static SpriteDrawCommandComparer Instance { get; } = new();
        public int Compare(SpriteDrawCommand left, SpriteDrawCommand right)
        {
            var depth = left.Depth.CompareTo(right.Depth);
            return depth != 0 ? depth : left.Order.CompareTo(right.Order);
        }
    }

    private const string InstancedVertexShader = """
        #version 330 core
        layout (location = 0) in vec4 aCornerUv;
        layout (location = 1) in vec4 aSizeOrigin;
        layout (location = 2) in vec2 aTransformX;
        layout (location = 3) in vec2 aTransformY;
        layout (location = 4) in vec2 aTranslation;
        layout (location = 5) in vec4 aColor;
        layout (location = 6) in vec4 aColorUv;
        layout (location = 7) in vec4 aNormalUv;
        layout (location = 8) in float aTextureLayer;
        uniform vec2 uCameraX;
        uniform vec2 uCameraY;
        uniform vec2 uCameraTranslation;
        uniform vec2 uViewportPosition;
        uniform vec2 uViewportSize;
        out vec2 frag_texCoords;
        out vec4 frag_color;
        out vec2 frag_worldPosition;
        out vec2 frag_tangent;
        out vec2 frag_bitangent;
        out vec2 frag_normalTexCoords;
        flat out float frag_textureLayer;
        void main()
        {
            vec2 local = (aCornerUv.xy - aSizeOrigin.zw) * aSizeOrigin.xy;
            vec2 world = aTransformX * local.x + aTransformY * local.y + aTranslation;
            vec2 screen = uCameraX * world.x + uCameraY * world.y + uCameraTranslation;
            vec2 relative = screen - uViewportPosition;
            gl_Position = vec4(relative.x * 2.0 / uViewportSize.x - 1.0,
                1.0 - relative.y * 2.0 / uViewportSize.y, 0.0, 1.0);
            frag_texCoords = mix(aColorUv.xy, aColorUv.zw, aCornerUv.zw);
            frag_normalTexCoords = mix(aNormalUv.xy, aNormalUv.zw, aCornerUv.zw);
            frag_color = aColor;
            frag_worldPosition = world;
            frag_tangent = length(aTransformX) > 0.0 ? normalize(aTransformX) : vec2(1.0, 0.0);
            frag_bitangent = length(aTransformY) > 0.0 ? normalize(aTransformY) : vec2(0.0, 1.0);
            frag_textureLayer = aTextureLayer;
        }
        """;

    private const string SdfFragmentShader = """
        #version 330 core
        uniform sampler2D uTexture;
        in vec2 frag_texCoords;
        in vec4 frag_color;
        out vec4 out_color;
        void main()
        {
            float distance = texture(uTexture, frag_texCoords).a;
            float width = max(fwidth(distance), 0.001);
            float alpha = smoothstep(0.5 - width, 0.5 + width, distance);
            out_color = vec4(frag_color.rgb, frag_color.a * alpha);
        }
        """;
}
