using Axolotl2D.Assets;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axolotl2D.Prefabs;

/// <summary>A versioned, data-authored hierarchy of GameObjects and components.</summary>
public sealed class PrefabAsset
{
    public const int CurrentFormatVersion = 1;
    public const string ContentType = "application/vnd.axolotl2d.prefab+json";
    public int FormatVersion { get; }
    public PrefabObject Root { get; }

    internal PrefabAsset(int formatVersion, PrefabObject root)
    {
        FormatVersion = formatVersion;
        Root = root;
    }
}

/// <summary>One GameObject definition inside a prefab hierarchy.</summary>
public sealed record PrefabObject(
    string? Id,
    string Name,
    bool Active,
    IReadOnlyList<string> Tags,
    PrefabTransform Transform,
    IReadOnlyList<PrefabComponent> Components,
    IReadOnlyList<PrefabObject> Children);

/// <summary>Local transform values stored by a prefab.</summary>
public readonly record struct PrefabTransform(Vector2 Position, float Rotation, Vector2 Scale);

/// <summary>A stable component ID and its component-owned JSON data.</summary>
public sealed record PrefabComponent(string Type, bool Enabled, JsonElement Data);

/// <summary>Loads and validates UTF-8 JSON <c>.axprefab</c> assets.</summary>
public sealed class PrefabAssetLoader : IAssetLoader<PrefabAsset>
{
    private const int MaximumBytes = 16 * 1024 * 1024;
    private const int MaximumObjects = 10_000;
    private const int MaximumComponents = 100_000;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 64,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly JsonElement EmptyData = JsonSerializer.SerializeToElement(new { });

    public async ValueTask<PrefabAsset> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var payload = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (payload.Length + read > MaximumBytes)
                throw new InvalidDataException($"Prefab assets cannot exceed {MaximumBytes} bytes.");
            payload.Write(buffer, 0, read);
        }
        payload.Position = 0;

        var document = await JsonSerializer.DeserializeAsync<PrefabDocumentData>(payload, Options, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("The prefab document is empty.");
        if (document.FormatVersion != PrefabAsset.CurrentFormatVersion)
            throw new InvalidDataException($"Prefab format version {document.FormatVersion} is unsupported.");
        if (document.Root is null)
            throw new InvalidDataException("A prefab requires a root GameObject.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var objectCount = 0;
        var componentCount = 0;
        var root = Convert(document.Root, ids, ref objectCount, ref componentCount, 0);
        return new PrefabAsset(document.FormatVersion, root);
    }

    private static PrefabObject Convert(PrefabObjectData source, HashSet<string> ids,
        ref int objectCount, ref int componentCount, int depth)
    {
        if (++objectCount > MaximumObjects)
            throw new InvalidDataException($"A prefab cannot contain more than {MaximumObjects} GameObjects.");
        if (depth >= Options.MaxDepth)
            throw new InvalidDataException($"A prefab hierarchy cannot exceed {Options.MaxDepth} levels.");
        if (source.Name is not null && string.IsNullOrWhiteSpace(source.Name))
            throw new InvalidDataException("Prefab GameObject names cannot be empty.");
        if (source.Id is not null && (string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 256 || !ids.Add(source.Id)))
            throw new InvalidDataException($"Prefab object ID '{source.Id}' is invalid or duplicated.");
        if (source.Tags is null || source.Components is null || source.Children is null)
            throw new InvalidDataException("Prefab tags, components, and children cannot be null.");
        if (source.Components.Any(component => component is null) || source.Children.Any(child => child is null))
            throw new InvalidDataException("Prefab components and children cannot contain null entries.");
        if (source.Tags.Any(string.IsNullOrWhiteSpace) || source.Tags.Distinct(StringComparer.Ordinal).Count() != source.Tags.Count)
            throw new InvalidDataException("Prefab tags must be non-empty and unique per GameObject.");

        componentCount += source.Components.Count;
        if (componentCount > MaximumComponents)
            throw new InvalidDataException($"A prefab cannot contain more than {MaximumComponents} components.");
        var components = source.Components.Select(component =>
        {
            if (string.IsNullOrWhiteSpace(component.Type) || component.Type.Length > 256)
                throw new InvalidDataException("Prefab component IDs must be between 1 and 256 characters.");
            if (component.Data.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Object))
                throw new InvalidDataException($"Prefab component '{component.Type}' data must be a JSON object.");
            return new PrefabComponent(component.Type, component.Enabled ?? true,
                component.Data.ValueKind == JsonValueKind.Undefined ? EmptyData.Clone() : component.Data.Clone());
        }).ToArray();

        var transform = source.Transform ?? new();
        var position = ReadVector(transform.Position, Vector2.Zero, "position");
        var scale = ReadVector(transform.Scale, Vector2.One, "scale");
        var rotation = transform.Rotation ?? 0f;
        if (!float.IsFinite(rotation)) throw new InvalidDataException("Prefab rotations must be finite.");
        var children = new List<PrefabObject>(source.Children.Count);
        foreach (var child in source.Children)
            children.Add(Convert(child, ids, ref objectCount, ref componentCount, depth + 1));
        return new(source.Id, source.Name ?? "GameObject", source.Active ?? true, source.Tags.ToArray(),
            new(position, rotation, scale), components, children.ToArray());
    }

    private static Vector2 ReadVector(PrefabVectorData? value, Vector2 fallback, string name)
    {
        if (value is null) return fallback;
        if (value.X is null || value.Y is null || !float.IsFinite(value.X.Value) || !float.IsFinite(value.Y.Value))
            throw new InvalidDataException($"Prefab {name} requires finite x and y values.");
        return new(value.X.Value, value.Y.Value);
    }

    private sealed class PrefabDocumentData
    {
        public PrefabDocumentData() { }
        public int FormatVersion { get; init; }
        public PrefabObjectData? Root { get; init; }
    }

    private sealed class PrefabObjectData
    {
        public PrefabObjectData() { }
        public string? Id { get; init; }
        public string? Name { get; init; }
        public bool? Active { get; init; }
        public List<string>? Tags { get; init; } = [];
        public PrefabTransformData? Transform { get; init; }
        public List<PrefabComponentData>? Components { get; init; } = [];
        public List<PrefabObjectData>? Children { get; init; } = [];
    }

    private sealed class PrefabTransformData
    {
        public PrefabTransformData() { }
        public PrefabVectorData? Position { get; init; }
        public float? Rotation { get; init; }
        public PrefabVectorData? Scale { get; init; }
    }

    private sealed class PrefabVectorData
    {
        public PrefabVectorData() { }
        public float? X { get; init; }
        public float? Y { get; init; }
    }

    private sealed class PrefabComponentData
    {
        public PrefabComponentData() { }
        public string Type { get; init; } = string.Empty;
        public bool? Enabled { get; init; }
        public JsonElement Data { get; init; }
    }
}
