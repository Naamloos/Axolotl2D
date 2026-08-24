# Camera Post-Processing

Post-processing applies one or more fragment shaders to a camera's completed world image. Each camera owns an ordered effect stack, so split-screen and inset cameras can use different effects. Normal screen-space drawing, including Axolotl2D UI, is composited afterward and remains unchanged.

## Create an effect

Inject the scene-scoped `ShaderLibrary` and a camera, then create effects in `Load`:

```csharp
public sealed class GameplayScene(
    Camera2D camera,
    ShaderLibrary shaders) : BaseScene
{
    private PostProcessEffect vignette = null!;

    public override void Load() =>
        vignette = shaders.CreatePostProcess(camera, VignetteFragmentShader);
}
```

The library supplies the full-screen vertex shader. Your fragment shader receives normalized texture coordinates and can use these engine-provided uniforms when declared:

- `sampler2D uTexture`: the previous pass, or the camera image for the first pass
- `vec2 uResolution`: camera render-target size in pixels
- `vec2 uTexelSize`: reciprocal render-target size, useful for neighboring pixel samples

```glsl
#version 330 core
uniform sampler2D uTexture;
in vec2 frag_texCoords;
out vec4 out_color;

void main()
{
    vec4 color = texture(uTexture, frag_texCoords);
    float distanceFromCenter = length(frag_texCoords - 0.5);
    float amount = 1.0 - smoothstep(0.25, 0.7, distanceFromCenter) * 0.65;
    out_color = vec4(color.rgb * amount, color.a);
}
```

Render targets follow the camera viewport size and are recreated automatically after a window or viewport resize.

## Stack effects

Effects run in creation order:

```csharp
var distortion = shaders.CreatePostProcess(camera, DistortionShader);
var grayscale = shaders.CreatePostProcess(camera, GrayscaleShader);
var vignette = shaders.CreatePostProcess(camera, VignetteShader);
```

Every pass samples the output of the previous pass. Set custom uniforms through `effect.Shader`, and toggle a pass without rebuilding the stack:

```csharp
distortion.Shader.SetFloat("uTime", elapsedTime);
grayscale.Enabled = false;
```

`PostProcessEffect.Dispose` detaches one effect. Normally no manual cleanup is needed: the scene's `ShaderLibrary` detaches its effects and disposes their programs when the scene scope ends.

Post-processing is intentionally shader-based rather than a material system. A pass is one program plus its uniforms; create separate programs when two cameras need different persistent uniform values.

See the `POST FX` screen in `Axolotl2D.Example` for animated distortion, grayscale, vignette, pixelation, chromatic aberration, ordering, and runtime toggles. It also combines the stack with `Camera2D.Shake` to demonstrate a non-shader camera effect.
