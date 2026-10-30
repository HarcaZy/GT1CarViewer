namespace GT1CarViewer.Core;

public sealed record CarAsset(int Index, byte[] TextureData, byte[] ModelData);

public sealed class CarArchive
{
    private static readonly byte[] TextureSignature = "@(#)GT-CTEX"u8.ToArray();
    private static readonly byte[] ModelSignature = "@(#)GT-CAR"u8.ToArray();
    private readonly GtArchive _archive;

    internal CarArchive(string name, GtArchive archive)
    {
        if (archive.Entries.Count == 0 || archive.Entries.Count % 2 != 0)
        {
            throw new InvalidDataException($"{name} does not contain texture/model entry pairs.");
        }

        Name = name;
        _archive = archive;
    }

    public string Name { get; }
    public int CarCount => _archive.Entries.Count / 2;

    public CarAsset ReadCar(int index)
    {
        if ((uint)index >= CarCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        byte[] texture = _archive.ExtractEntry(index * 2);
        byte[] model = _archive.ExtractEntry(index * 2 + 1);
        if (!texture.AsSpan().StartsWith(TextureSignature) || !model.AsSpan().StartsWith(ModelSignature))
        {
            throw new InvalidDataException($"{Name} entry pair {index} is not a GT-CTEX/GT-CAR pair.");
        }

        return new CarAsset(index, texture, model);
    }
}

public sealed class CarDataSource
{
    private static readonly byte[] ArchiveSignature = "@(#)GT-ARC"u8.ToArray();

    private CarDataSource(string sourcePath, IReadOnlyList<CarArchive> archives)
    {
        SourcePath = sourcePath;
        Archives = archives;
    }

    public string SourcePath { get; }
    public IReadOnlyList<CarArchive> Archives { get; }

    public static CarDataSource Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] prefix = new byte[ArchiveSignature.Length];
        if (stream.Length >= prefix.Length)
        {
            stream.ReadExactly(prefix);
        }

        if (stream.Length >= prefix.Length && prefix.AsSpan().SequenceEqual(ArchiveSignature))
        {
            if (stream.Length > int.MaxValue)
            {
                throw new InvalidDataException("The selected GT-ARC file is too large to load.");
            }

            stream.Position = 0;
            byte[] data = new byte[(int)stream.Length];
            stream.ReadExactly(data);
            string archiveName = Path.GetFileName(path);
            return new CarDataSource(path, [new CarArchive(archiveName, GtArchive.Parse(data))]);
        }

        using DiscImage disc = DiscImage.Open(path);
        var archives = new List<CarArchive>();
        foreach (string archiveName in new[] { "CAR.DAT", "CARCADE.DAT" })
        {
            IsoFile? file = disc.Files.FirstOrDefault(candidate =>
                !candidate.IsDirectory &&
                string.Equals(candidate.Name, archiveName, StringComparison.OrdinalIgnoreCase));
            if (file is not null)
            {
                archives.Add(new CarArchive(archiveName, GtArchive.Parse(disc.ReadFile(file))));
            }
        }

        if (archives.Count == 0)
        {
            throw new InvalidDataException("The ISO image contains neither CAR.DAT nor CARCADE.DAT.");
        }

        return new CarDataSource(path, archives);
    }

}
