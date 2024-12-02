using System.Numerics;

namespace Axolotl2D.Scenes
{
    /// <summary>
    /// Represents a scene in the game.
    /// </summary>
    public abstract class BaseScene
    {
        /// <summary>
        /// Scene Host. This can be used to switch to a different Scene when needed.
        /// </summary>
        protected SceneGameHost SceneGameHost { get => _sceneGameHost!; }
        internal SceneGameHost? _sceneGameHost;

        /// <summary>
        /// Game that this scene is running on.
        /// </summary>
        protected Game Game { get => _game!; }
        internal Game? _game;

        /// <summary>
        /// Called when the scene draws.
        /// </summary>
        /// <param name="frameDelta">Current frame delta</param>
        /// <param name="frameRate">Current frame rate</param>
        public virtual void Draw(double frameDelta, double frameRate) { }

        /// <summary>
        /// Called when the scene updates.
        /// </summary>
        /// <param name="frameDelta">Current frame delta</param>
        public virtual void Update(double frameDelta) { }

        /// <summary>
        /// Called to load the scene.
        /// </summary>
        public virtual void Load() { }

        /// <summary>
        /// Called when the window resizes
        /// </summary>
        /// <param name="size">New window size</param>
        public virtual void Resize(Vector2 size) { }

        /// <summary>
        /// Called to unload the scene.
        /// </summary>
        public virtual void Unload() { }
    }
}
