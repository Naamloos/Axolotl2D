using Axolotl2D.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Axolotl2D.Cef
{
    /// <summary>
    /// Provides extension methods for hosting.
    /// </summary>
    public static class Hosting
    {
        /// <summary>
        /// Adds the CefBrowserManager to the service collection.
        /// </summary>
        /// <param name="services">Service Collection</param>
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
