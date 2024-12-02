using Axolotl2D.Exceptions;
using Silk.NET.OpenGL;

namespace Axolotl2D.Shaders
{
    /// <summary>
    /// Represents a shader in the game.
    /// Do note that this class is not meant to be used directly.
    /// This wil change in the future as I figure out how to implement userland shaders.
    /// </summary>
    public class Shader
    {
        private string glslSourceCode;
        private GL openGL;
        private uint shaderPointer;
        private bool hasBeenCompiled = false;
        private ShaderType shaderType;
        private Game game;

        /// <summary>
        /// Initialize a new Shader object.
        /// </summary>
        /// <param name="glslSourceCode">GLSL code for this shader</param>
        /// <param name="shaderType">Type of this shader</param>
        /// <param name="game">Game to initialize shader on</param>
        public Shader(string glslSourceCode, ShaderType shaderType, Game game)
        {
            this.glslSourceCode = glslSourceCode;
            openGL = game.openGL!;
            this.shaderType = shaderType;
            this.game = game;
        }

        /// <summary>
        /// Compile the shader.
        /// </summary>
        /// <exception cref="ShaderCompileException">Shader failed to compile</exception>
        public void Compile()
        {
            uint shaderPointer = openGL.CreateShader(shaderType);
            openGL.ShaderSource(shaderPointer, glslSourceCode);
            openGL.CompileShader(shaderPointer);

            openGL.GetShader(shaderPointer, ShaderParameterName.CompileStatus, out int vertexShaderStatus);
            if (vertexShaderStatus != (int)GLEnum.True)
                throw new ShaderCompileException("Shader failed to compile: " + openGL.GetShaderInfoLog(shaderPointer), this);

            hasBeenCompiled = true;
            this.shaderPointer = shaderPointer;
        }

        /// <summary>
        /// Attach the shader to the program.
        /// </summary>
        /// <exception cref="ShaderNotCompiledException">Shader was not compiled yet</exception>
        public void AttachToProgram()
        {
            if (!hasBeenCompiled)
                throw new ShaderNotCompiledException("Tried attaching a shader to a program while it was not compiled yet!", this);
            openGL.AttachShader(game.shaderProgramPointer, shaderPointer);
        }

        /// <summary>
        /// Detach the shader from the program.
        /// </summary>
        /// <exception cref="ShaderNotCompiledException">Shader was not compiled yet</exception>
        public void DetachFromProgram()
        {
            if (!hasBeenCompiled)
                throw new ShaderNotCompiledException("Tried detaching a shader from a program while it was not compiled yet!", this);
            openGL.DetachShader(game.shaderProgramPointer, shaderPointer);
            openGL.DeleteShader(shaderPointer);
            hasBeenCompiled = false;
        }

        /// <summary>
        /// Get the pointer to the shader.
        /// </summary>
        /// <returns>Pointer to the shader</returns>
        /// <exception cref="ShaderNotCompiledException">Shader was not compiled yet</exception>
        public uint GetPointer() => hasBeenCompiled ? shaderPointer : throw new ShaderNotCompiledException("Tried accessing the shader pointer to a shader that was not compiled yet!", this);

        /// <summary>
        /// Create a basic fragment shader.
        /// </summary>
        /// <param name="game">Game to create the shader on.</param>
        /// <returns>A basic fragment shader</returns>
        public static Shader CreateBasicFragment(Game game)
        {
            // load from embedded resources as text
            using var shader = typeof(Shader).Assembly.GetManifestResourceStream("Axolotl2D.Shaders.BasicFragment.glsl");
            using var reader = new StreamReader(shader!);
            return new Shader(reader.ReadToEnd(), ShaderType.FragmentShader, game);
        }

        /// <summary>
        /// Create a basic vertex shader.
        /// </summary>
        /// <param name="game">Game to create the shader on.</param>
        /// <returns>A basic vertex shader</returns>
        public static Shader CreateBasicVertex(Game game)
        {
            // load from embedded resources as text
            using var shader = typeof(Shader).Assembly.GetManifestResourceStream("Axolotl2D.Shaders.BasicVertex.glsl");
            using var reader = new StreamReader(shader!);
            return new Shader(reader.ReadToEnd(), ShaderType.VertexShader, game);
        }
    }
}
