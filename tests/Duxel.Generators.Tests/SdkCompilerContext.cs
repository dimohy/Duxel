using System.Reflection;
using System.Runtime.Loader;

internal sealed class SdkCompilerContext(string applicationPath) : AssemblyLoadContext("Duxel generator SDK compiler", isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(applicationPath);
    private readonly string _directory = Path.GetDirectoryName(applicationPath)!;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is "Microsoft.CodeAnalysis" or "Microsoft.CodeAnalysis.CSharp")
        {
            var path = Path.Combine(_directory, "SdkRoslyn", assemblyName.Name + ".dll");
            if (!File.Exists(path))
                throw new FileNotFoundException("The explicitly selected SDK compiler is unavailable.", path);
            var expected = AssemblyName.GetAssemblyName(path);
            var actual = LoadFromAssemblyPath(path);
            if (actual.GetName().FullName != expected.FullName || actual.Location != path)
                throw new InvalidOperationException("The selected compiler identity or origin does not match: " + path);
            return actual;
        }
        // Generated consumer assemblies share the same public UI contracts as their validation host.
        if (assemblyName.Name == "Duxel.Core")
            return Default.LoadFromAssemblyName(assemblyName);
        var dependency = _resolver.ResolveAssemblyToPath(assemblyName);
        return dependency is null ? null : LoadFromAssemblyPath(dependency);
    }
}
