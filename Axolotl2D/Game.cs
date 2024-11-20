using Axolotl2D.Entities;
using Axolotl2D.Exceptions;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Axolotl2D
{
    public abstract class Game : IDisposable
    {
        public bool ClearOnDraw { get; set; } = true;
        public string Title { get; set; } = "";

        public double CurrentFramerate => _currentFramerate;
        private double _currentFramerate = 0;

        private GL? OpenGL;

        private readonly IWindow _window;
        private int _width;
        private int _height;

        private AxolotlColor _clearColor;
        private AxolotlShader? BasicVertex;
        private AxolotlShader? BasicFragment;

        private uint shaderProgram;

        public Game(int width, int height, AxolotlColor? clearColor = null, int maxDrawRate = 120, int maxUpdateRate = 120)
        {
            _width = width;
            _height = height;

            _clearColor = clearColor ?? AxolotlColor.FromRGB(0.4f, 0.6f, 1.0f);

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(_width, _height);
            options.WindowClass = "axl2d";
            options.WindowBorder = WindowBorder.Resizable;
            options.FramesPerSecond = maxDrawRate;
            options.VSync = false;
            options.UpdatesPerSecond = maxUpdateRate;

            _window = Window.Create(options);

            // Hook window events
            _window.Load += _onLoad;
            _window.Render += _onDraw;
            _window.FramebufferResize += _onResize;
            _window.Update += _onUpdate;
        }

        /// <summary>
        /// Gets called every update frame.
        /// </summary>
        /// <param name="frameDelta"></param>
        public abstract void OnUpdate(double frameDelta);

        private DateTimeOffset? lastFixedUpdate;
        private void _onUpdate(double frameDelta)
        {
            if (OpenGL is null)
                return;

            OnUpdate(frameDelta);
        }

        /// <summary>
        /// Starts the game loop.
        /// </summary>
        public void Start()
        {
            _window.Run();
        }

        /// <summary>
        /// Gets called when the game expects to load resources.
        /// </summary>
        public abstract void OnLoad();
        private void _onLoad()
        {
            // Prepare OpenGL context on load.
            OpenGL = _window.CreateOpenGL();

            OpenGL.ClearColor(_clearColor.R, _clearColor.G, _clearColor.B, _clearColor.A);

            // Load basic shaders
            BasicVertex = AxolotlShader.CreateBasicVertex(this);
            BasicFragment = AxolotlShader.CreateBasicFragment(this);

            // Compile basic shaders
            BasicVertex.Compile();
            BasicFragment.Compile();

            // Create shader program
            shaderProgram = OpenGL.CreateProgram();

            // Attach basic shaders to program
            BasicVertex.AttachToProgram();
            BasicFragment.AttachToProgram();

            OpenGL.LinkProgram(shaderProgram);

            OpenGL.GetProgram(shaderProgram, ProgramPropertyARB.LinkStatus, out int lStatus);
            if (lStatus != (int)GLEnum.True)
                throw new Exception("Program failed to link: " + OpenGL.GetProgramInfoLog(shaderProgram));

            BasicVertex.DetachFromProgram();
            BasicFragment.DetachFromProgram();

            OnLoad();
        }

        /// <summary>
        /// Gets called when the window resizes.
        /// </summary>
        public abstract void OnResize();
        private void _onResize(Vector2D<int> size)
        {
            if (OpenGL is null)
                return;

            // Handle resizes in GL context when window resizes.
            OpenGL.Viewport(size);
            _width = size.X;
            _height = size.Y;

            OnResize();
        }

        /// <summary>
        /// Gets called every frame.
        /// </summary>
        /// <param name="frameDelta"></param>
        /// <param name="frameRate"></param>
        public abstract void OnDraw(double frameDelta, double frameRate);
        private void _onDraw(double delta)
        {
            if(OpenGL is null)
                return;

            _currentFramerate = 1.0f / delta;

            if(ClearOnDraw)
                Clear();

            OnDraw(delta, _currentFramerate);

            _window.Title = $"{Title} | FPS: {Math.Round(_currentFramerate)}";

            _window.WindowBorder = WindowBorder.Fixed;
        }

        /// <summary>
        /// Clears the screen. Do not call this when ClearOnDraw is set to true. It's called automatically.
        /// </summary>
        public void Clear()
        {
            if (OpenGL is null)
                return;

            OpenGL.Clear(ClearBufferMask.ColorBufferBit);
        }

        internal GL GetOpenGLContext() => OpenGL ?? throw new EngineUninitializedException("Tried accessing the game's GL context without the engine being initialized!", this);
        internal uint GetShaderProgram() => shaderProgram;
        public int GetWidth() => _width;
        public int GetHeight() => _height;

        /// <summary>
        /// Disposes the game.
        /// </summary>
        public void Dispose()
        {
            _window.Dispose();
        }

        ~Game() => Dispose();
    }
}