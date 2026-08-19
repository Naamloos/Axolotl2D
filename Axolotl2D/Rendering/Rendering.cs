using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Axolotl2D.Rendering;

public interface IRendering : IDisposable
{
    void Initialize();
}

/// <summary>Owns the shared GPU buffers and texture uploads used by sprite batches.</summary>
public sealed unsafe class Rendering(Game game) : IRendering
{
    private readonly HashSet<Texture2D> uploadedTextures = new(ReferenceEqualityComparer.Instance);
    private GL openGL = null!;
    private uint vertexArray;
    private uint vertexBuffer;
    private uint indexBuffer;
    private bool initialized;

    public void Initialize()
    {
        if (initialized)
            return;

        openGL = game.openGL ?? throw new InvalidOperationException("Rendering can only initialize after the game window loads.");
        vertexArray = openGL.GenVertexArray();
        vertexBuffer = openGL.GenBuffer();
        indexBuffer = openGL.GenBuffer();

        openGL.BindVertexArray(vertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);

        const uint stride = 9 * sizeof(float);
        openGL.EnableVertexAttribArray(0);
        openGL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        openGL.EnableVertexAttribArray(1);
        openGL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        openGL.EnableVertexAttribArray(2);
        openGL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));

        openGL.UseProgram(game.shaderProgramPointer);
        openGL.Uniform1(openGL.GetUniformLocation(game.shaderProgramPointer, "uTexture"), 0);
        openGL.BindVertexArray(0);
        initialized = true;
    }

    internal void Draw(IReadOnlyList<SpriteDrawCommand> commands, Camera2D camera)
    {
        if (!initialized)
            Initialize();
        if (commands.Count == 0)
            return;

        var ordered = commands.OrderBy(command => command.Depth).ThenBy(command => command.Order);
        var batch = new List<SpriteDrawCommand>();
        Texture2D? texture = null;

        foreach (var command in ordered)
        {
            if (texture is not null && !ReferenceEquals(texture, command.Sprite.Texture))
            {
                Flush(texture, batch, camera);
                batch.Clear();
            }
            texture = command.Sprite.Texture;
            batch.Add(command);
        }

        if (texture is not null)
            Flush(texture, batch, camera);
    }

    private void Flush(Texture2D texture, IReadOnlyList<SpriteDrawCommand> commands, Camera2D camera)
    {
        var vertices = new float[commands.Count * 4 * 9];
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
                new(-origin.X, -origin.Y),
                new(size.X - origin.X, -origin.Y),
                new(size.X - origin.X, size.Y - origin.Y),
                new(-origin.X, size.Y - origin.Y)
            ];

            var source = sprite.Source;
            var left = (float)source.X / texture.Width;
            var top = (float)source.Y / texture.Height;
            var right = (float)(source.X + source.Width) / texture.Width;
            var bottom = (float)(source.Y + source.Height) / texture.Height;
            Span<Vector2> textureCoordinates = [new(left, top), new(right, top), new(right, bottom), new(left, bottom)];

            for (var corner = 0; corner < 4; corner++)
            {
                var position = Vector2.Transform(corners[corner], command.Transform);
                if (command.Space == CoordinateSpace.World)
                    position = camera.WorldToScreen(position);
                position = Coordinates.ScreenToNormalizedDevice(position, game.Viewport);

                vertices[vertexOffset++] = position.X;
                vertices[vertexOffset++] = position.Y;
                vertices[vertexOffset++] = command.Depth;
                vertices[vertexOffset++] = textureCoordinates[corner].X;
                vertices[vertexOffset++] = textureCoordinates[corner].Y;
                vertices[vertexOffset++] = command.Tint.R;
                vertices[vertexOffset++] = command.Tint.G;
                vertices[vertexOffset++] = command.Tint.B;
                vertices[vertexOffset++] = command.Tint.A;
            }

            var first = (uint)i * 4;
            var index = i * 6;
            indices[index] = first;
            indices[index + 1] = first + 1;
            indices[index + 2] = first + 2;
            indices[index + 3] = first;
            indices[index + 4] = first + 2;
            indices[index + 5] = first + 3;
        }

        openGL.BindVertexArray(vertexArray);
        openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        openGL.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)),
            ref MemoryMarshal.GetReference(vertices.AsSpan()), BufferUsageARB.DynamicDraw);
        openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        openGL.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)),
            ref MemoryMarshal.GetReference(indices.AsSpan()), BufferUsageARB.DynamicDraw);
        openGL.ActiveTexture(TextureUnit.Texture0);
        openGL.BindTexture(TextureTarget.Texture2D, GetTextureHandle(texture));
        openGL.DrawElements(PrimitiveType.Triangles, (uint)indices.Length, DrawElementsType.UnsignedInt, (void*)0);
        openGL.BindVertexArray(0);
    }

    private uint GetTextureHandle(Texture2D texture)
    {
        if (texture.Handle != 0)
            return texture.Handle;

        texture.Handle = openGL.GenTexture();
        openGL.BindTexture(TextureTarget.Texture2D, texture.Handle);
        openGL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)texture.Width, (uint)texture.Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, texture.Pixels.AsSpan());
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        uploadedTextures.Add(texture);
        return texture.Handle;
    }

    public void Dispose()
    {
        if (!initialized)
            return;
        foreach (var texture in uploadedTextures)
        {
            openGL.DeleteTexture(texture.Handle);
            texture.Handle = 0;
        }
        openGL.DeleteBuffer(vertexBuffer);
        openGL.DeleteBuffer(indexBuffer);
        openGL.DeleteVertexArray(vertexArray);
        uploadedTextures.Clear();
        initialized = false;
    }
}
