using Axolotl2D.Helpers;

namespace Axolotl2D.Drawable
{
    /// <summary>
    /// Manages assets for the game.
    /// </summary>
    public class SpriteManager(ILazyDependencyLoader<Game> lazyGame)
    {
        private readonly Dictionary<string, Sprite> loadedSprites = [];
        private readonly ILazyDependencyLoader<Game> lazyLoadedGame = lazyGame;

        /// <summary>
        /// Loads a sprite from a stream.
        /// </summary>
        /// <param name="key">Key to store sprite under.</param>
        /// <param name="assetStream">Stream to load sprite from.</param>
        /// <exception cref="Exception"></exception>
        public void LoadSprite(string key, Stream assetStream)
        {
            if (!lazyLoadedGame.IsLoaded)
            {
                throw new Exception("Attempted to load assets before game initialization!");
            }
            if (loadedSprites.ContainsKey(key))
            {
                throw new Exception($"Sprite with key {key} already exists!");
            }

            ArgumentNullException.ThrowIfNull(assetStream, nameof(assetStream));

            var _game = lazyLoadedGame.Value;
            var sprite = new Sprite(_game, assetStream);
            loadedSprites.Add(key, sprite);
        }

        /// <summary>
        /// Tries to get a sprite from the asset manager.
        /// </summary>
        /// <param name="key">Key to load sprite from.</param>
        /// <param name="sprite">Sprite to load. Can be casted to <see cref="Sprite"/></param>
        /// <returns>Whether loading the sprite was succesful</returns>
        public bool TryGetSprite(string key, out BaseDrawable? sprite)
        {
            var success = loadedSprites.TryGetValue(key, out Sprite? output);
            sprite = output;

            return success;
        }
    }
}
