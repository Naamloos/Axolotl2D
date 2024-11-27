using Axolotl2D.Entities;
using Axolotl2D.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D
{
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
        public static void UseSceneManagerGameHost<T>(this IServiceCollection services) where T: Game
        {
            if (services.Any(x => x.ImplementationType != null && x.ImplementationType.IsAssignableTo(typeof(IGameHost))))
            {
                throw new InvalidOperationException("Cannot register multiple IGameHost services!");
            }

            services.AddSingleton<Game, T>();
            services.AddSingleton<T, T>(x => (x.GetRequiredService<Game>() as T)!);
            services.AddHostedService<SceneGameHost>();
        }

        /// <summary>
        /// Registers the Asset Manager.
        /// </summary>
        /// <param name="services">Service Collection</param>
        public static void UseAssetManager(this IServiceCollection services)
        {
            if(!services.Any(x => x.ServiceType == typeof(ILazyDependencyLoader<>)))
            {
                services.AddTransient(typeof(ILazyDependencyLoader<>), typeof(LazyDependencyLoader<>));
            }

            services.AddSingleton<AssetManager>();
        }

        /// <summary>
        /// Registers a game scene.
        /// </summary>
        /// <typeparam name="T">Scene to register.</typeparam>
        /// <param name="services">Service provider</param>
        /// <exception cref="InvalidOperationException">Tried to register an abstract class or the BaseScene class itself.</exception>
        public static void AddScene<T>(this IServiceCollection services) where T : BaseScene
        {
            if(typeof(T).IsAbstract)
            {
                throw new InvalidOperationException("Game scene " + typeof(T).Name + " must NOT be an abstract class!");
            }

            services.AddTransient<T>();
        }
    }
}
