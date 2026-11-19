namespace Duxel.Core;

public abstract partial class UiWidget
{
    internal int RetainedTextLayoutCharacters => (_identity as IdentityCache)?.TextLayout?.Characters ?? 0;

    internal bool TryGetTextLayout(IPlatformTextBackend backend, ulong version, string font, string? secondary, string text,
        float size, out PlatformTextLayoutResult layout)
    {
        if (_identity is IdentityCache { TextLayout: { } memo } && memo.Size == size && memo.Version == version
            && memo.Backend.TryGetTarget(out var previous) && ReferenceEquals(previous, backend)
            && string.Equals(memo.Font, font, StringComparison.Ordinal)
            && string.Equals(memo.Secondary, secondary, StringComparison.Ordinal)
            && string.Equals(memo.Text, text, StringComparison.Ordinal))
        {
            layout = memo.Layout;
            return true;
        }
        layout = default;
        return false;
    }

    internal void RememberTextLayout(IPlatformTextBackend backend, ulong version, string font, string? secondary, string text,
        float size, PlatformTextLayoutResult layout)
    {
        if (_identity is not IdentityCache cache) return;
        // One metrics-only slot per scoped widget. Long/transient labels retain
        // the context cache policy rather than adding unbounded widget storage.
        if (text.Length > 256 || font.Length + (long)(secondary?.Length ?? 0) + text.Length > 1024)
        {
            if (cache.TextLayout is { } existing)
            { existing.Font = existing.Secondary = existing.Text = null; }
            return;
        }
        var memo = cache.TextLayout ??= new(backend);
        memo.Backend.SetTarget(backend);
        memo.Font = font; memo.Secondary = secondary; memo.Text = text;
        memo.Size = size; memo.Layout = layout; memo.Version = version;
    }

    internal sealed class TextLayoutMemo(IPlatformTextBackend backend)
    {
        internal readonly WeakReference<IPlatformTextBackend> Backend = new(backend);
        internal string? Font, Secondary, Text;
        internal float Size;
        internal ulong Version;
        internal PlatformTextLayoutResult Layout;
        internal int Characters => (Font?.Length ?? 0) + (Secondary?.Length ?? 0) + (Text?.Length ?? 0);
    }
}
