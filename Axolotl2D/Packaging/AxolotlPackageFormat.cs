using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Axolotl2D.Packages;

/// <summary>Constants and limits for the little-endian Axolotl package format.</summary>
public static class AxolotlPackageFormat
{
    internal static ReadOnlySpan<byte> Magic => "AXPKG\r\n\x1A"u8;
    internal static ReadOnlySpan<byte> SignatureMagic => "AXSIG\r\n\x1A"u8;
    /// <summary>The only package format version supported by this runtime.</summary>
    public const ushort CurrentVersion = 1;
    internal const ushort SignedFlag = 1;
    internal const ushort EcdsaP256Sha256 = 1;
    internal const int MaximumEntries = 100_000;
    internal const int MaximumStringBytes = 1_048_576;
    internal const long MaximumEntryBytes = 8L * 1024 * 1024 * 1024;
    internal const long MaximumManifestBytes = 4L * 1024 * 1024;
    internal const long MaximumAssemblyBytes = 512L * 1024 * 1024;
    internal const int MaximumSignatureBytes = 65_536;
    /// <summary>The reserved manifest entry name.</summary>
    public const string ManifestEntryName = "$module/manifest.json";
    /// <summary>The manifest entry content type.</summary>
    public const string ManifestContentType = "application/vnd.axolotl2d.module+json";
    /// <summary>The managed assembly entry content type.</summary>
    public const string AssemblyContentType = "application/vnd.microsoft.portable-executable";
}

/// <summary>Describes a module and the assets stored in its package.</summary>
public sealed class AxolotlPackageManifest
{
    /// <summary>The stable logical module ID.</summary>
    public required string Id { get; init; }
    /// <summary>The exact module version.</summary>
    public required string Version { get; init; }
    /// <summary>The package format version represented by this manifest.</summary>
    public ushort FormatVersion { get; init; } = AxolotlPackageFormat.CurrentVersion;
    /// <summary>The package entry containing the primary module assembly.</summary>
    public required string Assembly { get; init; }
    /// <summary>The generated registration type's exact name, when executable registration is available.</summary>
    public string? RegistrationType { get; init; }
    /// <summary>The optional <see cref="IAxolotlModule"/> implementation's exact name.</summary>
    public string? Entrypoint { get; init; }
    /// <summary>The logical signer ID, never a source of trust by itself.</summary>
    public string? SignerId { get; init; }
    /// <summary>Required packages and their exact versions.</summary>
    public IReadOnlyList<AxolotlPackageDependency> Dependencies { get; init; } = [];
    /// <summary>Runtime asset metadata.</summary>
    public IReadOnlyList<AxolotlPackageAsset> Assets { get; init; } = [];

    internal void Validate()
    {
        if (!IsValidId(Id))
            throw new InvalidDataException("The package manifest has an invalid module ID.");
        if (!IsValidVersion(Version))
            throw new InvalidDataException($"Package '{Id}' has invalid version '{Version}'.");
        if (FormatVersion != AxolotlPackageFormat.CurrentVersion)
            throw new InvalidDataException($"Manifest format version {FormatVersion} is unsupported.");
        ValidateEntryName(Assembly, "module assembly");
        if (SignerId is not null && !IsValidId(SignerId))
            throw new InvalidDataException($"Package '{Id}' has an invalid signer ID.");
        if (Dependencies is null || Assets is null)
            throw new InvalidDataException($"Package '{Id}' has missing dependency or asset metadata.");

        var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in Dependencies)
        {
            if (dependency is null || !IsValidId(dependency.Id) || !IsValidVersion(dependency.Version))
                throw new InvalidDataException($"Package '{Id}' contains a malformed dependency.");
            if (!dependencyIds.Add(dependency.Id))
                throw new InvalidDataException($"Package '{Id}' declares dependency '{dependency.Id}' more than once.");
        }

