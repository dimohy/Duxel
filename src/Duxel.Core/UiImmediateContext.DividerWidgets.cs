namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiVector2 MeasureDivider<TPresentation>(string? text = null, UiDividerOptions options = default) where TPresentation : IUiDividerPresentation =>
        _ui.MeasureDivider<TPresentation>(text ?? string.Empty, options);
    public UiVector2 MeasureFluentDivider(string text, UiDividerOptions options = default) =>
        _ui.LayoutFluentDivider(text, options, render : false);
}

public sealed partial class UiImmediateContext
{
    public void Divider(string? text = null, UiDividerOptions options = default) => Divider<UiFluentDividerPresentation>(text, options);
    public void Divider<TPresentation>(string? text = null, UiDividerOptions options = default) where TPresentation : IUiDividerPresentation
        => Divider<TPresentation>(text, options, false);

    public void Divider<TPresentation>(string? text, UiDividerOptions options, bool constrainLayout) where TPresentation : IUiDividerPresentation
    {
        EnsureNotMeasuring();
        options.Validate();
        text ??= string.Empty;
        var context = new UiControlContext(this);
        var assigned = constrainLayout && _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth
            ? _inputSlotBounds : null;
        var previousPlacements = _allocatedTextPlacements;
        var columns = _columnsActive;
        var table = _tableActive;
        _allocatedTextPlacements = assigned.HasValue && TPresentation.CanArrange ? 0 : -1;
        if (assigned.HasValue)
        {
            _columnsActive = false;
            _tableActive = false;
        }
        try
        {
            if (assigned is { } bounds && !TPresentation.CanArrange)
            {
                var natural = TPresentation.Measure(context, text, options);
                if (natural != new UiVector2(bounds.Width, bounds.Height))
                    throw new NotSupportedException("This Divider presentation must opt into CanArrange and place one common text root to accept a changed parent allocation.");
            }
            TPresentation.Render(context, text, options);
            if (assigned is { } root && TPresentation.CanArrange &&
                (_allocatedTextPlacements != 1 || _lastItemPos != new UiVector2(root.X, root.Y) || _lastItemSize != new UiVector2(root.Width, root.Height)))
                throw new InvalidOperationException("An allocated Divider presentation must place exactly one common root at the assigned bounds.");
        }
        finally
        {
            _allocatedTextPlacements = previousPlacements;
            _columnsActive = columns;
            _tableActive = table;
        }
    }

    internal UiVector2 MeasureDivider<TPresentation>(string text, UiDividerOptions options) where TPresentation : IUiDividerPresentation
    {
        options.Validate();
        var previous = _allocatedTextPlacements;
        _allocatedTextPlacements = TPresentation.CanArrange ? 0 : -1;
        try
        {
            var natural = TPresentation.Measure(new(this), text, options);
            if (!float.IsFinite(natural.X) || !float.IsFinite(natural.Y) || natural.X < 0 || natural.Y < 0)
                throw new InvalidOperationException("Divider presentation returned invalid extents.");
            var allocated = ConstrainToLayout(natural);
            if (allocated != natural && !TPresentation.CanArrange)
                throw new NotSupportedException("This Divider presentation must opt into CanArrange to accept a changed parent allocation.");
            return allocated;
        }
        finally
        {
            _allocatedTextPlacements = previous;
        }
    }

    internal void RenderFluentDivider(string text, UiDividerOptions options)
    {
        EnsureNotMeasuring();
        LayoutFluentDivider(text, options, render : true);
    }

