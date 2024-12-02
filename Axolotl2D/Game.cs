using Microsoft.Extensions.Hosting;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace Axolotl2D
{
    /// <summary>
    /// Represents the base game class for Axolotl2D.
    /// </summary>
    public abstract partial class Game : IDisposable
    {
        /// <summary>
        /// The window title.
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// The current Viewport of the game.
        /// </summary>
        public Vector2 Viewport
        {
            get => new(window.Size.X, window.Size.Y);
            set => window.Size = new Vector2D<int>((int)value.X, (int)value.Y);
        }

        /// <summary>
        /// Represents the current framerate of the game.
        /// </summary>
        public double CurrentFramerate { get; private set; }

        /// <summary>
        /// Represents the clear color of the game.
        /// </summary>
        public Color ClearColor
        {
            get => clearColor;
            set
            {
                clearColor = value;
                openGL?.ClearColor(clearColor.R, clearColor.G, clearColor.B, clearColor.A);
            }
        }

        private Color clearColor = Color.Cyan;

        internal GL? openGL;
        internal readonly IWindow window;
        internal IInputContext? input;

        private Shaders.Shader? basicVertexShader;
        private Shaders.Shader? basicFragmentShader;

        internal uint shaderProgramPointer;

        internal IServiceProvider serviceProvider;

        /// <summary>
        /// Construct a new game.
        /// </summary>
        /// <param name="serviceProvider">Service provider to relay.</param>
        /// <param name="maxDrawRate">Maximum frame rate.</param>
        /// <param name="maxUpdateRate">Maximum update rate.</param>
        public Game(IServiceProvider serviceProvider, int maxDrawRate = 120, int maxUpdateRate = 120) // TODO make configurable at runtime
        {
            this.serviceProvider = serviceProvider;

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1080, 720);
            options.WindowClass = "axl2d";
            options.WindowBorder = WindowBorder.Resizable;
            options.FramesPerSecond = maxDrawRate;
            options.VSync = false;
            options.UpdatesPerSecond = maxUpdateRate;

            window = Window.Create(options);

            // Hook window events
            window.Load += Load;
            window.Render += Draw;
            window.FramebufferResize += Resize;
            window.Update += Update;
        }

        /// <summary>
        /// Gets the mouse input helper.
        /// </summary>
        /// <returns>Mouse input helper.</returns>
        public IMouse? GetMouse() => input?.Mice[0];

        /// <summary>
        /// Gets the keyboard input helper.
        /// </summary>
        /// <returns>Keyboard input helper.</returns>
        public IKeyboard? GetKeyboard() => input?.Keyboards[0];

        internal void Start()
        {
            window.Run();
        }

        internal void Stop()
        {
            // remove all events
            window.Load -= Load;
            window.Render -= Draw;
            window.FramebufferResize -= Resize;
            window.Update -= Update;

            window.Close();
        }

        private void Update(double frameDelta)
        {
            if (openGL is null)
                return;

            OnUpdate?.Invoke(frameDelta);
        }

        /// <summary>
        /// Gets called when the game expects to load resources.
        /// </summary>
        private void Load()
        {
            // Prepare OpenGL context on load.
            openGL = window.CreateOpenGL();

            openGL.ClearColor(ClearColor.R, ClearColor.G, ClearColor.B, ClearColor.A);

            // Load basic shaders
            basicVertexShader = Shaders.Shader.CreateBasicVertex(this);
            basicFragmentShader = Shaders.Shader.CreateBasicFragment(this);

            // Compile basic shaders
            basicVertexShader.Compile();
            basicFragmentShader.Compile();

            // Create shader program
            shaderProgramPointer = openGL.CreateProgram();

            // Attach basic shaders to program
            basicVertexShader.AttachToProgram();
            basicFragmentShader.AttachToProgram();

            openGL.LinkProgram(shaderProgramPointer);

            openGL.GetProgram(shaderProgramPointer, ProgramPropertyARB.LinkStatus, out int lStatus);
            if (lStatus != (int)GLEnum.True)
                throw new Exception("Program failed to link: " + openGL.GetProgramInfoLog(shaderProgramPointer));

            basicVertexShader.DetachFromProgram();
            basicFragmentShader.DetachFromProgram();

            openGL.Enable(EnableCap.Blend);
            openGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            input = window.CreateInput();

            OnLoad?.Invoke();
        }

        private void Resize(Vector2D<int> size)
        {
            if (openGL is null)
                return;

            // Handle resizes in GL context when window resizes.
            openGL.Viewport(size);

            this.OnResize?.Invoke(new Vector2(size.X, size.Y));
        }

        private void Draw(double frameDelta)
        {
            if(openGL is null)
                return;

            CurrentFramerate = Math.Ceiling(1.0f / frameDelta);

            openGL.UseProgram(shaderProgramPointer);

            openGL.Clear(ClearBufferMask.ColorBufferBit);

            OnDraw?.Invoke(frameDelta, CurrentFramerate);

            window.Title = $"{Title} | FPS: {Math.Round(CurrentFramerate)}";
        }

        /// <summary>
        /// Event that gets called when the game attempts to clean up.
        /// </summary>
        protected abstract void Cleanup();

        /// <summary>
        /// Disposes the game.
        /// </summary>
        public void Dispose()
        {
            Cleanup();
            window.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer for the game.
        /// </summary>
        ~Game() => Dispose();
    }
}