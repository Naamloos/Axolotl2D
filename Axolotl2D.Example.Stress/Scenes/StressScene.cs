using Axolotl2D.Assets;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.UI;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Stress.Scenes;

[DefaultScene]
public sealed class StressScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input) : BaseScene
{
    private const int MinimumSprites = 1_000;
    private const int MaximumSprites = 250_000;
    private const int SpriteStep = 10_000;
    private const int DynamicTextLines = 24;
    private static readonly Color SpriteTint = Color.White;
    private static readonly Color SecondaryTextTint = Color.LightGray;
    private readonly Sprite[] atlasSprites = new Sprite[4];
    private readonly Sprite[] separateSprites = new Sprite[4];
    private readonly Sprite[] arraySprites = new Sprite[4];
    private readonly SpatialSpriteIndex spatialIndex = new(128f);
    private FontAsset font = null!;
    private UITransform uiRoot = null!;
    private InputAction increase = null!;
    private InputAction decrease = null!;
    private InputAction toggleAtlas = null!;
    private InputAction toggleSpread = null!;
    private InputAction toggleText = null!;
    private InputAction toggleUi = null!;
    private InputAction toggleCamera = null!;
    private InputAction reset = null!;
    private InputAction toggleSpatial = null!;
    private int spriteCount = 50_000;
    private ulong drawCount;
    private float phase;
    private TextureMode textureMode = TextureMode.Atlas;
    private bool spreadSprites = true;
    private bool churnText;
    private bool dirtyUi;
    private bool moveCamera;
    private bool useSpatial = true;

    public override void Load()
    {
        Game.ClearColor = Color.FromHTML("#08111F");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        font = assets.Get<FontAsset>("stress-font");
        CreateSprites();
        CreateUiLoad();

        increase = input.BindButton("Increase stress sprites", Key.Up);
        decrease = input.BindButton("Decrease stress sprites", Key.Down);
        toggleAtlas = input.BindButton("Toggle stress atlas", Key.A);
        toggleSpread = input.BindButton("Toggle off-screen spread", Key.C);
        toggleText = input.BindButton("Toggle dynamic text", Key.T);
        toggleUi = input.BindButton("Toggle dirty UI", Key.U);
        toggleCamera = input.BindButton("Toggle moving camera", Key.M);
        toggleSpatial = input.BindButton("Toggle spatial index", Key.I);
        reset = input.BindButton("Reset stress workload", Key.R);
        RebuildSpatialIndex();
        UpdateTitle();
    }

    public override void Update(double deltaTime)
    {
        var previousCount = spriteCount;
        var previousMode = textureMode;
        var previousSpread = spreadSprites;
        var changed = false;
        if (increase.WasPressedThisFrame)
        {
            spriteCount = AdjustSpriteCount(spriteCount, SpriteStep);
            changed = true;
        }
        if (decrease.WasPressedThisFrame)
        {
            spriteCount = AdjustSpriteCount(spriteCount, -SpriteStep);
            changed = true;
        }
        if (toggleAtlas.WasPressedThisFrame)
        {
            textureMode = (TextureMode)(((int)textureMode + 1) % 3);
            changed = true;
        }
        changed |= Toggle(toggleSpread, ref spreadSprites);
        changed |= Toggle(toggleText, ref churnText);
        changed |= Toggle(toggleUi, ref dirtyUi);
        changed |= Toggle(toggleCamera, ref moveCamera);
        changed |= Toggle(toggleSpatial, ref useSpatial);
        if (reset.WasPressedThisFrame)
        {
            spriteCount = 50_000;
            textureMode = TextureMode.Atlas;
            spreadSprites = true;
            churnText = false;
            dirtyUi = false;
            moveCamera = false;
            useSpatial = true;
            phase = 0f;
            camera.Position = Vector2.Zero;
            uiRoot.OffsetMin = new Vector2(16f, -52f);
            changed = true;
        }

        if (spriteCount != previousCount || textureMode != previousMode || spreadSprites != previousSpread)
            RebuildSpatialIndex();

        phase += (float)deltaTime;
        if (moveCamera)
            camera.Position = new Vector2(MathF.Sin(phase * 0.7f) * 900f, MathF.Cos(phase * 0.9f) * 360f);
        if (dirtyUi)
            uiRoot.OffsetMin = new Vector2(16f + MathF.Sin(phase * 5f) * 8f, -52f);
        if (changed) UpdateTitle();
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        if (useSpatial)
            spatialIndex.DrawVisible(spriteBatch, camera);
        else
        {
            var sprites = CurrentSprites();
            for (var index = 0; index < spriteCount; index++)
                spriteBatch.Draw(sprites[index & 3], Position(index, spriteCount, spreadSprites), tint: SpriteTint);
        }

        textRenderer.Draw(spriteBatch, font,
            "Up/Down sprites | A atlas/array/raw | I spatial | C spread | T text | U UI | M camera | R reset",
            15f, new Vector2(12f, 12f), SpriteTint, CoordinateSpace.Screen, depth: 90_000f);

        if (churnText)
            for (var index = 0; index < DynamicTextLines; index++)
                textRenderer.Draw(spriteBatch, font, $"atlas churn {drawCount:00000000}:{index:00}", 12f,
                    new Vector2(12f, 42f + index * 18f), SecondaryTextTint,
                    CoordinateSpace.Screen, depth: 90_000f);
        drawCount++;
    }

