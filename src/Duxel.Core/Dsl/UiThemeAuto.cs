using System;
using System.Threading;

namespace Duxel.Core.Dsl;

/// <summary>
/// Resolves theme files to <see cref="UiTheme"/> instances.
/// In NativeAOT builds, the source generator registers a resolver that maps
/// theme file names to pre-compiled themes. In managed builds, themes are
/// loaded at runtime from <c>.duxel-theme</c> files.
/// </summary>
public static class UiThemeAuto
{
    private static Func<string, UiTheme> s_resolver = DefaultResolver;

    public static UiTheme Resolve(string themeFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeFile);
        return s_resolver(themeFile);
    }

    public static void RegisterResolver(Func<string, UiTheme> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Interlocked.Exchange(ref s_resolver, resolver);
    }

    private static UiTheme DefaultResolver(string themeFile)
        => throw new NotSupportedException("Theme auto resolver is not registered. Ensure the source generator runs and .duxel-theme files are included as AdditionalFiles.");
}
