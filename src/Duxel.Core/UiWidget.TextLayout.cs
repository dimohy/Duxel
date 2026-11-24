namespace Duxel.Core;

public abstract partial class UiWidget
{
    internal int RetainedTextLayoutCharacters => (_identity as IdentityCache)?.TextLayout?.Characters ?? 0;

    internal bool TryGetTextLayout(IPlatformTextBackend backend, ulong version, in PlatformTextMeasureRequest request,
        out PlatformTextLayoutResult layout)
    {
        if (_identity is IdentityCache { TextLayout: { } memo } && memo.Size == request.FontSize && memo.Version == version
            && memo.RightToLeft == request.RightToLeft
            && memo.Backend.TryGetTarget(out var previous) && ReferenceEquals(previous, backend)
            && string.Equals(memo.Font, request.FontPath, StringComparison.Ordinal)
            && string.Equals(memo.Secondary, request.SecondaryFontPath, StringComparison.Ordinal)
            && string.Equals(memo.Text, request.Text, StringComparison.Ordinal))
        {
            layout = memo.Layout;
            return true;
        }
        layout = default;
        return false;
    }

    internal void RememberTextLayout(IPlatformTextBackend backend, ulong version, in PlatformTextMeasureRequest request,
        PlatformTextLayoutResult layout)
    {
        if (_identity is not IdentityCache cache) return;
        // One metrics-only slot per scoped widget. Long/transient labels retain
        // the context cache policy rather than adding unbounded widget storage.
        if (request.Text.Length > 256 || request.FontPath.Length + (long)(request.SecondaryFontPath?.Length ?? 0) + request.Text.Length > 1024)
        {
            if (cache.TextLayout is { } existing)
            { existing.Font = existing.Secondary = existing.Text = null; }
            return;
        }
        var memo = cache.TextLayout ??= new(backend);
        memo.Backend.SetTarget(backend);
        memo.Font = request.FontPath; memo.Secondary = request.SecondaryFontPath; memo.Text = request.Text;
        memo.Size = request.FontSize; memo.Layout = layout; memo.Version = version;
        memo.RightToLeft = request.RightToLeft;
    }

    internal sealed class TextLayoutMemo(IPlatformTextBackend backend)
    {
        internal readonly WeakReference<IPlatformTextBackend> Backend = new(backend);
        internal string? Font, Secondary, Text;
        internal float Size;
        internal ulong Version;
        internal bool RightToLeft;
        internal PlatformTextLayoutResult Layout;
        internal int Characters => (Font?.Length ?? 0) + (Secondary?.Length ?? 0) + (Text?.Length ?? 0);
    }
}
