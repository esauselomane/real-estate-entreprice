using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>Creates clearly schematic sample PNGs without depending on external photo hosting.</summary>
internal static class SampleImages
{
    public static IReadOnlyList<string> Create(string propertyType, int theme)
    {
        var images = new List<string>();
        for (var variant = 0; variant < 3; variant++) images.Add(CreateSchematic(propertyType, theme, variant));
        return images;
    }

    private static string CreateSchematic(string propertyType, int theme, int variant)
    {
        using var image = new Image<Rgb24>(720, 440);
        var wall = new Rgb24((byte)(220 - theme * 8), (byte)(225 - variant * 12), (byte)(210 - theme * 5));
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++) image[x, y] = PixelAt(x, y, propertyType, wall, variant);
        }
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }

    private static Rgb24 PixelAt(int x, int y, string type, Rgb24 wall, int variant)
    {
        if (y > 350) return new Rgb24(167, 192, 168);
        if (type == "Land")
        {
            if (y > 250 && (x / 70 + y / 55) % 2 == 0) return new Rgb24(123, 161, 126);
            return new Rgb24(222, 237, 226);
        }
        if (x > 120 && x < 610 && y > 140 && y < 350)
        {
            if (x > 440 && x < 530 && y > 220) return new Rgb24(18, 104, 94);
            if (y > 195 && y < 275 && ((x > 165 && x < 250) || (x > 290 && x < 375))) return new Rgb24(133, 178, 180);
            return wall;
        }
        if (y > 100 + Math.Abs(x - 365) / 5 && y < 155 && x > 95 && x < 635) return new Rgb24(12, 81, 73);
        if ((x - 580) * (x - 580) + (y - 85) * (y - 85) < 1900) return new Rgb24(244, 188, 145);
        return new Rgb24((byte)(235 - variant * 5), 243, 239);
    }
}
