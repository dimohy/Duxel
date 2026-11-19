namespace Duxel.Core;

/// <summary>A value-type font scope selected by a compiled control presentation.</summary>
public readonly struct UiControlTypographyScope : IDisposable
{
    private readonly UiImmediateContext? _ui;
    private readonly string? _primary, _secondary;
    private readonly float _size, _height;
    private readonly PlatformFontMetrics _metrics;
    public float LineHeight
    {
        get;
    }
    internal UiControlTypographyScope(float height)
    {
        LineHeight = height;
    }
    internal UiControlTypographyScope(UiImmediateContext ui, string? primary, string? secondary, float size, float height, PlatformFontMetrics metrics, float lineHeight)
    {
        _ui = ui;
        _primary = primary;
        _secondary = secondary;
        _size = size;
        _height = height;
        _metrics = metrics;
        LineHeight = lineHeight;
    }
    public void Dispose() => _ui?.RestoreControlTypography(_primary, _secondary, _size, _height, _metrics);
}

public sealed partial class UiImmediateContext
{
    private float _controlTypographyHeight;
    private PlatformFontMetrics _controlTypographyMetrics;
    private struct ControlMetricEntry
    {
        internal string? Font; internal float Size; internal long Generation; internal PlatformFontMetrics Metrics;
    }
    [System.Runtime.CompilerServices.InlineArray(4)]
    private struct ControlMetricSlots
    {
        private ControlMetricEntry _element0;
    }
    private ControlMetricSlots _controlMetricSlots;
    private int _controlMetricLast, _controlMetricNext;
    internal int ControlTypographyRetainedCharacters
    {
        get
        {
            var count = 0;
            for (var i = 0; i < 4; i++)
                count += _controlMetricSlots[i].Font?.Length ?? 0;
            return count;
        }
    }
    internal void ReleaseControlTypographyResources()
    {
        _controlMetricSlots = default;
        _controlMetricLast = _controlMetricNext = 0;
        _controlTypographyHeight = 0;
        _controlTypographyMetrics = default;
    }
    private bool TryGetControlFontMetrics(string font, float size, out PlatformFontMetrics metrics)
    {
        ref var last = ref _controlMetricSlots[_controlMetricLast];
        if (last.Generation == _directTextBackendGeneration && last.Size == size && string.Equals(last.Font, font, StringComparison.Ordinal))
        {
            metrics = last.Metrics;
            return true;
        }
        for (var i = 0; i < 4; i++)
        {
            ref var entry = ref _controlMetricSlots[i];
            if (entry.Generation == _directTextBackendGeneration && entry.Size == size && string.Equals(entry.Font, font, StringComparison.Ordinal))
            {
                _controlMetricLast = i;
                metrics = entry.Metrics;
                return true;
            }
        }
        metrics = default;
        return false;
    }

    internal UiControlTypographyScope BeginControlTypography<TTypeface, TMetrics>(bool hasText, float scale = 1)
        where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics
    {
        var height = TMetrics.LineHeight * _textSettings.Scale * scale;
        var physicalSize = TMetrics.FontSize * _textSettings.Scale * _contentScale * scale;
        var vector = IsVectorTextLayer();
        if (!float.IsFinite(height) || height <= 0 || !float.IsFinite(physicalSize) || physicalSize <= 0 || !vector && physicalSize > 512)
            throw new NotSupportedException("Control typography size is unsupported.");
        if (!hasText)
            return new(height);
        if (!_directTextEnabled || _platformTextBackend is not IPlatformFontMetricsBackend backend)
            throw new NotSupportedException("Fluent control text requires DirectText and a font-metrics backend.");
        var fonts = _typographyFonts ?? throw new InvalidOperationException("Typography font faces have not been configured.");
        var paths = TTypeface.Select(fonts);
        var metricSize = vector ? physicalSize : MathF.Max(1, MathF.Round(physicalSize));
        PlatformFontMetrics metrics;
        if (TryGetControlFontMetrics(paths.Primary, metricSize, out var cached))
            metrics = cached;
        else
        {
            if (!backend.TryGetFontMetrics(paths.Primary, metricSize, out metrics))
                throw new InvalidOperationException("Control font metrics are unavailable.");
            if (paths.Primary.Length <= 1024)
            {
                _controlMetricSlots[_controlMetricNext] = new()
                {
                    Font = paths.Primary,
                    Size = metricSize,
                    Generation = _directTextBackendGeneration,
                    Metrics = metrics
                };
                _controlMetricLast = _controlMetricNext;
                _controlMetricNext = (_controlMetricNext + 1) & 3;
            }
        }
        var scope = new UiControlTypographyScope(this, _directTextPrimaryFontPath, _directTextSecondaryFontPath, _pushedFontSize,
            _controlTypographyHeight, _controlTypographyMetrics, height);
        _directTextPrimaryFontPath = paths.Primary;
        _directTextSecondaryFontPath = paths.Secondary;
        _pushedFontSize = physicalSize;
        _controlTypographyHeight = height;
        _controlTypographyMetrics = metrics;
        return scope;
    }
    internal void RestoreControlTypography(string? primary, string? secondary, float size, float height, PlatformFontMetrics metrics)
    {
        _directTextPrimaryFontPath = primary;
        _directTextSecondaryFontPath = secondary;
        _pushedFontSize = size;
        _controlTypographyHeight = height;
        _controlTypographyMetrics = metrics;
    }

    internal void PaintTypographyButtonText(string text, UiRect bounds, UiVector2 textSize, UiColor color, float horizontalOffset = 0)
    {
        if (text.Length == 0)
            return;
        if (_controlTypographyHeight <= 0)
            throw new InvalidOperationException("The compiled presentation must enter its typography scope before painting text.");
        var clip = IntersectRect(ResolveItemClipRect(), bounds);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        var leading = (_controlTypographyHeight - (_controlTypographyMetrics.Ascent + _controlTypographyMetrics.Descent) / _contentScale) / 2;
        var position = new UiVector2(bounds.X + (bounds.Width - textSize.X) / 2 + horizontalOffset, bounds.Y + (bounds.Height - _controlTypographyHeight) / 2 + leading);
        PaintControlTypographyRun(text, position, color, clip);
    }
    private void PaintControlTypographyRun(string text, UiVector2 position, UiColor color, UiRect clip)
    {
        if (!IsPresentationPass)
            return;
        if (IsVectorTextLayer())
        {
            var mesh = GetVectorTextMesh(text, (int)MathF.Round(_pushedFontSize * 4), _pushedFontSize);
            position = position with
            {
                Y = position.Y + (_controlTypographyMetrics.Ascent - mesh.Baseline) / _contentScale
            };
        }
        else
        {
            if (!TryGetOrCreateDirectText(text, _pushedFontSize, out var entry))
                throw new InvalidOperationException("Control glyph data is unavailable.");
            position = position with
            {
                Y = position.Y + (_controlTypographyMetrics.Ascent - entry.FontAscent) / _contentScale
            };
        }
        if (!TryRenderDirectText(_builder, text, position, color, clip, _textSettings, out _))
            throw new InvalidOperationException("Control text rendering failed.");
    }
}
