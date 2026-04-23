using System.Drawing;

namespace BMDEditor.Bmd;

internal static class PaletteCodec
{
    public const int PaletteSize = 256;

    public static Color[] CreateDefault()
    {
        return Enumerable.Range(0, PaletteSize)
            .Select(index => Color.FromArgb(index, index, index))
            .ToArray();
    }

    public static Color[] CreateRandom(Random random)
    {
        return Enumerable.Range(0, PaletteSize)
            .Select(_ => Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)))
            .ToArray();
    }

    public static Color[] LoadFromPcx(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 768)
        {
            throw new InvalidDataException("Plik PCX jest za krotki, aby zawierac palete 256 kolorow.");
        }

        var paletteOffset = bytes.Length - 768;
        var colors = new Color[PaletteSize];
        for (var i = 0; i < colors.Length; i++)
        {
            var start = paletteOffset + (i * 3);
            colors[i] = Color.FromArgb(bytes[start], bytes[start + 1], bytes[start + 2]);
        }

        return colors;
    }
}
