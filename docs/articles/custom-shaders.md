# Custom Shaders

`ShaderLibrary` compiles custom GLSL programs and owns them for one scene DI scope. `SpriteBatch.UseShader` selects a program for a bounded set of draw commands.

## Create a shader after window load

Inject `ShaderLibrary` into a scene and create programs in `Load`:

```csharp
public sealed class GameplayScene(ShaderLibrary shaders) : BaseScene
{
    private ShaderProgram tintShader = null!;

    public override void Load() =>
        tintShader = shaders.Create(VertexSource, FragmentSource);
}
```

The library disposes its programs when the scene scope ends. A different scene receives a new library and cannot retain stale OpenGL programs from the previous scene.

Load shader source strings during `Game.InitializeAsync` if they come from files or embedded resources. Compile them in `Scene.Load`, after Axolotl2D creates the OpenGL context.

## Vertex input contract

Custom vertex shaders used by `SpriteBatch` must accept the batch layout:

```glsl
#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTextureCoord;
layout (location = 2) in vec4 aColor;

out vec2 frag_texCoords;
out vec4 frag_color;

void main()
{
    gl_Position = vec4(aPosition, 1.0);
    frag_texCoords = aTextureCoord;
    frag_color = aColor;
}
```

Axolotl2D converts vertices into normalized device coordinates on the CPU before the shader runs.

## Write a fragment shader

```glsl
#version 330 core
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uTint;

in vec2 frag_texCoords;
in vec4 frag_color;
out vec4 out_color;

void main()
{
    float pulse = 0.7 + 0.3 * sin(uTime * 4.0);
    out_color = texture(uTexture, frag_texCoords)
        * frag_color
        * uTint
        * vec4(pulse, 1.0, 1.0, 1.0);
}
```

`SpriteBatch` assigns texture unit zero to `uTexture` when the uniform exists.

## Set uniforms and draw inside a scope

```csharp
public override void Draw(double frameDelta, double frameRate)
{
    tintShader.SetFloat("uTime", (float)time.UnscaledTotalTime);
    tintShader.SetVector4("uTint", new Vector4(1, 0.8f, 0.8f, 1));

    using (spriteBatch.UseShader(tintShader))
    {
        spriteBatch.Draw(sprite, position);
    }
}
```

`SetInt`, `SetFloat`, `SetVector2`, and `SetVector4` validate that the uniform remains active after GLSL linking. A missing or optimized-out uniform throws `KeyNotFoundException`.

Nested shader scopes restore the previous shader when disposed. Draw commands share a batch when their texture and shader match.

Uniform values belong to the shader program for the current batch. Using one program with different per-sprite uniform values in the same batch is not supported. Use tint and vertex data for per-sprite values, or create separate shader programs when values must vary between batches.

For effects over a camera's completed world image, use [`ShaderLibrary.CreatePostProcess`](camera-post-processing.md) instead of `SpriteBatch.UseShader`.
