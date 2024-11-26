using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StbImageSharp;
using Silk.NET.Core.Native;
using System.Numerics;

namespace Axolotl2D.Drawable
{
    public class Sprite : BaseDrawable
    {
        private uint _vbo;
        private uint _ebo;
        private uint _vao;

        private uint _texture;

        private GL _gl;

        internal unsafe Sprite(Game game, Stream imageFile) : base(game, Vector2.Zero, Vector2.One)
        {
            _gl = game._openGL!;

            // Create a VAO.
            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            // Create a VBO.
            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);

            // Create an EBO.
            _ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

            fixed (void* indices = _indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(_indices.Length * sizeof(uint)), indices, BufferUsageARB.StaticDraw);

            const uint positionLocation = 0;
            _gl.EnableVertexAttribArray(positionLocation);
            _gl.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            const uint texCoordLocation = 1;
            _gl.EnableVertexAttribArray(texCoordLocation);
            _gl.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            _texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _texture);

            ImageResult img = ImageResult.FromStream(imageFile, ColorComponents.RedGreenBlueAlpha);

            fixed (byte* ptr = img.Data)
                // Here we use "result.Width" and "result.Height" to tell OpenGL about how big our texture is.
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)img.Width,
                    (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);

#pragma warning disable CS9193 // Argument should be a variable because it is passed to a 'ref readonly' parameter
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);
#pragma warning restore CS9193 // Argument should be a variable because it is passed to a 'ref readonly' parameter

            int location = _gl.GetUniformLocation(game._shaderProgram, "uTexture");
            _gl.Uniform1(location, 0);

            _gl.BindTexture(TextureTarget.Texture2D, 0);
            game.LoadedSprites++;
        }

        public unsafe override void Draw()
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            _gl.BindVertexArray(_vao);
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
            _gl.BindVertexArray(0);
        }

        public override void Draw(Vector2 position)
        {
            Position = position;
            Draw();
        }

        public override void Draw(Vector2 position, Vector2 size)
        {
            Bounds = (position, size);
            Draw();
        }
    }
}
