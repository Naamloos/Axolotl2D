using Axolotl2D.Entities;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Drawable
{
    /// <summary>
    /// Drawable class that draws a simple quad to the screen.
    /// Generally you should only use this for testing purposes.
    /// </summary>
    public class SimpleQuad : BaseDrawable
    {
        private readonly uint _vbo;
        private readonly uint _ebo;
        private readonly uint _vao;

        private readonly GL _gl;

        /// <summary>
        /// Initialize a new SimpleQuad object.
        /// </summary>
        /// <param name="game">Game to initialize on</param>
        /// <param name="position">Position to initialize at</param>
        /// <param name="size">Size to initialize at</param>
        public unsafe SimpleQuad(Game game, Vector2 position, Vector2 size) : base(game, position, size)
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

            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
        }

        /// <summary>
        /// Draw the quad to the screen.
        /// </summary>
        public unsafe override void Draw()
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            _gl.BindVertexArray(_vao);
            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*) 0);
            _gl.BindVertexArray(0);
        }

        /// <summary>
        /// Draw the quad to the screen at the given position.
        /// </summary>
        /// <param name="position">Position to draw at.</param>
        public override void Draw(Vector2 position)
        {
            Position = position;
            Draw();
        }

        /// <summary>
        /// Draw the quad to the screen at the given position and size.
        /// </summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="size">Size to draw with.</param>
        public override void Draw(Vector2 position, Vector2 size)
        {
            Bounds = (position, size);
            Draw();
        }
    }
}
