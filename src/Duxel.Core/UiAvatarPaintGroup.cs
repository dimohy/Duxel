namespace Duxel.Core;

/// <summary>Owns the inactive visual as one composite, independently of layout and accessibility.</summary>
internal sealed class UiAvatarPaintGroup<TPresentation> : IDisposable where TPresentation : IUiAvatarPresentation
{
    private readonly UiPaintGroup _group = new();
    private readonly List<Binding> _bindings = [];
    private readonly Action<UiControlContext> _paint;
    private UiRect _bounds;
    private UiAvatarContent _content;
    private UiAvatarOptions _options;
    private ulong _nextVersion;

    internal UiAvatarPaintGroup() => _paint = PaintContent;
    private void PaintContent(UiControlContext context) => TPresentation.Paint(context, _bounds, _content, _options);

    private readonly record struct PaintKey(UiAvatarContent Content, UiImageSource? Source, UiAvatarOptions Options,
        UiLayoutMeasurementKey Typography, bool Enabled, ulong IconVersion);

    private sealed class Binding(UiImmediateContext ui)
    {
        internal readonly WeakReference<UiImmediateContext> Context = new(ui);
        internal PaintKey Key;
        internal UiTheme Theme;
        internal bool Valid;
        internal int Frame = int.MinValue;
        internal ulong Version;
    }

    internal void Paint(UiImmediateContext ui, UiControlContext context, UiRect bounds,
        UiAvatarContent content, UiAvatarOptions options)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        Binding? binding = null;
        for (var index = _bindings.Count - 1; index >= 0; index--)
        {
            var candidate = _bindings[index];
            if (!candidate.Context.TryGetTarget(out var owner))
                _bindings.RemoveAt(index);
            else if (ReferenceEquals(owner, ui))
                binding = candidate;
        }
        if (binding is null)
            _bindings.Add(binding = new(ui));
        // An opaque custom painter makes no immutability promise: evaluate it on each requested frame.
        // SVG painters expose their document revision. Explicit icon versions opt other painters into reuse.
        ulong iconVersion = 0;
        var retainedIcon = content.Image is not null || content.Initials.Length > 0
            || content.Icon.TryGetContentVersion(out iconVersion);
        var key = new PaintKey(content, content.Image?.Source, options, context.LayoutMeasurementKey,
            context.IsEnabled, iconVersion);
        var theme = context.Theme;
        var retain = TPresentation.CanRetainPaint && retainedIcon;
        if (!binding.Valid || binding.Key != key || !SameTheme(binding.Theme, theme)
            || (!retain && binding.Frame != context.LayoutFrame))
        {
            binding.Version = checked(++_nextVersion);
            binding.Key = key;
            binding.Theme = theme;
            binding.Valid = true;
        }
        binding.Frame = context.LayoutFrame;
        _bounds = bounds;
        _content = content;
        _options = options;
        var destination = new UiRect(bounds.X + bounds.Width * .0625f, bounds.Y + bounds.Height * .0625f,
            bounds.Width * .875f, bounds.Height * .875f);
        context.PaintGroup(_group, bounds, binding.Version, _paint, .8f, destination, .875f, UiPaintGroupTextMode.Projected);
    }

    // UiTheme contains an InlineArray, whose default ValueType.Equals is unsupported.
    private static bool SameTheme(in UiTheme first, in UiTheme second)
    {
        if (first.FluentColorScheme != second.FluentColorScheme || first.ForcedColors != second.ForcedColors)
            return false;
        for (var index = 0; index < UiThemeColors.StyleColorCount; index++)
            if (first[(UiStyleColor)index] != second[(UiStyleColor)index])
                return false;
        return true;
    }

    public void Dispose()
    {
        _group.Dispose();
        _bindings.Clear();
        _content = default;
    }
}
