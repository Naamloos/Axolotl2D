using Silk.NET.OpenGL;
using System.Numerics;

namespace Axolotl2D.Drawable
{
    /// <summary>
    /// Drawable class that draws a simple quad to the screen.
    /// Generally you should only use this for testing purposes.
    /// </summary>
    public class SimpleQuad : BaseDrawable
    {
        /// <summary>
        /// Initialize a new SimpleQuad object.
        /// </summary>
        /// <param name="game">Game to initialize on</param>
        /// <param name="position">Position to initialize at</param>
        /// <param name="size">Size to initialize at</param>
        public SimpleQuad(Game game, Vector2 position, Vector2 size) : base(game) { }

        internal override void UpdateTexture() { }
    }
}
