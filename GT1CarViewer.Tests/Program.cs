using System.Buffers.Binary;
using System.Text;
using GT1CarViewer.Core;

var tests = new (string Name, Action Run)[]
{
    ("GT-ZIP back references", TestGtZip),
    ("GT-ARC entry extraction", TestGtArchive),
    ("Raw PlayStation BIN file lookup", TestDiscImage),
    ("GT-CAR LOD and face parsing", TestCarModel),
    ("Malformed GT-CAR faces are skipped", TestInvalidCarFace),
    ("A valid LOD survives a damaged later LOD", TestDamagedLaterLod),
    ("GT-CTEX palette and texel decoding", TestCarTexture),
    ("A positive UV correction moves faces upward", TestUvCorrection),
    ("OBJ and texture atlas export", TestObjExport)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

string? suppliedImage = Environment.GetEnvironmentVariable("GT1_TEST_BIN");
if (!string.IsNullOrWhiteSpace(suppliedImage))
{
    TestSuppliedDisc(suppliedImage);
    Console.WriteLine("PASS supplied Gran Turismo image and every car entry");
}

static void TestGtZip()
{
    byte[] encoded = [0x08, (byte)'A', (byte)'B', (byte)'C', 3, 2];
    byte[] decoded = GtZip.Decompress(encoded, 9);
    Assert(Encoding.ASCII.GetString(decoded) == "ABCABCABC", "back reference decoded incorrectly");
}

static void TestGtArchive()
{
    byte[] payload = Encoding.ASCII.GetBytes("@(#)GT-CAR");
    byte[] archiveBytes = BuildArchive(payload);
    GtArchive archive = GtArchive.Parse(archiveBytes);

    Assert(archive.Entries.Count == 1, "archive entry count was not read");
    Assert(archive.ContentType == 0x8001, "archive content type was not read");
    Assert(archive.ExtractEntry(0).SequenceEqual(payload), "archive entry was not decompressed");
}

static void TestDiscImage()
{
    byte[] archive = BuildArchive(Encoding.ASCII.GetBytes("@(#)GT-CAR"));
    using var stream = new MemoryStream(BuildRawDisc(archive));
    using DiscImage disc = DiscImage.Open(stream);

    Assert(disc.SectorSize == 2352, "raw sector size was not detected");
    Assert(disc.ReadFile("CAR.DAT").SequenceEqual(archive), "ISO file extent was read incorrectly");
}

static void TestCarModel()
{
    CarModel model = CarModel.Parse(BuildCarModel());
    CarLod lod = model.Lods[0];

    Assert(model.Lods.Count == 1, "LOD count was not parsed");
    Assert(lod.Vertices.Count == 3, "vertex count was not parsed");
    Assert(lod.Faces.Count == 1, "textured face was not parsed");
    Assert(lod.Faces[0].VertexIndices.SequenceEqual([0, 1, 2]), "packed vertex indices were decoded incorrectly");
    Assert(lod.Faces[0].TextureCoordinates![2] == new TextureCoordinate(50, 60), "texture coordinates were not parsed");
}

static void TestInvalidCarFace()
{
    byte[] data = BuildCarModel();
    int firstFace = 0x80 + 40 + 3 * 8;
    data[firstFace] = 1;
    data[firstFace + 1] = 1;

    CarModel model = CarModel.Parse(data);
    Assert(model.Lods[0].Faces.Count == 0, "a face with an out-of-range vertex was retained");
    Assert(model.Lods[0].SkippedFaceCount == 1, "the discarded face was not reported");
}

static void TestDamagedLaterLod()
{
    byte[] firstLod = BuildCarModel();
    byte[] data = new byte[firstLod.Length + 80];
    firstLod.CopyTo(data, 0);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x3C), 2);
    int secondLod = firstLod.Length + 40;
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(secondLod), 5000);

    CarModel model = CarModel.Parse(data);
    Assert(model.Lods.Count == 1, "the valid first LOD was not preserved");
    Assert(model.SkippedLodCount == 1, "the damaged LOD was not reported");
}