    internal UiVector2 LayoutFluentDivider(string text, UiDividerOptions options, bool render)
    {
        options.Validate();
        var scale = _textSettings.Scale;
        if (!float.IsFinite(scale) || scale <= 0)
            throw new InvalidOperationException("Divider scale must be finite and positive.");
        var hasText = text.Length > 0;
        var physicalSize = 12 * scale * _contentScale;
        var vector = IsVectorTextLayer();
        var font = default(PlatformFontMetrics);
        UiTypographyFonts? fonts = null;
        if (hasText)
        {
            if (!_directTextEnabled || _platformTextBackend is not IPlatformFontMetricsBackend backend)
                throw new NotSupportedException("Divider content requires typography metrics.");
            fonts = _typographyFonts ?? throw new InvalidOperationException("Typography font faces have not been configured.");
            if (!float.IsFinite(physicalSize) || physicalSize <= 0 || !vector && physicalSize > 512)
                throw new NotSupportedException("Divider typography size is unsupported.");
            if (!backend.TryGetFontMetrics(fonts.Regular, vector ? physicalSize : MathF.Max(1, MathF.Round(physicalSize)), out font))
                throw new InvalidOperationException("Divider font metrics are unavailable.");
        }
        var previousPrimary = _directTextPrimaryFontPath;
        var previousSecondary = _directTextSecondaryFontPath;
        var previousSize = _pushedFontSize;
        if (fonts is not null)
        {
            _directTextPrimaryFontPath = fonts.Regular;
            _directTextSecondaryFontPath = fonts.SecondaryRegular;
            _pushedFontSize = physicalSize;
        }
        try
        {
            var inset = options.Inset ? 12 * scale : 0;
            var length = options.Length > 0 ? options.Length : options.Vertical ? (hasText ? 84 : 20) * scale : MathF.Max(0, GetContentRegionAvail().X);
            if (_allocatedTextPlacements >= 0 && !options.Vertical)
                length = ConstrainLayoutWidth(length);
            var normalized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
            if (hasText && !options.Vertical)
                normalized = WrapText(normalized, MathF.Max(1, length - 2 * inset - 40 * scale));
            var textWidth = 0f;
            var lines = 1;
            var start = 0;
            for (var i = 0; hasText && i <= normalized.Length; i++)
            {
                if (i < normalized.Length && normalized[i] != '\n')
                    continue;
                if (i > start)
                {
                    var line = start == 0 && i == normalized.Length ? normalized : normalized.Substring(start, i - start);
                    if (!TryMeasureDirectText(line, _textSettings, out var measured))
                        throw new InvalidOperationException("Divider content measurement failed.");
                    textWidth = MathF.Max(textWidth, measured.X);
                }
                if (i < normalized.Length)
                    lines++;
                start = i + 1;
            }
            var textHeight = hasText ? lines * 16 * scale : 0;
            if (options.Vertical)
                length = MathF.Max(length, hasText ? MathF.Max(84 * scale, textHeight + 40 * scale) : 20 * scale);
            var size = options.Vertical ? new UiVector2(MathF.Max(scale, textWidth), length + 2 * inset) : new UiVector2(length, MathF.Max(scale, textHeight));
            if (_allocatedTextPlacements >= 0)
            {
                size = ConstrainToLayout(size);
                length = options.Vertical ? MathF.Max(0, size.Y - 2 * inset) : size.X;
            }
            if (!render)
                return size;
            var layout = PlaceControlText(size);
            if (!layout.Visible)
                return size;
            var paint = UiFluentDividerPalette.Resolve(_theme.FluentColorScheme, options.Appearance);
            var major = options.Vertical ? length : MathF.Max(0, length - 2 * inset);
            var origin = options.Vertical ? layout.Position.Y + inset : layout.Position.X + inset;
            void Line(float position, float span)
            {
                if (span <= 0)
                    return;
                var rect = options.Vertical ? new UiRect(layout.Position.X + (size.X - scale) / 2, position, scale, span)
                    : new UiRect(position, layout.Position.Y + (size.Y - scale) / 2, span, scale);
                _builder.AddRectFilled(rect, paint.Line, _whiteTexture, layout.ClipRect);
            }
            if (!hasText)
            {
                Line(origin, major);
                return size;
            }
            var contentMajor = options.Vertical ? textHeight : textWidth;
            var alignment = options.Alignment;
            if (!options.Vertical && options.RightToLeft)
                alignment = alignment == UiDividerAlignment.Start ? UiDividerAlignment.End : alignment == UiDividerAlignment.End ? UiDividerAlignment.Start : alignment;
            var free = major - contentMajor - 24 * scale;
            var before = alignment == UiDividerAlignment.Start ? 8 * scale : alignment == UiDividerAlignment.End ? MathF.Max(8 * scale, free - 8 * scale) : MathF.Max(8 * scale, free / 2);
            var after = MathF.Max(8 * scale, free - before);
            Line(origin, before);
            Line(origin + before + 24 * scale + contentMajor, after);
            var contentPosition = options.Vertical ? new UiVector2(layout.Position.X + (size.X - textWidth) / 2, origin + before + 12 * scale)
                : new UiVector2(origin + before + 12 * scale, layout.Position.Y + (size.Y - textHeight) / 2);
            var leading = (16 * scale - (font.Ascent + font.Descent) / _contentScale) / 2;
            start = 0;
            var row = 0;
            for (var i = 0; i <= normalized.Length; i++)
            {
                if (i < normalized.Length && normalized[i] != '\n')
                    continue;
                if (i > start)
                {
                    var line = start == 0 && i == normalized.Length ? normalized : normalized.Substring(start, i - start);
                    float ascent;
                    if (vector)
                        ascent = GetVectorTextMesh(line, (int)MathF.Round(physicalSize * 4), physicalSize).Baseline;
                    else
                    {
                        if (!TryGetOrCreateDirectText(line, physicalSize, out var entry))
                            throw new InvalidOperationException("Divider glyph data is unavailable.");
                        ascent = entry.FontAscent;
                    }
                    if (!TryMeasureDirectText(line, _textSettings, out var measured))
                        throw new InvalidOperationException("Divider line measurement failed.");
                    var textAlignment = options.Vertical ? UiDividerAlignment.Center : alignment;
                    var offset = textAlignment == UiDividerAlignment.Center ? (textWidth - measured.X) / 2 : textAlignment == UiDividerAlignment.End ? textWidth - measured.X : 0;
                    var position = new UiVector2(contentPosition.X + offset, contentPosition.Y + row * 16 * scale + leading + (font.Ascent - ascent) / _contentScale);
                    if (!TryRenderDirectText(_builder, line, position, paint.Content, layout.ClipRect, _textSettings, out _))
                        throw new InvalidOperationException("Divider text rendering failed.");
                }
                start = i + 1;
                row++;
            }
            return size;
        }
        finally
        {
            _directTextPrimaryFontPath = previousPrimary;
            _directTextSecondaryFontPath = previousSecondary;
            _pushedFontSize = previousSize;
        }
    }
}
