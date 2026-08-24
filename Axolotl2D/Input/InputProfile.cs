using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axolotl2D.Input;

/// <summary>Named control schemes containing serializable action bindings.</summary>
public sealed class InputProfile
{
    private const int CurrentFormatVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly Dictionary<string, Dictionary<string, InputBinding>> schemes = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Schemes => schemes.Keys;

    public void SetBinding(string scheme, string action, InputBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        if (!schemes.TryGetValue(scheme, out var bindings))
            schemes.Add(scheme, bindings = new(StringComparer.Ordinal));
        bindings[action] = binding;
    }

    public InputBinding GetBinding(string scheme, string action) =>
        TryGetBinding(scheme, action, out var binding)
            ? binding
            : throw new KeyNotFoundException($"Input profile has no '{action}' binding in the '{scheme}' scheme.");

    public bool TryGetBinding(string scheme, string action, out InputBinding binding)
    {
        binding = null!;
        return schemes.TryGetValue(scheme, out var bindings) && bindings.TryGetValue(action, out binding);
    }

    public IReadOnlyList<InputBindingConflict> FindConflicts(string scheme)
    {
        if (!schemes.TryGetValue(scheme, out var bindings))
            throw new KeyNotFoundException($"Input profile has no '{scheme}' scheme.");
        return FindConflicts(bindings);
    }

    public string ToJson() => JsonSerializer.Serialize(new ProfileDocument(CurrentFormatVersion, schemes), JsonOptions);

    public static InputProfile FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var document = JsonSerializer.Deserialize<ProfileDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("Input profile JSON is empty.");
        if (document.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported input profile format version {document.FormatVersion}.");
        if (document.Schemes is null)
            throw new InvalidDataException("Input profile schemes are missing.");

        var profile = new InputProfile();
        foreach (var (scheme, bindings) in document.Schemes)
        {
            if (bindings is null) throw new InvalidDataException($"Input scheme '{scheme}' has no bindings.");
            foreach (var (action, binding) in bindings)
                profile.SetBinding(scheme, action, binding ?? throw new InvalidDataException($"Input action '{action}' has no binding."));
        }
        return profile;
    }

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, ToJson());
    }

    public static InputProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromJson(File.ReadAllText(path));
    }

    internal IReadOnlyDictionary<string, InputBinding> GetScheme(string scheme) =>
        schemes.TryGetValue(scheme, out var bindings)
            ? bindings
            : throw new KeyNotFoundException($"Input profile has no '{scheme}' scheme.");

    internal static IReadOnlyList<InputBindingConflict> FindConflicts(
        IEnumerable<KeyValuePair<string, InputBinding>> bindings)
    {
        var entries = bindings.ToArray();
        var conflicts = new List<InputBindingConflict>();
        for (var first = 0; first < entries.Length; first++)
            for (var second = first + 1; second < entries.Length; second++)
                foreach (var firstControl in entries[first].Value.Controls)
                    if (entries[second].Value.Controls.Any(secondControl => Conflicts(firstControl, secondControl)) &&
                        !conflicts.Any(value => value.FirstAction == entries[first].Key &&
                            value.SecondAction == entries[second].Key && value.Control == firstControl))
                        conflicts.Add(new(entries[first].Key, entries[second].Key, firstControl));
        return conflicts;
    }

    private static bool Conflicts(InputControl first, InputControl second)
    {
        if (first == second) return true;
        if (first.GamepadIndex != second.GamepadIndex) return false;
        if (first.Kind == InputControlKind.GamepadStick && second.Kind == InputControlKind.GamepadAxis)
            return StickContains(first.Name, second.Name);
        if (second.Kind == InputControlKind.GamepadStick && first.Kind == InputControlKind.GamepadAxis)
            return StickContains(second.Name, first.Name);
        return false;
    }

    private static bool StickContains(string stick, string axis) => stick switch
    {
        nameof(GamepadStick.Left) => axis is nameof(GamepadAxis.LeftStickX) or nameof(GamepadAxis.LeftStickY),
        nameof(GamepadStick.Right) => axis is nameof(GamepadAxis.RightStickX) or nameof(GamepadAxis.RightStickY),
        _ => false
    };

    private sealed record ProfileDocument(
        int FormatVersion,
        Dictionary<string, Dictionary<string, InputBinding>> Schemes);
}

/// <summary>An in-progress interactive button binding capture.</summary>
public sealed class InputCapture
{
    private Action<InputCapture>? cancel;

    internal InputCapture(string actionName, Action<InputCapture> cancel)
    {
        ActionName = actionName;
        this.cancel = cancel;
    }

    public string ActionName { get; }
    public bool IsPending => !IsCompleted && !IsCanceled;
    public bool IsCompleted { get; private set; }
    public bool IsCanceled { get; private set; }
    public InputBinding? Binding { get; private set; }
    public event Action<InputBinding>? Completed;

    public void Cancel()
    {
        if (!IsPending) return;
        IsCanceled = true;
        Interlocked.Exchange(ref cancel, null)?.Invoke(this);
    }

    internal void Complete(InputBinding binding)
    {
        if (!IsPending) return;
        Binding = binding;
        IsCompleted = true;
        cancel = null;
        Completed?.Invoke(binding);
    }
}
