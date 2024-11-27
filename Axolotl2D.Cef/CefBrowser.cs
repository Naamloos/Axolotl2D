using Axolotl2D.Drawable;
using CefSharp;
using CefSharp.OffScreen;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

// https://github.com/cefsharp/CefSharp/wiki/General-Usage#need-to-knowlimitations
// https://github.com/cefsharp/CefSharp/issues/1714

namespace Axolotl2D.Cef
{
    // Requires setting a runtime identifier
    // <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>

    /// <summary>
    /// Represents a CefSharp browser that can be drawn to the screen.
    /// </summary>
    public class CefBrowser : BaseDrawable
    {
        private GL _gl;
        private ChromiumWebBrowser? _browser;
        private uint _vbo;
        private uint _ebo;
        private uint _vao;
        private uint _texture;
        private Vector2 _renderedSize;

        private IMouse? _mouse;

        private Vector2 _mousePos = Vector2.Zero;

        private string _url = "https://www.google.com";

        internal CefBrowser(Game game, Vector2 position, Vector2 size, string url) : base(game, position, size)
        {
            _url = url;

            _gl = game._openGL ?? throw new ArgumentNullException(nameof(game));
            InitializeBuffers();
            InitializeTexture();
            
            _mouse = game.GetMouse();
            _mouse!.MouseDown += mouseDown;
            _mouse!.MouseUp += mouseUp;
            _mouse!.Scroll += mouseScroll;
        }

        /// <summary>
        /// Enables the browser.
        /// </summary>
        /// <exception cref="InvalidOperationException">Browser was already enabled!</exception>
        public void Enable()
        {
            if(!_initialized)
            {
                _browser = new ChromiumWebBrowser(_url)
                {
                    Size = new System.Drawing.Size((int)Size.X, (int)Size.Y),
                };
                _browser.Paint += OnBrowserPaint;
                _browser.BrowserInitialized += BrowserInitialized;
            }
            else
            {
                throw new InvalidOperationException("Browser already enabled!");
            }
        }

        /// <summary>
        /// Disables the browser.
        /// </summary>
        /// <exception cref="InvalidOperationException">Browser was already disabled!</exception>
        public void Disable()
        {
            if (_initialized && _browser is not null)
            {
                _initialized = false;
                _browser.Paint -= OnBrowserPaint;
                _browser.BrowserInitialized -= BrowserInitialized;
                _browser.Dispose();
            }
            else
            {
                throw new InvalidOperationException("Browser already disabled!");
            }
        }

        /// <summary>
        /// Sets the URL of the browser.
        /// </summary>
        /// <param name="url">URL to set the browser to.</param>
        public void SetUrl(string url)
        {
            _url = url;
            if (_initialized && _browser is not null)
            {
                _browser.Load(url);
            }
        }

        private float oldScrollX = 0;
        private float oldScrollY = 0;
        private void mouseScroll(IMouse arg1, ScrollWheel arg2)
        {
            oldScrollX = arg2.X;
            oldScrollY = arg2.Y;
            var deltaX = oldScrollX;
            var deltaY = oldScrollY;
            if(_initialized)
                _browser.GetBrowserHost().SendMouseWheelEvent((int)_mousePos.X, (int)_mousePos.Y, (int)deltaX, (int)deltaY, CefEventFlags.None);
        }

        private MouseButtonType TranslateMouseButton(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => MouseButtonType.Left,
                MouseButton.Right => MouseButtonType.Right,
                MouseButton.Middle => MouseButtonType.Middle,
                _ => MouseButtonType.Left,
            };
        }

        private void mouseDown(IMouse arg1, MouseButton arg2)
        {
            // relay to browser
            if(_initialized)
                _browser.GetBrowserHost().SendMouseClickEvent(new MouseEvent((int)_mousePos.X, (int)_mousePos.Y, CefEventFlags.LeftMouseButton), TranslateMouseButton(arg2), false, 1);
        }

        private void mouseUp(IMouse arg1, MouseButton arg2)
        {
            if (_initialized)
                _browser.GetBrowserHost().SendMouseClickEvent(new MouseEvent((int)_mousePos.X, (int)_mousePos.Y, CefEventFlags.LeftMouseButton), TranslateMouseButton(arg2), true, 1);
        }

        private bool _initialized = false;
        private void BrowserInitialized(object? sender, EventArgs e)
        {
            _initialized = true;
            _browser.SetZoomLevel(0.25);
        }

        private unsafe void InitializeBuffers()
        {
            // Create a VAO.
            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            // Create a VBO.
            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);

            // Create an EBO.
            _ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

            fixed (void* indices = _indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(_indices.Length * sizeof(uint)), indices, BufferUsageARB.StaticDraw);

