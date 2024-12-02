using System.Buffers.Binary;
using System.Text;
using Silk.NET.OpenAL;

namespace Axolotl2D.Audio
{
    /// <summary>
    /// Represents a music track that can be played in the game.
    /// </summary>
    public class Song
    {
        private readonly uint songBufferPointer;
        private readonly uint sourcePointer;

        private readonly ALContext alContext;
        private readonly AL openAL;

        internal unsafe Song(Stream songStream, AL _al, ALContext _alContext)
        {
            this.openAL = _al;
            this.alContext = _alContext;

            ReadOnlySpan<byte> file = ReadStream(songStream);
            int index = 0;
            if (file[index++] != 'R' || file[index++] != 'I' || file[index++] != 'F' || file[index++] != 'F')
            {
                throw new Exception("Given file is not in RIFF format");
            }

            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
            index += 4;

            if (file[index++] != 'W' || file[index++] != 'A' || file[index++] != 'V' || file[index++] != 'E')
            {
                throw new Exception("Given file is not in WAVE format");
            }

            short numChannels = -1;
            int sampleRate = -1;
            int byteRate = -1;
            short blockAlign = -1;
            short bitsPerSample = -1;
            BufferFormat format = 0;

            sourcePointer = _al.GenSource();
            songBufferPointer = _al.GenBuffer();
            _al.SetSourceProperty(sourcePointer, SourceBoolean.Looping, true);

            while (index + 4 < file.Length)
            {
                var identifier = "" + (char)file[index++] + (char)file[index++] + (char)file[index++] + (char)file[index++];
                var size = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                index += 4;
                if (identifier == "fmt ")
                {
                    if (size != 16)
                    {
                        Console.WriteLine($"Unknown Audio Format with subchunk1 size {size}");
                    }
                    else
                    {
                        var audioFormat = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                        index += 2;
                        if (audioFormat != 1)
                        {
                            Console.WriteLine($"Unknown Audio Format with ID {audioFormat}");
                        }
                        else
                        {
                            numChannels = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                            index += 2;
                            sampleRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                            index += 4;
                            byteRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                            index += 4;
                            blockAlign = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                            index += 2;
                            bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                            index += 2;

                            if (numChannels == 1)
                            {
                                if (bitsPerSample == 8)
                                    format = BufferFormat.Mono8;
                                else if (bitsPerSample == 16)
                                    format = BufferFormat.Mono16;
                                else
                                {
                                    Console.WriteLine($"Can't Play mono {bitsPerSample} sound.");
                                }
                            }
                            else if (numChannels == 2)
                            {
                                if (bitsPerSample == 8)
                                    format = BufferFormat.Stereo8;
                                else if (bitsPerSample == 16)
                                    format = BufferFormat.Stereo16;
                                else
                                {
                                    Console.WriteLine($"Can't Play stereo {bitsPerSample} sound.");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Can't play audio with {numChannels} sound");
                            }
                        }
                    }
                }
                else if (identifier == "data")
                {
                    var data = file.Slice(index, size);
                    index += size;

                    fixed (byte* pData = data)
                        _al.BufferData(songBufferPointer, format, pData, size, sampleRate);
                    Console.WriteLine($"Read {size} bytes Data");
                }
                else if (identifier == "JUNK")
                {
                    // this exists to align things
                    index += size;
                }
                else if (identifier == "iXML")
                {
                    var v = file.Slice(index, size);
                    var str = Encoding.ASCII.GetString(v);
                    Console.WriteLine($"iXML Chunk: {str}");
                    index += size;
                }
                else
                {
                    Console.WriteLine($"Unknown Section: {identifier}");
                    index += size;
                }
            }
        }

        private byte[] ReadStream(Stream stream)
        {
            using MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Starts playing the song.
        /// </summary>
        public void Play()
        {
            openAL.SetSourceProperty(sourcePointer, SourceInteger.Buffer, songBufferPointer);
            openAL.SourcePlay(sourcePointer);
        }

        /// <summary>
        /// Stops playing the song.
        /// </summary>
        public void Stop()
        {
            openAL.SourceStop(sourcePointer);

            openAL.DeleteSource(sourcePointer);
            openAL.DeleteBuffer(songBufferPointer);
        }
    }
}
