using System.IO;

namespace Duxel.Core.Dsl;

public static class UiDslSourcePathResolver
{
    public static string ResolveFromProjectRoot(string relativeUiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUiPath);

        var assemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            throw new InvalidOperationException("Entry assembly name is not available.");
        }

        return ResolveFromProjectRoot(assemblyName + ".csproj", relativeUiPath);
    }

    public static string ResolveFromProjectRoot(string projectFileName, string relativeUiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUiPath);

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, projectFileName);
            if (File.Exists(projectPath))
            {
                var uiPath = Path.Combine(directory.FullName, relativeUiPath);
                if (!File.Exists(uiPath))
                {
                    throw new FileNotFoundException("UI source file not found.", uiPath);
                }

                return uiPath;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Project root not found while resolving UI source path.");
    }
}

