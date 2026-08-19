using Axolotl2D.Audio;
using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Axolotl2D
{
    /// <summary>
    /// Presents methods for injecting game-related services into a Host.
    /// </summary>
    public static class Hosting
    {
        /// <summary>
        /// Registers a game host that simply hosts the game.
        /// </summary>
        /// <typeparam name="T">Game to host</typeparam>
        /// <param name="services">Service Collection</param>
        /// <exception cref="InvalidOperationException">Can not register multiple IGameHost services.</exception>
        public static void UseSimpleGameHost<T>(this IServiceCollection services) where T : Game
        {
            if (services.Any(x => x.ImplementationType != null && x.ImplementationType.IsAssignableTo(typeof(IGameHost))))
            {
                throw new InvalidOperationException("Cannot register multiple IGameHost services!");
            }

            services.AddAxolotl2D();
            services.AddSingleton<Game, T>();
            services.AddSingleton<T, T>(x => (x.GetRequiredService<Game>() as T)!);
            services.AddHostedService<SimpleGameHost>();
        }

        /// <summary>
        /// Registers a game host that hosts the game using scenes.
        /// </summary>
        /// <typeparam name="T">Game to host</typeparam>
        /// <param name="services">Service Collection</param>
        /// <exception cref="InvalidOperationException">Can not register multiple IGameHost services.</exception>
        public static void UseSceneManagerGameHost<T>(this IServiceCollection services) where T : Game
        {
            if (services.Any(x => x.ImplementationType != null && x.ImplementationType.IsAssignableTo(typeof(IGameHost))))
            {
                throw new InvalidOperationException("Cannot register multiple IGameHost services!");
            }

            services.AddAxolotl2D();
            services.AddSingleton<Game, T>();
            services.AddSingleton<T, T>(x => (x.GetRequiredService<Game>() as T)!);
            services.AddHostedService<SceneGameHost>();
        }

        /// <summary>
        /// Registers the Asset Manager.
        /// </summary>
        /// <param name="services">Service Collection</param>
        public static IServiceCollection AddAxolotl2D(this IServiceCollection services)
        {
            services.TryAddSingleton<AssetManager>();
            services.TryAddSingleton<IAssetLoader<Texture2D>, TextureAssetLoader>();
            services.TryAddSingleton<IAssetLoader<SoundAsset>, SoundAssetLoader>();
            services.TryAddSingleton<IAssetLoader<FontAsset>, FontAssetLoader>();
            services.TryAddSingleton<Camera2D>();
            services.TryAddSingleton<Axolotl2D.Rendering.Rendering>();
            services.TryAddSingleton<IRendering>(provider => provider.GetRequiredService<Axolotl2D.Rendering.Rendering>());
            services.TryAddSingleton<SpriteBatch>();
            services.TryAddSingleton<TextRenderer>();
            services.TryAddScoped<IGameObjectFactory, GameObjectFactory>();
            services.TryAddSingleton<AudioPlayer>();
            return services;
        }

        /// <summary>Registers typed asset loading. Prefer <see cref="AddAxolotl2D"/> for new applications.</summary>
        public static void UseAssetManager(this IServiceCollection services)
        {
            services.AddAxolotl2D();
        }

        /// <summary>
        /// Registers a game scene.
        /// </summary>
        /// <typeparam name="T">Scene to register.</typeparam>
        /// <param name="services">Service provider</param>
        /// <exception cref="InvalidOperationException">Tried to register an abstract class or the BaseScene class itself.</exception>
        public static void AddScene<T>(this IServiceCollection services) where T : BaseScene
        {
            if (typeof(T).IsAbstract)
            {
                throw new InvalidOperationException("Game scene " + typeof(T).Name + " must NOT be an abstract class!");
            }

            services.AddScoped<T>();
        }

        /// <summary>
        /// Registers the Audio Player.
        /// </summary>
        /// <param name="services">Service provider</param>
        public static void UseAudioPlayer(this IServiceCollection services)
        {
            services.AddAxolotl2D();
        }
    }
}
