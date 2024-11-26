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
    public class SceneGameHost(Game game, IServiceProvider _services) : IGameHost
    {
        private BaseScene? _currentScene;

        public void ChangeScene<T>() where T : BaseScene => ChangeScene(typeof(T));

        public Task StartAsync(CancellationToken cancellationToken)
        {
            game.OnLoad += PreloadSceneManager;

            return Task.Run(() => game.Start(), cancellationToken);
        }

        private void PreloadSceneManager()
        {
            // find IScene in executing assembly where the DefaultSceneAttribute is applied
            var scenes = Assembly.
                GetEntryAssembly()!
                .GetTypes()
                .Where(t => t.IsAssignableTo(typeof(BaseScene)) && t.GetCustomAttribute<DefaultSceneAttribute>() != null);

            if (!scenes.Any())
            {
                throw new Exception("No scene found with DefaultSceneAttribute applied");
            }
            if (scenes.Count() > 1)
            {
                throw new Exception("Multiple scenes found with DefaultSceneAttribute applied");
            }

            ChangeScene(scenes.First());
            game.OnLoad -= PreloadSceneManager;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_currentScene != null)
            {
                game.OnUpdate -= _currentScene.Update;
                game.OnDraw -= _currentScene.Draw;
                game.OnResize -= _currentScene.Resize;
            }

            return Task.Run(() => game.Stop(), cancellationToken);
        }

        private void ChangeScene(Type t)
        {
            if (_services.GetRequiredService(t) is not BaseScene newScene)
            {
                throw new Exception("Tried switching to a scene that is not part of the service provider!");
            }
            if (_currentScene != null)
            {
                game.OnUpdate -= _currentScene.Update;
                game.OnDraw -= _currentScene.Draw;
                game.OnResize -= _currentScene.Resize;

                _currentScene.Unload();
            }

            // GC collecting
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

            _currentScene = newScene;
            _currentScene._sceneGameHost = this;

            _currentScene.Load();
            game.OnUpdate += _currentScene.Update;
            game.OnDraw += _currentScene.Draw;
            game.OnResize += _currentScene.Resize;
        }
    }
}
