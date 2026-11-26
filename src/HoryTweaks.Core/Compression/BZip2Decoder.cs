namespace HoryTweaks.Core.Compression;

/// <summary>
/// Pure-C# BZip2 stream decompressor (with block &amp; stream CRC-32 validation and output size cap)
/// used to unpack Cisco's official <c>openh264-*.dll.bz2</c> binary without external dependencies.
/// </summary>
internal static class BZip2Decoder
{
    private const int MaxAlphaSize = 258;
    private const int MaxCodeLen = 20;
    private const int MaxGroups = 6;
    private const int GroupSize = 50;
    private const int MaxSelectors = 2 + (900_000 / GroupSize);

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    internal static byte[] Decompress(byte[] compressed, int maxOutputBytes = 8 * 1024 * 1024)
    {
        if (compressed == null)
            throw new ArgumentNullException(nameof(compressed));

        using var input = new MemoryStream(compressed, writable: false);
        using var output = new MemoryStream();
        Decompress(input, output, maxOutputBytes);
        return output.ToArray();
    }

    internal static void Decompress(Stream input, Stream output, int maxOutputBytes = 8 * 1024 * 1024)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        var reader = new BitReader(input);
        int b0 = reader.ReadBits(8);
        int b1 = reader.ReadBits(8);
        int b2 = reader.ReadBits(8);
        int levelChar = reader.ReadBits(8);
        if (b0 != 'B' || b1 != 'Z' || b2 != 'h' || levelChar < '1' || levelChar > '9')
            throw new InvalidDataException("Invalid BZip2 stream header.");

        int blockSize100k = levelChar - '0';
        int maxBlockSize = blockSize100k * 100_000;
        byte[] ttData = new byte[maxBlockSize];
        int[] ttNext = new int[maxBlockSize];
        uint combinedCrc = 0;
        long totalWritten = 0;

