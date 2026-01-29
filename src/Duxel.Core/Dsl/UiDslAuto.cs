using System;
using System.Threading;

namespace Duxel.Core.Dsl;

public static class UiDslAuto
{
    private static Func<string, Action<IUiDslEmitter>> s_resolver = DefaultResolver;

    public static Action<IUiDslEmitter> Render(string uiFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uiFile);
        return s_resolver(uiFile);
    }

    public static void RegisterResolver(Func<string, Action<IUiDslEmitter>> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Interlocked.Exchange(ref s_resolver, resolver);
    }

    private static Action<IUiDslEmitter> DefaultResolver(string uiFile)
        => throw new NotSupportedException("UI DSL auto renderer is not generated. Ensure the source generator runs and .ui files are included as AdditionalFiles.");
}

