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
        public Shader Shader { get; }

        internal ShaderCompileException(string reason, Shader shader) : base(reason)
        {
            Shader = shader;
        }
    }
}
