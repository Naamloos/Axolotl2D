using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.UI;
using Microsoft.Extensions.Hosting;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Bletris.Scenes;

[DefaultScene]
public sealed class MainMenuScene(
    AssetManager assets,
    InputActionMap input,
    IHostApplicationLifetime applicationLifetime) : BaseScene
{
    private InputAction play = null!;
    private InputAction quit = null!;
    private bool playRequested;
    private bool quitRequested;
    private bool quitArmed;

    public override void Load()
    {
        Game.Title = "Bletris";
        Game.ClearColor = Color.FromHTML("#0B1220");
        play = input.BindButton("Play", Key.Enter, Key.Space);
        quit = input.BindButton("Quit", Key.Escape);

        var font = assets.Get<FontAsset>("ui-font");
        var logo = Instantiate("Menu logo");
        var logoLayout = logo.AddComponent<UITransform>();
        logoLayout.Anchor = new Vector2(0.5f);
        logoLayout.Pivot = new Vector2(0.5f);
        logoLayout.AnchoredPosition = new Vector2(0f, -190f);
        logoLayout.Size = new Vector2(180f, 120f);
        logo.AddComponent<UIVisual>().Sprite = new Sprite(assets.Get<Texture2D>("block"));

        var title = Instantiate("Menu title");
        var titleLayout = title.AddComponent<UITransform>();
        titleLayout.Anchor = new Vector2(0.5f);
        titleLayout.Pivot = new Vector2(0.5f);
        titleLayout.AnchoredPosition = new Vector2(0f, -90f);
        titleLayout.Size = new Vector2(420f, 70f);
        var titleText = title.AddComponent<UIText>();
        titleText.Font = font;
        titleText.Text = "BLETRIS";
        titleText.FontSize = 44f;
        titleText.HorizontalAlignment = UIHorizontalAlignment.Center;
        titleText.VerticalAlignment = UIVerticalAlignment.Center;

        AddButton("PLAY", 10f, () => playRequested = true);
        AddButton("QUIT", 90f, () => quitRequested = true);

        var hint = Instantiate("Menu hint");
        var hintLayout = hint.AddComponent<UITransform>();
        hintLayout.Anchor = new Vector2(0.5f);
        hintLayout.Pivot = new Vector2(0.5f);
        hintLayout.AnchoredPosition = new Vector2(0f, 170f);
        hintLayout.Size = new Vector2(420f, 40f);
        var hintText = hint.AddComponent<UIText>();
        hintText.Font = font;
        hintText.Text = "ENTER / SPACE TO PLAY     ESC TO QUIT";
        hintText.FontSize = 13f;
        hintText.Color = Color.FromHTML("#94A3B8");
        hintText.HorizontalAlignment = UIHorizontalAlignment.Center;
        hintText.VerticalAlignment = UIVerticalAlignment.Center;

        void AddButton(string label, float y, Action clicked)
        {
            var gameObject = Instantiate($"{label} button");
            var layout = gameObject.AddComponent<UITransform>();
            layout.Anchor = new Vector2(0.5f);
            layout.Pivot = new Vector2(0.5f);
            layout.AnchoredPosition = new Vector2(0f, y);
            layout.Size = new Vector2(260f, 58f);

            var visual = gameObject.AddComponent<UIVisual>();
            visual.Primitive = UIPrimitive.Rectangle;
            visual.Color = Color.FromHTML("#1E3A5F");

            var text = gameObject.AddComponent<UIText>();
            text.Font = font;
            text.Text = label;
            text.FontSize = 22f;
            text.HorizontalAlignment = UIHorizontalAlignment.Center;
            text.VerticalAlignment = UIVerticalAlignment.Center;
            text.Depth = 1f;

            var button = gameObject.AddComponent<UIButton>();
            button.PointerEntered += () => visual.Color = Color.FromHTML("#256D85");
            button.PointerExited += () => visual.Color = Color.FromHTML("#1E3A5F");
            button.PressedChanged += pressed =>
                visual.Color = pressed ? Color.FromHTML("#164E63") : Color.FromHTML("#256D85");
            button.Clicked += clicked;
        }
    }

    public override void Update(double deltaTime)
    {
        if (play.WasPressedThisFrame)
            playRequested = true;
        if (!quit.IsPressed)
            quitArmed = true;
        if (quitArmed && quit.WasPressedThisFrame)
            quitRequested = true;

        if (playRequested)
            SceneGameHost.ChangeScene<BletrisScene>();
        else if (quitRequested)
            applicationLifetime.StopApplication();
    }
}
