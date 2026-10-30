using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace GT1CarViewer.Core;

public readonly record struct CarVertex(float X, float Y, float Z);
public readonly record struct TextureCoordinate(byte U, byte V);

public sealed record CarFace(
    IReadOnlyList<int> VertexIndices,
    IReadOnlyList<TextureCoordinate>? TextureCoordinates,
    int PaletteIndex,
    uint FlatColor,
    byte FaceType)
{
    public bool IsTextured => TextureCoordinates is not null;
}

public sealed record CarLod(
    ushort Scale,
    IReadOnlyList<CarVertex> Vertices,
    IReadOnlyList<CarFace> Faces,
    int SkippedFaceCount);

public sealed class CarModel
{
    private const int MaximumVertices = 4096;
    private const int MaximumNormals = 4096;
    private const int MaximumFaces = 8192;
    private static readonly byte[] Signature = "@(#)GT-CAR"u8.ToArray();

    private CarModel(IReadOnlyList<CarLod> lods, int declaredLodCount, int skippedLodCount)
    {
        Lods = lods;
        DeclaredLodCount = declaredLodCount;
        SkippedLodCount = skippedLodCount;
    }

    public IReadOnlyList<CarLod> Lods { get; }
    public int DeclaredLodCount { get; }
    public int SkippedLodCount { get; }

