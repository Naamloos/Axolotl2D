using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D
{
    public class LazyLoader<T> : ILazyLoader<T>
    {
        public bool IsLoaded 
        { 
            get 
            { 
                _value ??= _services.GetService<T>();
                return _value != null;
            }
        }

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

        public LazyLoader(IServiceProvider services)
        {
            _services = services;
        }
    }

    public interface ILazyLoader<T>
    {
        public bool IsLoaded { get; }
        public T Value { get; }
    }
}
