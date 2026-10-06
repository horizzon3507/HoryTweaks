using System.Buffers.Binary;

namespace HoryTweaks.Core.Audio;

/// <summary>
/// Pure decoder for RIFF/WAVE audio. Only uncompressed little-endian PCM16 is supported.
/// </summary>
internal static class WavDecoder
{
    /// <summary>
    /// Decoded PCM audio payload.
    /// </summary>
    /// <param name="Channels">Channel count from the fmt chunk.</param>
    /// <param name="SampleRate">Sample rate in Hz from the fmt chunk.</param>
    /// <param name="Samples">Interleaved 16-bit PCM samples, little-endian.</param>
    internal readonly record struct PcmAudio(int Channels, int SampleRate, short[] Samples);

    /// <summary>
    /// Tries to decode a RIFF/WAVE PCM16 buffer.
    /// </summary>
    /// <param name="wav">The raw file bytes.</param>
    /// <param name="audio">The decoded audio, or default when parsing fails.</param>
    /// <returns>True when the buffer is a well-formed PCM16 WAV.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> wav, out PcmAudio audio)
    {
        audio = default;

        // RIFF header: "RIFF" + size + "WAVE"
        if (wav.Length < 12 ||
            !wav[..4].SequenceEqual("RIFF"u8) ||
            !wav.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return false;
        }

        int riffSize = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(4, 4));
        if (riffSize < 4)
        {
            return false;
        }

        int bodyLength = Math.Min(wav.Length - 8, riffSize);
        ReadOnlySpan<byte> body = wav.Slice(8, bodyLength);

        ushort audioFormat = 0;
        int channels = 0;
        int sampleRate = 0;
        int blockAlign = 0;
        int bitsPerSample = 0;
        bool haveFmt = false;
        ReadOnlySpan<byte> data = default;
        bool haveData = false;

        int offset = 4; // skip "WAVE"
        while (offset + 8 <= body.Length)
        {
            ReadOnlySpan<byte> id = body.Slice(offset, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(offset + 4, 4));
            int chunkStart = offset + 8;
            if (size < 0 || size > body.Length - chunkStart)
            {
                return false; // truncated chunk
            }

            if (id.SequenceEqual("fmt "u8))
            {
                if (size < 16)
                {
                    return false;
                }

                ReadOnlySpan<byte> fmt = body.Slice(chunkStart, size);
                audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(fmt.Slice(4, 4));
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(12, 2));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(14, 2));
                haveFmt = true;
            }
            else if (id.SequenceEqual("data"u8) && !haveData)
            {
                data = body.Slice(chunkStart, size);
                haveData = true;
            }

            // Chunks are word aligned: odd sizes carry one pad byte.
            offset = chunkStart + size + (size & 1);
        }

        if (!haveFmt || !haveData)
        {
            return false;
        }

        if (audioFormat != 1 || bitsPerSample != 16 || channels <= 0 || sampleRate <= 0)
        {
            return false;
        }

        // Data must consist of whole sample frames.
        if (blockAlign != channels * 2 || data.Length % blockAlign != 0)
        {
            return false;
        }

        int sampleCount = data.Length / 2;
        short[] samples = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 2, 2));
        }

        audio = new PcmAudio(channels, sampleRate, samples);
        return true;
    }
}
