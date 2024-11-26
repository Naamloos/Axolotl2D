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
                calculateVertices();
            }
        }
        private Vector2 _position;
        public Vector2 Size
        {
            get => _size;
            set
            {
                _size = value;
                calculateVertices();
            }
        }
        private Vector2 _size;

        public float Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                calculateVertices();
            }
        }
        private float _rotation;

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
                calculateVertices();
            }
        }

        // These are the vertices that make up the quad. Each point has 3 values (x, y, z) and 2 values for the texture coordinates (u, v).
        protected float[] _vertices = new float[20];
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

            calculateVertices();

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

        protected void calculateVertices()
        {
            float x1 = Position.X / _cachedViewport.X * 2 - 1;
            float y1 = 1 - (Position.Y + Size.Y) / _cachedViewport.Y * 2;
            float x2 = (Position.X + Size.X) / _cachedViewport.X * 2 - 1;
            float y2 = 1 - Position.Y / _cachedViewport.Y * 2;

            Vector2 center = new Vector2((x1 + x2) / 2, (y1 + y2) / 2);

            Vector2[] vertices = new Vector2[]
            {
                new Vector2(x1, y1),
                new Vector2(x1, y2),
                new Vector2(x2, y2),
                new Vector2(x2, y1)
            };

            float cos = MathF.Cos(Rotation);
            float sin = MathF.Sin(Rotation);

            float aspectRatio = _cachedViewport.X / _cachedViewport.Y;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 dir = vertices[i] - center;
                dir.X *= aspectRatio; // Adjust for aspect ratio
                vertices[i] = new Vector2(
                    dir.X * cos - dir.Y * sin,
                    dir.X * sin + dir.Y * cos
                );
                vertices[i].X /= aspectRatio; // Revert aspect ratio adjustment
                vertices[i] += center;
            }

            _vertices[0] = vertices[0].X;
            _vertices[1] = vertices[0].Y;
            _vertices[2] = 0;
            _vertices[3] = 1.0f;
            _vertices[4] = 1.0f;

            _vertices[5] = vertices[1].X;
            _vertices[6] = vertices[1].Y;
            _vertices[7] = 0;
            _vertices[8] = 1.0f;
            _vertices[9] = 0.0f;

            _vertices[10] = vertices[2].X;
            _vertices[11] = vertices[2].Y;
            _vertices[12] = 0;
            _vertices[13] = 0.0f;
            _vertices[14] = 0.0f;

            _vertices[15] = vertices[3].X;
            _vertices[16] = vertices[3].Y;
            _vertices[17] = 0;
            _vertices[18] = 0.0f;
            _vertices[19] = 1.0f;
        }
    }
}
