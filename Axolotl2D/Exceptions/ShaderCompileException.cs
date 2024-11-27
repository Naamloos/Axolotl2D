using Axolotl2D.Shaders;

namespace Axolotl2D.Exceptions
{
    /// <summary>
    /// Thrown when a shader fails to compile.
    /// </summary>
    public class ShaderCompileException : Exception
    {
        /// <summary>
        /// The shader that failed to compile.
        /// </summary>
        public Shader Shader { get; }

        internal ShaderCompileException(string reason, Shader shader) : base(reason)
        {
            Shader = shader;
        }
    }
}
