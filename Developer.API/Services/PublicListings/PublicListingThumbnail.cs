using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Developer.API.Services.PublicListings;

/// <summary>Uses an existing thumb when present, otherwise derives a small preview from the gallery image.</summary>
public static class PublicListingThumbnail
{
    public static string? Create(IReadOnlyList<string> images)
    {
        foreach (var source in images)
        {
            if (source.Contains("_thumb.", StringComparison.OrdinalIgnoreCase)) return source;
        }
        var first = images.FirstOrDefault();
        if (first is null || !first.StartsWith("data:image/", StringComparison.Ordinal)) return first;
        try
        {
            var bytes = Convert.FromBase64String(first[(first.IndexOf(',') + 1)..]);
            var information = Image.Identify(bytes);
            if ((long)information.Width * information.Height > 40_000_000) return null;
            using var image = Image.Load(bytes);
            image.Mutate(ResizeThumbnail);
            using var output = new MemoryStream();
            image.SaveAsJpeg(output);
            return "data:image/jpeg;base64," + Convert.ToBase64String(output.ToArray());
        }
        catch (Exception exception) when (exception is FormatException or UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }

    private static void ResizeThumbnail(IImageProcessingContext context) => context.Resize(new ResizeOptions
    {
        Size = new Size(150, 100), Mode = ResizeMode.Crop
    });
}
