using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Axolotl2D.Packages;

/// <summary>A validated package index whose payloads remain on disk until opened.</summary>
public sealed class AxolotlPackage : IDisposable
{
    private readonly IReadOnlyDictionary<string, AxolotlPackageEntry> entriesByName;
    private readonly FileStream fileLock;

    internal AxolotlPackage(string path, IReadOnlyList<AxolotlPackageEntry> entries,
        AxolotlPackageManifest manifest, AxolotlPackageSignature? signature, FileStream fileLock)
    {
        Path = path;
        Entries = entries;
        Manifest = manifest;
        Signature = signature;
        this.fileLock = fileLock;
        entriesByName = entries.ToDictionary(entry => entry.Name, StringComparer.Ordinal);
    }

    /// <summary>The absolute source path retained under a read lock.</summary>
    public string Path { get; }
    /// <summary>The structurally validated entry index.</summary>
    public IReadOnlyList<AxolotlPackageEntry> Entries { get; }
    /// <summary>The validated module manifest.</summary>
    public AxolotlPackageManifest Manifest { get; }
    /// <summary>The signature trailer, when present.</summary>
    public AxolotlPackageSignature? Signature { get; }

    /// <summary>Looks up an indexed entry without reading its payload.</summary>
    public bool TryGetEntry(string name, out AxolotlPackageEntry? entry) => entriesByName.TryGetValue(name, out entry);

    /// <summary>Opens a bounded stream over one entry payload.</summary>
    public Stream OpenEntry(string name)
    {
        if (!entriesByName.TryGetValue(name, out var entry))
            throw new KeyNotFoundException($"Package entry '{name}' does not exist.");
        var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Position = entry.Offset;
        return new BoundedReadStream(stream, entry.Length, leaveOpen: false);
    }

    /// <inheritdoc />
    public void Dispose() => fileLock.Dispose();
}