static void TestCarTexture()
{
    CarTexture texture = CarTexture.Parse(BuildCarTexture());

    Assert(texture.Width == 256 && texture.Height == 224, "texture dimensions were not reported");
    Assert(texture.ColorCount == 1, "car color count was not parsed");
    Assert(texture.GetTexel(0, 0) == 1 && texture.GetTexel(1, 0) == 2, "packed texels were not decoded");
    Assert(texture.GetColor(0, 0, 1) == new Rgb32(255, 0, 0), "BGR555 palette color was not expanded");
    Assert(texture.Sample(0, 1, 0, 0) == new Rgb32(0, 0, 255), "the original texture coordinate changed");
    Assert(texture.Sample(0, 1, 0, 0, verticalUvOffset: 1) == new Rgb32(255, 0, 0), "a positive UV offset did not move the sample upward");
}

static void TestUvCorrection()
{
    CarModel model = CarModel.Parse(BuildCarModel());
    CarTexture texture = CarTexture.Parse(BuildCarTexture());
    ObjExport original = ObjExporter.Create(model, texture, "test", "texture.bmp");
    ObjExport corrected = ObjExporter.Create(model, texture, "test", "texture.bmp", verticalUvOffset: 4);
    float originalV = float.Parse(
        original.ObjText.Split('\n').First(line => line.StartsWith("vt ", StringComparison.Ordinal)).Split(' ')[2],
        System.Globalization.CultureInfo.InvariantCulture);
    float correctedV = float.Parse(
        corrected.ObjText.Split('\n').First(line => line.StartsWith("vt ", StringComparison.Ordinal)).Split(' ')[2],
        System.Globalization.CultureInfo.InvariantCulture);

    Assert(correctedV > originalV, "a positive UV offset did not raise the exported UV face");
    Assert(Math.Abs(correctedV - originalV - 4.0f / (CarTexture.TextureHeight * 16)) < 0.000001,
        "the exported UV correction did not use four texture pixels");
}

static void TestObjExport()
{
    ObjExport export = ObjExporter.Create(
        CarModel.Parse(BuildCarModel()),
        CarTexture.Parse(BuildCarTexture()),
        "GT1 Test Car",
        "gt1_texture.bmp");

    Assert(export.ObjText.Contains("mtllib GT1_Test_Car.mtl", StringComparison.Ordinal), "OBJ material library is missing");
    Assert(export.ObjText.Contains("f 1/1 2/2 3/3", StringComparison.Ordinal), "OBJ face indices were not exported");
    Assert(export.MtlText.Contains("map_Kd gt1_texture.bmp", StringComparison.Ordinal), "texture mapping is missing from MTL");
    Assert(export.TextureBitmap.AsSpan(0, 2).SequenceEqual("BM"u8), "texture atlas is not a BMP file");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(export.TextureBitmap.AsSpan(18)) == 256, "BMP width is incorrect");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(export.TextureBitmap.AsSpan(22)) == 224 * 16, "palette atlas height is incorrect");
}

static void TestSuppliedDisc(string path)
{
    CarDataSource source = CarDataSource.Open(path);
    CarArchive cars = source.Archives.Single(archive => archive.Name.Equals("CAR.DAT", StringComparison.OrdinalIgnoreCase));
    CarArchive arcade = source.Archives.Single(archive => archive.Name.Equals("CARCADE.DAT", StringComparison.OrdinalIgnoreCase));

    Assert(cars.CarCount == 688, "CAR.DAT car count does not match the disc");
    Assert(arcade.CarCount == 40, "CARCADE.DAT car count does not match the disc");
    string extractedArchivePath = Path.Combine(Path.GetTempPath(), $"GT1CarViewer-{Guid.NewGuid():N}.DAT");
    try
    {
        using (DiscImage disc = DiscImage.Open(path))
        {
            IsoFile carFile = disc.Files.Single(file => file.Name.Equals("CAR.DAT", StringComparison.OrdinalIgnoreCase));
            File.WriteAllBytes(extractedArchivePath, disc.ReadFile(carFile));
        }

        CarArchive extractedArchive = CarDataSource.Open(extractedArchivePath).Archives.Single();
        Assert(extractedArchive.CarCount == 688, "opening a standalone CAR.DAT returned the wrong vehicle count");
    }
    finally
    {
        if (File.Exists(extractedArchivePath))
        {
            File.Delete(extractedArchivePath);
        }
    }

    foreach (CarArchive bank in source.Archives)
    {
        for (int index = 0; index < bank.CarCount; index++)
        {
            CarAsset asset = bank.ReadCar(index);
            CarModel model;
            try
            {
                model = CarModel.Parse(asset.ModelData);
            }
            catch (InvalidDataException error)
            {
                throw new InvalidDataException($"{bank.Name} car {index}: {error.Message}", error);
            }

            CarTexture texture = CarTexture.Parse(asset.TextureData);
            Assert(model.Lods[0].Faces.Count > 0, $"{bank.Name} model {index} has no faces");
            Assert(texture.ColorCount > 0, $"{bank.Name} texture {index} has no palettes");
        }
    }
}

