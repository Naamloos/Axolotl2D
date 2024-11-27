using System.Numerics;

namespace Axolotl2D
{
    public abstract partial class Game
    {
        /// <summary>
        /// Represents a delegate that is called when the game draws.
        /// </summary>
        /// <param name="frameDelta">Current frame delta.</param>
        /// <param name="frameRate">Current frame rate.</param>
        public delegate void DrawDelegate(double frameDelta, double frameRate);

        /// <summary>
        /// Represents a delegate that is called when the game updates.
        /// </summary>
        /// <param name="frameDelta">Current frame delta.</param>
        public delegate void UpdateDelegate(double frameDelta);

        /// <summary>
        /// Represents a delegate that is called when the game loads.
        /// </summary>
        public delegate void LoadDelegate();

        /// <summary>
        /// Represents a delegate that is called when the game window resizes.
        /// </summary>
        /// <param name="size"></param>
        public delegate void ResizeDelegate(Vector2 size);

        /// <summary>
        /// Called when the game draws.
        /// </summary>
        public event DrawDelegate? OnDraw;

        /// <summary>
        /// Called when the game updates.
        /// </summary>
        public event UpdateDelegate? OnUpdate;

        /// <summary>
        /// Called when the game loads.
        /// </summary>
        public event LoadDelegate? OnLoad;

        /// <summary>
        /// Called when the game window resizes.
        /// </summary>
        public event ResizeDelegate? OnResize;
    }
}