        var assetNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in Assets)
        {
            if (asset is null)
                throw new InvalidDataException($"Package '{Id}' contains null asset metadata.");
            ValidateEntryName(asset.Name, "asset name");
            ValidateEntryName(asset.Entry, "asset entry");
            if (string.IsNullOrWhiteSpace(asset.RuntimeType) || asset.RuntimeType.Length > 4096 || !assetNames.Add(asset.Name))
                throw new InvalidDataException($"Package '{Id}' contains malformed or duplicate asset metadata for '{asset.Name}'.");
            foreach (var dependency in asset.Dependencies ?? [])
                ValidateEntryName(dependency, "asset dependency");
        }
    }

    internal static void ValidateEntryName(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value.Contains('\\') || value.StartsWith('/') ||
            value.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException($"Invalid {description} '{value}'.");
    }

    private static bool IsValidId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private static bool IsValidVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
        var prerelease = value.IndexOf('-');
        var build = value.IndexOf('+');
        if (build >= 0 && prerelease > build) return false;
        var coreEnd = new[] { prerelease, build }.Where(index => index >= 0).DefaultIfEmpty(value.Length).Min();
        var core = value[..coreEnd].Split('.');
        if (core.Length != 3 || core.Any(part => part.Length == 0 || part.Any(character => !char.IsAsciiDigit(character)) ||
            part.Length > 1 && part[0] == '0')) return false;
        if (prerelease >= 0 && !IsValidVersionSuffix(value[(prerelease + 1)..(build >= 0 ? build : value.Length)])) return false;
        return build < 0 || IsValidVersionSuffix(value[(build + 1)..]);
    }

    private static bool IsValidVersionSuffix(string value) => value.Length > 0 && !value.StartsWith('.') &&
        !value.EndsWith('.') && !value.Contains("..", StringComparison.Ordinal) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');
}

/// <summary>A required module ID and exact version.</summary>
/// <param name="Id">The required package ID.</param><param name="Version">The exact required version.</param>
public sealed record AxolotlPackageDependency(string Id, string Version);
/// <summary>Maps a logical asset name to its package entry and runtime type.</summary>
/// <param name="Name">The package-local asset name.</param><param name="Entry">The payload entry name.</param>
/// <param name="RuntimeType">The target runtime type.</param><param name="Dependencies">Optional logical asset dependencies.</param>
public sealed record AxolotlPackageAsset(string Name, string Entry, string RuntimeType,
    IReadOnlyList<string>? Dependencies = null);
/// <summary>Describes the detached signature trailer of a package.</summary>
/// <param name="SignerId">The logical signer.</param><param name="Algorithm">The format algorithm ID.</param>
/// <param name="SignedLength">The number of signed bytes.</param><param name="Value">The signature bytes.</param>
public sealed record AxolotlPackageSignature(string SignerId, ushort Algorithm, long SignedLength, byte[] Value);

/// <summary>A package entry indexed without loading its payload.</summary>
public sealed record AxolotlPackageEntry(string Name, string ContentType, long Length, long Offset);

/// <summary>One streaming input used when writing a package.</summary>
public sealed record AxolotlPackageContent(string Name, string ContentType, string Path);