static byte[] BuildCarModel()
{
    byte[] data = new byte[0x80 + 40 + 3 * 8 + 28];
    Encoding.ASCII.GetBytes("@(#)GT-CAR").CopyTo(data, 0);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x3C), 1);
    int position = 0x80;
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(position), 3);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(position + 12), 1);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(position + 36), 16);
    position += 40;
    foreach ((short x, short y, short z) in new (short, short, short)[] { (0, 0, 0), (100, 0, 0), (0, 100, 0) })
    {
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(position), x);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(position + 2), y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(position + 4), z);
        position += 8;
    }

    data[position] = 0;
    data[position + 1] = 2;
    data[position + 2] = 8;
    position += 16;
    data[position++] = 10;
    data[position++] = 20;
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(position), 0);
    position += 2;
    data[position++] = 30;
    data[position++] = 40;
    position += 2;
    data[position++] = 50;
    data[position++] = 60;
    data[position++] = 70;
    data[position] = 80;
    return data;
}

static byte[] BuildCarTexture()
{
    byte[] data = new byte[0x8060 + 0x200];
    Encoding.ASCII.GetBytes("@(#)GT-CTEX").CopyTo(data, 0);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x0E), 1);
    data[0x1060] = 0x21;
    data[0x1060 + CarTexture.TextureWidth / 2] = 0x13;
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x8060 + 2), 0x001F);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x8060 + 4), 0x03E0);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x8060 + 6), 0x7C00);
    return data;
}

static byte[] BuildArchive(byte[] payload)
{
    var compressed = new List<byte>();
    for (int offset = 0; offset < payload.Length; offset += 8)
    {
        compressed.Add(0);
        compressed.AddRange(payload.Skip(offset).Take(8));
    }

    byte[] result = new byte[28 + compressed.Count];
    Encoding.ASCII.GetBytes("@(#)GT-ARC").CopyTo(result, 0);
    BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(12), 0x8001);
    BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(14), 1);
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), 28);
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20), (uint)compressed.Count);
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(24), (uint)payload.Length);
    compressed.CopyTo(result, 28);
    return result;
}

static byte[] BuildRawDisc(byte[] fileData)
{
    const int sectorSize = 2352;
    const int userDataOffset = 24;
    const int blockSize = 2048;
    byte[] image = new byte[24 * sectorSize];

    for (int sector = 16; sector < 24; sector++)
    {
        int start = sector * sectorSize;
        image.AsSpan(start + 1, 10).Fill(0xFF);
        image[start + 15] = 2;
    }

    byte[] descriptor = new byte[blockSize];
    descriptor[0] = 1;
    Encoding.ASCII.GetBytes("CD001").CopyTo(descriptor, 1);
    descriptor[6] = 1;
    MakeRecord(22, blockSize, true, [0]).CopyTo(descriptor, 156);
    descriptor.CopyTo(image, 16 * sectorSize + userDataOffset);

    byte[] directory = new byte[blockSize];
    int position = 0;
    foreach (byte[] record in new[]
    {
        MakeRecord(22, blockSize, true, [0]),
        MakeRecord(22, blockSize, true, [1]),
        MakeRecord(23, fileData.Length, false, Encoding.ASCII.GetBytes("CAR.DAT;1"))
    })
    {
        record.CopyTo(directory, position);
        position += record.Length;
    }

    directory.CopyTo(image, 22 * sectorSize + userDataOffset);
    fileData.CopyTo(image, 23 * sectorSize + userDataOffset);
    return image;
}

static byte[] MakeRecord(uint extent, int size, bool isDirectory, byte[] name)
{
    int length = 33 + name.Length + (name.Length % 2 == 0 ? 1 : 0);
    byte[] record = new byte[length];
    record[0] = (byte)length;
    BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(2), extent);
    BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(6), extent);
    BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(10), (uint)size);
    BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(14), (uint)size);
    record[25] = isDirectory ? (byte)2 : (byte)0;
    BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(28), 1);
    BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(30), 1);
    record[32] = (byte)name.Length;
    name.CopyTo(record, 33);
    return record;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
