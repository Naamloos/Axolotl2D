using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Axolotl2D.Drawable
{
    /// <summary>
    /// Abstract class that represents a drawable object.
    /// This can be a Sprite, Text, or any other drawable object.
    /// </summary>
    public abstract class BaseDrawable : IDisposable
    {
        /// <summary>
        /// The current position of this drawable object.
        /// </summary>
        public Vector2 Position
        {
            get => position;
            set
            {
                position = value;
                OnMove(value);
            }
        }
        internal Vector2 position = Vector2.Zero;

        /// <summary>
        /// The current size of this drawable object.
        /// </summary>
        public Vector2 Size
        {
            get => size;
            set
            {
                size = value;
                OnResize(value);
            }
        }
        internal Vector2 size = Vector2.One;

        /// <summary>
        /// The current rotation of this drawable object.
        /// Objects are rotated around their center.
        /// </summary>
        public float Rotation
        {
            get => rotation;
            set
            {
                rotation = value;
                OnRotate(value);
            }
        }
        internal float rotation = 0f;

        /// <summary>
        /// When updating both the position and size, use this property to update both at the same time.
        /// This ensures drawable vertices don't get calculated twice.
        /// </summary>
        public (Vector2, Vector2) Bounds
        {
            get => (Position, Size);
            set
            {
                Position = value.Item1;
                Size = value.Item2;
                OnBoundsUpdate(value.Item1, value.Item2);
            }
        }

        // These are the vertices that make up the quad. Each point has 3 values (x, y, z) and 2 values for the texture coordinates (u, v).
        internal float[] vertices = new float[20];

        // These are the indices that make up the quad. So 3 points make up a triangle. These are drawn in order.
        internal uint[] indices =
        [
            0u, 1u, 3u,
            1u, 2u, 3u
        ];

        internal Game game;
        internal Vector2 cachedViewport;
        internal readonly uint vboPointer;
        internal readonly uint eboPointer;
        internal readonly uint vaoPointer;

        internal readonly uint texturePointer;

        internal readonly GL openGL;

        internal BaseDrawable(Game game)
        {
            this.game = game;
            openGL = game._openGL!;

            // Create a VAO.
            vaoPointer = openGL.GenVertexArray();
            openGL.BindVertexArray(vaoPointer);

            // Create a VBO.
            vboPointer = openGL.GenBuffer();
            openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vboPointer);

            // fix vertices and buffer data
            openGL.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), ref MemoryMarshal.GetReference(vertices.AsSpan()), BufferUsageARB.StaticDraw);

            // Create an EBO.
            eboPointer = openGL.GenBuffer();
            openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, eboPointer);

            // fix indices and buffer data
            openGL.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), ref MemoryMarshal.GetReference(indices.AsSpan()), BufferUsageARB.StaticDraw);

            // Set up vertex attributes.
            const uint positionLocation = 0;
            openGL.EnableVertexAttribArray(positionLocation);
            openGL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            // Set up texture attributes.
            const uint texCoordLocation = 1;
            openGL.EnableVertexAttribArray(texCoordLocation);
            openGL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            // Unbind everything.
            openGL.BindVertexArray(0);
            openGL.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            openGL.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            // Generate a texture to draw to.
            texturePointer = openGL.GenTexture();

            cachedViewport = game.Viewport;
            this.game.OnResize += OnResizeWindow;
        }

        /// <summary>
        /// Updates the texture of the drawable object.
        /// </summary>
        internal abstract void UpdateTexture();

        /// <summary>
        /// Draws the drawable object with the currently defined position and size.
        /// </summary>
        public virtual void Draw()
        {
            UpdateTexture();
            openGL.BindBuffer(BufferTargetARB.ArrayBuffer, vboPointer);

            // fix vertices and buffer data
            openGL.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), ref MemoryMarshal.GetReference(vertices.AsSpan()), BufferUsageARB.StaticDraw);

            openGL.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            openGL.BindVertexArray(vaoPointer);
            openGL.BindTexture(TextureTarget.Texture2D, texturePointer);
            openGL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, ref MemoryMarshal.GetReference(Span<uint>.Empty));
            openGL.BindVertexArray(0);
        }

        /// <summary>
        /// Draws the drawable object with the given position. Updates the current position.
        /// </summary>
        /// <param name="position">Position to draw object at.</param>
        public virtual void Draw(Vector2 position)
        {
            Position = position;
            Draw();
        }

        /// <summary>
        /// Draws the drawable object with the given position and size. Updates the current position and size.
        /// </summary>
        /// <param name="position">Position to draw at</param>
        /// <param name="size">Size to draw at</param>
        public virtual void Draw(Vector2 position, Vector2 size)
        {
            Bounds = (position, size);
            Draw();
        }

        /// <summary>
        /// Called when the game window resizes.
        /// </summary>
        /// <param name="size">New game window size.</param>
        private void OnResizeWindow(Vector2 size)
        {
            cachedViewport = size;
            CalculateVertices();
        }

        /// <summary>
        /// Called when the drawable object is resized.
        /// </summary>
        /// <param name="size">New size</param>
        internal virtual void OnResize(Vector2 size)
        {
            CalculateVertices();
        }

        /// <summary>
        /// Called when the drawable object is moved.
        /// </summary>
        /// <param name="position">New position</param>
        internal virtual void OnMove(Vector2 position)
        {
            CalculateVertices();
        }

        /// <summary>
        /// Called when the drawable object's bounds are updated.
        /// </summary>
        /// <param name="position">New position</param>
        /// <param name="size">New size</param>
        internal virtual void OnBoundsUpdate(Vector2 position, Vector2 size)
        {
            CalculateVertices();
        }

        /// <summary>
        /// Called when the drawable object is rotated.
        /// </summary>
        /// <param name="rotation">New rotation</param>
        internal virtual void OnRotate(float rotation)
        {
            CalculateVertices();
        }

        /// <summary>
        /// Calculates the vertices of the drawable object. Can be overridden when texture data is in a different format.
        /// </summary>
        internal virtual void CalculateVertices()
        {
            float x1 = Position.X / cachedViewport.X * 2 - 1;
            float y1 = 1 - (Position.Y + Size.Y) / cachedViewport.Y * 2;
            float x2 = (Position.X + Size.X) / cachedViewport.X * 2 - 1;
            float y2 = 1 - Position.Y / cachedViewport.Y * 2;

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

            float aspectRatio = cachedViewport.X / cachedViewport.Y;

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

            this.vertices[0] = vertices[0].X;
            this.vertices[1] = vertices[0].Y;
            this.vertices[2] = 0;
            this.vertices[3] = 1.0f;
            this.vertices[4] = 1.0f;

            this.vertices[5] = vertices[1].X;
            this.vertices[6] = vertices[1].Y;
            this.vertices[7] = 0;
            this.vertices[8] = 1.0f;
            this.vertices[9] = 0.0f;

            this.vertices[10] = vertices[2].X;
            this.vertices[11] = vertices[2].Y;
            this.vertices[12] = 0;
            this.vertices[13] = 0.0f;
            this.vertices[14] = 0.0f;

            this.vertices[15] = vertices[3].X;
            this.vertices[16] = vertices[3].Y;
            this.vertices[17] = 0;
            this.vertices[18] = 0.0f;
            this.vertices[19] = 1.0f;
        }


        /// <summary>
        /// Disposes of this drawable object.
        /// </summary>
        public virtual void Dispose()
        {
            game.OnResize -= OnResizeWindow;
        }

        /// <summary>
        /// Finalizer for the drawable object.
        /// </summary>
        ~BaseDrawable() => Dispose();
    }
}
