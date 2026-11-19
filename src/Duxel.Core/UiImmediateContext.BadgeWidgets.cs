namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiVector2 MeasureBadge<TPresentation>(UiBadgeContent content, UiFluentBadgeOptions options = default) where TPresentation : IUiBadgePresentation =>
        _ui.MeasureBadge<TPresentation>(content, options);
    public UiVector2 MeasureFluentBadge<TIcon>(UiBadgeContent content, UiFluentBadgeOptions options = default) where TIcon : IUiBadgeIcon =>
        _ui.LayoutFluentBadge<TIcon>(content, options, render: false);
    /// <summary>Paints a decorative badge inside existing control bounds without placing another layout item.</summary>
    public void PaintFluentBadge(UiRect bounds, UiBadgeContent content, UiFluentBadgeOptions options = default)
        => _ui.PaintFluentBadgeAt(bounds, content, options);
}

public readonly record struct UiBadgeMetrics(float Height, float Padding, float FontSize, float LineHeight, float IconSize)
{
    public static UiBadgeMetrics FromSize(UiBadgeSize size) => size switch
    {
        UiBadgeSize.Tiny => new(6, 0, 4, 4, 6),
        UiBadgeSize.ExtraSmall => new(10, 0, 6, 6, 10),
        UiBadgeSize.Small => new(16, 4, 10, 14, 12),
        UiBadgeSize.Medium => new(20, 6, 12, 16, 12),
        UiBadgeSize.Large => new(24, 6, 12, 16, 16),
        UiBadgeSize.ExtraLarge => new(32, 8, 12, 16, 20),
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
}

public sealed partial class UiImmediateContext
{
    public void Badge(string text, UiFluentBadgeOptions options = default) => Badge<UiFluentBadgePresentation>(new(text ?? string.Empty), options);

    public void CounterBadge(UiCounterBadgeValue value, ref UiBadgeTextCache cache, UiFluentBadgeOptions options = default)
    {
        EnsureNotMeasuring();
        UiCounterBadgeBehavior.ValidateOptions(options);
        Badge<UiFluentBadgePresentation>(UiCounterBadgeBehavior.Resolve(value, ref cache), options);
    }
    public void Badge<TPresentation>(UiBadgeContent content, UiFluentBadgeOptions options = default) where TPresentation : IUiBadgePresentation
        => Badge<TPresentation>(content, options, false);

    public void Badge<TPresentation>(UiBadgeContent content, UiFluentBadgeOptions options, bool constrainLayout) where TPresentation : IUiBadgePresentation
    {
        EnsureNotMeasuring();
        options.Validate();
        var context = new UiControlContext(this);
        var assigned = constrainLayout && _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth
            ? _inputSlotBounds : null;
        var previousPlacements = _allocatedTextPlacements;
        var columns = _columnsActive;
        var table = _tableActive;
        _allocatedTextPlacements = assigned.HasValue && content.Visible && TPresentation.CanArrange ? 0 : -1;
        if (assigned.HasValue)
        {
            _columnsActive = false;
            _tableActive = false;
        }
        try
        {
            if (!content.Visible)
            {
                if (assigned is { } hidden)
                    AdvanceCursor(new(hidden.Width, hidden.Height));
                return;
            }
            if (assigned is { } bounds && !TPresentation.CanArrange)
            {
                var natural = TPresentation.Measure(context, content, options);
                if (natural != new UiVector2(bounds.Width, bounds.Height))
                    throw new NotSupportedException("This Badge presentation must opt into CanArrange and place one common text root to accept a changed parent allocation.");
            }
            TPresentation.Render(context, content, options);
            if (assigned is { } root && TPresentation.CanArrange &&
                (_allocatedTextPlacements != 1 || _lastItemPos != new UiVector2(root.X, root.Y) || _lastItemSize != new UiVector2(root.Width, root.Height)))
                throw new InvalidOperationException("An allocated Badge presentation must place exactly one common root at the assigned bounds.");
        }
        finally
        {
            _allocatedTextPlacements = previousPlacements;
            _columnsActive = columns;
            _tableActive = table;
        }
    }

    internal UiVector2 MeasureBadge<TPresentation>(UiBadgeContent content, UiFluentBadgeOptions options) where TPresentation : IUiBadgePresentation
    {
        options.Validate();
        if (!content.Visible)
            return ConstrainToLayout(default);
        var previous = _allocatedTextPlacements;
        _allocatedTextPlacements = TPresentation.CanArrange ? 0 : -1;
        try
        {
            var natural = TPresentation.Measure(new(this), content, options);
            if (!float.IsFinite(natural.X) || !float.IsFinite(natural.Y) || natural.X < 0 || natural.Y < 0)
                throw new InvalidOperationException("Badge presentation returned invalid extents.");
            var allocated = ConstrainToLayout(natural);
            if (allocated != natural && !TPresentation.CanArrange)
                throw new NotSupportedException("This Badge presentation must opt into CanArrange to accept a changed parent allocation.");
            return allocated;
        }
        finally
        {
            _allocatedTextPlacements = previous;
        }
    }

    internal void RenderFluentBadge<TIcon>(UiBadgeContent content, UiFluentBadgeOptions options) where TIcon : IUiBadgeIcon
    {
        EnsureNotMeasuring();
        LayoutFluentBadge<TIcon>(content, options, render: true);
    }

    internal void PaintFluentBadgeAt(UiRect bounds, UiBadgeContent content, UiFluentBadgeOptions options)
    {
        EnsureNotMeasuring();
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)
            || bounds.Width < 0 || bounds.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        LayoutFluentBadge<UiNoBadgeIcon>(content, options, render: true, bounds);
    }

    internal UiVector2 LayoutFluentBadge<TIcon>(UiBadgeContent content, UiFluentBadgeOptions options, bool render,
        UiRect? paintBounds = null) where TIcon : IUiBadgeIcon
    {
        options.Validate();
        if (!content.Visible)
            return default;
        var text = content.Text ?? string.Empty;
        if (TIcon.IsPresent && text.Length == 0 && string.IsNullOrWhiteSpace(content.AccessibleLabel))
            throw new ArgumentException("An icon-only badge requires an accessible label.", nameof(content));
        var metrics = UiBadgeMetrics.FromSize(options.Size);
        var scale = _textSettings.Scale;
        var physicalSize = metrics.FontSize * scale * _contentScale;
        if (!float.IsFinite(physicalSize) || physicalSize <= 0)
            throw new InvalidOperationException("Badge typography scale must be finite and positive.");
        var vector = IsVectorTextLayer();
        var font = default(PlatformFontMetrics);
        UiTypographyFonts? fonts = null;
        if (text.Length > 0)
        {
            if (!_directTextEnabled || _platformTextBackend is not IPlatformFontMetricsBackend fontMetrics)
                throw new NotSupportedException("Text badges require DirectText and a font-metrics backend.");
            fonts = _typographyFonts ?? throw new InvalidOperationException("Typography font faces have not been configured.");
            if (!vector && physicalSize > 512)
                throw new NotSupportedException("Badge raster typography exceeds512 physical pixels.");
            if (!fontMetrics.TryGetFontMetrics(fonts.Semibold, vector ? physicalSize : MathF.Max(1, MathF.Round(physicalSize)), out font))
                throw new InvalidOperationException("Badge font metrics are unavailable.");
        }
        var previousPrimary = _directTextPrimaryFontPath;
        var previousSecondary = _directTextSecondaryFontPath;
        var previousSize = _pushedFontSize;
        if (fonts is not null)
        {
            _directTextPrimaryFontPath = fonts.Semibold;
            _directTextSecondaryFontPath = fonts.SecondarySemibold;
            _pushedFontSize = physicalSize;
        }
        try
        {
            var measured = default(UiVector2);
            if (text.Length > 0 && !TryMeasureDirectText(text, _textSettings, out measured))
                throw new InvalidOperationException("Badge measurement failed.");
            var textWidth = measured.X;
            var hasText = text.Length > 0;
            var icon = TIcon.IsPresent;
            var iconWidth = metrics.IconSize * scale;
            var negativeMargin = -2 * scale;
            var gap = (options.Size == UiBadgeSize.ExtraLarge ? 6 : 4) * scale;
            var iconContribution = icon ? iconWidth + negativeMargin + (hasText ? gap : negativeMargin) : 0;
            var contentWidth = textWidth + iconContribution;
            var fixedWidth = content.Dot || options.Size is UiBadgeSize.Tiny or UiBadgeSize.ExtraSmall;
            var height = (content.Dot ? 6 : metrics.Height) * scale;
            var padding = content.Dot ? 0 : metrics.Padding * scale;
            var width = fixedWidth ? height : MathF.Max(height, contentWidth + padding * 2);
            var size = paintBounds is { } allocated ? new(allocated.Width, allocated.Height)
                : _allocatedTextPlacements >= 0 ? ConstrainToLayout(new(width, height)) : new UiVector2(width, height);
            if (!render)
                return size;
            UiRect bounds, textClip;
            if (paintBounds is { } adornment)
            {
                bounds = adornment;
                textClip = IntersectRect(CurrentClipRect, bounds);
                if (textClip.Width <= 0 || textClip.Height <= 0)
                    return size;
            }
            else
            {
                var layout = PlaceControlText(size);
                if (!layout.Visible)
                    return size;
                bounds = new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y);
                textClip = layout.ClipRect;
            }
            width = bounds.Width;
            height = bounds.Height;
            using var rootClip = paintBounds.HasValue || _allocatedTextPlacements >= 0 ? new UiControlContext(this).Clip(bounds) : default;
            var paint = UiFluentBadgePalette.Resolve(_theme.BadgeColorScheme, options.Appearance, options.Color);
            if (_theme.ForcedColors is { } system)
                paint = new(new UiColor((system.Canvas.Rgba & 0x00ffffffu) | (paint.Background.Rgba & 0xff000000u)),
                    system.CanvasText, system.CanvasText);
            var radius = options.Shape switch
            {
                UiBadgeShape.Circular => height / 2,
                UiBadgeShape.Rounded => (options.Size is UiBadgeSize.Tiny or UiBadgeSize.ExtraSmall or UiBadgeSize.Small ? 2 : 4) * scale,
                UiBadgeShape.Square => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(options))
            };
            if ((paint.Background.Rgba >> 24) != 0 || (paint.Border.Rgba >> 24) != 0)
                AddDesignedFrame(bounds, paint.Background, paint.Border, radius,
                    options.Appearance == UiBadgeAppearance.Ghost && _theme.ForcedColors is null ? 0 : scale);
            var start = bounds.X + (width - contentWidth) / 2;
            var textX = start;
            if (icon)
            {
                var iconX = options.IconPosition == UiBadgeIconPosition.Before ? start + negativeMargin : start + textWidth + (hasText ? gap : negativeMargin);
                if (options.IconPosition == UiBadgeIconPosition.Before)
                    textX = start + iconContribution;
                TIcon.Paint(new(this), new(iconX, bounds.Y + (height - iconWidth) / 2, iconWidth, iconWidth), paint.Foreground);
            }
            if (hasText)
            {
                float ascent;
                if (vector)
                    ascent = GetVectorTextMesh(text, (int)MathF.Round(physicalSize * 4), physicalSize).Baseline;
                else
                {
                    if (!TryGetOrCreateDirectText(text, physicalSize, out var entry))
                        throw new InvalidOperationException("Badge glyph data is unavailable.");
                    ascent = entry.FontAscent;
                }
                var baseline = bounds.Y + (height - (font.Ascent + font.Descent) / _contentScale) / 2 + font.Ascent / _contentScale;
                AddTextInternal(_builder, text, new(textX, baseline - ascent / _contentScale), paint.Foreground, textClip, _textSettings, _lineHeight);
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
