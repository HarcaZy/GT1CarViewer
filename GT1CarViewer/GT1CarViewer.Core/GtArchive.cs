using System.Buffers.Binary;
using System.Text;

namespace GT1CarViewer.Core;

public sealed record GtArchiveEntry(uint Offset, uint CompressedSize, uint DecompressedSize);

public sealed class GtArchive
{
    private const int HeaderSize = 16;
    private const int EntrySize = 12;
    private const int MaximumEntrySize = 64 * 1024 * 1024;
    private readonly byte[] _data;

    private GtArchive(byte[] data, ushort contentType, IReadOnlyList<GtArchiveEntry> entries)
    {
        _data = data;
        ContentType = contentType;
        Entries = entries;
    }

    public ushort ContentType { get; }
    public IReadOnlyList<GtArchiveEntry> Entries { get; }

    public static GtArchive Parse(ReadOnlySpan<byte> source)
    {
        ReadOnlySpan<byte> signature = "@(#)GT-ARC"u8;
        if (source.Length < HeaderSize || !source[..signature.Length].SequenceEqual(signature))
        {
            throw new InvalidDataException("The file does not have a GT-ARC header.");
        }

        ushort contentType = BinaryPrimitives.ReadUInt16LittleEndian(source[12..]);
        ushort entryCount = BinaryPrimitives.ReadUInt16LittleEndian(source[14..]);
        if (contentType is not (0x0001 or 0x8001))
        {
            throw new InvalidDataException($"Unsupported GT-ARC content type 0x{contentType:X4}.");
        }

        int tableEnd = checked(HeaderSize + entryCount * EntrySize);
        if (source.Length < tableEnd)
        {
            throw new InvalidDataException("The GT-ARC entry table is truncated.");
        }

        var entries = new List<GtArchiveEntry>(entryCount);
        for (int index = 0; index < entryCount; index++)
        {
            int position = HeaderSize + index * EntrySize;
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(source[position..]);
            uint compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(source[(position + 4)..]);
            uint decompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(source[(position + 8)..]);

            if (offset < tableEnd || offset > source.Length || compressedSize > source.Length - offset)
            {
                throw new InvalidDataException($"GT-ARC entry {index} points outside the archive.");
            }

            if (decompressedSize > MaximumEntrySize)
            {
                throw new InvalidDataException($"GT-ARC entry {index} exceeds the supported size limit.");
            }

            entries.Add(new GtArchiveEntry(offset, compressedSize, decompressedSize));
        }

        return new GtArchive(source.ToArray(), contentType, entries);
    }

    public byte[] ExtractEntry(int index)
    {
        if ((uint)index >= Entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        GtArchiveEntry entry = Entries[index];
        ReadOnlySpan<byte> compressed = _data.AsSpan((int)entry.Offset, (int)entry.CompressedSize);
        int outputSize = (int)entry.DecompressedSize;

        if (ContentType == 0x8001)
        {
            return GtZip.Decompress(compressed, outputSize);
        }

        if (entry.CompressedSize != entry.DecompressedSize)
        {
            throw new InvalidDataException($"Uncompressed GT-ARC entry {index} has inconsistent sizes.");
        }

        return compressed.ToArray();
    }
}
