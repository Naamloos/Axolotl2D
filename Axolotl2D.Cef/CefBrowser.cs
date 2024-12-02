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
        private ChromiumWebBrowser? cefBrowser;
        private Vector2 renderedFrameSize;
        private IMouse mouse;
        private Vector2 mousePosition = Vector2.Zero;

        private string baseUrl = "https://www.google.com";
        private byte[] browserFrameBuffer = Array.Empty<byte>();
        private bool browserViewDirty = false;

        internal CefBrowser(Game game, Vector2 position, Vector2 size, string url) : base(game)
        {
            // directly setting internal variables to avoid calling the property setter
            this.position = position;
            this.size = size;
            baseUrl = url;

            mouse = game.GetMouse()!;
            mouse.MouseDown += mouseDown;
            mouse.MouseUp += mouseUp;
            mouse.Scroll += mouseScroll;
        }

        /// <summary>
        /// Enables the browser.
        /// </summary>
        /// <exception cref="InvalidOperationException">Browser was already enabled!</exception>
        public void Enable()
        {
            if(!_initialized)
            {
                cefBrowser = new ChromiumWebBrowser(baseUrl)
                {
                    Size = new System.Drawing.Size((int)Size.X, (int)Size.Y),
                };
                cefBrowser.Paint += OnBrowserPaint;
                cefBrowser.BrowserInitialized += BrowserInitialized;
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
            if (_initialized && cefBrowser is not null)
            {
                _initialized = false;
                cefBrowser.Paint -= OnBrowserPaint;
                cefBrowser.BrowserInitialized -= BrowserInitialized;
                cefBrowser.Dispose();
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
            baseUrl = url;
            if (_initialized && cefBrowser is not null)
            {
                cefBrowser.Load(url);
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
                cefBrowser.GetBrowserHost().SendMouseWheelEvent((int)mousePosition.X, (int)mousePosition.Y, (int)deltaX, (int)deltaY, CefEventFlags.None);
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
                cefBrowser.GetBrowserHost().SendMouseClickEvent(new MouseEvent((int)mousePosition.X, (int)mousePosition.Y, CefEventFlags.LeftMouseButton), TranslateMouseButton(arg2), false, 1);
        }

        private void mouseUp(IMouse arg1, MouseButton arg2)
        {
            if (_initialized)
                cefBrowser.GetBrowserHost().SendMouseClickEvent(new MouseEvent((int)mousePosition.X, (int)mousePosition.Y, CefEventFlags.LeftMouseButton), TranslateMouseButton(arg2), true, 1);
        }

        private bool _initialized = false;
        private void BrowserInitialized(object? sender, EventArgs e)
        {
            _initialized = true;
        }

        private void OnBrowserPaint(object? sender, OnPaintEventArgs e)
        {
            if (browserFrameBuffer.Length != e.Width * e.Height * 4)
                browserFrameBuffer = new byte[e.Width * e.Height * 4];

            Marshal.Copy(e.BufferHandle, browserFrameBuffer, 0, browserFrameBuffer.Length);
            renderedFrameSize = new Vector2(e.Width, e.Height);
            browserViewDirty = true;
        }

        internal override void OnResize(Vector2 size)
        {
            if (_initialized && cefBrowser is not null)
            {
                cefBrowser.Size = new System.Drawing.Size((int)size.X, (int)size.Y);
                cefBrowser.GetBrowserHost().Invalidate(PaintElementType.View);
            }

            base.OnResize(size);
        }

        /// <summary>
        /// Draws the browser to the screen.
        /// </summary>
        public override void Draw()
        {
            base.Draw();

            // update mouse position and clicks in the browser
            if (_initialized && mouse is not null && cefBrowser is not null)
            {
                float mouseX = (mouse.Position.X - Position.X) / Size.X * cefBrowser.Size.Width;
                float mouseY = (mouse.Position.Y - Position.Y) / Size.Y * cefBrowser.Size.Height;
                mousePosition = new Vector2(mouseX, mouseY);
                var mouseEvent = new MouseEvent((int)mouseX, (int)mouseY, CefEventFlags.None);
                cefBrowser.GetBrowserHost().SendMouseMoveEvent(mouseEvent, false);
            }
        }

        /// <summary>
        /// Calculates the vertices of the browser.
        /// </summary>
        internal override void CalculateVertices()
        {
            // The vertices are a bit different from the Sprite class, thus must be calculated differently.
            float x1 = Position.X / cachedViewport.X * 2 - 1;
            float y1 = 1 - (Position.Y + Size.Y) / cachedViewport.Y * 2;
            float x2 = (Position.X + Size.X) / cachedViewport.X * 2 - 1;
            float y2 = 1 - Position.Y / cachedViewport.Y * 2;

            Vector2 center = new((x1 + x2) / 2, (y1 + y2) / 2);

            Vector2[] vertices =
            [
                    new(x2, y1),
                    new(x2, y2),
                    new(x1, y2),
                    new(x1, y1)
            ];

            float cos = MathF.Cos(Rotation);
            float sin = MathF.Sin(Rotation);

            float aspectRatio = cachedViewport.X / cachedViewport.Y;

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

            base.vertices[0] = vertices[0].X;
            base.vertices[1] = vertices[0].Y;
            base.vertices[2] = 0;
            base.vertices[3] = 1.0f;
            base.vertices[4] = 1.0f;

            base.vertices[5] = vertices[1].X;
            base.vertices[6] = vertices[1].Y;
            base.vertices[7] = 0;
            base.vertices[8] = 1.0f;
            base.vertices[9] = 0.0f;

            base.vertices[10] = vertices[2].X;
            base.vertices[11] = vertices[2].Y;
            base.vertices[12] = 0;
            base.vertices[13] = 0.0f;
            base.vertices[14] = 0.0f;

            base.vertices[15] = vertices[3].X;
            base.vertices[16] = vertices[3].Y;
            base.vertices[17] = 0;
            base.vertices[18] = 0.0f;
            base.vertices[19] = 1.0f;
        }

        internal override void UpdateTexture()
        {
            if (browserViewDirty)
            {
                CalculateVertices();
                openGL.BindTexture(TextureTarget.Texture2D, texturePointer);

                openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                openGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                openGL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)renderedFrameSize.X, (uint)renderedFrameSize.Y, 0, PixelFormat.Bgra, PixelType.UnsignedByte, ref MemoryMarshal.GetReference(browserFrameBuffer.AsSpan()));

                int location = openGL.GetUniformLocation(game.shaderProgramPointer, "uTexture");
                openGL.Uniform1(location, 0);

                openGL.BindTexture(TextureTarget.Texture2D, 0);

                browserViewDirty = false;
            }
        }
    }
}