    internal static void RunSelfCheck()
    {
        if (AdjustSpriteCount(MinimumSprites, -SpriteStep) != MinimumSprites ||
            AdjustSpriteCount(MaximumSprites, SpriteStep) != MaximumSprites ||
            !float.IsFinite(Position(249_999, MaximumSprites, true).X))
            throw new InvalidOperationException("Stress workload bounds are invalid.");
    }

    private void CreateSprites()
    {
        var colors = new[]
        {
            (R: (byte)69, G: (byte)217, B: (byte)232),
            (R: (byte)245, G: (byte)213, B: (byte)71),
            (R: (byte)177, G: (byte)108, B: (byte)226),
            (R: (byte)89, G: (byte)201, B: (byte)108)
        };
        var atlas = new TextureAtlasBuilder();
        var textures = new Texture2D[colors.Length];
        for (var index = 0; index < colors.Length; index++)
        {
            var color = colors[index];
            var texture = SolidTexture(color.R, color.G, color.B);
            textures[index] = texture;
            separateSprites[index] = new(texture);
            atlas.Add(index.ToString(), texture);
        }
        var packed = atlas.Build(maximumSize: 64);
        for (var index = 0; index < atlasSprites.Length; index++)
            atlasSprites[index] = packed.GetSprite(index.ToString());
        var textureArray = new TextureArray2D(textures);
        for (var index = 0; index < arraySprites.Length; index++)
            arraySprites[index] = textureArray.GetSprite(index);
    }

    private void CreateUiLoad()
    {
        var rootObject = Instantiate("Stress UI layout");
        uiRoot = rootObject.AddComponent<UITransform>();
        uiRoot.AnchorMin = new Vector2(0f, 1f);
        uiRoot.AnchorMax = Vector2.One;
        uiRoot.OffsetMin = new Vector2(16f, -52f);
        uiRoot.OffsetMax = new Vector2(-16f, -16f);
        var layout = rootObject.AddComponent<UILayoutGroup>();
        layout.Direction = UILayoutDirection.Horizontal;
        layout.Padding = new Vector4(4f);
        layout.Spacing = 2f;
        layout.ExpandChildren = true;

        var colors = new[] { Color.Cyan, Color.Yellow, Color.Magenta, Color.Green };
        for (var index = 0; index < 48; index++)
        {
            var item = Instantiate($"Stress UI item {index + 1}");
            var transform = item.AddComponent<UITransform>();
            transform.SetParent(uiRoot, screenPositionStays: false);
            transform.Size = new Vector2(20f, 28f);
            item.AddComponent<UIVisual>().Color = colors[index & 3];
        }
    }

    private void UpdateTitle() => Game.Title =
        $"Axolotl2D Stress | {spriteCount:N0} | {textureMode.ToString().ToLowerInvariant()} | " +
        $"{(useSpatial ? "indexed" : "direct")} | {(spreadSprites ? "spread" : "visible")} | text {(churnText ? "churn" : "cached")} | " +
        $"UI {(dirtyUi ? "dirty" : "stable")}";

    private Sprite[] CurrentSprites() => textureMode switch
    {
        TextureMode.Atlas => atlasSprites,
        TextureMode.Array => arraySprites,
        _ => separateSprites
    };

    private void RebuildSpatialIndex()
    {
        spatialIndex.Clear();
        var sprites = CurrentSprites();
        for (var index = 0; index < spriteCount; index++)
            spatialIndex.Add(sprites[index & 3], Matrix3x2.CreateTranslation(Position(index, spriteCount, spreadSprites)),
                SpriteTint);
    }

    private static bool Toggle(InputAction action, ref bool value)
    {
        if (!action.WasPressedThisFrame) return false;
        value = !value;
        return true;
    }

    private static int AdjustSpriteCount(int current, int delta) =>
        Math.Clamp(current + delta, MinimumSprites, MaximumSprites);

    private static Vector2 Position(int index, int count, bool spread)
    {
        if (!spread)
            return new Vector2(index * 37 % 1_040 - 520f, index * 91 % 640 - 320f);

        const int columns = 512;
        const float spacing = 12f;
        var rows = (count + columns - 1) / columns;
        return new Vector2((index % columns - columns / 2f) * spacing,
            (index / columns - (rows - 1) / 2f) * spacing);
    }

    private static Texture2D SolidTexture(byte red, byte green, byte blue)
    {
        const int size = 8;
        var pixels = new byte[size * size * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = red;
            pixels[index + 1] = green;
            pixels[index + 2] = blue;
            pixels[index + 3] = 255;
        }
        return new(size, size, pixels);
    }

    private enum TextureMode { Atlas, Array, Raw }
}
