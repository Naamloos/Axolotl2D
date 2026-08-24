using Axolotl2D.Assets;
using Axolotl2D.Rendering;
using Axolotl2D.UI;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class UIScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer) : ExampleSceneBase(assets)
{
    private int clicks;

    public override void Load()
    {
        LoadExample("Retained UI", "#111827");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;

        var clickButton = AddControl("Button", new Vector2(70f, 145f), new Vector2(250f, 55f));
        var clickVisual = clickButton.AddComponent<UIVisual>();
        clickVisual.Color = Color.FromHTML("#1D4ED8");
        var clickText = AddText(clickButton, "CLICK ME", 18f);
        var button = clickButton.AddComponent<UIButton>();
        button.NavigationOrder = 100;
        button.PointerEntered += () => clickVisual.Color = Color.FromHTML("#2563EB");
        button.PointerExited += () => clickVisual.Color = Color.FromHTML("#1D4ED8");
        button.Clicked += () => clickText.Text = $"CLICKS: {++clicks}";

        var toggleObject = AddControl("Toggle", new Vector2(70f, 230f), new Vector2(250f, 55f));
        var toggleVisual = toggleObject.AddComponent<UIVisual>();
        toggleVisual.Color = Color.FromHTML("#374151");
        AddText(toggleObject, "TOGGLE: OFF", 17f);
        var toggleText = toggleObject.GetComponent<UIText>()!;
        var toggle = toggleObject.AddComponent<UIToggle>();
        toggle.NavigationOrder = 101;
        toggle.ValueChanged += value =>
        {
            toggleText.Text = value ? "TOGGLE: ON" : "TOGGLE: OFF";
            toggleVisual.Color = Color.FromHTML(value ? "#059669" : "#374151");
        };

        var sliderObject = AddControl("Slider", new Vector2(70f, 315f), new Vector2(380f, 38f));
        var progress = sliderObject.AddComponent<UIProgressBar>();
        progress.Value = 0.35f;
        progress.BackgroundColor = Color.FromHTML("#374151");
        progress.FillColor = Color.FromHTML("#F59E0B");
        var slider = sliderObject.AddComponent<UISlider>();
        slider.NavigationOrder = 102;
        slider.Step = 0.1f;
        slider.SetValue(0.35f, notify: false);
        slider.ValueChanged += value => progress.Value = value;

        AddScrollView();
    }

    public override void Draw(double frameDelta, double frameRate) =>
        DrawText(spriteBatch, textRenderer,
            "Buttons, focus navigation, toggle, slider/progress, layout, clipping and mouse-wheel scrolling",
            new Vector2(24f, 70f), 15f);

    private void AddScrollView()
    {
        var viewport = AddControl("Scroll viewport", new Vector2(580f, 135f), new Vector2(390f, 430f));
        var background = viewport.AddComponent<UIVisual>();
        background.Color = Color.FromHTML("#1F2937");
        background.Depth = -1f;

        var contentObject = Instantiate("Scroll content");
        var content = contentObject.AddComponent<UITransform>();
        content.SetParent(viewport.GetComponent<UITransform>(), screenPositionStays: false);
        content.Size = new Vector2(390f, 660f);
        var layout = contentObject.AddComponent<UILayoutGroup>();
        layout.Direction = UILayoutDirection.Vertical;
        layout.Padding = new Vector4(16f, 16f, 16f, 16f);
        layout.Spacing = 10f;
        layout.ExpandChildren = true;

        for (var index = 0; index < 10; index++)
        {
            var row = Instantiate($"Scroll row {index + 1}");
            var rowTransform = row.AddComponent<UITransform>();
            rowTransform.SetParent(content, screenPositionStays: false);
            rowTransform.Size = new Vector2(358f, 52f);
            row.AddComponent<UIVisual>().Color = Color.FromHTML(index % 2 == 0 ? "#334155" : "#293548");
            AddText(row, $"CLIPPED ROW {index + 1:00}", 15f);
        }

        var scroll = viewport.AddComponent<UIScrollView>();
        scroll.Content = content;
        scroll.ContentSize = content.Size;
    }

    private Axolotl2D.GameObjects.GameObject AddControl(string name, Vector2 position, Vector2 size)
    {
        var gameObject = Instantiate(name);
        var transform = gameObject.AddComponent<UITransform>();
        transform.Anchor = Vector2.Zero;
        transform.Pivot = Vector2.Zero;
        transform.AnchoredPosition = position;
        transform.Size = size;
        return gameObject;
    }

    private UIText AddText(Axolotl2D.GameObjects.GameObject gameObject, string label, float size)
    {
        var text = gameObject.AddComponent<UIText>();
        text.Font = Font;
        text.Text = label;
        text.FontSize = size;
        text.HorizontalAlignment = UIHorizontalAlignment.Center;
        text.VerticalAlignment = UIVerticalAlignment.Center;
        text.Depth = 1f;
        return text;
    }
}
