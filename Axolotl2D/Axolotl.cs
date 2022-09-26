using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Axolotl2D
{
    public abstract class Axolotl : IDisposable
    {
        public double CurrentFramerate
        {
            get => _currentFramerate;
        }
        private double _currentFramerate = 0;

        private readonly IWindow _window;
        private GL? _gl;
        private ImGuiController? _imgui;

        public Axolotl(string title, int width, int height)
        {
            var options = WindowOptions.Default;
            options.Title = title;
            options.Size = new Vector2D<int>(width, height);
            options.WindowClass = "axolotl2d";
            options.WindowBorder = WindowBorder.Fixed;

            _window = Window.Create(options);

            // Hook window events
            _window.Load += onWindowLoad;
            _window.Render += onWindowRender;
            _window.FramebufferResize += onWindowResize;
        }

        public void Start()
        {
            _window.Run();
        }

        public abstract void OnLoad();
        private void onWindowLoad()
        {
            // Prepare OpenGL context on load.
            _gl = _window.CreateOpenGL();
            _gl.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
            _imgui = new ImGuiController(_gl, _window, _window.CreateInput());
            OnLoad();
        }

        public abstract void OnResize();
        private void onWindowResize(Vector2D<int> size)
        {
            if (_gl is null)
                return;

            // Handle resizes in GL context when window resizes.
            _gl.Viewport(size);

            OnResize();
        }

        public abstract void OnDraw(double frameDelta, double frameRate);
        private void onWindowRender(double delta)
        {
            if(_gl is null || _imgui is null)
                return;

            _currentFramerate = 1.0f / delta;

            _imgui.Update((float)delta);

            _gl.Clear(ClearBufferMask.ColorBufferBit);
            ImGuiNET.ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0));
            ImGuiNET.ImGui.Text("Axolotl2D debug menu");
            ImGuiNET.ImGui.Text($"Framerate: {_currentFramerate}");
            if(ImGuiNET.ImGui.Button("force throw exception"))
            {
                throw new Exception("Forced exception from engine debugger");
            }
            _imgui.Render();

            OnDraw(delta, _currentFramerate);
        }

        public void Dispose()
        {
            _window.Dispose();
        }

        ~Axolotl() => Dispose();
    }
}