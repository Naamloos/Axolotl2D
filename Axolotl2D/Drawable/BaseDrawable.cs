using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Drawable
{
    public abstract class BaseDrawable : IDisposable
    {
        public Vector2 Position
        {
            get => _position;
            set
            {
                _position = value;
                _vertices = calculateVertices();
            }
        }
        private Vector2 _position;
        public Vector2 Size
        {
            get => _size;
            set
            {
                _size = value;
                _vertices = calculateVertices();
            }
        }
        private Vector2 _size;

        /// <summary>
        /// When updating both the position and size, use this property to update both at the same time.
        /// </summary>
        public (Vector2, Vector2) Bounds
        {
            get => (Position, Size);
            set
            {
                Position = value.Item1;
                Size = value.Item2;
                _vertices = calculateVertices();
            }
        }

        // These are the vertices that make up the quad. Each point has 3 values (x, y, z) and 2 values for the texture coordinates (u, v).
        protected float[] _vertices = new float[12];
        // These are the indices that make up the quad. So 3 points make up a triangle. These are drawn in order.
        protected uint[] _indices =
        [
            0u, 1u, 3u,
            1u, 2u, 3u
        ];

        private Game _game;
        private Vector2 _cachedViewport;

        internal BaseDrawable(Game game, Vector2 position, Vector2 size)
        {
            Position = position;
            Size = size;
            _game = game;
            _cachedViewport = game.Viewport;

            this._vertices = calculateVertices();

            _game.OnResize += resizeWindow;
        }

        /// <summary>
        /// Draws the drawable object with the current position and size.
        /// </summary>
        public abstract void Draw();

        /// <summary>
        /// Draws the drawable object with the given position. Updates the current position.
        /// </summary>
        /// <param name="position">Position to draw object at.</param>
        public abstract void Draw(Vector2 position);

        /// <summary>
        /// Draws the drawable object with the given position and size. Updates the current position and size.
        /// </summary>
        /// <param name="position">Position to draw at</param>
        /// <param name="size">Size to draw at</param>
        public abstract void Draw(Vector2 position, Vector2 size);

        /// <summary>
        /// Disposes of the drawable object.
        /// </summary>
        public virtual void Dispose()
        {
            _game.OnResize -= resizeWindow;
        }

        private void resizeWindow(Vector2 size)
        {
            _cachedViewport = size;
            calculateVertices();
        }

        protected float[] calculateVertices()
        {
            float x1 = Position.X / _cachedViewport.X * 2 - 1;
            float y1 = 1 - (Position.Y + Size.Y) / _cachedViewport.Y * 2;
            float x2 = (Position.X + Size.X) / _cachedViewport.X * 2 - 1;
            float y2 = 1 - Position.Y / _cachedViewport.Y * 2;

            return
            [
                x1, y1, 0, 1.0f, 1.0f,
                x1, y2, 0, 1.0f, 0.0f,
                x2, y2, 0, 0.0f, 0.0f,
                x2, y1, 0, 0.0f, 1.0f
            ];
        }
    }
}
