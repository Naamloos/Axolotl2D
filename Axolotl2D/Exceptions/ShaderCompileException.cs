using Axolotl2D.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Exceptions
{
    public class ShaderCompileException : Exception
    {
        public AxolotlShader Shader { get; }

        internal ShaderCompileException(string reason, AxolotlShader shader) : base(reason)
        {
            Shader = shader;
        }
    }
}
