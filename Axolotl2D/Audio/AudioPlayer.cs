using Axolotl2D.Assets;
using Silk.NET.OpenAL;
using System.Numerics;

namespace Axolotl2D.Audio;

/// <summary>Plays shared SoundAssets through an OpenAL device.</summary>
public sealed unsafe class AudioPlayer : IDisposable
{
    private readonly ALContext contextApi = ALContext.GetApi(true);
    private readonly AL openAL = AL.GetApi();
    private readonly Dictionary<SoundAsset, uint> buffers = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<SoundPlayback> playbacks = [];
    private readonly HashSet<SoundPlayback> pausedByPlayer = [];
    private readonly AudioRuntime? runtime;
    private readonly Device* device;
    private readonly Context* context;
    private Vector2 listenerPosition;
    private Vector2 listenerVelocity;
    private float masterVolume = 1f;
    private bool muted;
    private bool disposed;

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            ValidateNonNegative(value, nameof(MasterVolume));
            masterVolume = value;
            ApplyMasterVolume();
        }
    }

    public bool Muted
    {
        get => muted;
        set
        {
            muted = value;
            ApplyMasterVolume();
        }
    }

    /// <summary>The listener position in game-defined 2D world units.</summary>
    public Vector2 ListenerPosition
    {
        get => listenerPosition;
        set
        {
            ValidateVector(value, nameof(ListenerPosition));
            listenerPosition = value;
            EnsureCurrent();
            openAL.SetListenerProperty(ListenerVector3.Position, value.X, value.Y, 0f);
        }
    }

    public Vector2 ListenerVelocity
    {
        get => listenerVelocity;
        set
        {
            ValidateVector(value, nameof(ListenerVelocity));
            listenerVelocity = value;
            EnsureCurrent();
            openAL.SetListenerProperty(ListenerVector3.Velocity, value.X, value.Y, 0f);
        }
    }

    public AudioPlayer() : this(null) { }

    internal AudioPlayer(AudioRuntime? runtime)
    {
        this.runtime = runtime;
        device = contextApi.OpenDevice("");
        if (device is null)
            throw new InvalidOperationException("OpenAL could not open an audio device.");
        context = contextApi.CreateContext(device, null);
        if (context is null)
        {
            contextApi.CloseDevice(device);
            throw new InvalidOperationException("OpenAL could not create an audio context.");
        }
        EnsureCurrent();
        openAL.DistanceModel(DistanceModel.InverseDistanceClamped);
        runtime?.Attach(this);
    }

    public SoundPlayback Play(SoundAsset asset, bool loop = false, float volume = 1f,
        float pitch = 1f, float pan = 0f)
    {
        ValidatePan(pan);
        return CreatePlayback(asset, loop, volume, pitch, spatial: false, new Vector2(pan, 0f),
            referenceDistance: 1f, maximumDistance: 1f, rolloffFactor: 0f);
    }

    public void PlayOneShot(SoundAsset asset, float volume = 1f, float pitch = 1f, float pan = 0f) =>
        Play(asset, volume: volume, pitch: pitch, pan: pan);

    public SoundPlayback PlaySpatial(SoundAsset asset, Vector2 position, bool loop = false, float volume = 1f,
        float pitch = 1f, float referenceDistance = 100f, float maximumDistance = 1000f,
        float rolloffFactor = 1f)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.Channels != 1)
            throw new NotSupportedException("Spatial playback requires a mono sound asset.");
        ValidateVector(position, nameof(position));
        if (!float.IsFinite(referenceDistance) || referenceDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(referenceDistance));
        if (!float.IsFinite(maximumDistance) || maximumDistance < referenceDistance)
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        ValidateNonNegative(rolloffFactor, nameof(rolloffFactor));
        return CreatePlayback(asset, loop, volume, pitch, spatial: true, position,
            referenceDistance, maximumDistance, rolloffFactor);
    }

    public void PlayOneShotSpatial(SoundAsset asset, Vector2 position, float volume = 1f, float pitch = 1f,
        float referenceDistance = 100f, float maximumDistance = 1000f, float rolloffFactor = 1f) =>
        PlaySpatial(asset, position, volume: volume, pitch: pitch, referenceDistance: referenceDistance,
            maximumDistance: maximumDistance, rolloffFactor: rolloffFactor);

    public void PauseAll()
    {
        foreach (var playback in playbacks.ToArray())
            if (playback.State == SoundPlaybackState.Playing)
            {
                playback.Pause();
                pausedByPlayer.Add(playback);
            }
    }

    public void ResumeAll()
    {
        foreach (var playback in pausedByPlayer.ToArray())
            if (!playback.IsDisposed)
                playback.Play();
        pausedByPlayer.Clear();
    }

    public void StopAll()
    {
        foreach (var playback in playbacks.ToArray())
            playback.Dispose();
        pausedByPlayer.Clear();
    }

    internal void Update()
    {
        if (disposed || playbacks.Count == 0)
            return;
        EnsureCurrent();
        foreach (var playback in playbacks.ToArray())
            playback.Refresh();
    }

    private SoundPlayback CreatePlayback(SoundAsset asset, bool loop, float volume, float pitch,
        bool spatial, Vector2 position, float referenceDistance, float maximumDistance, float rolloffFactor)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(asset);
        ValidateNonNegative(volume, nameof(volume));
        if (!float.IsFinite(pitch) || pitch <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pitch));

        EnsureCurrent();
        var source = openAL.GenSource();
        openAL.SetSourceProperty(source, SourceInteger.Buffer, GetBuffer(asset));
        var playback = new SoundPlayback(openAL, source, EnsureCurrent, RemovePlayback, spatial,
            position, loop, volume, pitch, referenceDistance, maximumDistance, rolloffFactor);
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

    private void ApplyMasterVolume()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        EnsureCurrent();
        openAL.SetListenerProperty(ListenerFloat.Gain, muted ? 0f : masterVolume);
    }

    private void EnsureCurrent()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        contextApi.MakeContextCurrent(context);
    }

    private void RemovePlayback(SoundPlayback playback)
    {
        playbacks.Remove(playback);
        pausedByPlayer.Remove(playback);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        EnsureCurrent();
        foreach (var playback in playbacks.ToArray())
            playback.Dispose();
        foreach (var buffer in buffers.Values)
            openAL.DeleteBuffer(buffer);
        buffers.Clear();
        runtime?.Detach(this);
        contextApi.MakeContextCurrent(null);
        contextApi.DestroyContext(context);
        contextApi.CloseDevice(device);
        disposed = true;
        openAL.Dispose();
        contextApi.Dispose();
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateVector(Vector2 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidatePan(float value)
    {
        if (!float.IsFinite(value) || value < -1f || value > 1f)
            throw new ArgumentOutOfRangeException(nameof(value), "Pan must be between -1 and 1.");
    }
}

/// <summary>A controllable playback source for one sound.</summary>
public sealed class SoundPlayback : IDisposable
{
    private readonly AL openAL;
    private readonly uint source;
    private readonly Action ensureCurrent;
    private readonly Action<SoundPlayback> onDisposed;
    private bool stoppedExplicitly;
    private bool disposed;

    public bool IsSpatial { get; }
    public bool IsDisposed => disposed;

    public SoundPlaybackState State
    {
        get
        {
            if (disposed) return SoundPlaybackState.Disposed;
            ensureCurrent();
            openAL.GetSourceProperty(source, GetSourceInteger.SourceState, out var state);
            return (SourceState)state switch
            {
                SourceState.Initial => SoundPlaybackState.Initial,
                SourceState.Playing => SoundPlaybackState.Playing,
                SourceState.Paused => SoundPlaybackState.Paused,
                _ => SoundPlaybackState.Stopped
            };
        }
    }

    public float Volume
    {
        get => GetFloat(SourceFloat.Gain);
        set
        {
            ValidateNonNegative(value, nameof(Volume));
            SetFloat(SourceFloat.Gain, value);
        }
    }

    public float Pitch
    {
        get => GetFloat(SourceFloat.Pitch);
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(Pitch));
            SetFloat(SourceFloat.Pitch, value);
        }
    }

    public bool Loop
    {
        get => GetBoolean(SourceBoolean.Looping);
        set => SetBoolean(SourceBoolean.Looping, value);
    }

    public float Pan
    {
        get
        {
            EnsureNonSpatial();
            return Position.X;
        }
        set
        {
            EnsureNonSpatial();
            if (!float.IsFinite(value) || value < -1f || value > 1f)
                throw new ArgumentOutOfRangeException(nameof(Pan));
            Position = new Vector2(value, 0f);
        }
    }

    public Vector2 Position
    {
        get
        {
            EnsureAlive();
            ensureCurrent();
            openAL.GetSourceProperty(source, SourceVector3.Position, out Vector3 value);
            return new Vector2(value.X, value.Y);
        }
        set
        {
            EnsureVector(value, nameof(Position));
            EnsureAlive();
            ensureCurrent();
            openAL.SetSourceProperty(source, SourceVector3.Position, value.X, value.Y, 0f);
        }
    }

    public Vector2 Velocity
    {
        get
        {
            EnsureAlive();
            ensureCurrent();
            openAL.GetSourceProperty(source, SourceVector3.Velocity, out Vector3 value);
            return new Vector2(value.X, value.Y);
        }
        set
        {
            EnsureVector(value, nameof(Velocity));
            EnsureAlive();
            ensureCurrent();
            openAL.SetSourceProperty(source, SourceVector3.Velocity, value.X, value.Y, 0f);
        }
    }

    public float ReferenceDistance
    {
        get => GetFloat(SourceFloat.ReferenceDistance);
        set
        {
            EnsureSpatial();
            if (!float.IsFinite(value) || value <= 0f || value > MaximumDistance)
                throw new ArgumentOutOfRangeException(nameof(ReferenceDistance));
            SetFloat(SourceFloat.ReferenceDistance, value);
        }
    }

    public float MaximumDistance
    {
        get => GetFloat(SourceFloat.MaxDistance);
        set
        {
            EnsureSpatial();
            if (!float.IsFinite(value) || value < ReferenceDistance)
                throw new ArgumentOutOfRangeException(nameof(MaximumDistance));
            SetFloat(SourceFloat.MaxDistance, value);
        }
    }

    public float RolloffFactor
    {
        get => GetFloat(SourceFloat.RolloffFactor);
        set
        {
            EnsureSpatial();
            ValidateNonNegative(value, nameof(RolloffFactor));
            SetFloat(SourceFloat.RolloffFactor, value);
        }
    }

    public event Action? Completed;

    internal SoundPlayback(AL openAL, uint source, Action ensureCurrent, Action<SoundPlayback> onDisposed,
        bool spatial, Vector2 position, bool loop, float volume, float pitch,
        float referenceDistance, float maximumDistance, float rolloffFactor)
    {
        this.openAL = openAL;
        this.source = source;
        this.ensureCurrent = ensureCurrent;
        this.onDisposed = onDisposed;
        IsSpatial = spatial;
        SetBoolean(SourceBoolean.SourceRelative, !spatial);
        SetBoolean(SourceBoolean.Looping, loop);
        SetFloat(SourceFloat.Gain, volume);
        SetFloat(SourceFloat.Pitch, pitch);
        Position = position;
        SetFloat(SourceFloat.ReferenceDistance, referenceDistance);
        SetFloat(SourceFloat.MaxDistance, maximumDistance);
        SetFloat(SourceFloat.RolloffFactor, rolloffFactor);
    }

    public void Play()
    {
        EnsureAlive();
        ensureCurrent();
        stoppedExplicitly = false;
        openAL.SourcePlay(source);
    }

    public void Pause()
    {
        EnsureAlive();
        ensureCurrent();
        openAL.SourcePause(source);
    }

    public void Stop()
    {
        EnsureAlive();
        ensureCurrent();
        stoppedExplicitly = true;
        openAL.SourceStop(source);
    }

    internal void Refresh()
    {
        if (disposed || Loop || stoppedExplicitly || State != SoundPlaybackState.Stopped)
            return;
        Completed?.Invoke();
        Dispose();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        ensureCurrent();
        openAL.SourceStop(source);
        openAL.DeleteSource(source);
        disposed = true;
        onDisposed(this);
    }

    private float GetFloat(SourceFloat property)
    {
        EnsureAlive();
        ensureCurrent();
        openAL.GetSourceProperty(source, property, out float value);
        return value;
    }

    private bool GetBoolean(SourceBoolean property)
    {
        EnsureAlive();
        ensureCurrent();
        openAL.GetSourceProperty(source, property, out bool value);
        return value;
    }

    private void SetFloat(SourceFloat property, float value)
    {
        EnsureAlive();
        ensureCurrent();
        openAL.SetSourceProperty(source, property, value);
    }

    private void SetBoolean(SourceBoolean property, bool value)
    {
        EnsureAlive();
        ensureCurrent();
        openAL.SetSourceProperty(source, property, value);
    }

    private void EnsureAlive() => ObjectDisposedException.ThrowIf(disposed, this);

    private void EnsureSpatial()
    {
        EnsureAlive();
        if (!IsSpatial)
            throw new InvalidOperationException("Distance attenuation is only available for spatial playback.");
    }

    private void EnsureNonSpatial()
    {
        EnsureAlive();
        if (IsSpatial)
            throw new InvalidOperationException("Pan is only available for non-spatial playback.");
    }

    private static void EnsureVector(Vector2 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(name);
    }
}

public enum SoundPlaybackState
{
    Initial,
    Playing,
    Paused,
    Stopped,
    Disposed
}
