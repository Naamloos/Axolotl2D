using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenAL;

namespace Axolotl2D.Audio
{
    /// <summary>
    /// Represents the audio player for the game.
    /// </summary>
    public unsafe class AudioPlayer : IDisposable
    {
        private readonly ALContext _alContext;
        private readonly AL _al;
        private readonly Device* _device;
        private readonly Context* _context;

        /// <summary>
        /// Creates a new instance of the audio player.
        /// </summary>
        /// <exception cref="Exception">Something went wrong initializing the audio player.</exception>
        public AudioPlayer()
        {
            _alContext = ALContext.GetApi(true);
            _al = AL.GetApi();
            _device = _alContext.OpenDevice("");
            if (_device == null)
            {
                throw new Exception("Could not create device");
            }

            _context = _alContext.CreateContext(_device, null);
            _alContext.MakeContextCurrent(_context);

            _al.GetError();
        }

        /// <summary>
        /// Disposes of the audio player.
        /// </summary>
        public void Dispose()
        {
            _alContext.DestroyContext(_context);
            _alContext.CloseDevice(_device);
        }

        /// <summary>
        /// Loads a song from a stream.
        /// </summary>
        /// <param name="songStream">Stream to load song from. This MUST be a .WAV file!!</param>
        /// <returns>The loaded song</returns>
        public Song LoadSong(Stream songStream)
        {
            return new Song(songStream, _al, _alContext);
        }
    }
}
