using Silk.NET.OpenAL;

namespace Axolotl2D.Audio
{
    /// <summary>
    /// Represents the audio player for the game.
    /// </summary>
    public unsafe class AudioPlayer : IDisposable
    {
        private readonly ALContext alContext;
        private readonly AL openAL;
        private readonly Device* devicePointer;
        private readonly Context* contextPointer;

        /// <summary>
        /// Creates a new instance of the audio player.
        /// </summary>
        /// <exception cref="Exception">Something went wrong initializing the audio player.</exception>
        public AudioPlayer()
        {
            alContext = ALContext.GetApi(true);
            openAL = AL.GetApi();
            devicePointer = alContext.OpenDevice("");
            if (devicePointer == null)
            {
                throw new Exception("Could not create device");
            }

            contextPointer = alContext.CreateContext(devicePointer, null);
            alContext.MakeContextCurrent(contextPointer);

            openAL.GetError();
        }

        /// <summary>
        /// Disposes of the audio player.
        /// </summary>
        public void Dispose()
        {
            alContext.DestroyContext(contextPointer);
            alContext.CloseDevice(devicePointer);
        }

        /// <summary>
        /// Loads a song from a stream.
        /// </summary>
        /// <param name="songStream">Stream to load song from. This MUST be a .WAV file!!</param>
        /// <returns>The loaded song</returns>
        public Song LoadSong(Stream songStream)
        {
            return new Song(songStream, openAL, alContext);
        }
    }
}
