using System.Globalization;
using System.Text;

namespace GT1CarViewer.Core;

public sealed record ObjExport(
    string ObjText,
    string MtlText,
    byte[] TextureBitmap,
    string MaterialFileName,
    string TextureFileName);

public static class ObjExporter
{
    public static ObjExport Create(
        CarModel model,
        CarTexture texture,
        string modelName,
        string textureFileName,
        int lodIndex = 0,
        int colorIndex = 0,
        int verticalUvOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(texture);
        if ((uint)lodIndex >= model.Lods.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(lodIndex));
        }

        string safeName = MakeSafeName(modelName);
        string safeTextureName = $"{MakeSafeName(Path.GetFileNameWithoutExtension(textureFileName))}{Path.GetExtension(textureFileName)}";
        CarLod lod = model.Lods[lodIndex];
        var obj = new StringBuilder();
        obj.AppendLine($"mtllib {safeName}.mtl");
        obj.AppendLine($"o {safeName}");

        double scale = lod.Scale / 4096.0;
        foreach (CarVertex vertex in lod.Vertices)
        {
            obj.Append("v ")
                .Append((vertex.X * scale).ToString("G9", CultureInfo.InvariantCulture)).Append(' ')
                .Append((vertex.Y * scale).ToString("G9", CultureInfo.InvariantCulture)).Append(' ')
                .Append((vertex.Z * scale).ToString("G9", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        int textureIndex = 1;
        foreach (CarFace face in lod.Faces)
        {
            if (face.TextureCoordinates is not null)
            {
                int palette = Math.Clamp(face.PaletteIndex, 0, 15);
                foreach (TextureCoordinate coordinate in face.TextureCoordinates)
                {
                    double u = coordinate.U / (double)CarTexture.TextureWidth;
                    double v = CarTexture.MapVerticalCoordinateToAtlas(coordinate.V, palette, verticalUvOffset);
                    obj.Append("vt ")
                        .Append(u.ToString("G9", CultureInfo.InvariantCulture)).Append(' ')
                        .Append(v.ToString("G9", CultureInfo.InvariantCulture))
                        .AppendLine();
                }
            }

            obj.AppendLine("usemtl body");
            obj.Append('f');
            for (int i = 0; i < face.VertexIndices.Count; i++)
            {
                obj.Append(' ').Append(face.VertexIndices[i] + 1);
                if (face.TextureCoordinates is not null)
                {
                    obj.Append('/').Append(textureIndex++);
                }
            }

            obj.AppendLine();
        }

        string mtl = $"newmtl body{Environment.NewLine}Kd 1 1 1{Environment.NewLine}map_Kd {safeTextureName}{Environment.NewLine}";
        return new ObjExport(
            obj.ToString(),
            mtl,
            texture.CreatePaletteAtlasBitmap(colorIndex),
            $"{safeName}.mtl",
            safeTextureName);
    }

    private static string MakeSafeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var result = new string(name.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-'
                ? character
                : '_').ToArray()).Trim('_');
        return result.Length == 0 ? "car" : result;
    }
}
