namespace GT1CarViewer.Core;

public static class GtZip
{
    public static byte[] Decompress(ReadOnlySpan<byte> source, int expectedSize)
    {
        if (expectedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize));
        }

        var output = new byte[expectedSize];
        int sourceOffset = 0;
        int outputOffset = 0;

        while (outputOffset < expectedSize)
        {
            if (sourceOffset >= source.Length)
            {
                throw new InvalidDataException("GT-ZIP stream ended before its declared output size.");
            }

            byte flags = source[sourceOffset++];
            for (int bit = 0; bit < 8 && outputOffset < expectedSize; bit++, flags >>= 1)
            {
                if ((flags & 1) == 0)
                {
                    if (sourceOffset >= source.Length)
                    {
                        throw new InvalidDataException("GT-ZIP literal is truncated.");
                    }

                    output[outputOffset++] = source[sourceOffset++];
                    continue;
                }

                if (source.Length - sourceOffset < 2)
                {
                    throw new InvalidDataException("GT-ZIP back reference is truncated.");
                }

                int length = source[sourceOffset++] + 3;
                int distance = source[sourceOffset++];
                if (distance >= 0x80)
                {
                    if (sourceOffset >= source.Length)
                    {
                        throw new InvalidDataException("GT-ZIP extended back reference is truncated.");
                    }

                    distance = ((distance - 0x80) << 8) | source[sourceOffset++];
                }
                distance++;

                for (int i = 0; i < length && outputOffset < expectedSize; i++)
                {
                    output[outputOffset] = distance <= outputOffset
                        ? output[outputOffset - distance]
                        : (byte)0;
                    outputOffset++;
                }
            }
        }

        return output;
    }
}
