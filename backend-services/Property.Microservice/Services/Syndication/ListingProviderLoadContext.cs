using System.Reflection;
using System.Runtime.Loader;

namespace Property.Microservice.Services.Syndication;

/// <summary>Loads a deployed provider and its private dependencies while sharing the host's contract assembly.</summary>
internal sealed class ListingProviderLoadContext(string assemblyPath) : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver resolver = new(assemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Sharing the contract identity makes plugin implementations assignable to IListingProvider.
        var contractAssembly = typeof(IListingProvider).Assembly;
        if (assemblyName.Name == contractAssembly.GetName().Name) return contractAssembly;
        var path = resolver.ResolveAssemblyToPath(assemblyName);
        return path == null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path == null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
