namespace Duxel.Core;

public enum UiFluentIconStyle
{
    Regular, Filled, Interactive
}
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class UiFluentIconAttribute : Attribute
{
    public UiFluentIconAttribute(string name, UiFluentIconStyle style = UiFluentIconStyle.Regular) : this(name, style, 0) { }
    public UiFluentIconAttribute(string name, UiFluentIconStyle style, int size)
    {
        Name = name;
        Style = style;
        Size = size;
    }
    public string Name
    {
        get;
    }
    public UiFluentIconStyle Style
    {
        get;
    }
    /// <summary>Zero selects the adaptive20/24 pair; a positive size pins that exact catalog glyph.</summary>
    public int Size
    {
        get;
    }
}

public interface IUiIconFontFace
{
    static abstract string Path
    {
        get;
    }
    static abstract float AscentEm
    {
        get;
    }
    static abstract float DescentEm
    {
        get;
    }
}
public readonly struct UiFluentRegularIconFont : IUiIconFontFace
{
    public static float AscentEm => 1;
    public static float DescentEm => 0;
    public static string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "FluentIcons", "FluentSystemIcons-Regular.ttf");
}
public readonly struct UiFluentFilledIconFont : IUiIconFontFace
{
    public static float AscentEm => 1;
    public static float DescentEm => 0;
    public static string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "FluentIcons", "FluentSystemIcons-Filled.ttf");
}

[UiFluentIcon("add")] public readonly partial struct UiFluentAddIcon;
[UiFluentIcon("add", UiFluentIconStyle.Filled)] public readonly partial struct UiFluentAddFilledIcon;
[UiFluentIcon("add", UiFluentIconStyle.Interactive)] public readonly partial struct UiFluentAddInteractiveIcon;

public sealed partial class UiImmediateContext
{
    internal void PaintFontIcon<TFont>(string glyph, UiRect bounds, UiColor color) where TFont : IUiIconFontFace
    {
        if (!_directTextEnabled || _platformTextBackend is null)
            throw new NotSupportedException("Font icons require an explicit DirectText backend.");
        if (_platformTextBackend is not IPlatformVectorTextBackend)
            throw new NotSupportedException("Font icons require a vector glyph backend.");
        var size = bounds.Height * _contentScale;
        if (!float.IsFinite(size) || size <= 0)
            throw new NotSupportedException("Font icon size is unsupported.");
        // The pinned Fluent fonts have unitsPerEm=500, ascent=500 and descent=0.
        using var scope = new UiControlTypographyScope(this, _directTextPrimaryFontPath, _directTextSecondaryFontPath, _pushedFontSize,
            _controlTypographyHeight, _controlTypographyMetrics, bounds.Height);
        _directTextPrimaryFontPath = TFont.Path;
        _directTextSecondaryFontPath = null;
        _pushedFontSize = size;
        _controlTypographyHeight = bounds.Height;
        _controlTypographyMetrics = new(size * TFont.AscentEm, size * TFont.DescentEm, 0);
        var previousVectorPaint = _fontIconVectorPaint;
        _fontIconVectorPaint = true;
        try
        {
            if (!TryMeasureDirectText(glyph, _textSettings, out var measured))
                throw new InvalidOperationException("Font icon glyph measurement failed.");
            PaintTypographyButtonText(glyph, bounds, measured, color);
        }
        finally { _fontIconVectorPaint = previousVectorPaint; }
    }
}
