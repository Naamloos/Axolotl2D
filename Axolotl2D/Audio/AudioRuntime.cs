namespace Axolotl2D.Audio;

internal sealed class AudioRuntime
{
    private AudioPlayer? player;

    public void Attach(AudioPlayer value) => player = value;

    public void Detach(AudioPlayer value)
    {
        if (ReferenceEquals(player, value))
            player = null;
    }

    public void Update() => player?.Update();
}
