using Microsoft.Extensions.DependencyInjection;

namespace Axolotl2D.Helpers
{
    /// <summary>
    /// A lazy dependency loader that can be used to check if a dependency is loaded and load it if it is not.
    /// </summary>
    /// <typeparam name="T">Type of the dependency to load.</typeparam>
    public class LazyDependencyLoader<T> : ILazyDependencyLoader<T>
    {
        /// <summary>
        /// Gets whether the dependency is loaded.
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                value ??= serviceProvider.GetService<T>();
                return value != null;
            }
        }

        /// <summary>
        /// Gets the value of the dependency.
        /// </summary>
        public T Value
        {
            get
            {
                value ??= serviceProvider.GetService<T>();
                return value!;
            }
        }

        private IServiceProvider serviceProvider;
        private T? value;

        /// <summary>
        /// Creates a new instance of the LazyDependencyLoader.
        /// </summary>
        /// <param name="serviceProvider"></param>
        public LazyDependencyLoader(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }
    }

    /// <summary>
    /// Represents a lazy dependency loader.
    /// </summary>
    /// <typeparam name="T">Type of dependency to load.</typeparam>
    public interface ILazyDependencyLoader<T>
    {
        /// <summary>
        /// Gets whether the dependency is loaded.
        /// </summary>
        public bool IsLoaded { get; }

        /// <summary>
        /// Gets the value of the dependency.
        /// </summary>
        public T Value { get; }
    }
}
