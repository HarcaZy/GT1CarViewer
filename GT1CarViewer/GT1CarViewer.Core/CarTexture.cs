using System.Buffers.Binary;

namespace GT1CarViewer.Core;

public readonly record struct Rgb32(byte R, byte G, byte B, byte A = 255);

public sealed class CarTexture
{
    public const int TextureWidth = 256;
    public const int TextureHeight = 224;
    public const int PaletteCount = 16;
    public const int MaximumVerticalUvOffset = 32;
    private const int BitmapOffset = 0x1060;
    private const int PaletteOffset = 0x8060;
    private const int PaletteBytesPerColor = 0x200;
    private readonly byte[] _pixels;
    private readonly byte[] _paletteData;

    private CarTexture(int colorCount, byte[] colorIds, byte[] pixels, byte[] paletteData)
    {
        ColorCount = colorCount;
        ColorIds = colorIds;
        _pixels = pixels;
        _paletteData = paletteData;
    }

    public int Width => TextureWidth;
    public int Height => TextureHeight;
    public int ColorCount { get; }
    public IReadOnlyList<byte> ColorIds { get; private init; } = [];

    public static CarTexture Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < PaletteOffset || data[..16].IndexOf("GT-CTEX"u8) < 0)
        {
            throw new InvalidDataException("The selected entry is not a complete GT-CTEX texture.");
        }

        int colorCount = BinaryPrimitives.ReadUInt16LittleEndian(data[0x0E..]);
        if (colorCount is < 1 or > 16)
        {
            colorCount = data[0x0E];
        }

        if (colorCount is < 1 or > 16)
        {
            throw new InvalidDataException($"The GT-CTEX texture declares an invalid color count ({colorCount}).");
        }

        int paletteLength = checked(colorCount * PaletteBytesPerColor);
        if (data.Length < PaletteOffset + paletteLength || data.Length < BitmapOffset + (TextureWidth * TextureHeight / 2))
        {
            throw new InvalidDataException("The GT-CTEX texture is truncated.");
        }

        return new CarTexture(
            colorCount,
            data.Slice(0x10, colorCount).ToArray(),
            data.Slice(BitmapOffset, TextureWidth * TextureHeight / 2).ToArray(),
            data.Slice(PaletteOffset, paletteLength).ToArray());
    }

    public byte GetTexel(int x, int y)
    {
        if ((uint)x >= TextureWidth || (uint)y >= TextureHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Texture coordinates are outside the GT-CTEX bitmap.");
        }

        int pixelIndex = y * TextureWidth + x;
        byte packed = _pixels[pixelIndex / 2];
        return (byte)((pixelIndex & 1) == 0 ? packed & 0x0F : packed >> 4);
    }

    public Rgb32 GetColor(int colorIndex, int paletteIndex, int texelIndex)
    {
        if ((uint)colorIndex >= ColorCount)
        {
            throw new ArgumentOutOfRangeException(nameof(colorIndex));
        }

        if ((uint)paletteIndex >= PaletteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(paletteIndex));
        }

        if ((uint)texelIndex >= 16)
        {
            throw new ArgumentOutOfRangeException(nameof(texelIndex));
        }

        int offset = colorIndex * PaletteBytesPerColor + paletteIndex * 32 + texelIndex * 2;
        ushort color = BinaryPrimitives.ReadUInt16LittleEndian(_paletteData.AsSpan(offset));
        if (texelIndex == 0 || color == 0xFFFF)
        {
            return new Rgb32(0, 0, 0, 0);
        }

        int red = color & 0x1F;
        int green = (color >> 5) & 0x1F;
        int blue = (color >> 10) & 0x1F;
        return new Rgb32(Expand5(red), Expand5(green), Expand5(blue));
    }

    public Rgb32 Sample(byte u, byte v, int colorIndex, int paletteIndex, int verticalUvOffset = 0) =>
        GetColor(colorIndex, paletteIndex, GetTexel(u, AdjustVerticalCoordinate(v, verticalUvOffset)));

    public static int AdjustVerticalCoordinate(int coordinate, int verticalUvOffset)
    {
        ValidateVerticalUvOffset(verticalUvOffset);
        return Math.Clamp(coordinate - verticalUvOffset, 0, TextureHeight - 1);
    }

    public static double MapVerticalCoordinateToAtlas(byte coordinate, int paletteIndex, int verticalUvOffset = 0)
    {
        if ((uint)paletteIndex >= PaletteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(paletteIndex));
        }

        int textureRow = AdjustVerticalCoordinate(coordinate, verticalUvOffset);
        return 1.0 - (paletteIndex * TextureHeight + textureRow) / (double)(TextureHeight * PaletteCount);
    }

    public byte[] CreatePaletteAtlasBitmap(int colorIndex)
    {
        if ((uint)colorIndex >= ColorCount)
        {
            throw new ArgumentOutOfRangeException(nameof(colorIndex));
        }

        const int bitsPerPixel = 24;
        const int headerSize = 54;
        int atlasHeight = TextureHeight * PaletteCount;
        int rowSize = (TextureWidth * 3 + 3) & ~3;
        int imageSize = checked(rowSize * atlasHeight);
        byte[] bitmap = new byte[headerSize + imageSize];
        bitmap[0] = (byte)'B';
        bitmap[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(2), bitmap.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(10), headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(18), TextureWidth);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(22), atlasHeight);
        BinaryPrimitives.WriteUInt16LittleEndian(bitmap.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bitmap.AsSpan(28), bitsPerPixel);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(34), imageSize);

        for (int palette = 0; palette < PaletteCount; palette++)
        {
            for (int y = 0; y < TextureHeight; y++)
            {
                int outputRow = atlasHeight - 1 - (palette * TextureHeight + y);
                int outputOffset = headerSize + outputRow * rowSize;
                for (int x = 0; x < TextureWidth; x++)
                {
                    Rgb32 color = Sample((byte)x, (byte)y, colorIndex, palette);
                    bitmap[outputOffset++] = color.B;
                    bitmap[outputOffset++] = color.G;
                    bitmap[outputOffset++] = color.R;
                }
            }
        }

        return bitmap;
    }

    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));

    private static void ValidateVerticalUvOffset(int verticalUvOffset)
    {
        if (verticalUvOffset is < -MaximumVerticalUvOffset or > MaximumVerticalUvOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verticalUvOffset),
                $"The UV adjustment must be between {-MaximumVerticalUvOffset} and {MaximumVerticalUvOffset} pixels.");
        }
    }
}
