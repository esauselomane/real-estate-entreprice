using System.Reflection;
using Microsoft.AspNetCore.DataProtection;

namespace Property.Microservice.Services.Syndication;

/// <summary>Loads trusted, deployed provider assemblies configured by the operator. No user-supplied DLLs are loaded.</summary>
public static class ListingProviderRegistration
{
    public static IServiceCollection AddListingProviders(this IServiceCollection services, IConfiguration configuration)
    {
        var protection = services.AddDataProtection().SetApplicationName("Property.Syndication");
        var keysPath = configuration["Syndication:DataProtectionKeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        services.AddScoped<AgencyApiKeyProtector>();
        var assemblies = new List<Assembly> { typeof(ListingProviderRegistration).Assembly };
        foreach (var name in configuration.GetSection("Syndication:ProviderAssemblies").Get<string[]>() ?? [])
        {
            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Any(IsInvalidAssemblyNameCharacter))
                throw new InvalidOperationException("Provider assembly names may contain only letters, digits, dots, underscores and hyphens.");
            var path = Path.Combine(AppContext.BaseDirectory, "providers", name, name + ".dll");
            var context = new ListingProviderLoadContext(path);
            assemblies.Add(context.LoadFromAssemblyPath(path));
        }
        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var type in assembly.GetTypes().Where(IsProvider))
                services.AddScoped(typeof(IListingProvider), type);
        }
        services.AddScoped<IProviderCredentialSource, ConfigurationProviderCredentialSource>();
        services.AddScoped<ListingProviderRegistry>();
        services.AddScoped<SyndicationCoordinator>();
        services.AddScoped<SyndicationProcessor>();
        return services;
    }

    private static bool IsInvalidAssemblyNameCharacter(char value) => !char.IsAsciiLetterOrDigit(value) && value is not ('.' or '_' or '-');

    private static bool IsProvider(Type type) => type.IsClass && !type.IsAbstract && typeof(IListingProvider).IsAssignableFrom(type);
}
