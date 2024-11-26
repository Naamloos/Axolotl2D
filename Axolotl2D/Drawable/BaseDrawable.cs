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
                _vertices = CalculateVertices();
            }
        }
        private Vector2 _position;

        public Vector2 Size
        {
            get => _size;
            set
            {
                _size = value;
                _vertices = CalculateVertices();
            }
        }
        private Vector2 _size;

        public float Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                _vertices = CalculateVertices();
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
                _vertices = CalculateVertices();
            }
        }

        // These are the vertices that make up the quad. Each point has 3 values (x, y, z) and 2 values for the texture coordinates (u, v).
        protected float[] _vertices = new float[20];
        // These are the indices that make up the quad. So 3 points make up a triangle. These are drawn in order.
        protected uint[] _indices =
        {
            0u, 1u, 3u,
            1u, 2u, 3u
        };

        private readonly Game _game;
        private Vector2 _cachedViewport;

        internal BaseDrawable(Game game, Vector2 position, Vector2 size, float rotation = 0)
        {
            Position = position;
            Size = size;
            Rotation = rotation;
            _game = game;
            _cachedViewport = game.Viewport;

            this._vertices = CalculateVertices();

            _game.OnResize += ResizeWindow;
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
            _game.OnResize -= ResizeWindow;

            GC.SuppressFinalize(this);
        }

        private void ResizeWindow(Vector2 size)
        {
            _cachedViewport = size;
            CalculateVertices();
        }

        protected float[] CalculateVertices()
        {
            float x1 = Position.X / _cachedViewport.X * 2 - 1;
            float y1 = 1 - (Position.Y + Size.Y) / _cachedViewport.Y * 2;
            float x2 = (Position.X + Size.X) / _cachedViewport.X * 2 - 1;
            float y2 = 1 - Position.Y / _cachedViewport.Y * 2;

            Vector2[] corners = new Vector2[]
            {
                new Vector2(x1, y1),
                new Vector2(x1, y2),
                new Vector2(x2, y2),
                new Vector2(x2, y1)
            };

            Vector2 center = new Vector2((x1 + x2) / 2, (y1 + y2) / 2);
            float cos = MathF.Cos(Rotation);
            float sin = MathF.Sin(Rotation);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 dir = corners[i] - center;
                corners[i] = new Vector2(
                    center.X + dir.X * cos - dir.Y * sin,
                    center.Y + dir.X * sin + dir.Y * cos
                );
            }

            return new float[]
            {
                corners[0].X, corners[0].Y, 0, 1.0f, 1.0f,
                corners[1].X, corners[1].Y, 0, 1.0f, 0.0f,
                corners[2].X, corners[2].Y, 0, 0.0f, 0.0f,
                corners[3].X, corners[3].Y, 0, 0.0f, 1.0f
            };
        }
    }
}