            const uint positionLocation = 0;
            _gl.EnableVertexAttribArray(positionLocation);
            _gl.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            const uint texCoordLocation = 1;
            _gl.EnableVertexAttribArray(texCoordLocation);
            _gl.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
        }

        private unsafe void InitializeTexture()
        {
            _texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)Size.X, (uint)Size.Y, 0, PixelFormat.Bgra, PixelType.UnsignedByte, null);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void OnBrowserPaint(object? sender, OnPaintEventArgs e)
        {
            if (frameBuffer.Length != e.Width * e.Height * 4)
                frameBuffer = new byte[e.Width * e.Height * 4];

            Marshal.Copy(e.BufferHandle, frameBuffer, 0, frameBuffer.Length);
            _renderedSize = new Vector2(e.Width, e.Height);
            dirty = true;
        }

        internal override void onResize(Vector2 size)
        {
            if (_initialized && _browser is not null)
            {
                _browser.Size = new System.Drawing.Size((int)size.X, (int)size.Y);
                _browser.GetBrowserHost().Invalidate(PaintElementType.View);
            }
            base.onResize(size);
        }

        private byte[] frameBuffer = Array.Empty<byte>();
        private bool dirty = false;

        private unsafe void updateFromBuffer()
        {
            if (dirty)
            {
                calculateVertices();
                _gl.BindTexture(TextureTarget.Texture2D, _texture);

                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                fixed (byte* ptr = frameBuffer)
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)_renderedSize.X, (uint)_renderedSize.Y, 0, PixelFormat.Bgra, PixelType.UnsignedByte, ptr);

                int location = _gl.GetUniformLocation(_game._shaderProgram, "uTexture");
                _gl.Uniform1(location, 0);

                _gl.BindTexture(TextureTarget.Texture2D, 0);

                dirty = false;
            }
        }

        /// <summary>
        /// Draws the browser to the screen.
        /// </summary>
        public unsafe override void Draw()
        {
            updateFromBuffer();

            _gl = _game._openGL ?? throw new ArgumentNullException(nameof(_game._openGL));

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            _gl.BindVertexArray(_vao);
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
            _gl.BindVertexArray(0);

            // update mouse position and clicks in the browser
            if (_initialized && _mouse is not null && _browser is not null)
            {
                float mouseX = (_mouse.Position.X - Position.X) / Size.X * _browser.Size.Width;
                float mouseY = (_mouse.Position.Y - Position.Y) / Size.Y * _browser.Size.Height;
                _mousePos = new Vector2(mouseX, mouseY);
                var mouseEvent = new MouseEvent((int)mouseX, (int)mouseY, CefEventFlags.None);
                _browser.GetBrowserHost().SendMouseMoveEvent(mouseEvent, false);
            }
        }

        /// <summary>
        /// Draws the browser to the screen at the given position.
        /// </summary>
        /// <param name="position">Position to draw at.</param>
        public override void Draw(Vector2 position)
        {
            Position = position;
            Draw();
        }

        /// <summary>
        /// Draws the browser to the screen at the given position and size.
        /// </summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="size">Size to draw with.</param>
        public override void Draw(Vector2 position, Vector2 size)
        {
            Bounds = (position, size);
            Draw();
        }

        internal override void calculateVertices()
        {
            float x1 = Position.X / _cachedViewport.X * 2 - 1;
            float y1 = 1 - (Position.Y + Size.Y) / _cachedViewport.Y * 2;
            float x2 = (Position.X + Size.X) / _cachedViewport.X * 2 - 1;
            float y2 = 1 - Position.Y / _cachedViewport.Y * 2;

            Vector2 center = new Vector2((x1 + x2) / 2, (y1 + y2) / 2);

            Vector2[] vertices = new Vector2[]
            {
                    new Vector2(x2, y1),
                    new Vector2(x2, y2),
                    new Vector2(x1, y2),
                    new Vector2(x1, y1)
            };

            float cos = MathF.Cos(Rotation);
            float sin = MathF.Sin(Rotation);

            float aspectRatio = _cachedViewport.X / _cachedViewport.Y;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 dir = vertices[i] - center;
                dir.X *= aspectRatio; // Adjust for aspect ratio
                vertices[i] = new Vector2(
                    dir.X * cos - dir.Y * sin,
                    dir.X * sin + dir.Y * cos
                );
                vertices[i].X /= aspectRatio; // Revert aspect ratio adjustment
                vertices[i] += center;
            }

            _vertices[0] = vertices[0].X;
            _vertices[1] = vertices[0].Y;
            _vertices[2] = 0;
            _vertices[3] = 1.0f;
            _vertices[4] = 1.0f;

            _vertices[5] = vertices[1].X;
            _vertices[6] = vertices[1].Y;
            _vertices[7] = 0;
            _vertices[8] = 1.0f;
            _vertices[9] = 0.0f;

            _vertices[10] = vertices[2].X;
            _vertices[11] = vertices[2].Y;
            _vertices[12] = 0;
            _vertices[13] = 0.0f;
            _vertices[14] = 0.0f;

            _vertices[15] = vertices[3].X;
            _vertices[16] = vertices[3].Y;
            _vertices[17] = 0;
            _vertices[18] = 0.0f;
            _vertices[19] = 1.0f;
        }

    }
}
