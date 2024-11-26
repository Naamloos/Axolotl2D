using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Cef
{
    public static class Hosting
    {
        public static void UseCefBrowserManager(this IServiceCollection services)
        {
            if (!services.Any(x => x.ServiceType == typeof(ILazyDependencyLoader<>)))
            {
                services.AddTransient(typeof(ILazyDependencyLoader<>), typeof(LazyDependencyLoader<>));
            }
            services.AddSingleton<CefBrowserManager>();
        }
    }
}
