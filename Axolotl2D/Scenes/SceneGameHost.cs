using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Axolotl2D.Scenes
{
    /// <summary>
    /// Represents a service that hosts the game using Scenes.
    /// </summary>
    /// <param name="game">Game to host</param>
    /// <param name="_services">Services</param>
    public class SceneGameHost(Game game, IServiceProvider _services) : IGameHost
    {
        private BaseScene? currentScene;

        /// <summary>
        /// Switches to a different scene.
        /// </summary>
        /// <typeparam name="T">Type of the Scene to switch to</typeparam>
        public void ChangeScene<T>() where T : BaseScene => ChangeScene(typeof(T));

        /// <summary>
        /// Starts the game.
        /// </summary>
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

        /// <summary>
        /// Stops the game.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (currentScene != null)
            {
                game.OnUpdate -= currentScene.Update;
                game.OnDraw -= currentScene.Draw;
                game.OnResize -= currentScene.Resize;
            }

            return Task.Run(() => game.Stop(), cancellationToken);
        }

        private void ChangeScene(Type t)
        {
            if (_services.GetRequiredService(t) is not BaseScene newScene)
            {
                throw new Exception("Tried switching to a scene that is not part of the service provider!");
            }
            if (currentScene != null)
            {
                game.OnUpdate -= currentScene.Update;
                game.OnDraw -= currentScene.Draw;
                game.OnResize -= currentScene.Resize;

                currentScene.Unload();
            }

            // GC collecting
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

            currentScene = newScene;
            currentScene.sceneGameHost = this;
            currentScene.game = game;

            currentScene.Load();
            game.OnUpdate += currentScene.Update;
            game.OnDraw += currentScene.Draw;
            game.OnResize += currentScene.Resize;
        }
    }
}
