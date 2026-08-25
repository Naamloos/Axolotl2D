using Axolotl2D.Assets;
using Axolotl2D.Scenes;
using Axolotl2D.UI;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public abstract class ExampleSceneBase(AssetManager assets) : BaseScene
{
    private static readonly (string Label, Type Scene)[] Scenes =
    [
        ("SPRITES", typeof(SpriteScene)),
        ("ANIMATION", typeof(AnimationScene)),
        ("DISPLAY", typeof(DisplayScene)),
        ("AUDIO", typeof(AudioScene)),
        ("PHYSICS", typeof(PhysicsScene)),
        ("LIGHTING", typeof(LightingScene)),
        ("CAMERAS", typeof(CameraScene)),
        ("TARGETS", typeof(RenderTargetScene)),
        ("INPUT", typeof(InputScene)),
        ("SHADERS", typeof(ShaderScene)),
        ("POST FX", typeof(PostProcessScene)),
        ("PARTICLES", typeof(ParticleScene)),
        ("PREFABS", typeof(PrefabScene)),
        ("UI", typeof(UIScene)),
        ("SAVE", typeof(SaveScene))
    ];

    private Type? requestedScene;
    protected FontAsset Font => assets.Get<FontAsset>("ui-font");

    protected void LoadExample(string title, string clearColor)
    {
        Game.Title = $"Axolotl2D Examples - {title}";
        Game.ClearColor = Color.FromHTML(clearColor);

        var navigation = Instantiate("Example navigation");
        var navigationTransform = navigation.AddComponent<UITransform>();
        navigationTransform.AnchorMin = Vector2.Zero;
        navigationTransform.AnchorMax = Vector2.UnitX;
        navigationTransform.OffsetMin = new Vector2(10f, 10f);
        navigationTransform.OffsetMax = new Vector2(-10f, 54f);
        var layout = navigation.AddComponent<UILayoutGroup>();
        layout.Direction = UILayoutDirection.Horizontal;
        layout.Spacing = 5f;
        layout.ExpandChildren = true;

        foreach (var (label, scene) in Scenes)
            AddNavigationButton(navigationTransform, label, scene);
    }

    public sealed override void Update(double deltaTime)
    {
        if (requestedScene is not null)
        {
            SceneGameHost.ChangeScene(requestedScene);
            return;
        }
        UpdateExample(deltaTime);
    }

    protected virtual void UpdateExample(double deltaTime) { }

    protected void DrawText(Axolotl2D.Rendering.SpriteBatch spriteBatch,
        Axolotl2D.Rendering.TextRenderer textRenderer, string text, Vector2 position,
        float size = 16f, Color? color = null) =>
        textRenderer.Draw(spriteBatch, Font, text, size, position, color ?? Color.White,
            Axolotl2D.Rendering.CoordinateSpace.Screen, depth: 90f);

    private void AddNavigationButton(UITransform parent, string label, Type scene)
    {
        var active = GetType() == scene;
        var gameObject = Instantiate($"{label} navigation button");
        var transform = gameObject.AddComponent<UITransform>();
        transform.SetParent(parent, screenPositionStays: false);
        transform.Size = new Vector2(100f, 44f);

        var visual = gameObject.AddComponent<UIVisual>();
        visual.Color = Color.FromHTML(active ? "#2F81F7" : "#26354F");
        visual.Depth = 100f;

        var text = gameObject.AddComponent<UIText>();
        text.Font = Font;
        text.Text = label;
        text.FontSize = 11f;
        text.HorizontalAlignment = UIHorizontalAlignment.Center;
        text.VerticalAlignment = UIVerticalAlignment.Center;
        text.Depth = 101f;

        var button = gameObject.AddComponent<UIButton>();
        button.Depth = 102f;
        button.Interactable = !active;
        button.PointerEntered += () => visual.Color = Color.FromHTML("#3D5A80");
        button.PointerExited += () => visual.Color = Color.FromHTML("#26354F");
        button.PressedChanged += pressed => visual.Color = Color.FromHTML(pressed ? "#1B263B" : "#3D5A80");
        button.Clicked += () => requestedScene = scene;
    }
}
