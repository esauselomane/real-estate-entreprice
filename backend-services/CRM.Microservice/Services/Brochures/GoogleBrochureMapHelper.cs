using System.Globalization;

namespace CRM.Microservice.Services.Brochures;
//esau testing
/// <summary>Builds a static Google map with one dotted enclosure around all valid locations.</summary>
public static class GoogleBrochureMapHelper
{
    //why changes not picked
    public static string? BuildUrl(IEnumerable<(double Latitude, double Longitude)> coordinates, string? apiKey)
    {
        var points = coordinates.Where(IsValid).Distinct().ToList();
        if (points.Count == 0 || string.IsNullOrWhiteSpace(apiKey)) return null;
        var projected = points.Select(Project).ToList();
        var centerX = (projected.Min(point => point.X) + projected.Max(point => point.X)) / 2;
        var centerY = (projected.Min(point => point.Y) + projected.Max(point => point.Y)) / 2;
        var radius = Math.Max(0.0001, projected.Max(point => Math.Sqrt(Math.Pow(point.X - centerX, 2) + Math.Pow(point.Y - centerY, 2))) * 1.25);
        var parameters = new List<string> { "size=640x500", "scale=2", "maptype=roadmap", "key=" + Uri.EscapeDataString(apiKey) };
        parameters.Add("markers=" + Uri.EscapeDataString("color:red|" + string.Join("|", points.Select(Format))));
        // Short rounded-looking strokes spaced around a projected circle.
        // Including each stroke in the request also fits the whole enclosure.
        for (var degrees = 0; degrees < 360; degrees += 10)
        {
            var start = Unproject(centerX + radius * Math.Cos(degrees * Math.PI / 180), centerY + radius * Math.Sin(degrees * Math.PI / 180));
            var end = Unproject(centerX + radius * Math.Cos((degrees + 1) * Math.PI / 180), centerY + radius * Math.Sin((degrees + 1) * Math.PI / 180));
            parameters.Add("path=" + Uri.EscapeDataString("color:0x185abdff|weight:4|" + Format(start) + "|" + Format(end)));
        }
        var url = "https://maps.googleapis.com/maps/api/staticmap?" + string.Join("&", parameters);
        
        if (url.Length > 16384) throw new InvalidOperationException("Too many brochure map locations. Reduce the selected properties.");
        return url;
    }

    private static bool IsValid((double Latitude, double Longitude) point) => double.IsFinite(point.Latitude) && double.IsFinite(point.Longitude)
        && Math.Abs(point.Latitude) <= 85 && Math.Abs(point.Longitude) <= 180 && (point.Latitude != 0 || point.Longitude != 0);
    private static (double X, double Y) Project((double Latitude, double Longitude) point) =>
        (point.Longitude * Math.PI / 180, Math.Log(Math.Tan(Math.PI / 4 + point.Latitude * Math.PI / 360)));
    private static (double Latitude, double Longitude) Unproject(double x, double y) =>
        ((2 * Math.Atan(Math.Exp(y)) - Math.PI / 2) * 180 / Math.PI, x * 180 / Math.PI);
    private static string Format((double Latitude, double Longitude) point) =>
        point.Latitude.ToString("F6", CultureInfo.InvariantCulture) + "," + point.Longitude.ToString("F6", CultureInfo.InvariantCulture);
}
