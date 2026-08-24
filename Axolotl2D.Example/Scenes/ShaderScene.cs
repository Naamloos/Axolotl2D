using Axolotl2D.Assets;
using Axolotl2D.Rendering;
using Axolotl2D.Shaders;
using Axolotl2D.Timing;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class ShaderScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    ShaderLibrary shaders,
    TimeService time) : ExampleSceneBase(assets)
{
    private Sprite logo = null!;
    private ShaderProgram pulse = null!;

    public override void Load()
    {
        LoadExample("Custom shaders", "#251636");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        logo = new Sprite(assets.Get<Texture2D>("logo"));
        pulse = shaders.Create(VertexShader, FragmentShader);
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        DrawText(spriteBatch, textRenderer, "Scoped GLSL program with a per-frame time uniform",
            new Vector2(24f, 70f), 15f);
        pulse.SetFloat("uTime", (float)time.UnscaledTotalTime);
        using (spriteBatch.UseShader(pulse))
        {
            spriteBatch.Draw(logo, new Vector2(320f, 180f), new Vector2(220f, 150f),
                space: CoordinateSpace.Screen, depth: 5f);
            spriteBatch.Draw(logo, new Vector2(650f, 330f), new Vector2(300f, 200f),
                rotation: -0.2f, space: CoordinateSpace.Screen, depth: 5f);
        }
    }

    private const string VertexShader = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec2 aTextureCoord;
        layout (location = 2) in vec4 aColor;
        out vec2 frag_texCoords;
        out vec4 frag_color;
        void main() {
            gl_Position = vec4(aPosition, 1.0);
            frag_texCoords = aTextureCoord;
            frag_color = aColor;
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        uniform sampler2D uTexture;
        uniform float uTime;
        in vec2 frag_texCoords;
        in vec4 frag_color;
        out vec4 out_color;
        void main() {
            float pulse = 0.65 + 0.35 * sin(uTime * 4.0 + frag_texCoords.x * 8.0);
            out_color = texture(uTexture, frag_texCoords) * frag_color * vec4(pulse, 1.0, 1.0, 1.0);
        }
        """;
}
