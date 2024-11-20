using Axolotl2D.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Exceptions
{
    public class ShaderNotCompiledException : Exception
    {
        public AxolotlShader Shader { get; }
        public ShaderNotCompiledException(string message, AxolotlShader shader) : base(message)
        {
            Shader = shader;
        }
    }
}
