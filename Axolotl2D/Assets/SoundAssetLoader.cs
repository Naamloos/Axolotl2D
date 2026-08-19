using System.Buffers.Binary;

namespace Axolotl2D.Assets;

/// <summary>Uncompressed PCM audio ready for OpenAL playback.</summary>
public sealed class SoundAsset(byte[] samples, int sampleRate, short channels, short bitsPerSample)
{
    public ReadOnlyMemory<byte> Samples { get; } = samples;
    public int SampleRate { get; } = sampleRate;
    public short Channels { get; } = channels;
    public short BitsPerSample { get; } = bitsPerSample;
}

/// <summary>Loads RIFF/WAVE PCM audio.</summary>
public sealed class SoundAssetLoader : IAssetLoader<SoundAsset>
{
    /// <inheritdoc />
    public async ValueTask<SoundAsset> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        var file = memory.ToArray().AsSpan();

        if (file.Length < 12 || !file[..4].SequenceEqual("RIFF"u8) || !file.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Sound assets must be RIFF/WAVE files.");

        short channels = 0;
        short bitsPerSample = 0;
        int sampleRate = 0;
        byte[]? samples = null;

        for (var index = 12; index + 8 <= file.Length;)
        {
            var id = file.Slice(index, 4);
            var length = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index + 4, 4));
            index += 8;
            if (length < 0 || index + length > file.Length)
                throw new InvalidDataException("The WAVE file contains an invalid chunk length.");

            if (id.SequenceEqual("fmt "u8))
            {
                if (length < 16 || BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2)) != 1)
                    throw new NotSupportedException("Only uncompressed PCM WAVE audio is supported.");
                channels = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index + 4, 4));
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                samples = file.Slice(index, length).ToArray();
            }

            index += length + (length & 1);
        }

        if (samples is null || sampleRate <= 0 || channels is not (1 or 2) || bitsPerSample is not (8 or 16))
            throw new NotSupportedException("The WAVE file must contain mono/stereo 8-bit or 16-bit PCM data.");

        return new SoundAsset(samples, sampleRate, channels, bitsPerSample);
    }
}
