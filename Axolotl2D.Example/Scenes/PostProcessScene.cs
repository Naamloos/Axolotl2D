using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Axolotl2D.Shaders;
using Axolotl2D.Timing;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class PostProcessScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    ShaderLibrary shaders,
    InputActionMap input,
    TimeService time) : ExampleSceneBase(assets)
{
    private PostProcessEffect distortion = null!;
    private PostProcessEffect grayscale = null!;
    private PostProcessEffect vignette = null!;
    private PostProcessEffect pixelation = null!;
    private PostProcessEffect chromaticAberration = null!;
    private InputAction toggleDistortion = null!;
    private InputAction toggleGrayscale = null!;
    private InputAction toggleVignette = null!;
    private InputAction togglePixelation = null!;
    private InputAction toggleChromaticAberration = null!;
    private InputAction shake = null!;

    public override void Load()
    {
        LoadExample("Camera post-processing", "#171B2E");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;

        var logo = new Sprite(assets.Get<Texture2D>("logo"));
        for (var index = 0; index < 15; index++)
        {
            var image = Instantiate($"Post-processing subject {index + 1}");
            image.Transform.LocalPosition = new Vector2((index % 5 - 2) * 180f, (index / 5 - 1) * 145f);
            image.Transform.LocalScale = new Vector2(0.12f + index % 3 * 0.025f);
            image.Transform.LocalRotation = (index % 2 == 0 ? 1f : -1f) * index * 0.035f;
            image.AddComponent<SpriteRenderer>().Sprite = logo;
        }

        distortion = shaders.CreatePostProcess(camera, DistortionShader);
        grayscale = shaders.CreatePostProcess(camera, GrayscaleShader);
        vignette = shaders.CreatePostProcess(camera, VignetteShader);
        pixelation = shaders.CreatePostProcess(camera, PixelationShader);
        chromaticAberration = shaders.CreatePostProcess(camera, ChromaticAberrationShader);
        pixelation.Enabled = false;
        chromaticAberration.Enabled = false;
        toggleDistortion = input.BindButton("Toggle distortion", Key.Number1);
        toggleGrayscale = input.BindButton("Toggle grayscale", Key.Number2);
        toggleVignette = input.BindButton("Toggle vignette", Key.Number3);
        togglePixelation = input.BindButton("Toggle pixelation", Key.Number4);
        toggleChromaticAberration = input.BindButton("Toggle chromatic aberration", Key.Number5);
        shake = input.BindButton("Shake camera", Key.Space);
    }

    protected override void UpdateExample(double deltaTime)
    {
        if (toggleDistortion.WasPressedThisFrame) distortion.Enabled = !distortion.Enabled;
        if (toggleGrayscale.WasPressedThisFrame) grayscale.Enabled = !grayscale.Enabled;
        if (toggleVignette.WasPressedThisFrame) vignette.Enabled = !vignette.Enabled;
        if (togglePixelation.WasPressedThisFrame) pixelation.Enabled = !pixelation.Enabled;
        if (toggleChromaticAberration.WasPressedThisFrame)
            chromaticAberration.Enabled = !chromaticAberration.Enabled;
        if (shake.WasPressedThisFrame) camera.Shake(18f, 0.4f);
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        distortion.Shader.SetFloat("uTime", (float)time.UnscaledTotalTime);
        DrawText(spriteBatch, textRenderer,
            $"Ordered camera effects | 1 distortion: {State(distortion)} | 2 grayscale: {State(grayscale)} | 3 vignette: {State(vignette)}",
            new Vector2(24f, 70f), 14f);
        DrawText(spriteBatch, textRenderer,
            $"4 pixelation: {State(pixelation)} | 5 chromatic aberration: {State(chromaticAberration)} | Space: screen shake",
            new Vector2(24f, 94f), 14f, Color.LightGray);
        DrawText(spriteBatch, textRenderer, "Navigation and this text are screen-space, so camera effects do not alter them.",
            new Vector2(24f, 118f), 14f, Color.LightGray);
    }

    private static string State(PostProcessEffect effect) => effect.Enabled ? "ON" : "OFF";

    private const string DistortionShader = """
        #version 330 core
        uniform sampler2D uTexture;
        uniform float uTime;
        in vec2 frag_texCoords;
        out vec4 out_color;
        void main() {
            vec2 uv = frag_texCoords;
            uv.x += sin(uv.y * 24.0 + uTime * 2.5) * 0.008;
            out_color = texture(uTexture, uv);
        }
        """;

    private const string GrayscaleShader = """
        #version 330 core
        uniform sampler2D uTexture;
        in vec2 frag_texCoords;
        out vec4 out_color;
        void main() {
            vec4 color = texture(uTexture, frag_texCoords);
            float luminance = dot(color.rgb, vec3(0.2126, 0.7152, 0.0722));
            out_color = vec4(mix(color.rgb, vec3(luminance), 0.45), color.a);
        }
        """;

    private const string VignetteShader = """
        #version 330 core
        uniform sampler2D uTexture;
        in vec2 frag_texCoords;
        out vec4 out_color;
        void main() {
            vec4 color = texture(uTexture, frag_texCoords);
            float distanceFromCenter = length((frag_texCoords - 0.5) * vec2(1.0, 0.72));
            float vignette = 1.0 - smoothstep(0.22, 0.62, distanceFromCenter) * 0.7;
            out_color = vec4(color.rgb * vignette, color.a);
        }
        """;

    private const string PixelationShader = """
        #version 330 core
        uniform sampler2D uTexture;
        uniform vec2 uResolution;
        in vec2 frag_texCoords;
        out vec4 out_color;
        void main() {
            const float pixelSize = 6.0;
            vec2 uv = (floor(frag_texCoords * uResolution / pixelSize) + 0.5) * pixelSize / uResolution;
            out_color = texture(uTexture, uv);
        }
        """;

    private const string ChromaticAberrationShader = """
        #version 330 core
        uniform sampler2D uTexture;
        uniform vec2 uTexelSize;
        in vec2 frag_texCoords;
        out vec4 out_color;
        void main() {
            vec2 offset = vec2(uTexelSize.x * 3.0, 0.0);
            vec4 center = texture(uTexture, frag_texCoords);
            out_color = vec4(
                texture(uTexture, frag_texCoords + offset).r,
                center.g,
                texture(uTexture, frag_texCoords - offset).b,
                center.a);
        }
        """;
}