    public static CarModel Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 0x80 || !data[..Signature.Length].SequenceEqual(Signature))
        {
            throw new InvalidDataException("The selected entry is not a complete GT-CAR model.");
        }

        int lodCount = BinaryPrimitives.ReadUInt16LittleEndian(data[0x3C..]);
        if (lodCount is < 1 or > 8)
        {
            throw new InvalidDataException($"The GT-CAR model declares an invalid LOD count ({lodCount}).");
        }

        var reader = new CarDataReader(data, 0x80);
        var lods = new List<CarLod>(lodCount);
        int skippedLodCount = 0;
        for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
        {
            try
            {
                lods.Add(ReadLod(ref reader));
                if (lodIndex + 1 < lodCount)
                {
                    reader.Skip(40);
                }
            }
            catch (InvalidDataException) when (lods.Count > 0)
            {
                skippedLodCount = lodCount - lods.Count;
                break;
            }
        }

        return new CarModel(new ReadOnlyCollection<CarLod>(lods), lodCount, skippedLodCount);
    }

    private static CarLod ReadLod(ref CarDataReader reader)
    {
        int vertexCount = ReadCount(ref reader, MaximumVertices, "vertex");
        int normalCount = ReadCount(ref reader, MaximumNormals, "normal");
        int triangleCount = ReadCount(ref reader, MaximumFaces, "triangle");
        int quadCount = ReadCount(ref reader, MaximumFaces, "quad");
        reader.Skip(4);
        int texturedTriangleCount = ReadCount(ref reader, MaximumFaces, "textured triangle");
        int texturedQuadCount = ReadCount(ref reader, MaximumFaces, "textured quad");
        reader.Skip(20);
        ushort scale = reader.ReadUInt16();
        reader.Skip(2);

        var vertices = new List<CarVertex>(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            short z = reader.ReadInt16();
            reader.Skip(2);
            vertices.Add(new CarVertex(x, y, -z));
        }

        reader.Skip(checked(normalCount * 8));
        int totalFaceCount = checked(triangleCount + quadCount + texturedTriangleCount + texturedQuadCount);
        var faces = new List<CarFace>(totalFaceCount);
        int skippedFaceCount = 0;
        ReadFaces(ref reader, vertices.Count, triangleCount, isQuad: false, textured: false, faces, ref skippedFaceCount);
        ReadFaces(ref reader, vertices.Count, quadCount, isQuad: true, textured: false, faces, ref skippedFaceCount);
        ReadFaces(ref reader, vertices.Count, texturedTriangleCount, isQuad: false, textured: true, faces, ref skippedFaceCount);
        ReadFaces(ref reader, vertices.Count, texturedQuadCount, isQuad: true, textured: true, faces, ref skippedFaceCount);

        return new CarLod(
            scale,
            new ReadOnlyCollection<CarVertex>(vertices),
            new ReadOnlyCollection<CarFace>(faces),
            skippedFaceCount);
    }

    private static int ReadCount(ref CarDataReader reader, int maximum, string kind)
    {
        int count = reader.ReadUInt16();
        if (count > maximum)
        {
            throw new InvalidDataException($"The GT-CAR LOD declares too many {kind}s ({count}).");
        }

        return count;
    }

    private static void ReadFaces(
        ref CarDataReader reader,
        int vertexCount,
        int count,
        bool isQuad,
        bool textured,
        List<CarFace> faces,
        ref int skippedFaceCount)
    {
        for (int faceIndex = 0; faceIndex < count; faceIndex++)
        {
            byte[] packed = reader.ReadBytes(6);
            int[] indices =
            [
                packed[0] | ((packed[1] & 1) << 8),
                (packed[1] >> 1) | ((packed[2] & 3) << 7),
                (packed[2] >> 2) | ((packed[3] & 7) << 6)
            ];
            if (isQuad)
            {
                indices = [.. indices, packed[4] | ((packed[5] & 1) << 8)];
            }

            bool invalidVertexReference = indices.Any(index => index >= vertexCount);
            reader.Skip(6);
            byte[] faceData = reader.ReadBytes(4);
            uint flatColor = (uint)(faceData[0] | (faceData[1] << 8) | (faceData[2] << 16));
            if (!textured)
            {
                if (invalidVertexReference)
                {
                    skippedFaceCount++;
                }
                else
                {
                    faces.Add(new CarFace(indices, null, 0, flatColor, faceData[3]));
                }

                continue;
            }

            var coordinates = new List<TextureCoordinate>(isQuad ? 4 : 3)
            {
                reader.ReadTextureCoordinate()
            };
            ushort rawPalette = reader.ReadUInt16();
            int paletteIndex = (rawPalette >> 4) + (rawPalette & 0x3F);
            coordinates.Add(reader.ReadTextureCoordinate());
            reader.Skip(2);
            coordinates.Add(reader.ReadTextureCoordinate());
            TextureCoordinate fourth = reader.ReadTextureCoordinate();
            if (isQuad)
            {
                coordinates.Add(fourth);
            }

            if (invalidVertexReference)
            {
                skippedFaceCount++;
            }
            else
            {
                faces.Add(new CarFace(indices, new ReadOnlyCollection<TextureCoordinate>(coordinates), paletteIndex, flatColor, faceData[3]));
            }
        }
    }

    private ref struct CarDataReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public CarDataReader(ReadOnlySpan<byte> data, int position)
        {
            _data = data;
            _position = position;
        }

        public short ReadInt16()
        {
            EnsureRemaining(2);
            short value = BinaryPrimitives.ReadInt16LittleEndian(_data[_position..]);
            _position += 2;
            return value;
        }

        public ushort ReadUInt16()
        {
            EnsureRemaining(2);
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_data[_position..]);
            _position += 2;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            EnsureRemaining(count);
            byte[] value = _data.Slice(_position, count).ToArray();
            _position += count;
            return value;
        }

        public TextureCoordinate ReadTextureCoordinate()
        {
            EnsureRemaining(2);
            var coordinate = new TextureCoordinate(_data[_position], _data[_position + 1]);
            _position += 2;
            return coordinate;
        }

        public void Skip(int count)
        {
            EnsureRemaining(count);
            _position += count;
        }

        private void EnsureRemaining(int count)
        {
            if (count < 0 || count > _data.Length - _position)
            {
                throw new InvalidDataException("The GT-CAR model ends inside a geometry record.");
            }
        }
    }
}
