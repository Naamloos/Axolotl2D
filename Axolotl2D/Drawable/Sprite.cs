﻿using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

            _gl = _game.GetOpenGLContext();

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

            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);

            int location = _gl.GetUniformLocation(_game.GetShaderProgram(), "uTexture");
            _gl.Uniform1(location, 0);

            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _game._loadedSprites++;
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
            var viewportWidth = _game.GetWidth();
            var viewportHeight = _game.GetHeight();

            _vertices[0] = x / viewportWidth * 2 - 1;
            _vertices[1] = 1 - (y + height) / viewportHeight * 2;
            _vertices[2] = 0;

            _vertices[5] = x / viewportWidth * 2 - 1;
            _vertices[6] = 1 - y / viewportHeight * 2;
            _vertices[7] = 0;

            _vertices[10] = (x + width) / viewportWidth * 2 - 1;
            _vertices[11] = 1 - y / viewportHeight * 2;
            _vertices[12] = 0;

            _vertices[15] = (x + width) / viewportWidth * 2 - 1;
            _vertices[16] = 1 - (y + height) / viewportHeight * 2;
            _vertices[17] = 0;
        }
    }
}
