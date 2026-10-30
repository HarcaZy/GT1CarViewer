using System.Buffers.Binary;
using System.Text;

namespace GT1CarViewer.Core;

public sealed record IsoFile(string Name, string Path, uint Extent, uint Length, bool IsDirectory);

public sealed class DiscImage : IDisposable
{
    private const int IsoBlockSize = 2048;
    private const int RawSectorSize = 2352;
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly int _userDataOffset;
    private readonly object _streamLock = new();
    private bool _disposed;

    private DiscImage(Stream stream, bool leaveOpen, int sectorSize, int userDataOffset, IReadOnlyList<IsoFile> files)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
        SectorSize = sectorSize;
        _userDataOffset = userDataOffset;
        Files = files;
    }

    public int SectorSize { get; }
    public IReadOnlyList<IsoFile> Files { get; }

    public static DiscImage Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return Open(stream, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static DiscImage Open(Stream stream, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek || stream.Length < 17L * IsoBlockSize)
        {
            throw new InvalidDataException("The disc image must be a seekable ISO image.");
        }

        (int sectorSize, int userDataOffset) = DetectSectorLayout(stream);
        var reader = new DiscImage(stream, leaveOpen, sectorSize, userDataOffset, []);
        try
        {
            IsoFile root = reader.ReadRootDirectory();
            var files = new List<IsoFile>();
            reader.ReadDirectory(root, "", files, new HashSet<uint>(), 0);
            return new DiscImage(stream, leaveOpen, sectorSize, userDataOffset, files);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    public byte[] ReadFile(string name)
    {
        IsoFile? file = Files.FirstOrDefault(candidate =>
            !candidate.IsDirectory &&
            (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(System.IO.Path.GetFileName(candidate.Path), name, StringComparison.OrdinalIgnoreCase)));

        return file is null
            ? throw new FileNotFoundException($"The disc image does not contain {name}.")
            : ReadFile(file);
    }

    public byte[] ReadFile(IsoFile file)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (file.IsDirectory || file.Length > int.MaxValue)
        {
            throw new InvalidDataException("The selected ISO entry is not a supported regular file.");
        }

        return ReadExtent(file);
    }

    private byte[] ReadExtent(IsoFile file)
    {
        if (file.Length > int.MaxValue)
        {
            throw new InvalidDataException("The selected ISO entry exceeds the supported file size.");
        }

        long start = (long)file.Extent * IsoBlockSize;
        long end = start + file.Length;
        if (end > _stream.Length / (double)SectorSize * IsoBlockSize)
        {
            throw new InvalidDataException($"ISO file {file.Path} extends beyond the image.");
        }

        byte[] result = new byte[(int)file.Length];
        int written = 0;
        while (written < result.Length)
        {
            uint block = file.Extent + (uint)(written / IsoBlockSize);
            int inBlock = written % IsoBlockSize;
            int count = Math.Min(IsoBlockSize - inBlock, result.Length - written);
            byte[] data = ReadBlock(block);
            data.AsSpan(inBlock, count).CopyTo(result.AsSpan(written));
            written += count;
        }

        return result;
    }

    private static (int SectorSize, int UserDataOffset) DetectSectorLayout(Stream stream)
    {
        if (stream.Length % RawSectorSize == 0)
        {
            byte[] raw = new byte[RawSectorSize];
            stream.Position = 16L * RawSectorSize;
            stream.ReadExactly(raw);
            bool sync = raw[0] == 0 && raw.AsSpan(1, 10).IndexOfAnyExcept((byte)0xFF) < 0 && raw[11] == 0;
            int offset = raw[15] switch
            {
                1 => 16,
                2 => 24,
                _ => -1
            };

            if (sync && offset >= 0 && IsPrimaryVolumeDescriptor(raw.AsSpan(offset, IsoBlockSize)))
            {
                return (RawSectorSize, offset);
            }
        }

        if (stream.Length % IsoBlockSize == 0)
        {
            byte[] block = new byte[IsoBlockSize];
            stream.Position = 16L * IsoBlockSize;
            stream.ReadExactly(block);
            if (IsPrimaryVolumeDescriptor(block))
            {
                return (IsoBlockSize, 0);
            }
        }

        throw new InvalidDataException("No ISO9660 primary volume descriptor was found at sector 16.");
    }

    private static bool IsPrimaryVolumeDescriptor(ReadOnlySpan<byte> block) =>
        block.Length >= IsoBlockSize &&
        block[0] == 1 &&
        block.Slice(1, 5).SequenceEqual("CD001"u8) &&
        block[6] == 1;

    private IsoFile ReadRootDirectory()
    {
        byte[] descriptor = ReadBlock(16);
        if (!IsPrimaryVolumeDescriptor(descriptor))
        {
            throw new InvalidDataException("The ISO9660 primary volume descriptor is malformed.");
        }

        return ParseRecord(descriptor.AsSpan(156), "", requireDirectory: true);
    }

    private void ReadDirectory(IsoFile directory, string parent, List<IsoFile> files, HashSet<uint> visited, int depth)
    {
        if (depth > 16 || !visited.Add(directory.Extent))
        {
            return;
        }

        byte[] data = ReadExtent(directory);
        int position = 0;
        while (position < data.Length)
        {
            int recordLength = data[position];
            if (recordLength == 0)
            {
                position = ((position / IsoBlockSize) + 1) * IsoBlockSize;
                continue;
            }

            if (recordLength < 34 || recordLength > data.Length - position)
            {
                throw new InvalidDataException($"ISO directory {parent} contains an invalid record.");
            }

            IsoFile file = ParseRecord(data.AsSpan(position, recordLength), parent, requireDirectory: false);
            position += recordLength;
            if (file.Name is "." or "..")
            {
                continue;
            }

            files.Add(file);
            if (file.IsDirectory)
            {
                ReadDirectory(file, file.Path, files, visited, depth + 1);
            }
        }
    }

    private static IsoFile ParseRecord(ReadOnlySpan<byte> record, string parent, bool requireDirectory)
    {
        if (record.Length < 34 || record[0] < 34 || record[0] > record.Length)
        {
            throw new InvalidDataException("An ISO9660 directory record is malformed.");
        }

        int nameLength = record[32];
        if (33 + nameLength > record[0])
        {
            throw new InvalidDataException("An ISO9660 file name exceeds its directory record.");
        }

        string name = nameLength == 1 && record[33] == 0
            ? "."
            : nameLength == 1 && record[33] == 1
                ? ".."
                : Encoding.ASCII.GetString(record.Slice(33, nameLength));
        int version = name.IndexOf(';');
        if (version >= 0)
        {
            name = name[..version];
        }

        uint extent = BinaryPrimitives.ReadUInt32LittleEndian(record[2..]);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(record[10..]);
        bool isDirectory = (record[25] & 2) != 0;
        if (requireDirectory && !isDirectory)
        {
            throw new InvalidDataException("The ISO9660 root entry is not a directory.");
        }

        string path = parent.Length == 0 ? name : $"{parent}\\{name}";
        return new IsoFile(name, path, extent, length, isDirectory);
    }

    private byte[] ReadBlock(uint block)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long position = checked((long)block * SectorSize + _userDataOffset);
        if (position + IsoBlockSize > _stream.Length)
        {
            throw new InvalidDataException($"ISO block {block} lies outside the image.");
        }

        byte[] data = new byte[IsoBlockSize];
        lock (_streamLock)
        {
            _stream.Position = position;
            _stream.ReadExactly(data);
        }

        return data;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }
}
