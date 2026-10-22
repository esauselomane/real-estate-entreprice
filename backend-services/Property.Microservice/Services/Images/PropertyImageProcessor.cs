using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Property.Microservice.Services.Images;

/// <summary>Shared validation and normalisation for agency and public-owner property photographs.</summary>
public static class PropertyImageProcessor
{
    public const int MaximumPictures = 10;
    public const int MaximumImageBytes = 2 * 1024 * 1024;

    public static string Process(string dataUrl)
    {
        if (!dataUrl.StartsWith("data:image/", StringComparison.Ordinal)) throw new ArgumentException("Upload JPEG, PNG or WebP photographs rather than external image links.");
        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || comma > 40 || dataUrl.Length > MaximumImageBytes * 4 / 3 + 100) throw new ArgumentException("Each photograph must not exceed 2 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch (FormatException) { throw new ArgumentException("Invalid image encoding."); }
        if (bytes.Length > MaximumImageBytes) throw new ArgumentException("Each photograph must not exceed 2 MB.");
        try
        {
            var jpeg = bytes.Length > 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
            var png = bytes.Length > 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var webp = bytes.Length > 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
            if (!jpeg && !png && !webp) throw new ArgumentException("Use JPEG, PNG or WebP photographs.");
            var options = new SixLabors.ImageSharp.Formats.DecoderOptions { SkipMetadata = true, MaxFrames = 1 };
            var info = Image.Identify(options, bytes);
            if ((long)info.Width * info.Height > 40_000_000) throw new ArgumentException("Photograph dimensions are too large.");
            using var image = Image.Load(options, bytes);
            var format = image.Metadata.DecodedImageFormat?.Name;
            if (format is not ("JPEG" or "PNG" or "WEBP")) throw new ArgumentException("Use JPEG, PNG or WebP photographs.");
            image.Mutate(PrepareLargeImage);
            image.Metadata.ExifProfile = null; image.Metadata.XmpProfile = null; image.Metadata.IptcProfile = null;
            using var output = new MemoryStream();
            image.SaveAsJpeg(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 });
            return "data:image/jpeg;base64," + Convert.ToBase64String(output.ToArray());
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        { throw new ArgumentException("The photograph could not be decoded."); }
    }

    private static void PrepareLargeImage(IImageProcessingContext context)
    {
        context.AutoOrient();
        context.Resize(new ResizeOptions { Size = new Size(1200, 1200), Mode = ResizeMode.Max, Sampler = KnownResamplers.Lanczos3 });
    }

    public static List<string> ReadGallery(string json) => JsonSerializer.Deserialize<List<string>>(json) ?? [];
}
