namespace Duxel.Core;

public enum UiSvgIconColorMode
{
    Authored,
    MultiplyForeground
}

/// <summary>Owns a regular/filled SVG painter pair; borrows documents and reuses the UiSvg texture path.</summary>
public sealed class UiSvgIcon : IDisposable
{
    private readonly UiSvg _regular;
    private readonly UiSvg _filled;
    private readonly UiSvgIconColorMode _colorMode;
    private bool _disposed;
    public UiMenuIcon MenuIcon { get; }
    public UiTabIcon TabIcon { get; }

    public UiSvgIcon(IUiSvgDocument document, UiSvgIconColorMode colorMode = UiSvgIconColorMode.Authored)
        : this(document, document, colorMode) { }

    public UiSvgIcon(IUiSvgDocument regular, IUiSvgDocument filled,
        UiSvgIconColorMode colorMode = UiSvgIconColorMode.Authored)
    {
        ArgumentNullException.ThrowIfNull(regular);
        ArgumentNullException.ThrowIfNull(filled);
        if ((uint)colorMode > 1) throw new ArgumentOutOfRangeException(nameof(colorMode));
        _regular = new(regular);
        _filled = ReferenceEquals(regular, filled) ? _regular : new(filled);
        _colorMode = colorMode;
        UiCompiledIconPainter painter = Paint;
        MenuIcon = new(painter, ReadContentVersion);
        TabIcon = new(painter);
    }

    private ulong ReadContentVersion()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return unchecked((ulong)_regular.Document.Revision);
    }

    private void Paint(UiControlContext context, UiRect bounds, UiColor color, bool filled, UiButtonInteraction interaction)
    {
        var svg = filled ? _filled : _regular;
        // Foreground modulation is explicit; it does not rewrite SVG fills or currentColor.
        context.PaintSvg(svg, bounds, _colorMode == UiSvgIconColorMode.MultiplyForeground ? color : null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _regular.Dispose();
        _filled.Dispose();
    }
}
