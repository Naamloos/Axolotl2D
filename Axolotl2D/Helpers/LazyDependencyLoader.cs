using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                _value ??= _services.GetService<T>();
                return _value != null;
            }
        }

        /// <summary>
        /// Gets the value of the dependency.
        /// </summary>
        public T Value
        {
            get
            {
                _value ??= _services.GetService<T>();
                return _value!;
            }
        }

        private IServiceProvider _services;
        private T? _value;

        /// <summary>
        /// Creates a new instance of the LazyDependencyLoader.
        /// </summary>
        /// <param name="services"></param>
        public LazyDependencyLoader(IServiceProvider services)
        {
            _services = services;
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