/// <summary>Reads and structurally validates an Axolotl package without executing it.</summary>
public static class AxolotlPackageReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Opens and indexes a package. Dispose the result to release its file lock.</summary>
    public static AxolotlPackage Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = System.IO.Path.GetFullPath(path);
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
        Span<byte> magic = stackalloc byte[8];
        ReadExactly(stream, magic, "package magic");
        if (!magic.SequenceEqual(AxolotlPackageFormat.Magic))
            throw new InvalidDataException("The file is not an Axolotl package.");

        Span<byte> header = stackalloc byte[8];
        ReadExactly(stream, header, "package header");
        var version = BinaryPrimitives.ReadUInt16LittleEndian(header);
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(header[2..]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        if (version != AxolotlPackageFormat.CurrentVersion)
            throw new InvalidDataException($"Axolotl package format version {version} is unsupported.");
        if ((flags & ~AxolotlPackageFormat.SignedFlag) != 0)
            throw new InvalidDataException("The package uses unsupported flags.");
        if (count <= 0 || count > AxolotlPackageFormat.MaximumEntries)
            throw new InvalidDataException("The package entry count is invalid.");

        var entries = new List<AxolotlPackageEntry>(count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var entryHeader = new byte[16];
        for (var index = 0; index < count; index++)
        {
            ReadExactly(stream, entryHeader, "entry header");
            var nameLength = BinaryPrimitives.ReadInt32LittleEndian(entryHeader);
            var typeLength = BinaryPrimitives.ReadInt32LittleEndian(entryHeader[4..]);
            var dataLength = BinaryPrimitives.ReadInt64LittleEndian(entryHeader[8..]);
            ValidateLength(nameLength, AxolotlPackageFormat.MaximumStringBytes, "entry name");
            ValidateLength(typeLength, AxolotlPackageFormat.MaximumStringBytes, "entry type");
            if (dataLength < 0 || dataLength > AxolotlPackageFormat.MaximumEntryBytes)
                throw new InvalidDataException("A package entry has an invalid data length.");
            var name = ReadString(stream, nameLength, "entry name");
            var type = ReadString(stream, typeLength, "entry type");
            AxolotlPackageManifest.ValidateEntryName(name, "entry name");
            if (string.IsNullOrWhiteSpace(type) || !names.Add(name))
                throw new InvalidDataException($"Duplicate or malformed package entry '{name}'.");
            var offset = stream.Position;
            if (dataLength > stream.Length - offset)
                throw new EndOfStreamException($"Package entry '{name}' is truncated.");
            entries.Add(new(name, type, dataLength, offset));
            stream.Position += dataLength;
        }

        var signedLength = stream.Position;
        AxolotlPackageSignature? signature = null;
        if ((flags & AxolotlPackageFormat.SignedFlag) != 0)
            signature = ReadSignature(stream, signedLength);
        if (stream.Position != stream.Length)
            throw new InvalidDataException("The package has unexpected trailing data.");

        var manifestEntries = entries.Where(entry => entry.Name == AxolotlPackageFormat.ManifestEntryName).ToArray();
        if (manifestEntries.Length != 1 || manifestEntries[0].Length > AxolotlPackageFormat.MaximumManifestBytes ||
            manifestEntries[0].ContentType != AxolotlPackageFormat.ManifestContentType)
            throw new InvalidDataException("The package must contain exactly one valid manifest entry.");
        stream.Position = manifestEntries[0].Offset;
        var manifestBytes = new byte[checked((int)manifestEntries[0].Length)];
        ReadExactly(stream, manifestBytes, "package manifest");
        AxolotlPackageManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AxolotlPackageManifest>(manifestBytes, JsonOptions)
                ?? throw new InvalidDataException("The package manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The package manifest is malformed.", exception);
        }
        manifest.Validate();
        if (signature is not null && !string.Equals(manifest.SignerId, signature.SignerId, StringComparison.Ordinal))
            throw new InvalidDataException("The manifest signer ID does not match the signature block.");
        if (!entries.Any(entry => entry.Name == manifest.Assembly && entry.ContentType == AxolotlPackageFormat.AssemblyContentType))
            throw new InvalidDataException("The package module assembly is missing or has the wrong type.");
        foreach (var asset in manifest.Assets)
            if (!entries.Any(entry => entry.Name == asset.Entry))
                throw new InvalidDataException($"Manifest asset entry '{asset.Entry}' is missing.");
        return new(fullPath, entries, manifest, signature, stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static AxolotlPackageSignature ReadSignature(Stream stream, long signedLength)
    {
        Span<byte> magic = stackalloc byte[8];
        ReadExactly(stream, magic, "signature magic");
        if (!magic.SequenceEqual(AxolotlPackageFormat.SignatureMagic))
            throw new InvalidDataException("The package signature block is malformed.");
        Span<byte> header = stackalloc byte[10];
        ReadExactly(stream, header, "signature header");
        var algorithm = BinaryPrimitives.ReadUInt16LittleEndian(header);
        var signerLength = BinaryPrimitives.ReadInt32LittleEndian(header[2..]);
        var signatureLength = BinaryPrimitives.ReadInt32LittleEndian(header[6..]);
        if (algorithm != AxolotlPackageFormat.EcdsaP256Sha256)
            throw new InvalidDataException("The package signature algorithm is unsupported.");
        ValidateLength(signerLength, AxolotlPackageFormat.MaximumStringBytes, "signer ID");
        ValidateLength(signatureLength, AxolotlPackageFormat.MaximumSignatureBytes, "signature");
        if (signatureLength != 64)
            throw new InvalidDataException("The ECDSA P-256 signature length is invalid.");
        var signer = ReadString(stream, signerLength, "signer ID");
        if (string.IsNullOrWhiteSpace(signer))
            throw new InvalidDataException("The signature signer ID is empty.");
        var value = new byte[signatureLength];
        ReadExactly(stream, value, "signature");
        return new(signer, algorithm, signedLength, value);
    }

    private static string ReadString(Stream stream, int length, string description)
    {
        var bytes = new byte[length];
        ReadExactly(stream, bytes, description);
        try { return StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException exception) { throw new InvalidDataException($"The {description} is not valid UTF-8.", exception); }
    }

    private static void ValidateLength(int length, int maximum, string description)
    {
        if (length < 0 || length > maximum)
            throw new InvalidDataException($"The {description} length is invalid.");
    }

    internal static void ReadExactly(Stream stream, Span<byte> buffer, string description)
    {
        try { stream.ReadExactly(buffer); }
        catch (EndOfStreamException exception) { throw new EndOfStreamException($"The {description} is truncated.", exception); }
    }
}

internal sealed class BoundedReadStream : Stream
{
    private readonly Stream inner;
    private readonly long start;
    private readonly long length;
    private readonly bool leaveOpen;
    private long remaining;

    public BoundedReadStream(Stream inner, long length, bool leaveOpen)
    {
        this.inner = inner;
        start = inner.CanSeek ? inner.Position : 0;
        this.length = length;
        this.leaveOpen = leaveOpen;
        remaining = length;
    }
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => length - remaining; set => Seek(value, SeekOrigin.Begin); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        if (remaining == 0) return 0;
        var read = inner.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
        remaining -= read;
        return read;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (remaining == 0) return 0;
        var read = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], cancellationToken).ConfigureAwait(false);
        remaining -= read;
        return read;
    }
    protected override void Dispose(bool disposing) { if (disposing && !leaveOpen) inner.Dispose(); base.Dispose(disposing); }
    public override ValueTask DisposeAsync() => leaveOpen ? ValueTask.CompletedTask : inner.DisposeAsync();
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!CanSeek) throw new NotSupportedException();
        var position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(Position + offset),
            SeekOrigin.End => checked(length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (position < 0 || position > length)
            throw new IOException("Cannot seek outside the bounded stream.");
        inner.Position = checked(start + position);
        remaining = length - position;
        return position;
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
