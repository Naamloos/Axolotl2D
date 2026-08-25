using Microsoft.Extensions.Hosting;
using Axolotl2D.Rendering;
using Axolotl2D.Input;
using Axolotl2D.Timing;
using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;
using System.Diagnostics;

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
        public string Title
        {
            get => title;
            set
            {
                title = value ?? throw new ArgumentNullException(nameof(value));
                UpdateWindowTitle();
            }
        }

        /// <summary>
        /// The current Viewport of the game.
        /// </summary>
        public Vector2 Viewport
        {
            get => new(window.Size.X, window.Size.Y);
            set
            {
                ValidateSize(value);
                window.Size = new Vector2D<int>((int)value.X, (int)value.Y);
            }
        }

        public bool VSync
        {
            get => window.VSync;
            set => window.VSync = value;
        }

        public double MaximumDrawRate
        {
            get => window.FramesPerSecond;
            set
            {
                ValidateRate(value, nameof(MaximumDrawRate));
                window.FramesPerSecond = value;
            }
        }

        public double MaximumUpdateRate
        {
            get => window.UpdatesPerSecond;
            set
            {
                ValidateRate(value, nameof(MaximumUpdateRate));
                window.UpdatesPerSecond = value;
            }
        }

        public bool ShowFramerateInTitle
        {
            get => showFramerateInTitle;
            set
            {
                showFramerateInTitle = value;
                UpdateWindowTitle();
            }
        }

        public GameWindowMode WindowMode
        {
            get => windowMode;
            set => SetWindowMode(value);
        }

        /// <summary>
        /// Represents the current framerate of the game.
        /// </summary>
        public double CurrentFramerate { get; private set; }

        /// <summary>Time spent in update callbacks during the previous update, in milliseconds.</summary>
        public double LastUpdateMilliseconds { get; private set; }

        /// <summary>Time spent producing the previous completed frame, in milliseconds.</summary>
        public double LastDrawMilliseconds { get; private set; }

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
        private string title = "";
        private bool showFramerateInTitle = true;
        private GameWindowMode windowMode;
        private readonly WindowBorder windowedBorder;
        private Vector2D<int> windowedSize;
        private Vector2D<int> windowedPosition;
        private bool hasWindowedBounds;

        internal GL? openGL;
        internal readonly IWindow window;
        internal IInputContext? input;

        private Shaders.Shader? basicVertexShader;
        private Shaders.Shader? basicFragmentShader;

        internal uint shaderProgramPointer;

        private IRendering? rendering;
        private InputActionSystem? inputActions;
        private TimeService? time;
        private CameraManager? cameras;
        private Audio.AudioRuntime? audioRuntime;
        private int closed;
        private int disposed;

        internal IServiceProvider serviceProvider;

        /// <summary>
        /// Construct a new game.
        /// </summary>
        /// <param name="serviceProvider">Service provider to relay.</param>
        /// <param name="maxDrawRate">Maximum frame rate.</param>
        /// <param name="maxUpdateRate">Maximum update rate.</param>
        public Game(IServiceProvider serviceProvider, int maxDrawRate = 120, int maxUpdateRate = 120)
            : this(serviceProvider, new GameWindowOptions
            {
                MaximumDrawRate = maxDrawRate,
                MaximumUpdateRate = maxUpdateRate
            })
        {
        }

        public Game(IServiceProvider serviceProvider, GameWindowOptions settings)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            ArgumentNullException.ThrowIfNull(settings);
            ValidateSize(settings.Size);
            ValidateRate(settings.MaximumDrawRate, nameof(settings.MaximumDrawRate));
            ValidateRate(settings.MaximumUpdateRate, nameof(settings.MaximumUpdateRate));
            if (!Enum.IsDefined(settings.Mode))
                throw new ArgumentOutOfRangeException(nameof(settings.Mode));

            title = settings.Title ?? throw new ArgumentNullException(nameof(settings.Title));
            showFramerateInTitle = settings.ShowFramerateInTitle;
            windowMode = settings.Mode;
            windowedBorder = settings.Resizable ? WindowBorder.Resizable : WindowBorder.Fixed;
            windowedSize = new Vector2D<int>((int)settings.Size.X, (int)settings.Size.Y);

            var options = WindowOptions.Default;
            options.Size = windowedSize;
            options.Title = title;
            options.WindowClass = "axl2d";
            options.WindowBorder = windowMode == GameWindowMode.BorderlessFullscreen
                ? WindowBorder.Hidden
                : windowedBorder;
            options.WindowState = windowMode == GameWindowMode.Fullscreen
                ? WindowState.Fullscreen
                : WindowState.Normal;
            options.FramesPerSecond = settings.MaximumDrawRate;
            options.VSync = settings.VSync;
            options.UpdatesPerSecond = settings.MaximumUpdateRate;

            window = Window.Create(options);

            // Hook window events
            window.Load += Load;
            window.Render += Draw;
            window.FramebufferResize += Resize;
            window.Update += Update;
            window.Closing += Close;
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

        /// <summary>Gets the first connected gamepad, if present.</summary>
        public IGamepad? GetGamepad() => input?.Gamepads.FirstOrDefault();

        /// <summary>Gets a connected gamepad by device index, if present.</summary>
        public IGamepad? GetGamepad(int index) =>
            input?.Gamepads.FirstOrDefault(gamepad => gamepad.Index == index);

        /// <summary>
        /// Loads application resources before the window and scene start.
        /// </summary>
        /// <param name="cancellationToken">Stops startup resource loading.</param>
        protected virtual Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        internal async Task InitializeGameAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        internal void Start()
        {
            var completed = false;
            try
            {
                window.Run();
                completed = true;
            }
            finally
            {
                try
                {
                    Close();
                }
                finally
                {
                    Dispose(completed);
                }
            }
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

            var started = Stopwatch.GetTimestamp();
            try
            {
                time!.BeginFrame(frameDelta);
                audioRuntime!.Update();
                inputActions!.Update();
                OnUpdate?.Invoke(time.DeltaTime);
                cameras!.Update(time.DeltaTime);
            }
            finally
            {
                LastUpdateMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Gets called when the game expects to load resources.
        /// </summary>
        private void Load()
        {
            if (windowMode == GameWindowMode.BorderlessFullscreen)
                ApplyBorderlessFullscreen();

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

            rendering = serviceProvider.GetRequiredService<IRendering>();
            rendering.Initialize();

            input = window.CreateInput();
            time = serviceProvider.GetRequiredService<TimeService>();
            inputActions = serviceProvider.GetRequiredService<InputActionSystem>();
            cameras = serviceProvider.GetRequiredService<CameraManager>();
            audioRuntime = serviceProvider.GetRequiredService<Audio.AudioRuntime>();

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
            if (openGL is null)
                return;

            var started = Stopwatch.GetTimestamp();
            CurrentFramerate = frameDelta > 0d ? 1d / frameDelta : 0d;

            openGL.UseProgram(shaderProgramPointer);

            openGL.Clear(ClearBufferMask.ColorBufferBit);
            rendering!.BeginFrame();
            try
            {
                OnDraw?.Invoke(frameDelta, CurrentFramerate);
            }
            finally
            {
                rendering.EndFrame();
                LastDrawMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }

            UpdateWindowTitle();
        }

        private void SetWindowMode(GameWindowMode mode)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (windowMode == mode)
                return;

            if (windowMode == GameWindowMode.Windowed)
            {
                windowedSize = window.Size;
                windowedPosition = window.Position;
                hasWindowedBounds = true;
            }

            windowMode = mode;
            switch (mode)
            {
                case GameWindowMode.Windowed:
                    window.WindowState = WindowState.Normal;
                    window.WindowBorder = windowedBorder;
                    if (hasWindowedBounds)
                        window.Position = windowedPosition;
                    window.Size = windowedSize;
                    break;
                case GameWindowMode.BorderlessFullscreen:
                    ApplyBorderlessFullscreen();
                    break;
                case GameWindowMode.Fullscreen:
                    window.WindowBorder = windowedBorder;
                    window.WindowState = WindowState.Fullscreen;
                    break;
            }
        }

        private void ApplyBorderlessFullscreen()
        {
            var bounds = window.Monitor.Bounds;
            window.WindowState = WindowState.Normal;
            window.WindowBorder = WindowBorder.Hidden;
            window.Position = bounds.Origin;
            window.Size = bounds.Size;
        }

        private void UpdateWindowTitle()
        {
            window.Title = showFramerateInTitle
                ? $"{title} | FPS: {Math.Round(CurrentFramerate)}"
                : title;
        }

        private static void ValidateSize(Vector2 size)
        {
            if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(size));
        }

        private static void ValidateRate(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0d)
                throw new ArgumentOutOfRangeException(name);
        }

        private void Close()
        {
            if (Interlocked.Exchange(ref closed, 1) != 0)
                return;
            try
            {
                Closing?.Invoke();
            }
            finally
            {
                rendering?.Dispose();
            }
        }

        /// <summary>
        /// Event that gets called when the game attempts to clean up.
        /// </summary>
        protected abstract void Cleanup();

        /// <summary>
        /// Disposes the game.
        /// </summary>
        public void Dispose() => Dispose(true);

        private void Dispose(bool disposeWindow)
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            try
            {
                Cleanup();
            }
            finally
            {
                window.Closing -= Close;
                if (disposeWindow)
                    window.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}
