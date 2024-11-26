using Axolotl2D.Drawable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Services
{
    public class AssetManager
    {
        private Dictionary<string, Sprite> sprites = [];
        private ILazyDependencyLoader<Game> _lazyGame;

        public AssetManager(ILazyDependencyLoader<Game> lazyGame) 
        {
            _lazyGame = lazyGame;
        }

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
            if(assetStream == null)
            {
                throw new ArgumentNullException(nameof(assetStream));
            }

            var _game = _lazyGame.Value;
            var sprite = new Sprite(_game, assetStream);
            sprites.Add(key, sprite);
        }

        public Sprite GetSprite(string key)
        {
            if (!sprites.ContainsKey(key))
            {
                throw new Exception($"Sprite with key {key} does not exist!");
            }
            return sprites[key];
        }
    }
}
