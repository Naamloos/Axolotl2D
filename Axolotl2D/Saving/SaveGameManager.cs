using System.Reflection;
using System.Text.Json;

namespace Axolotl2D.Saving;

/// <summary>Configures versioned save files for one game.</summary>
public sealed class SaveGameOptions
{
    public string GameId { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "AxolotlGame";
    public string? DirectoryPath { get; set; }
    public JsonSerializerOptions SerializerOptions { get; set; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}

/// <summary>Metadata read from a save slot without deserializing game data.</summary>
public sealed record SaveSlotInfo(string Slot, int DataVersion, DateTimeOffset SavedAt, long FileSize);

/// <summary>Reads and writes typed, versioned JSON save slots. Games reconstruct runtime objects from loaded data.</summary>
public sealed class SaveGameManager
{
    private const int FormatVersion = 1;
    private readonly SaveGameOptions options;
    private readonly string directory;

    public SaveGameManager(SaveGameOptions options)
    {
        this.options = options;
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GameId);
        directory = options.DirectoryPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Axolotl2D", Sanitize(options.GameId), "Saves");
    }

    public string DirectoryPath => directory;

    public async Task SaveAsync<T>(string slot, T data, int dataVersion = 1,
        CancellationToken cancellationToken = default)
    {
        ValidateSlot(slot);
        if (dataVersion <= 0) throw new ArgumentOutOfRangeException(nameof(dataVersion));
        ArgumentNullException.ThrowIfNull(data);
        Directory.CreateDirectory(directory);
        var path = GetPath(slot);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var envelope = new SaveEnvelope(FormatVersion, slot, dataVersion, DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(data, options.SerializerOptions));
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, options.SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public async Task<T?> LoadAsync<T>(string slot, int currentDataVersion = 1,
        Func<int, JsonElement, T>? migrate = null, CancellationToken cancellationToken = default)
    {
        ValidateSlot(slot);
        if (currentDataVersion <= 0) throw new ArgumentOutOfRangeException(nameof(currentDataVersion));
        var path = GetPath(slot);
        if (!File.Exists(path)) return default;
        var envelope = await ReadEnvelopeAsync(path, cancellationToken).ConfigureAwait(false);
        if (envelope.DataVersion > currentDataVersion)
            throw new InvalidDataException($"Save slot '{slot}' uses newer data version {envelope.DataVersion}.");
        if (envelope.DataVersion < currentDataVersion)
            return migrate is not null
                ? migrate(envelope.DataVersion, envelope.Data)
                : throw new InvalidDataException($"Save slot '{slot}' requires migration from data version {envelope.DataVersion}.");
        return envelope.Data.Deserialize<T>(options.SerializerOptions)
            ?? throw new InvalidDataException($"Save slot '{slot}' contains null game data.");
    }

    public async Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory)) return [];
        var slots = new List<SaveSlotInfo>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var envelope = await ReadEnvelopeAsync(path, cancellationToken).ConfigureAwait(false);
                slots.Add(new(envelope.Slot, envelope.DataVersion, envelope.SavedAt, new FileInfo(path).Length));
            }
            catch (InvalidDataException) { }
            catch (JsonException) { }
        }
        return slots.OrderByDescending(slot => slot.SavedAt).ToArray();
    }

    public bool Exists(string slot) { ValidateSlot(slot); return File.Exists(GetPath(slot)); }
    public bool Delete(string slot) { ValidateSlot(slot); var path = GetPath(slot); if (!File.Exists(path)) return false; File.Delete(path); return true; }

    private async Task<SaveEnvelope> ReadEnvelopeAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var envelope = await JsonSerializer.DeserializeAsync<SaveEnvelope>(stream, options.SerializerOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException($"Save file '{path}' is empty.");
        if (envelope.FormatVersion != FormatVersion || envelope.DataVersion <= 0 || string.IsNullOrWhiteSpace(envelope.Slot))
            throw new InvalidDataException($"Save file '{path}' has invalid metadata.");
        return envelope;
    }

    private string GetPath(string slot) => Path.Combine(directory, Sanitize(slot) + ".json");

    private static void ValidateSlot(string slot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        if (slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.Contains('/') || slot.Contains('\\'))
            throw new ArgumentException("Save slot names cannot contain path or invalid filename characters.", nameof(slot));
    }

    private static string Sanitize(string value) => string.Concat(value.Select(character =>
        Path.GetInvalidFileNameChars().Contains(character) || character is '/' or '\\' ? '_' : character));

    private sealed record SaveEnvelope(
        int FormatVersion,
        string Slot,
        int DataVersion,
        DateTimeOffset SavedAt,
        JsonElement Data);
}