/// <summary>Writes deterministic, optionally signed Axolotl packages.</summary>
public static class AxolotlPackageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes a manifest using the deterministic package JSON settings.</summary>
    public static byte[] SerializeManifest(AxolotlPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.Validate();
        return JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
    }

    /// <summary>Writes a complete package, replacing the destination only after writing succeeds.</summary>
    public static void Write(string outputPath, AxolotlPackageManifest manifest,
        IEnumerable<AxolotlPackageContent> contents, ECDsa? signingKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(contents);
        manifest.Validate();

        var allContents = new List<AxolotlPackageContent>();
        var manifestPath = Path.GetTempFileName();
        var fullOutputPath = Path.GetFullPath(outputPath);
        var temporaryOutputPath = fullOutputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(manifestPath, SerializeManifest(manifest));
            allContents.Add(new(AxolotlPackageFormat.ManifestEntryName, AxolotlPackageFormat.ManifestContentType, manifestPath));
            allContents.AddRange(contents);
            ValidateContents(allContents, manifest);

            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            using (var stream = new FileStream(temporaryOutputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                WriteHeader(stream, allContents.Count, signingKey is not null);
                foreach (var content in allContents)
                    WriteEntry(stream, content);

                if (signingKey is not null)
                    WriteSignature(stream, manifest.SignerId!, signingKey);
            }
            File.Move(temporaryOutputPath, fullOutputPath, overwrite: true);
        }
        finally
        {
            File.Delete(manifestPath);
            File.Delete(temporaryOutputPath);
        }
    }

    private static void ValidateContents(IReadOnlyList<AxolotlPackageContent> contents, AxolotlPackageManifest manifest)
    {
        if (contents.Count > AxolotlPackageFormat.MaximumEntries)
            throw new InvalidDataException("The package contains too many entries.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var content in contents)
        {
            AxolotlPackageManifest.ValidateEntryName(content.Name, "entry name");
            if (string.IsNullOrWhiteSpace(content.ContentType) ||
                Encoding.UTF8.GetByteCount(content.Name) > AxolotlPackageFormat.MaximumStringBytes ||
                Encoding.UTF8.GetByteCount(content.ContentType) > AxolotlPackageFormat.MaximumStringBytes ||
                !names.Add(content.Name))
                throw new InvalidDataException($"Duplicate or malformed package entry '{content.Name}'.");
            var length = new FileInfo(content.Path).Length;
            if (length < 0 || length > AxolotlPackageFormat.MaximumEntryBytes)
                throw new InvalidDataException($"Entry '{content.Name}' is unreasonably large.");
        }
        if (!names.Contains(manifest.Assembly))
            throw new InvalidDataException($"Module assembly entry '{manifest.Assembly}' is missing.");
        foreach (var asset in manifest.Assets)
            if (!names.Contains(asset.Entry))
                throw new InvalidDataException($"Asset entry '{asset.Entry}' is missing.");
    }

    private static void WriteHeader(Stream stream, int entryCount, bool signed)
    {
        stream.Write(AxolotlPackageFormat.Magic);
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(header, AxolotlPackageFormat.CurrentVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(header[2..], signed ? AxolotlPackageFormat.SignedFlag : (ushort)0);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], entryCount);
        stream.Write(header);
    }

    private static void WriteEntry(Stream stream, AxolotlPackageContent content)
    {
        var name = Encoding.UTF8.GetBytes(content.Name);
        var type = Encoding.UTF8.GetBytes(content.ContentType);
        var length = new FileInfo(content.Path).Length;
        Span<byte> header = stackalloc byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(header, name.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], type.Length);
        BinaryPrimitives.WriteInt64LittleEndian(header[8..], length);
        stream.Write(header);
        stream.Write(name);
        stream.Write(type);
        using var input = File.OpenRead(content.Path);
        input.CopyTo(stream);
    }

    private static void WriteSignature(FileStream stream, string signerId, ECDsa key)
    {
        if (string.IsNullOrWhiteSpace(signerId))
            throw new InvalidDataException("A signed package requires a signer ID in its manifest.");
        if (key.KeySize != 256)
            throw new CryptographicException("Axolotl packages require an ECDSA P-256 signing key.");
        var signedLength = stream.Position;
        stream.Position = 0;
        var hash = SHA256.HashData(new BoundedReadStream(stream, signedLength, leaveOpen: true));
        var signature = key.SignHash(hash);
        stream.Position = signedLength;
        stream.Write(AxolotlPackageFormat.SignatureMagic);
        var signer = Encoding.UTF8.GetBytes(signerId);
        Span<byte> header = stackalloc byte[10];
        BinaryPrimitives.WriteUInt16LittleEndian(header, AxolotlPackageFormat.EcdsaP256Sha256);
        BinaryPrimitives.WriteInt32LittleEndian(header[2..], signer.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[6..], signature.Length);
        stream.Write(header);
        stream.Write(signer);
        stream.Write(signature);
    }
}
