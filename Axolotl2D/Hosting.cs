using Axolotl2D.Input;
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
        public static void AddGame<T>(this IServiceCollection services) where T : Game
        {
            services.AddSingleton<Game, T>();
            services.AddTransient(typeof(ILazyDependencyLoader<>), typeof(LazyDependencyLoader<>));
            services.AddHostedService<GameHost>();
        }
    }
}
