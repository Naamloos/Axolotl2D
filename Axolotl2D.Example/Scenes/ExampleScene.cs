using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Physics;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.Shaders;
using Axolotl2D.Timing;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

[DefaultScene]
public sealed class ExampleScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input,
    TimeService time,
    ShaderLibrary shaders,
    ILogger<ExampleScene> logger) : BaseScene
{
    private FontAsset font = null!;
    private Sprite logoSprite = null!;
    private InputAction move = null!;
    private InputAction zoomOut = null!;
    private InputAction zoomIn = null!;
    private InputAction spawn = null!;
    private InputAction spawnPhysics = null!;
    private InputAction destroy = null!;
    private InputAction pause = null!;
    private InputAction changeScene = null!;
    private ShaderProgram pulseShader = null!;
    private int spawnedCount;

    public override void Load()
    {
        Game.Title = "Input actions, time, shaders, and Box2D physics";
        Game.ClearColor = Color.FromHTML("#17213A");
        font = assets.Get<FontAsset>("ui-font");
        logoSprite = new Sprite(assets.Get<Texture2D>("logo"));
        move = input.BindVector2("Move camera", Key.A, Key.D, Key.W, Key.S);
        zoomOut = input.BindButton("Zoom out", Key.Q);
        zoomIn = input.BindButton("Zoom in", Key.E);
        spawn = input.BindButton("Spawn", Key.Space);
        spawnPhysics = input.BindButton("Spawn physics", Key.B);
        destroy = input.BindButton("Destroy", Key.Backspace);
        pause = input.BindButton("Pause", Key.P);
        changeScene = input.BindButton("Change scene", Key.Tab);
        pulseShader = shaders.Create(PulseVertexShader, PulseFragmentShader);

        var ground = Instantiate("Physics ground");
        ground.Transform.LocalPosition = new Vector2(0, 320);
        var groundBody = ground.AddComponent<PhysicsBody>();
        groundBody.Type = Box2D.NET.B2BodyType.b2_staticBody;
        groundBody.AddBox(new Vector2(900, 40));

        for (var index = 0; index < 6; index++)
            SpawnAxolotl();
        SpawnPhysicsAxolotl();

        logger.LogInformation("Scene created {Count} DI-composed GameObjects", GameObjects.Count);
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        textRenderer.Draw(spriteBatch, font,
            $"WASD pan | Q/E zoom | Space spawn | B physics | P pause ({time.IsPaused}) | Tab change scene",
            14, new Vector2(24, 24), Color.White, CoordinateSpace.Screen, depth: 10);
        pulseShader.SetFloat("uTime", (float)time.UnscaledTotalTime);
        using (spriteBatch.UseShader(pulseShader))
            spriteBatch.Draw(logoSprite, new Vector2(80, 100), new Vector2(90, 60), space: CoordinateSpace.Screen, depth: 9);
    }

    public override void Update(double frameDelta)
    {
        camera.Pan(move.Value * (float)frameDelta * 350f / camera.Zoom);

        var viewportCenter = Game.Viewport / 2f;
        if (zoomOut.IsPressed) camera.ZoomAt(MathF.Pow(0.45f, (float)frameDelta), viewportCenter);
        if (zoomIn.IsPressed) camera.ZoomAt(MathF.Pow(2.2f, (float)frameDelta), viewportCenter);

        if (spawn.WasPressedThisFrame)
            SpawnAxolotl();
        if (spawnPhysics.WasPressedThisFrame)
            SpawnPhysicsAxolotl();

        if (destroy.WasPressedThisFrame)
            GameObjects.LastOrDefault(gameObject => gameObject.Name.StartsWith("Axolotl", StringComparison.Ordinal))?.Destroy();
        if (pause.WasPressedThisFrame)
            time.IsPaused = !time.IsPaused;

        if (changeScene.WasPressedThisFrame)
            SceneGameHost.ChangeScene<ExampleScene2>();
    }

    private void SpawnAxolotl()
    {
        var index = spawnedCount++;
        var gameObject = Instantiate($"Axolotl {index + 1}");
        gameObject.Transform.LocalPosition = new Vector2((index % 3 - 1) * 270, (index % 6 / 3 - 0.5f) * 230);
        gameObject.Transform.LocalScale = new Vector2(0.28f);
        gameObject.AddComponent<SpriteRenderer>().Sprite = logoSprite;
        gameObject.AddComponent<Spinner>().Speed = 0.15f + index * 0.07f;
    }

    private void SpawnPhysicsAxolotl()
    {
        var gameObject = Instantiate($"Axolotl physics {++spawnedCount}");
        gameObject.Transform.LocalPosition = new Vector2(0, -220);
        gameObject.Transform.LocalScale = new Vector2(0.12f);
        gameObject.AddComponent<SpriteRenderer>().Sprite = logoSprite;
        var body = gameObject.AddComponent<PhysicsBody>();
        body.AddBox(new Vector2(92, 62), restitution: 0.35f);
    }

    private const string PulseVertexShader = """
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

    private const string PulseFragmentShader = """
        #version 330 core
        uniform sampler2D uTexture;
        uniform float uTime;
        in vec2 frag_texCoords;
        in vec4 frag_color;
        out vec4 out_color;
        void main() {
            float pulse = 0.65 + 0.35 * sin(uTime * 4.0);
            out_color = texture(uTexture, frag_texCoords) * frag_color * vec4(pulse, 1.0, 1.0, 1.0);
        }
        """;
}

public sealed class Spinner(GameObject gameObject, ILogger<Spinner> logger) : Component(gameObject)
{
    public float Speed { get; set; } = 1f;
    public override void Start() => logger.LogDebug("Started {GameObject}", GameObject.Name);
    public override void FixedUpdate(double fixedDeltaTime) => Transform.Rotate(Speed * (float)fixedDeltaTime);
    public override void OnDestroy() => logger.LogDebug("Destroyed {GameObject}", GameObject.Name);
}
