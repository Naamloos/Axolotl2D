﻿using Axolotl2D.Drawable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Services
{
    /// <summary>
    /// Manages assets for the game.
    /// </summary>
    public class AssetManager(ILazyDependencyLoader<Game> lazyGame)
    {
        private readonly Dictionary<string, Sprite> sprites = [];
        private readonly ILazyDependencyLoader<Game> _lazyGame = lazyGame;

        /// <summary>
        /// Loads a sprite from a stream.
        /// </summary>
        /// <param name="key">Key to store sprite under.</param>
        /// <param name="assetStream">Stream to load sprite from.</param>
        /// <exception cref="Exception"></exception>
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

        /// <summary>
        /// Tries to get a sprite from the asset manager.
        /// </summary>
        /// <param name="key">Key to load sprite from.</param>
        /// <param name="sprite">Sprite to load. Can be casted to <see cref="Sprite"/></param>
        /// <returns>Whether loading the sprite was succesful</returns>
        public bool TryGetSprite(string key, out BaseDrawable? sprite)
        {
            var success = sprites.TryGetValue(key, out Sprite? output);
            sprite = output;

            return success;
        }
    }
}
