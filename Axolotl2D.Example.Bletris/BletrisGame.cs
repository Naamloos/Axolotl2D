using Axolotl2D.Assets;
using Axolotl2D.Audio;
using Axolotl2D.Example.Assets;
using Axolotl2D.Packages;
using Axolotl2D.Prefabs;
using Axolotl2D.Rendering;
using Axolotl2D.Saving;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace Axolotl2D.Example.Bletris;

public sealed class BletrisGame : Game
{
    private const int ProfileVersion = 2;
    private static readonly Vector2 DesignViewport = new(1080f, 720f);
    private readonly AssetManager assets;
    private readonly AxolotlPackageManager packages;
    private readonly AudioPlayer audio;
    private readonly SaveGameManager saves;
    private readonly ILogger<BletrisGame> logger;
    private readonly Dictionary<BletrisSound, SoundAsset> sounds = [];
    private SoundAsset? musicAsset;
    private SoundPlayback? music;
    private Task pendingSave = Task.CompletedTask;

    public int HighScore { get; private set; }
    public float Volume => audio.MasterVolume;
    public bool Muted => audio.Muted;
    public float ScreenScale => Math.Max(0.01f,
        Math.Min(Viewport.X / DesignViewport.X, Viewport.Y / DesignViewport.Y));

    public BletrisGame(IServiceProvider services, AssetManager assets, AxolotlPackageManager packages,
        AudioPlayer audio, SaveGameManager saves, ILogger<BletrisGame> logger)
        : base(services, new GameWindowOptions
        {
            Title = "Bletris",
            Size = new Vector2(1080f, 720f),
            MaximumDrawRate = 144d,
            MaximumUpdateRate = 120d,
            ShowFramerateInTitle = true
        })
    {
        this.assets = assets;
        this.packages = packages;
        this.audio = audio;
        this.saves = saves;
        this.logger = logger;
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await packages.LoadAsync(Path.Combine(AppContext.BaseDirectory, ExampleAssetPackage.FileName),
            ExampleAssetPackage.TrustPolicy(), cancellationToken);
        await assets.LoadPackageAsync<Texture2D>("block", ExampleAssetPackage.Id, "logo", cancellationToken);
        await assets.LoadPackageAsync<Texture2D>("logo", ExampleAssetPackage.Id, "logo", cancellationToken);
        await assets.LoadPackageAsync<Texture2D>("mascot", ExampleAssetPackage.Id, "run", cancellationToken);
        await assets.LoadPackageAsync<FontAsset>("ui-font", ExampleAssetPackage.Id, "ui-font", cancellationToken);
        await assets.LoadPackageAsync<PrefabAsset>("menu-piece", ExampleAssetPackage.Id,
            "prefabs/physics-tooling", cancellationToken);
        musicAsset = await assets.LoadPackageAsync<SoundAsset>("music", ExampleAssetPackage.Id, "music", cancellationToken);

        var profile = await saves.LoadAsync<BletrisProfile>("profile", ProfileVersion, MigrateProfile,
            cancellationToken);
        HighScore = Math.Max(0, profile?.HighScore ?? 0);
        audio.MasterVolume = Math.Clamp(profile?.Volume ?? 0.7f, 0f, 1f);
        audio.Muted = profile?.Muted ?? false;

        sounds.Add(BletrisSound.Move, CreateTone(220f, 0.035f));
        sounds.Add(BletrisSound.Rotate, CreateTone(440f, 0.06f));
        sounds.Add(BletrisSound.Drop, CreateTone(110f, 0.1f));
        sounds.Add(BletrisSound.Clear, CreateTone(740f, 0.18f));
        sounds.Add(BletrisSound.GameOver, CreateTone(82f, 0.35f));
        sounds.Add(BletrisSound.Ui, CreateTone(520f, 0.045f));

        OnLoad += ApplyLoadedDisplayMode;
        OnLoad += StartMusic;

        void ApplyLoadedDisplayMode()
        {
            var mode = profile?.WindowMode ?? GameWindowMode.Windowed;
            WindowMode = Enum.IsDefined(mode) ? mode : GameWindowMode.Windowed;
            OnLoad -= ApplyLoadedDisplayMode;
        }
    }

    public void RecordScore(int score)
    {
        if (score <= HighScore) return;
        HighScore = score;
        QueueSave();
    }

    public void SetVolume(float value) => audio.MasterVolume = Math.Clamp(value, 0f, 1f);

    public void SetMuted(bool value) => audio.Muted = value;

    public void CycleWindowMode() => WindowMode = WindowMode switch
    {
        GameWindowMode.Windowed => GameWindowMode.BorderlessFullscreen,
        GameWindowMode.BorderlessFullscreen => GameWindowMode.Fullscreen,
        _ => GameWindowMode.Windowed
    };

    public void SaveSettings() => QueueSave();

    public void PauseAudio() => audio.PauseAll();

    public void ResumeAudio() => audio.ResumeAll();

    public void PlayUiSound(float pitch = 1f) =>
        audio.PlayOneShot(sounds[BletrisSound.Ui], volume: 0.22f, pitch: pitch);

    public void PlaySpatialSound(BletrisSound sound, Vector2 position, float pitch = 1f)
    {
        var volume = sound == BletrisSound.Move ? 0.12f : 0.32f;
        audio.PlayOneShotSpatial(sounds[sound], position, volume, pitch,
            referenceDistance: 140f, maximumDistance: 900f, rolloffFactor: 0.8f);
    }

    private void QueueSave()
    {
        var profile = new BletrisProfile(HighScore, Volume, Muted, WindowMode);
        pendingSave = SaveAfterAsync(pendingSave, profile);
    }

    private async Task SaveAfterAsync(Task previous, BletrisProfile profile)
    {
        await previous.ConfigureAwait(false);
        try { await saves.SaveAsync("profile", profile, ProfileVersion).ConfigureAwait(false); }
        catch (Exception exception) { logger.LogError(exception, "Could not save Bletris profile"); }
    }

    private static BletrisProfile MigrateProfile(int version, JsonElement data) => version switch
    {
        1 => new BletrisProfile(data.GetProperty("highScore").GetInt32(), 0.7f, false,
            GameWindowMode.Windowed),
        _ => throw new InvalidDataException($"Cannot migrate Bletris profile version {version}.")
    };

    private void StartMusic() => music = audio.Play(musicAsset!, loop: true, volume: 0.32f);

    protected override void Cleanup()
    {
        OnLoad -= StartMusic;
        pendingSave.GetAwaiter().GetResult();
        music?.Dispose();
    }

    private static SoundAsset CreateTone(float frequency, float duration)
    {
        const int sampleRate = 22050;
        var sampleCount = (int)(sampleRate * duration);
        var samples = new byte[sampleCount * sizeof(short)];
        for (var index = 0; index < sampleCount; index++)
        {
            var fade = 1f - index / (float)sampleCount;
            var value = (short)(MathF.Sin(index * MathF.Tau * frequency / sampleRate) * short.MaxValue * 0.2f * fade);
            BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(index * sizeof(short), sizeof(short)), value);
        }
        return new SoundAsset(samples, sampleRate, channels: 1, bitsPerSample: 16);
    }
}

public enum BletrisSound
{
    Move,
    Rotate,
    Drop,
    Clear,
    GameOver,
    Ui
}

internal sealed record BletrisProfile(int HighScore, float Volume, bool Muted, GameWindowMode WindowMode);
