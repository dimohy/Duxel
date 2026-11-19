namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiVector2 MeasureLink<TPresentation>(string label, UiLinkContent<TPresentation> content, float width = 0) where TPresentation : IUiLinkPresentation =>
        _ui.MeasureLink(label, content, width);
}

public sealed partial class UiImmediateContext
{
    internal UiControlTextLayoutKey CurrentControlTextLayoutKey => new(_directTextPrimaryFontPath, _directTextSecondaryFontPath, _pushedFontSize, ControlTypographyLineHeight,
        _contentScale, _textSettings.Scale, _textSettings.LineHeightScale, _textSettings.PixelSnap, _textSettings.UseBaseline,
        _textSettings.UseFallbackGlyph, IsVectorTextLayer(), _directTextBackendGeneration);
    // A configured native text path does not consult the atlas missing-glyph observer.
    internal bool CanRetainControlTextLayout => (_textSettings.MissingGlyphObserver is null || _directTextEnabled && HasDirectTextBackend())
        && (_directTextPrimaryFontPath?.Length ?? 0) <= 1024 && (_directTextSecondaryFontPath?.Length ?? 0) <= 1024;
    public bool Link<TBehavior, TPresentation>(string label, Uri destination, Action<Uri> navigate, UiLinkContent<TPresentation> content, float width = 0)
        where TBehavior : IUiButtonBehavior where TPresentation : IUiLinkPresentation
        => Link<TBehavior, TPresentation>(label, destination, navigate, content, width, false);

    public bool Link<TBehavior, TPresentation>(string label, Uri destination, Action<Uri> navigate, UiLinkContent<TPresentation> content, float width, bool constrainLayout)
        where TBehavior : IUiButtonBehavior where TPresentation : IUiLinkPresentation
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(navigate);
        if (!Link<TBehavior, TPresentation>(label, label, content, width, constrainLayout))
            return false;
        navigate(destination);
        return true;
    }
    internal bool Link<TBehavior, TPresentation>(string id, string label, UiLinkContent<TPresentation> content, float width, bool constrainLayout = false)
        where TBehavior : IUiButtonBehavior where TPresentation : IUiLinkPresentation
    {
        EnsureNotMeasuring();
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(content);
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        var columns = _columnsActive;
        var table = _tableActive;
        try
        {
            var layout = ResolveLinkLayout(context, label, content, width, constrainLayout);
            var assigned = constrainLayout && _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth && _inputSlotBounds.HasValue;
            if (assigned)
            {
                _columnsActive = false;
                _tableActive = false;
            }
            var position = AdvanceCursor(layout.Size);
            var bounds = new UiRect(position.X, position.Y, layout.Size.X, layout.Size.Y);
            using var rootClip = assigned ? context.Clip(bounds) : default;
            var resolvedId = ResolveId(id);
            ClaimAccessibilityField(resolvedId);
            var interaction = TBehavior.Process(context, id, bounds);
            interaction = ApplyAccessibilityInvoke(resolvedId, bounds, interaction);
            if (interaction.Hovered && IsControlEnabled)
                SetMouseCursor(UiMouseCursor.Hand);
            TPresentation.Paint(context, layout, bounds, interaction);
            ExposeAccessibility(resolvedId, _accessibilityParent, UiAccessibilityRole.Link, label, bounds,
                readOnly: true, focusable: TBehavior.AccessibilityFocus, valuePattern: false);
            return interaction.Activated;
        }
        finally
        {
            _columnsActive = columns;
            _tableActive = table;
            content.EndFrame();
        }
    }

    internal UiVector2 MeasureLink<TPresentation>(string label, UiLinkContent<TPresentation> content, float width) where TPresentation : IUiLinkPresentation
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(content);
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        try
        {
            return ResolveLinkLayout(context, label, content, width, true).Size;
        }
        finally
        {
            content.EndFrame();
        }
    }

    private UiLinkLayout ResolveLinkLayout<TPresentation>(UiControlContext context, string label, UiLinkContent<TPresentation> content, float width, bool constrainLayout)
        where TPresentation : IUiLinkPresentation
    {
        if (constrainLayout && (IsMeasuring || _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth && _inputSlotBounds.HasValue))
            width = ConstrainLayoutWidth(width > 0 ? width : MathF.Max(0, GetContentRegionAvail().X));
        content.Measure(this, context, GetDisplayLabel(label), width, CurrentControlTextLayoutKey, CanRetainControlTextLayout);
        var natural = content.Layout;
        if (!constrainLayout)
            return natural;
        var allocated = ConstrainToLayout(natural.Size);
        if (allocated == natural.Size)
            return natural;
        var arranged = TPresentation.Arrange(context, natural, allocated);
        if (arranged.Size != allocated)
            throw new InvalidOperationException("Link presentation did not honor the parent allocation.");
        return arranged;
    }
    internal float ControlTypographyBaseline =>
        (_controlTypographyHeight - (_controlTypographyMetrics.Ascent + _controlTypographyMetrics.Descent) / _contentScale) / 2 + _controlTypographyMetrics.Ascent / _contentScale;
}

internal readonly record struct UiControlTextLayoutKey(string? Primary, string? Secondary, float FontSize, float Height,
    float ContentScale, float TextScale, float LineScale, bool PixelSnap, bool Baseline, bool FallbackGlyph, bool Vector, long Generation);
