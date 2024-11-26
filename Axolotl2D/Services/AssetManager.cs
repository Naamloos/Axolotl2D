using Axolotl2D.Drawable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Services
{
    public class AssetManager(ILazyDependencyLoader<Game> lazyGame)
    {
        private readonly Dictionary<string, Sprite> sprites = [];
        private readonly ILazyDependencyLoader<Game> _lazyGame = lazyGame;

        public void LoadSprite(string key, Stream assetStream)
        {
            if(!_lazyGame.IsLoaded)
            {
                throw new Exception("Attempted to load assets before game initialization!");
            }
            if(sprites.ContainsKey(key))
            {
                throw new Exception($"Sprite with key {key} already exists!");
            }

            ArgumentNullException.ThrowIfNull(assetStream, nameof(assetStream));

            var _game = _lazyGame.Value;
            var sprite = new Sprite(_game, assetStream);
            sprites.Add(key, sprite);
        }

        public bool TryGetSprite(string key, out BaseDrawable? sprite)
        {
            var success = sprites.TryGetValue(key, out Sprite? output);
            sprite = output;

            return success;
        }
    }
}
