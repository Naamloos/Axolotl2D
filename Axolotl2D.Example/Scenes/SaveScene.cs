using Axolotl2D.Assets;
using Axolotl2D.Rendering;
using Axolotl2D.Saving;
using Axolotl2D.UI;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class SaveScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    SaveGameManager saves) : ExampleSceneBase(assets)
{
    private const string Slot = "example";
    private UIText status = null!;
    private Task<string>? operation;
    private int counter;

    public override void Load()
    {
        LoadExample("Save games", "#1D2430");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        AddButton("SAVE", 170f, () => Start(SaveAsync));
        AddButton("LOAD", 250f, () => Start(LoadAsync));
        AddButton("DELETE", 330f, Delete);

        var statusObject = Instantiate("Save status");
        var statusTransform = statusObject.AddComponent<UITransform>();
        statusTransform.Anchor = Vector2.Zero;
        statusTransform.AnchoredPosition = new Vector2(420f, 185f);
        statusTransform.Size = new Vector2(560f, 220f);
        status = statusObject.AddComponent<UIText>();
        status.Font = Font;
        status.Text = saves.Exists(Slot) ? "Slot exists. Load or overwrite it." : "No example slot exists yet.";
        status.FontSize = 19f;
        status.Color = Color.FromHTML("#D6E4FF");
        status.VerticalAlignment = UIVerticalAlignment.Center;
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        DrawText(spriteBatch, textRenderer, "Typed, versioned JSON slots with atomic replacement",
            new Vector2(24f, 70f), 15f);
        DrawText(spriteBatch, textRenderer, $"Directory: {saves.DirectoryPath}",
            new Vector2(24f, 600f), 12f, Color.LightGray);
    }

    protected override void UpdateExample(double deltaTime)
    {
        if (operation is not { IsCompleted: true }) return;
        try { status.Text = operation.GetAwaiter().GetResult(); }
        catch (Exception exception) { status.Text = $"Error: {exception.Message}"; }
        operation = null;
    }

    private void AddButton(string label, float y, Action clicked)
    {
        var gameObject = Instantiate($"{label} button");
        var transform = gameObject.AddComponent<UITransform>();
        transform.Anchor = Vector2.Zero;
        transform.AnchoredPosition = new Vector2(90f, y);
        transform.Size = new Vector2(250f, 58f);
        var visual = gameObject.AddComponent<UIVisual>();
        visual.Color = Color.FromHTML("#355070");
        var text = gameObject.AddComponent<UIText>();
        text.Font = Font;
        text.Text = label;
        text.FontSize = 18f;
        text.HorizontalAlignment = UIHorizontalAlignment.Center;
        text.VerticalAlignment = UIVerticalAlignment.Center;
        text.Depth = 1f;
        var button = gameObject.AddComponent<UIButton>();
        button.PointerEntered += () => visual.Color = Color.FromHTML("#4A6A91");
        button.PointerExited += () => visual.Color = Color.FromHTML("#355070");
        button.Clicked += clicked;
    }

    private void Start(Func<Task<string>> action)
    {
        if (operation is not null) return;
        status.Text = "Working...";
        operation = action();
    }

    private async Task<string> SaveAsync()
    {
        var value = ++counter;
        await saves.SaveAsync(Slot, new ExampleSaveData(value, DateTimeOffset.UtcNow));
        return $"Saved counter {value}.";
    }

    private async Task<string> LoadAsync()
    {
        var data = await saves.LoadAsync<ExampleSaveData>(Slot);
        if (data is null) return "No save found.";
        counter = data.Counter;
        return $"Loaded counter {data.Counter}\nSaved at {data.SavedAt.LocalDateTime:g}.";
    }

    private void Delete()
    {
        if (operation is not null) return;
        status.Text = saves.Delete(Slot) ? "Deleted the example slot." : "No save found.";
    }
}

public sealed record ExampleSaveData(int Counter, DateTimeOffset SavedAt);
