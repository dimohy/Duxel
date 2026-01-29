using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Silk.NET.Core.Loader;

namespace Duxel.Platform.Glfw;

internal sealed class AppBasePathResolver : PathResolver
{
    public override IEnumerable<string> EnumeratePossibleLibraryLoadTargets(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            yield break;
        }

        if (Path.IsPathRooted(name) || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar))
        {
            yield return name;
            yield break;
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            yield return Path.Combine(baseDir, name);
        }

        yield return name;
    }
}

internal static class SilkNetPathResolverOverride
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        var field = typeof(PathResolver).GetField("<Default>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new InvalidOperationException("Failed to locate PathResolver.Default backing field.");
        }

        field.SetValue(null, new AppBasePathResolver());
    }
}

