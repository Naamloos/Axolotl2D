using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Axolotl2D.MSBuild;

internal sealed class ContentPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<IContentImporter> importers;
    private readonly string intermediateDirectory;

    public ContentPipeline(IReadOnlyList<IContentImporter> importers, string intermediateDirectory)
    {
        this.importers = importers;
        this.intermediateDirectory = intermediateDirectory;
    }

    public PipelineAsset Import(string sourcePath, string logicalName, string? importerName,
        IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        var context = new ContentImporterContext(sourcePath, NormalizeName(logicalName), metadata);
        var importer = string.IsNullOrWhiteSpace(importerName)
            ? importers.FirstOrDefault(candidate => candidate.CanImport(context))
            : importers.FirstOrDefault(candidate => string.Equals(candidate.GetType().FullName, importerName, StringComparison.Ordinal)
                || string.Equals(candidate.GetType().Name, importerName, StringComparison.Ordinal));
        if (importer is null)
            throw new InvalidOperationException($"No content importer accepts '{sourcePath}'.");

        Directory.CreateDirectory(intermediateDirectory);
        var identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.LogicalName)))[..24];
        var outputPath = Path.Combine(intermediateDirectory, identifier + ".bin");
        var statePath = outputPath + ".json";
        var source = new FileInfo(sourcePath);
        var fingerprint = new ImportFingerprint(source.FullName, source.Length, source.LastWriteTimeUtc.Ticks,
            importer.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(metadata, JsonOptions),
            importer.GetType().Module.ModuleVersionId.ToString("D"));
        if (File.Exists(outputPath) && File.Exists(statePath))
        {
            var state = JsonSerializer.Deserialize<ImportState>(File.ReadAllText(statePath), JsonOptions);
            if (state is not null && state.Fingerprint == fingerprint)
                return new(context.LogicalName, state.RuntimeType, state.ContentType, outputPath, state.Dependencies ?? []);
        }

        var imported = importer.ImportAsync(context, cancellationToken).AsTask().GetAwaiter().GetResult();
        var normalizedResultName = NormalizeName(imported.LogicalName);
        if (!string.Equals(normalizedResultName, context.LogicalName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Importer '{importer.GetType().Name}' changed asset name '{context.LogicalName}' to '{normalizedResultName}'.");
        File.WriteAllBytes(outputPath, imported.Payload);
        File.WriteAllText(statePath, JsonSerializer.Serialize(
            new ImportState(fingerprint, imported.RuntimeType, imported.ContentType, imported.Dependencies ?? []), JsonOptions));
        return new(context.LogicalName, imported.RuntimeType, imported.ContentType, outputPath, imported.Dependencies ?? []);
    }

    public static string DefaultName(string projectDirectory, string sourcePath)
    {
        var name = Path.GetRelativePath(projectDirectory, sourcePath).Replace('\\', '/');
        if (name.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) name = name[7..];
        var extension = Path.GetExtension(name);
        return NormalizeName(name[..^extension.Length]);
    }

    public static string NormalizeName(string name)
    {
        var normalized = name.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 1024 ||
            normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidOperationException($"Invalid Axolotl asset name '{name}'.");
        return normalized;
    }

    private sealed record ImportFingerprint(string SourcePath, long Length, long LastWriteTicks,
        string Importer, string Metadata, string? ImporterModuleId = null);
    private sealed record ImportState(ImportFingerprint Fingerprint, string RuntimeType, string ContentType,
        IReadOnlyList<string>? Dependencies = null);
}

internal sealed record PipelineAsset(string Name, string RuntimeType, string ContentType, string OutputPath,
    IReadOnlyList<string> Dependencies);

public sealed class ImportAxolotlAssets : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Assets { get; set; } = [];
    public ITaskItem[] ImporterAssemblies { get; set; } = [];
    [Required] public string ProjectDirectory { get; set; } = null!;
    [Required] public string IntermediateDirectory { get; set; } = null!;
    [Output] public ITaskItem[] CompiledAssets { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            var pipeline = new ContentPipeline(ContentImporters.Load(ImporterAssemblies.Select(item => item.ItemSpec)), IntermediateDirectory);
            var results = new List<ITaskItem>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Assets)
            {
                var sourcePath = Path.GetFullPath(item.ItemSpec, ProjectDirectory);
                var requestedName = item.GetMetadata("Name");
                var name = string.IsNullOrWhiteSpace(requestedName)
                    ? ContentPipeline.DefaultName(ProjectDirectory, sourcePath)
                    : ContentPipeline.NormalizeName(requestedName);
                if (!names.Add(name))
                    throw new InvalidOperationException($"Duplicate Axolotl asset name '{name}'.");
                var metadata = item.MetadataNames.Cast<string>().ToDictionary(key => key, item.GetMetadata, StringComparer.Ordinal);
                var asset = pipeline.Import(sourcePath, name, item.GetMetadata("Importer"), metadata);
                var result = new TaskItem(asset.OutputPath);
                result.SetMetadata("Name", asset.Name);
                result.SetMetadata("EntryName", "$assets/" + asset.Name);
                result.SetMetadata("RuntimeType", asset.RuntimeType);
                result.SetMetadata("ContentType", asset.ContentType);
                result.SetMetadata("DependenciesJson", JsonSerializer.Serialize(asset.Dependencies));
                results.Add(result);
            }
            CompiledAssets = results.ToArray();
            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }
}
