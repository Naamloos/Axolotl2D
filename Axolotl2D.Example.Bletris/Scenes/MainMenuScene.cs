using Axolotl2D.Animation;
using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Physics;
using Axolotl2D.Prefabs;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.Timing;
using Axolotl2D.UI;
using Box2D.NET;
using Microsoft.Extensions.Hosting;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Bletris.Scenes;

[DefaultScene]
public sealed class MainMenuScene(
    AssetManager assets,
    InputActionMap input,
    BletrisGame bletris,
    Camera2D camera,
    TweenService tweens,
    TimeService time,
    IHostApplicationLifetime applicationLifetime) : BaseScene
{
    private InputAction play = null!;
    private InputAction quit = null!;
    private bool playRequested;
    private bool quitRequested;
    private bool quitArmed;
    private Action? applyScale;

    public override void Load()
    {
        Game.Title = "Bletris";
        Game.ClearColor = Color.FromHTML("#0B1220");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        time.IsPaused = false;
        bletris.ResumeAudio();
        play = input.Bind("Play", InputBinding.Button(
            InputControl.From(Key.Enter), InputControl.From(Key.Space), InputControl.From(ButtonName.A)));
        quit = input.Bind("Quit", InputBinding.Button(
            InputControl.From(Key.Escape), InputControl.From(ButtonName.Back)));

        CreatePhysicsBackdrop();
        CreateAnimatedMascot();

        var font = assets.Get<FontAsset>("ui-font");
        var title = Instantiate("Menu title");
        var titleLayout = title.AddComponent<UITransform>();
        titleLayout.Anchor = new Vector2(0.5f);
        titleLayout.Pivot = new Vector2(0.5f);
        titleLayout.AnchoredPosition = new Vector2(0f, -135f);
        titleLayout.Size = new Vector2(420f, 70f);
        var titleText = title.AddComponent<UIText>();
        titleText.Font = font;
        titleText.Text = "BLETRIS";
        titleText.FontSize = 44f;
        titleText.HorizontalAlignment = UIHorizontalAlignment.Center;
        titleText.VerticalAlignment = UIVerticalAlignment.Center;

        var buttonPanel = Instantiate("Menu button layout");
        var buttonPanelLayout = buttonPanel.AddComponent<UITransform>();
        buttonPanelLayout.Anchor = new Vector2(0.5f);
        buttonPanelLayout.Pivot = new Vector2(0.5f);
        buttonPanelLayout.AnchoredPosition = new Vector2(0f, 10f);
        buttonPanelLayout.Size = new Vector2(300f, 174f);
        var buttonLayout = buttonPanel.AddComponent<UILayoutGroup>();
        buttonLayout.Direction = UILayoutDirection.Vertical;
        buttonLayout.Spacing = 10f;
        buttonLayout.ExpandChildren = true;

        var buttons = new List<(UITransform Layout, UIText Text)>();
        AddButton("PLAY", () => playRequested = true, 0);
        UIText modeText = null!;
        modeText = AddButton($"MODE  {Game.WindowMode}", () =>
        {
            bletris.CycleWindowMode();
            modeText.Text = $"MODE  {Game.WindowMode}";
        }, 1);
        AddButton("QUIT", () => quitRequested = true, 2);

        var progressObject = Instantiate("High score progress");
        var progressLayout = progressObject.AddComponent<UITransform>();
        progressLayout.Anchor = new Vector2(0.5f);
        progressLayout.Pivot = new Vector2(0.5f);
        progressLayout.AnchoredPosition = new Vector2(0f, 125f);
        progressLayout.Size = new Vector2(300f, 8f);
        var highScoreProgress = progressObject.AddComponent<UIProgressBar>();
        highScoreProgress.Value = Math.Clamp(bletris.HighScore / 10000f, 0f, 1f);
        highScoreProgress.FillColor = Color.FromHTML("#45D9E8");

        var volumeObject = Instantiate("Volume slider");
        var volumeLayout = volumeObject.AddComponent<UITransform>();
        volumeLayout.Anchor = new Vector2(0.5f);
        volumeLayout.Pivot = new Vector2(0.5f);
        volumeLayout.AnchoredPosition = new Vector2(0f, 165f);
        volumeLayout.Size = new Vector2(300f, 24f);
        var volumeVisual = volumeObject.AddComponent<UIVisual>();
        volumeVisual.Primitive = UIPrimitive.Rectangle;
        volumeVisual.Color = Color.FromHTML("#1E293B");
        var volumeFill = volumeObject.AddComponent<UIProgressBar>();
        volumeFill.Value = bletris.Volume;
        volumeFill.BackgroundColor = Color.Transparent;
        volumeFill.FillColor = Color.FromHTML("#256D85");
        var volumeText = volumeObject.AddComponent<UIText>();
        volumeText.Font = font;
        volumeText.Text = $"VOLUME  {bletris.Volume:P0}";
        volumeText.FontSize = 13f;
        volumeText.HorizontalAlignment = UIHorizontalAlignment.Center;
        volumeText.VerticalAlignment = UIVerticalAlignment.Center;
        volumeText.Depth = 2f;
        var volume = volumeObject.AddComponent<UISlider>();
        volume.NavigationOrder = 3;
        volume.Depth = 3f;
        volume.Step = 0.1f;
        volume.SetValue(bletris.Volume, notify: false);
        volume.ValueChanged += value =>
        {
            bletris.SetVolume(value);
            volumeFill.Value = value;
            volumeText.Text = $"VOLUME  {value:P0}";
        };

        var muteObject = Instantiate("Mute toggle");
        var muteLayout = muteObject.AddComponent<UITransform>();
        muteLayout.Anchor = new Vector2(0.5f);
        muteLayout.Pivot = new Vector2(0.5f);
        muteLayout.AnchoredPosition = new Vector2(0f, 205f);
        muteLayout.Size = new Vector2(300f, 34f);
        var muteVisual = muteObject.AddComponent<UIVisual>();
        muteVisual.Primitive = UIPrimitive.Rectangle;
        var muteText = muteObject.AddComponent<UIText>();
        muteText.Font = font;
        muteText.FontSize = 13f;
        muteText.HorizontalAlignment = UIHorizontalAlignment.Center;
        muteText.VerticalAlignment = UIVerticalAlignment.Center;
        muteText.Depth = 2f;
        var mute = muteObject.AddComponent<UIToggle>();
        mute.NavigationOrder = 4;
        mute.Depth = 3f;
        mute.SetValue(bletris.Muted, notify: false);
        UpdateMute(bletris.Muted);
        mute.ValueChanged += value =>
        {
            bletris.SetMuted(value);
            UpdateMute(value);
        };

        var hint = Instantiate("Menu hint");
        var hintLayout = hint.AddComponent<UITransform>();
        hintLayout.Anchor = new Vector2(0.5f);
        hintLayout.Pivot = new Vector2(0.5f);
        hintLayout.AnchoredPosition = new Vector2(0f, 265f);
        hintLayout.Size = new Vector2(620f, 40f);
        var hintText = hint.AddComponent<UIText>();
        hintText.Font = font;
        hintText.Text = $"HIGH SCORE {bletris.HighScore:000000}   |   KEYBOARD, GAMEPAD OR MOUSE";
        hintText.FontSize = 13f;
        hintText.Color = Color.FromHTML("#94A3B8");
        hintText.HorizontalAlignment = UIHorizontalAlignment.Center;
        hintText.VerticalAlignment = UIVerticalAlignment.Center;

        applyScale = () =>
        {
            var scale = bletris.ScreenScale;
            camera.Zoom = scale;
            titleLayout.AnchoredPosition = new Vector2(0f, -135f) * scale;
            titleLayout.Size = new Vector2(420f, 70f) * scale;
            titleText.FontSize = 44f * scale;
            buttonPanelLayout.AnchoredPosition = new Vector2(0f, 10f) * scale;
            buttonPanelLayout.Size = new Vector2(300f, 174f) * scale;
            buttonLayout.Spacing = 10f * scale;
            foreach (var (layout, text) in buttons)
            {
                layout.Size = new Vector2(300f, 52f) * scale;
                text.FontSize = 17f * scale;
            }
            progressLayout.AnchoredPosition = new Vector2(0f, 125f) * scale;
            progressLayout.Size = new Vector2(300f, 8f) * scale;
            volumeLayout.AnchoredPosition = new Vector2(0f, 165f) * scale;
            volumeLayout.Size = new Vector2(300f, 24f) * scale;
            volumeText.FontSize = 13f * scale;
            muteLayout.AnchoredPosition = new Vector2(0f, 205f) * scale;
            muteLayout.Size = new Vector2(300f, 34f) * scale;
            muteText.FontSize = 13f * scale;
            hintLayout.AnchoredPosition = new Vector2(0f, 265f) * scale;
            hintLayout.Size = new Vector2(620f, 40f) * scale;
            hintText.FontSize = 13f * scale;
        };
        applyScale();

        UIText AddButton(string label, Action clicked, int navigationOrder)
        {
            var gameObject = Instantiate($"{label} button");
            var layout = gameObject.AddComponent<UITransform>();
            layout.SetParent(buttonPanelLayout, screenPositionStays: false);
            layout.Size = new Vector2(300f, 52f);

            var visual = gameObject.AddComponent<UIVisual>();
            visual.Primitive = UIPrimitive.Rectangle;
            visual.Color = Color.FromHTML("#1E3A5F");

            var text = gameObject.AddComponent<UIText>();
            text.Font = font;
            text.Text = label;
            text.FontSize = 17f;
            text.HorizontalAlignment = UIHorizontalAlignment.Center;
            text.VerticalAlignment = UIVerticalAlignment.Center;
            text.Depth = 1f;

            var button = gameObject.AddComponent<UIButton>();
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
            buttons.Add((layout, text));
            return text;
        }

        void UpdateMute(bool value)
        {
            muteText.Text = value ? "SOUND  MUTED" : "SOUND  ON";
            muteVisual.Color = Color.FromHTML(value ? "#713B4C" : "#1E3A5F");
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

    public override void Unload()
    {
        applyScale = null;
        bletris.SaveSettings();
        time.IsPaused = false;
        bletris.ResumeAudio();
    }

    public override void Resize(Vector2 size) => applyScale?.Invoke();

    private void CreateAnimatedMascot()
    {
        var sheet = new SpriteSheet(assets.Get<Texture2D>("mascot"), 255, 255, spacing: 3);
        var mascot = Instantiate("Animated menu mascot");
        mascot.Transform.LocalPosition = new Vector2(0f, -245f);
        mascot.Transform.LocalScale = new Vector2(0.38f);
        var renderer = mascot.AddComponent<SpriteRenderer>();
        renderer.Sprite = sheet[0];
        renderer.Depth = -5f;
        var animator = mascot.AddComponent<SpriteAnimator>();
        var frames = sheet.Sprites.Select((sprite, index) =>
            new SpriteAnimationFrame(sprite, index % 2 == 0 ? 0.09d : 0.13d,
                index == 1 ? "step" : null));
        animator.Add("idle", new SpriteAnimation(frames, SpriteAnimationPlayback.PingPong));
        animator.FrameChanged += _ => renderer.Tint = Color.White;
        animator.MarkerReached += _ => renderer.Tint = Color.Cyan;
        animator.Play("idle");
        tweens.To(new Vector2(0.36f), new Vector2(0.42f), 0.8,
            value => mascot.Transform.LocalScale = value,
            new TweenOptions(Ease.InOutQuad, RepeatCount: -1, Yoyo: true));
    }

    private void CreatePhysicsBackdrop()
    {
        var ground = Instantiate("Menu physics ground");
        ground.Transform.LocalPosition = new Vector2(0f, 320f);
        ground.AddComponent<PhysicsBody>().Type = B2BodyType.b2_staticBody;
        ground.AddComponent<SegmentCollider>(collider =>
        {
            collider.Point1 = new Vector2(-520f, 0f);
            collider.Point2 = new Vector2(520f, 0f);
        });

        var anchor = Instantiate("Menu pendulum anchor");
        anchor.Transform.LocalPosition = new Vector2(-390f, -260f);
        var anchorBody = anchor.AddComponent<PhysicsBody>();
        anchorBody.Type = B2BodyType.b2_staticBody;
        anchor.AddComponent<CircleCollider>().Radius = 8f;

        var packaged = Instantiate(assets.Get<PrefabAsset>("menu-piece"), "Packaged menu pendulum");
        packaged.Transform.LocalPosition = new Vector2(-390f, -120f);
        packaged.GetComponent<SpriteRenderer>()!.Tint = new Color(0.27f, 0.85f, 0.91f, 0.28f);
        packaged.AddComponent<DistanceJoint>(joint =>
        {
            joint.ConnectedBody = anchorBody;
            joint.Length = 140f;
            joint.MaximumLength = 150f;
            joint.EnableSpring = true;
            joint.Hertz = 2f;
            joint.DampingRatio = 0.2f;
        });

        CreatePiece("Circle decoration", new Vector2(390f, -250f), gameObject =>
            gameObject.AddComponent<CircleCollider>().Radius = 42f);
        CreatePiece("Capsule decoration", new Vector2(445f, -80f), gameObject =>
            gameObject.AddComponent<CapsuleCollider>(collider =>
            {
                collider.Point1 = new Vector2(0f, -32f);
                collider.Point2 = new Vector2(0f, 32f);
                collider.Radius = 20f;
            }));

        void CreatePiece(string name, Vector2 position, Action<GameObject> addCollider)
        {
            var piece = Instantiate(name);
            piece.Transform.LocalPosition = position;
            piece.Transform.LocalScale = new Vector2(0.07f);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.Sprite = new Sprite(assets.Get<Texture2D>("block"));
            renderer.Tint = new Color(0.7f, 0.55f, 0.95f, 0.24f);
            renderer.Depth = -10f;
            piece.AddComponent<PhysicsBody>();
            addCollider(piece);
        }
    }
}