        while (true)
        {
            ulong magic = ((ulong)reader.ReadBits(24) << 24) | (uint)reader.ReadBits(24);
            if (magic == 0x177245385090UL)
            {
                uint storedCombinedCrc = reader.ReadUInt32();
                if (storedCombinedCrc != combinedCrc)
                    throw new InvalidDataException($"BZip2 stream CRC mismatch (expected 0x{storedCombinedCrc:X8}, got 0x{combinedCrc:X8}).");

                break;
            }

            if (magic != 0x314159265359UL)
                throw new InvalidDataException($"Invalid BZip2 block magic 0x{magic:X12}.");

            uint storedBlockCrc = reader.ReadUInt32();
            bool randomized = reader.ReadBits(1) != 0;
            if (randomized)
                throw new NotSupportedException("Randomized BZip2 blocks are not supported.");

            int origPtr = reader.ReadBits(24);
            if (origPtr < 0 || origPtr >= maxBlockSize)
                throw new InvalidDataException("Invalid BZip2 origin pointer.");

            // Symbol bitmap (16 groups of 16 bytes)
            byte[] seqToUnseq = new byte[256];
            int nInUse = 0;
            int inUse16 = reader.ReadBits(16);
            for (int i = 0; i < 16; i++)
            {
                if ((inUse16 & (1 << (15 - i))) == 0)
                    continue;

                int bits = reader.ReadBits(16);
                for (int j = 0; j < 16; j++)
                {
                    if ((bits & (1 << (15 - j))) != 0)
                    {
                        seqToUnseq[nInUse++] = (byte)(i * 16 + j);
                    }
                }
            }

            if (nInUse == 0)
                throw new InvalidDataException("BZip2 block has no symbols in use.");

            int alphaSize = nInUse + 2;
            int eob = nInUse + 1;

            int nGroups = reader.ReadBits(3);
            if (nGroups < 2 || nGroups > MaxGroups)
                throw new InvalidDataException("Invalid BZip2 Huffman group count.");

            int nSelectors = reader.ReadBits(15);
            if (nSelectors < 1 || nSelectors > MaxSelectors)
                throw new InvalidDataException("Invalid BZip2 selector count.");

            byte[] selectorMtf = new byte[nSelectors];
            for (int i = 0; i < nSelectors; i++)
            {
                int j = 0;
                while (reader.ReadBits(1) != 0)
                {
                    j++;
                    if (j >= nGroups)
                        throw new InvalidDataException("Invalid BZip2 selector unary code.");
                }
                selectorMtf[i] = (byte)j;
            }

            // Undo MTF on selectors
            byte[] pos = new byte[MaxGroups];
            for (byte v = 0; v < nGroups; v++)
                pos[v] = v;

            byte[] selectors = new byte[nSelectors];
            for (int i = 0; i < nSelectors; i++)
            {
                byte v = selectorMtf[i];
                byte tmp = pos[v];
                while (v > 0)
                {
                    pos[v] = pos[v - 1];
                    v--;
                }
                pos[0] = tmp;
                selectors[i] = tmp;
            }

            // Read Huffman code lengths and build canonical decode tables
            var tables = new HuffmanTable[nGroups];
            for (int t = 0; t < nGroups; t++)
            {
                int[] lengths = new int[alphaSize];
                int curr = reader.ReadBits(5);
                for (int i = 0; i < alphaSize; i++)
                {
                    while (true)
                    {
                        if (curr < 1 || curr > MaxCodeLen)
                            throw new InvalidDataException("Invalid BZip2 Huffman code length.");

                        if (reader.ReadBits(1) == 0)
                            break;

                        curr += reader.ReadBits(1) != 0 ? -1 : 1;
                    }
                    lengths[i] = curr;
                }
                tables[t] = HuffmanTable.Build(lengths, alphaSize);
            }

            // Decode MTF + RUNA/RUNB symbols into ttData
            byte[] mtf = new byte[256];
            for (int i = 0; i < nInUse; i++)
                mtf[i] = seqToUnseq[i];

            int[] cftab = new int[256];
            int blockLen = 0;
            int groupPos = 0;
            int groupNo = -1;
            HuffmanTable? currentTable = null;

            int NextSymbol()
            {
                if (groupPos == 0)
                {
                    groupNo++;
                    if (groupNo >= nSelectors)
                        throw new InvalidDataException("BZip2 selector index out of range.");
                    groupPos = GroupSize;
                    currentTable = tables[selectors[groupNo]];
                }
                groupPos--;
                return currentTable!.Decode(reader);
            }

            int nextSym = NextSymbol();
            while (nextSym != eob)
            {
                if (nextSym == 0 || nextSym == 1)
                {
                    int runLen = 0;
                    int bitPos = 0;
                    while (nextSym == 0 || nextSym == 1)
                    {
                        if (bitPos >= 24)
                            throw new InvalidDataException("BZip2 run length overflow.");

                        runLen += (nextSym + 1) << bitPos;
                        bitPos++;
                        nextSym = NextSymbol();
                    }

                    byte ch = mtf[0];
                    if (blockLen + runLen > maxBlockSize)
                        throw new InvalidDataException("BZip2 block exceeds declared size.");

                    cftab[ch] += runLen;
                    for (int r = 0; r < runLen; r++)
                    {
                        ttData[blockLen++] = ch;
                    }
                }
                else
                {
                    int idx = nextSym - 1;
                    if (idx >= nInUse)
                        throw new InvalidDataException("BZip2 MTF symbol out of range.");

                    byte ch = mtf[idx];
                    for (int k = idx; k > 0; k--)
                        mtf[k] = mtf[k - 1];
                    mtf[0] = ch;

                    if (blockLen >= maxBlockSize)
                        throw new InvalidDataException("BZip2 block exceeds declared size.");

                    cftab[ch]++;
                    ttData[blockLen++] = ch;
                    nextSym = NextSymbol();
                }
            }

            if (origPtr >= blockLen)
                throw new InvalidDataException("BZip2 origin pointer exceeds decoded block length.");

            // Inverse Burrows-Wheeler Transform
            int sum = 0;
            for (int i = 0; i < 256; i++)
            {
                int count = cftab[i];
                cftab[i] = sum;
                sum += count;
            }

            for (int i = 0; i < blockLen; i++)
            {
                byte ch = ttData[i];
                ttNext[cftab[ch]++] = i;
            }

            // Decode RLE1 and compute block CRC-32
            uint blockCrc = 0xFFFFFFFFu;
            int tPos = ttNext[origPtr];
            int remaining = blockLen;
            int prevByte = -1;
            int runCount = 0;

            void EmitByte(byte b)
            {
                totalWritten++;
                if (totalWritten > maxOutputBytes)
                    throw new InvalidDataException("BZip2 decompressed output exceeds size limit.");

                output.WriteByte(b);
                blockCrc = (blockCrc << 8) ^ Crc32Table[((blockCrc >> 24) ^ b) & 0xFF];
            }

            while (remaining > 0)
            {
                byte currByte = ttData[tPos];
                tPos = ttNext[tPos];
                remaining--;

                if (runCount == 4)
                {
                    for (int r = 0; r < currByte; r++)
                    {
                        EmitByte((byte)prevByte);
                    }
                    prevByte = -1;
                    runCount = 0;
                    continue;
                }

                EmitByte(currByte);
                if (currByte == prevByte)
                {
                    runCount++;
                }
                else
                {
                    prevByte = currByte;
                    runCount = 1;
                }
            }

            uint computedBlockCrc = ~blockCrc;
            if (computedBlockCrc != storedBlockCrc)
                throw new InvalidDataException($"BZip2 block CRC mismatch (expected 0x{storedBlockCrc:X8}, got 0x{computedBlockCrc:X8}).");

            combinedCrc = ((combinedCrc << 1) | (combinedCrc >> 31)) ^ computedBlockCrc;
        }
    }

    private static uint[] BuildCrc32Table()
    {
        // BZip2 uses MSB-first CRC-32 polynomial 0x04C11DB7.
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i << 24;
            for (int bit = 0; bit < 8; bit++)
            {
                c = (c & 0x80000000u) != 0 ? (c << 1) ^ 0x04C11DB7u : (c << 1);
            }
            table[i] = c;
        }
        return table;
    }

    private sealed class BitReader(Stream stream)
    {
        private readonly Stream _stream = stream;
        private uint _buffer;
        private int _bits;

        internal int ReadBits(int count)
        {
            while (_bits < count)
            {
                int next = _stream.ReadByte();
                if (next < 0)
                    throw new EndOfStreamException("Unexpected end of BZip2 stream.");

                _buffer = (_buffer << 8) | (uint)next;
                _bits += 8;
            }

            _bits -= count;
            return (int)((_buffer >> _bits) & ((1u << count) - 1u));
        }

        internal uint ReadUInt32()
        {
            uint hi = (uint)ReadBits(16);
            uint lo = (uint)ReadBits(16);
            return (hi << 16) | lo;
        }
    }

    private sealed class HuffmanTable
    {
        private readonly int _minLen;
        private readonly int _maxLen;
        private readonly int[] _limit;
        private readonly int[] _base;
        private readonly int[] _perm;

        private HuffmanTable(int minLen, int maxLen, int[] limit, int[] baseArr, int[] perm)
        {
            _minLen = minLen;
            _maxLen = maxLen;
            _limit = limit;
            _base = baseArr;
            _perm = perm;
        }

        internal static HuffmanTable Build(int[] lengths, int alphaSize)
        {
            int minLen = MaxCodeLen;
            int maxLen = 0;
            for (int i = 0; i < alphaSize; i++)
            {
                if (lengths[i] < minLen) minLen = lengths[i];
                if (lengths[i] > maxLen) maxLen = lengths[i];
            }

            int[] perm = new int[MaxAlphaSize];
            int pp = 0;
            for (int len = minLen; len <= maxLen; len++)
            {
                for (int sym = 0; sym < alphaSize; sym++)
                {
                    if (lengths[sym] == len)
                        perm[pp++] = sym;
                }
            }

            int[] baseArr = new int[MaxCodeLen + 2];
            int[] limit = new int[MaxCodeLen + 2];

            for (int i = 0; i < alphaSize; i++)
                baseArr[lengths[i] + 1]++;
            for (int i = 1; i <= MaxCodeLen + 1; i++)
                baseArr[i] += baseArr[i - 1];

            int vec = 0;
            for (int i = minLen; i <= maxLen; i++)
            {
                vec += baseArr[i + 1] - baseArr[i];
                limit[i] = vec - 1;
                vec <<= 1;
            }

            for (int i = minLen + 1; i <= maxLen; i++)
            {
                baseArr[i] = ((limit[i - 1] + 1) << 1) - baseArr[i];
            }

            return new HuffmanTable(minLen, maxLen, limit, baseArr, perm);
        }

        internal int Decode(BitReader reader)
        {
            int len = _minLen;
            int vec = reader.ReadBits(len);
            while (vec > _limit[len])
            {
                len++;
                if (len > _maxLen)
                    throw new InvalidDataException("Invalid BZip2 Huffman code.");

                vec = (vec << 1) | reader.ReadBits(1);
            }

            int index = vec - _base[len];
            if ((uint)index >= (uint)_perm.Length)
                throw new InvalidDataException("BZip2 Huffman symbol index out of range.");

            return _perm[index];
        }
    }
}
