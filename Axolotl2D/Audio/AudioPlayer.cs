using Axolotl2D.Assets;
using Silk.NET.OpenAL;

namespace Axolotl2D.Audio;

/// <summary>Plays shared SoundAssets through an OpenAL device.</summary>
public sealed unsafe class AudioPlayer : IDisposable
{
    private readonly ALContext contextApi = ALContext.GetApi(true);
    private readonly AL openAL = AL.GetApi();
    private readonly Dictionary<SoundAsset, uint> buffers = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<SoundPlayback> playbacks = [];
    private readonly Device* device;
    private readonly Context* context;
    private bool disposed;

    public AudioPlayer()
    {
        device = contextApi.OpenDevice("");
        if (device is null)
            throw new InvalidOperationException("OpenAL could not open an audio device.");
        context = contextApi.CreateContext(device, null);
        if (context is null)
        {
            contextApi.CloseDevice(device);
            throw new InvalidOperationException("OpenAL could not create an audio context.");
        }
        contextApi.MakeContextCurrent(context);
    }

    public SoundPlayback Play(SoundAsset asset, bool loop = false, float volume = 1f)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(asset);

        var source = openAL.GenSource();
        openAL.SetSourceProperty(source, SourceInteger.Buffer, GetBuffer(asset));
        openAL.SetSourceProperty(source, SourceBoolean.Looping, loop);
        openAL.SetSourceProperty(source, SourceFloat.Gain, Math.Max(0f, volume));
        var playback = new SoundPlayback(openAL, source, RemovePlayback);
        playbacks.Add(playback);
        playback.Play();
        return playback;
    }

    private uint GetBuffer(SoundAsset asset)
    {
        if (buffers.TryGetValue(asset, out var existing))
            return existing;

        var format = (asset.Channels, asset.BitsPerSample) switch
        {
            (1, 8) => BufferFormat.Mono8,
            (1, 16) => BufferFormat.Mono16,
            (2, 8) => BufferFormat.Stereo8,
            (2, 16) => BufferFormat.Stereo16,
            _ => throw new NotSupportedException("OpenAL supports mono/stereo 8-bit or 16-bit PCM audio.")
        };

        var buffer = openAL.GenBuffer();
        var samples = asset.Samples.Span;
        fixed (byte* pointer = samples)
            openAL.BufferData(buffer, format, pointer, samples.Length, asset.SampleRate);
        buffers.Add(asset, buffer);
        return buffer;
    }

    private void RemovePlayback(SoundPlayback playback) => playbacks.Remove(playback);

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        foreach (var playback in playbacks.ToArray())
            playback.Dispose();
        foreach (var buffer in buffers.Values)
            openAL.DeleteBuffer(buffer);
        buffers.Clear();
        contextApi.MakeContextCurrent(null);
        contextApi.DestroyContext(context);
        contextApi.CloseDevice(device);
        openAL.Dispose();
        contextApi.Dispose();
    }
}

/// <summary>A controllable playback source for one sound.</summary>
public sealed class SoundPlayback : IDisposable
{
    private readonly AL openAL;
    private readonly uint source;
    private readonly Action<SoundPlayback> onDisposed;
    private bool disposed;

    internal SoundPlayback(AL openAL, uint source, Action<SoundPlayback> onDisposed)
    {
        this.openAL = openAL;
        this.source = source;
        this.onDisposed = onDisposed;
    }

    public void Play() => openAL.SourcePlay(source);
    public void Pause() => openAL.SourcePause(source);
    public void Stop() => openAL.SourceStop(source);

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Stop();
        openAL.DeleteSource(source);
        onDisposed(this);
    }
}
