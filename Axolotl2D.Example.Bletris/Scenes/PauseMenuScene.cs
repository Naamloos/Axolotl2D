using Axolotl2D.Assets;
using Axolotl2D.Input;
using Axolotl2D.Scenes;
using Axolotl2D.Timing;
using Axolotl2D.UI;
using Microsoft.Extensions.Hosting;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Bletris.Scenes;

public sealed class PauseMenuScene(
    AssetManager assets,
    InputActionMap input,
    UIEventSystem uiEvents,
    BletrisGame bletris,
    TimeService time,
    IHostApplicationLifetime applicationLifetime) : BaseScene
{
    private InputAction resume = null!;
    private PauseRequest request;
    private bool resumeArmed;
    private Action? applyScale;

    public override void Load()
    {
        time.IsPaused = true;
        bletris.PauseAudio();
        resume = input.Bind("Resume", InputBinding.Button(
            InputControl.From(Key.P), InputControl.From(Key.Escape),
            InputControl.From(ButtonName.Start), InputControl.From(ButtonName.Back)));

        var font = assets.Get<FontAsset>("ui-font");
        var overlay = Instantiate("Pause backdrop");
        var overlayLayout = overlay.AddComponent<UITransform>();
        overlayLayout.AnchorMin = Vector2.Zero;
        overlayLayout.AnchorMax = Vector2.One;
        overlayLayout.OffsetMin = Vector2.Zero;
        overlayLayout.OffsetMax = Vector2.Zero;
        var overlayVisual = overlay.AddComponent<UIVisual>();
        overlayVisual.Primitive = UIPrimitive.Rectangle;
        overlayVisual.Color = new Color(0.02f, 0.04f, 0.09f, 0.82f);

        var title = Instantiate("Pause title");
        var titleLayout = title.AddComponent<UITransform>();
        titleLayout.Anchor = new Vector2(0.5f);
        titleLayout.Pivot = new Vector2(0.5f);
        titleLayout.AnchoredPosition = new Vector2(0f, -125f);
        titleLayout.Size = new Vector2(360f, 70f);
        var titleText = title.AddComponent<UIText>();
        titleText.Font = font;
        titleText.Text = "PAUSED";
        titleText.FontSize = 42f;
        titleText.HorizontalAlignment = UIHorizontalAlignment.Center;
        titleText.VerticalAlignment = UIVerticalAlignment.Center;
        titleText.Depth = 1f;

        var panel = Instantiate("Pause buttons");
        var panelLayout = panel.AddComponent<UITransform>();
        panelLayout.Anchor = new Vector2(0.5f);
        panelLayout.Pivot = new Vector2(0.5f);
        panelLayout.AnchoredPosition = new Vector2(0f, 35f);
        panelLayout.Size = new Vector2(300f, 176f);
        var layout = panel.AddComponent<UILayoutGroup>();
        layout.Direction = UILayoutDirection.Vertical;
        layout.Spacing = 10f;
        layout.ExpandChildren = true;

        var buttons = new List<(UITransform Layout, UIText Text)>();
        var resumeButton = AddButton("RESUME", () => request = PauseRequest.Resume, 0);
        AddButton("MAIN MENU", () => request = PauseRequest.MainMenu, 1);
        AddButton("QUIT", () => request = PauseRequest.Quit, 2);
        uiEvents.SetFocus(resumeButton);

        applyScale = () =>
        {
            var scale = bletris.ScreenScale;
            titleLayout.AnchoredPosition = new Vector2(0f, -125f) * scale;
            titleLayout.Size = new Vector2(360f, 70f) * scale;
            titleText.FontSize = 42f * scale;
            panelLayout.AnchoredPosition = new Vector2(0f, 35f) * scale;
            panelLayout.Size = new Vector2(300f, 176f) * scale;
            layout.Spacing = 10f * scale;
            foreach (var (buttonLayout, text) in buttons)
            {
                buttonLayout.Size = new Vector2(300f, 52f) * scale;
                text.FontSize = 17f * scale;
            }
        };
        applyScale();

        UIButton AddButton(string label, Action clicked, int navigationOrder)
        {
            var buttonObject = Instantiate($"{label} button");
            var buttonLayout = buttonObject.AddComponent<UITransform>();
            buttonLayout.SetParent(panelLayout, screenPositionStays: false);
            buttonLayout.Size = new Vector2(300f, 52f);

            var visual = buttonObject.AddComponent<UIVisual>();
            visual.Primitive = UIPrimitive.Rectangle;
            visual.Color = Color.FromHTML("#1E3A5F");

            var text = buttonObject.AddComponent<UIText>();
            text.Font = font;
            text.Text = label;
            text.FontSize = 17f;
            text.HorizontalAlignment = UIHorizontalAlignment.Center;
            text.VerticalAlignment = UIVerticalAlignment.Center;
            text.Depth = 1f;

            var button = buttonObject.AddComponent<UIButton>();
            button.NavigationOrder = navigationOrder;
            button.Depth = 2f;
            button.PointerEntered += () => visual.Color = Color.FromHTML("#256D85");
            button.PointerExited += () => visual.Color = Color.FromHTML("#1E3A5F");
            button.PressedChanged += pressed =>
                visual.Color = pressed ? Color.FromHTML("#164E63") : Color.FromHTML("#256D85");
            button.FocusChanged += focused =>
                visual.Color = focused ? Color.FromHTML("#256D85") : Color.FromHTML("#1E3A5F");
            button.Clicked += () =>
            {
                bletris.PlayUiSound();
                clicked();
            };
            buttons.Add((buttonLayout, text));
            return button;
        }
    }

    public override void Update(double deltaTime)
    {
        if (!resume.IsPressed)
            resumeArmed = true;
        if (resumeArmed && resume.WasPressedThisFrame)
            request = PauseRequest.Resume;

        switch (request)
        {
            case PauseRequest.Resume:
                SceneGameHost.PopScene();
                break;
            case PauseRequest.MainMenu:
                SceneGameHost.ChangeScene<MainMenuScene>();
                break;
            case PauseRequest.Quit:
                applicationLifetime.StopApplication();
                break;
        }
    }

    public override void Resize(Vector2 size) => applyScale?.Invoke();

    public override void Unload()
    {
        applyScale = null;
        time.IsPaused = false;
        bletris.ResumeAudio();
    }

    private enum PauseRequest
    {
        None,
        Resume,
        MainMenu,
        Quit
    }
}
