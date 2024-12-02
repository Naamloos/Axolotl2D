using Silk.NET.OpenGL;
using StbImageSharp;
using System.Numerics;

namespace Axolotl2D.Drawable
{
    /// <summary>
    /// Represents an image that can be drawn to the screen.
    /// Use <seealso cref="SpriteManager.LoadSprite(string, Stream)"/> to instantiate this class.
    /// </summary>
    public class Sprite : BaseDrawable
    {
        private readonly Stream imageFile;

        internal Sprite(Game game, Stream imageFile) : base(game)
        {
            this.imageFile = imageFile;
        }

        private bool textureLoaded = false;

        private int wrapMode = (int)TextureWrapMode.Repeat;
        private int minFilter = (int)TextureMinFilter.Nearest;
        private int magFilter = (int)TextureMagFilter.Nearest;
        internal override void UpdateTexture()
        {
            if (textureLoaded) // Ensure we don't load the texture on every tick.
                return;

            openGL.BindTexture(TextureTarget.Texture2D, texturePointer);

            ImageResult img = ImageResult.FromStream(imageFile, ColorComponents.RedGreenBlueAlpha);

            ReadOnlySpan<byte> imgDataSpan = img.Data.AsSpan();
            openGL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)img.Width,
                (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, imgDataSpan);

            openGL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, ref wrapMode);
            openGL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, ref wrapMode);
            openGL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, ref minFilter);
            openGL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, ref magFilter);

            int location = openGL.GetUniformLocation(game.shaderProgramPointer, "uTexture");
            openGL.Uniform1(location, 0);

            openGL.BindTexture(TextureTarget.Texture2D, 0);

            textureLoaded = true;
        }
    }
}
