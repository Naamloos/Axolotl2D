using Axolotl2D.Entities;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Drawable
{
    public class SimpleQuad
    {
        private float[] _vertices;
        private uint[] _indices;

        private uint _vbo;
        private uint _ebo;
        private uint _vao;

        private GL _gl;

        private Game _game;

        public unsafe SimpleQuad(Game baseGame)
        {
            _game = baseGame;

            _gl = _game.GetOpenGLContext();

            // Create a VAO.
            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            // These are all the points we have in our quad.
            _vertices =
            [
                 0.5f,  0.5f, 0.0f,
                 0.5f, -0.5f, 0.0f,
                -0.5f, -0.5f, 0.0f,
                -0.5f,  0.5f, 0.0f
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
            _gl.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
        }

        public unsafe void Draw()
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            _gl.BindVertexArray(_vao);
            _gl.UseProgram(_game.GetShaderProgram());
            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*) 0);
            _gl.BindVertexArray(0);
        }

        public void SetRect(float x, float y, float width, float height)
        {
            var viewportWidth = _game.GetWidth();
            var viewportHeight = _game.GetHeight();

            _vertices[0] = x / viewportWidth * 2 - 1;
            _vertices[1] = 1 - y / viewportHeight * 2;
            _vertices[2] = 0;

            _vertices[3] = (x + width) / viewportWidth * 2 - 1;
            _vertices[4] = 1 - y / viewportHeight * 2;
            _vertices[5] = 0;

            _vertices[6] = (x + width) / viewportWidth * 2 - 1;
            _vertices[7] = 1 - (y + height) / viewportHeight * 2;
            _vertices[8] = 0;

            _vertices[9] = x / viewportWidth * 2 - 1;
            _vertices[10] = 1 - (y + height) / viewportHeight * 2;
            _vertices[11] = 0;
        }
    }
}
