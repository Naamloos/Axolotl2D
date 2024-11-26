using Axolotl2D.Attributes;
using Axolotl2D.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Services
{
    public class SceneGameHost : IGameHost
    {
        private Game _game;
        private BaseScene? _currentScene;
        private IServiceProvider _services;

        public SceneGameHost(Game game, IServiceProvider _services) 
        {
            this._services = _services;
            this._game = game;
        }

        public void ChangeScene<T>() where T : BaseScene => changeScene(typeof(T));

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _game.OnLoad += preloadSceneManager;

            return Task.Run(() => _game.start());
        }

        private void preloadSceneManager()
        {
            // find IScene in executing assembly where the DefaultSceneAttribute is applied
            var scenes = Assembly.
                GetEntryAssembly()!
                .GetTypes()
                .Where(t => t.IsAssignableTo(typeof(BaseScene)) && t.GetCustomAttribute<DefaultSceneAttribute>() != null);

            if (scenes.Count() == 0)
            {
                throw new Exception("No scene found with DefaultSceneAttribute applied");
            }
            if (scenes.Count() > 1)
            {
                throw new Exception("Multiple scenes found with DefaultSceneAttribute applied");
            }

            changeScene(scenes.First());
            _game.OnLoad -= preloadSceneManager;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_currentScene != null)
            {
                _game.OnUpdate -= _currentScene.Update;
                _game.OnDraw -= _currentScene.Draw;
                _game.OnResize -= _currentScene.Resize;
            }

            return Task.Run(() => _game.stop());
        }

        private void changeScene(Type t)
        {
            var newScene = _services.GetRequiredService(t) as BaseScene;
            if (newScene == null)
            {
                throw new Exception("Tried switching to a scene that is not part of the service provider!");
            }
            if (_currentScene != null)
            {
                _game.OnUpdate -= _currentScene.Update;
                _game.OnDraw -= _currentScene.Draw;
                _game.OnResize -= _currentScene.Resize;

                _currentScene.Unload();
            }

            _currentScene = newScene;
            _currentScene._sceneGameHost = this;

            _currentScene.Load();
            _game.OnUpdate += _currentScene.Update;
            _game.OnDraw += _currentScene.Draw;
            _game.OnResize += _currentScene.Resize;
        }
    }
}
