using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StbImageSharp;
using Silk.NET.Core.Native;

namespace Axolotl2D.Drawable
{
    public class Sprite : IDrawable
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Width { get; private set; }
        public float Height { get; private set; }

        private int viewportWidth = 0;
        private int viewportHeight = 0;


        private float[] _vertices;
        private uint[] _indices;

        private uint _vbo;
        private uint _ebo;
        private uint _vao;

        private uint _texture;

        private GL _gl;
        private Game _game;

        public unsafe Sprite(Game game, Stream imageFile)
        {
            _game = game;

            _gl = _game._openGL!;

            // Create a VAO.
            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            // These are all the points we have in our quad.
            _vertices =
            [
              // X      Y     Z     Tex X Tex Y
                 0.5f,  0.5f, 0.0f, 1.0f, 1.0f,
                 0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
                -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
                -0.5f,  0.5f, 0.0f, 0.0f, 1.0f
            ];

            // Create a VBO.
            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);

            // These are the indices that make up the quad. So 3 points make up a triangle. These are drawn in order.
            _indices =
            [
                0u, 1u, 3u,
                1u, 2u, 3u
            ];

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

            int location = _gl.GetUniformLocation(_game._shaderProgram, "uTexture");
            _gl.Uniform1(location, 0);

            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _game.LoadedSprites++;
        }

        public void Draw(float x, float y, float width, float height)
        {
            SetRect(x, y, width, height);
            Draw();
        }

        public unsafe void Draw()
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

        public void SetRect(float x, float y, float width, float height)
        {
            var viewportWidth = _game.WindowWidth;
            var viewportHeight = _game.WindowHeight;

            // check if any values changed
            if (x == X &&
                y == Y &&
                width == Width &&
                height == Height &&
                viewportWidth == this.viewportWidth &&
                viewportHeight == this.viewportHeight) { return; }

            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
            this.viewportWidth = viewportWidth;
            this.viewportHeight = viewportHeight;

            _vertices[0] = X / viewportWidth * 2 - 1;
            _vertices[1] = 1 - (Y + height) / viewportHeight * 2;
            _vertices[2] = 0;

            _vertices[5] = X / viewportWidth * 2 - 1;
            _vertices[6] = 1 - Y / viewportHeight * 2;
            _vertices[7] = 0;

            _vertices[10] = (X + Width) / viewportWidth * 2 - 1;
            _vertices[11] = 1 - Y / viewportHeight * 2;
            _vertices[12] = 0;

            _vertices[15] = (X + Width) / viewportWidth * 2 - 1;
            _vertices[16] = 1 - (Y + Height) / viewportHeight * 2;
            _vertices[17] = 0;
        }
    }
}
