internal sealed record SamplePropertyDefinition(string Code, string Address, string Type, int Size,
    decimal Latitude, decimal Longitude, decimal? SalePrice, decimal? RentalPrice, bool Hot, string Status, int ImageTheme, string RoomLettingType = "None")
{
    public static IReadOnlyList<SamplePropertyDefinition> CreateScenario()
    {
        var definitions = CreateCoreScenario();
        for (var index = definitions.Count + 1; index <= 50; index++) definitions.Add(CreateRandomProperty(index));
        return definitions;
    }

    private static SamplePropertyDefinition CreateRandomProperty(int index)
    {
        string[] types = ["Residential", "Commercial", "Industrial", "Retail", "Land"];
        string[] areas = ["Sea Point", "Green Point", "Observatory", "Gardens", "Woodstock", "Century City"];
        var type = types[Random.Shared.Next(types.Length)];
        var area = areas[Random.Shared.Next(areas.Length)];
        var purpose = Random.Shared.Next(3);
        var roomType = "None";
        if (type == "Residential" && index % 4 == 0) roomType = index % 8 == 0 ? "PrivateRoom" : "SharedRoom";
        decimal? salePrice = purpose != 1 && roomType == "None" ? Random.Shared.Next(500, 9000) * 1000m : null;
        decimal? rentalPrice = purpose != 0 || roomType != "None" ? Random.Shared.Next(30, 450) * 100m : null;
        var latitude = -33.925m + Random.Shared.Next(-200, 201) / 10000m;
        var longitude = 18.430m + Random.Shared.Next(-200, 201) / 10000m;
        return new SamplePropertyDefinition($"RANDOM-{index:000}", $"{Random.Shared.Next(50, 999)} Sample Avenue, {area}, Cape Town",
            type, Random.Shared.Next(50, 1500), latitude, longitude, salePrice, rentalPrice,
            index % 3 == 0 || Random.Shared.Next(4) == 0, "Approved", Random.Shared.Next(6), roomType);
    }

    private static List<SamplePropertyDefinition> CreateCoreScenario() =>
    [
        new("SEA-POINT", "10 Ocean View Road, Sea Point, Cape Town", "Residential", 240, -33.918m, 18.386m, 2800000, null, true, "Approved", 0),
        new("GREEN-POINT", "22 Garden Lane, Green Point, Cape Town", "Residential", 180, -33.910m, 18.405m, null, 14500, true, "Approved", 1),
        new("CENTURY-CITY", "3 Canal Square, Century City, Cape Town", "Commercial", 600, -33.892m, 18.510m, 6500000, 38000, true, "Approved", 2),
        new("WOODSTOCK", "18 Workshop Avenue, Woodstock, Cape Town", "Industrial", 950, -33.930m, 18.450m, null, 27000, false, "Approved", 3),
        new("GARDENS", "5 Market Walk, Gardens, Cape Town", "Retail", 160, -33.936m, 18.413m, 1900000, 18000, false, "Approved", 4),
        new("CONSTANTIA", "7 Meadow Estate, Constantia, Cape Town", "Land", 2200, -34.022m, 18.420m, 3200000, null, false, "Approved", 5),
        new("DISTANT", "12 Jacaranda Road, Hatfield, Pretoria", "Residential", 300, -25.748m, 28.238m, 2300000, null, false, "Approved", 1),
        new("PRIVATE-ROOM", "16 Student Lane, Observatory, Cape Town", "Residential", 24, -33.936m, 18.468m, null, 5500, false, "Approved", 1, "PrivateRoom"),
        new("ROOM-SHARE", "24 Student Lane, Observatory, Cape Town", "Residential", 30, -33.938m, 18.470m, null, 3200, false, "Approved", 3, "SharedRoom"),
        new("HIDDEN", "32 Ocean View Road, Sea Point, Cape Town", "Residential", 210, -33.920m, 18.389m, 2500000, null, false, "Unpublished", 2),
        new("REVIEW", "48 Garden Lane, Green Point, Cape Town", "Residential", 190, -33.912m, 18.408m, null, 12500, false, "Pending review", 0)
    ];
}
