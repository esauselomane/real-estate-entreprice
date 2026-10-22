using Microsoft.Data.SqlClient;
using Developer.API.Data;

// No application startup, seeders, users or properties are invoked by this production utility.
await using var identity = new SqlConnection(Environment.GetEnvironmentVariable("IDENTITY_CONNECTION") ?? throw new InvalidOperationException("IDENTITY_CONNECTION is required."));
await identity.OpenAsync();
await using var query = identity.CreateCommand();
query.CommandText = "SELECT TenantId, AgencyName FROM TenantAgencies WHERE AgencyName = @name";
query.Parameters.AddWithValue("@name", "Kasi Dwellers");
var agencies = new List<(string Id, string Name)>();
await using (var reader = await query.ExecuteReaderAsync())
    while (await reader.ReadAsync()) agencies.Add((reader.GetString(0), reader.GetString(1)));
if (agencies.Count != 1) throw new InvalidOperationException($"Expected one existing Kasi Dwellers agency, found {agencies.Count}. No records changed.");
var agency = agencies[0];
Console.WriteLine($"Verified production agency: {agency.Name}; TenantId={agency.Id}");
if (!args.Contains("--configure-key")) return;
var expectedAgency = Environment.GetEnvironmentVariable("EXPECTED_AGENCY_ID");
if (agency.Id != expectedAgency) throw new InvalidOperationException("Verified agency does not match deployment configuration.");
var rawKey = Environment.GetEnvironmentVariable("PUBLIC_WEBSITE_API_KEY") ?? throw new InvalidOperationException("Website key secret is required.");
await using var developer = new SqlConnection(Environment.GetEnvironmentVariable("DEVELOPER_CONNECTION"));
await developer.OpenAsync();
await using var command = developer.CreateCommand();
command.CommandText = """
    IF NOT EXISTS (SELECT 1 FROM AgencyApiKeys WHERE KeyHash=@hash)
    INSERT INTO AgencyApiKeys (AgencyId, AgencyName, Name, Description, KeyHash, KeyPrefix, IsRevoked, CreatedBy, CreatedAt)
    VALUES (@agency, @name, 'Kasi Dwellers website', 'Production public website key', @hash, @prefix, 0, 'production-deployment', SYSUTCDATETIME());
    """;
command.Parameters.AddWithValue("@agency", agency.Id); command.Parameters.AddWithValue("@name", agency.Name);
command.Parameters.AddWithValue("@hash", ApiKeyGenerator.HashKey(rawKey)); command.Parameters.AddWithValue("@prefix", ApiKeyGenerator.GetPrefix(rawKey));
await command.ExecuteNonQueryAsync();
Console.WriteLine("Production website API key configured. No users or properties created.");
