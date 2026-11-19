using System.Globalization;

namespace Duxel.Core;

internal readonly record struct UiTreeLabelLayout(UiVector2 Size, UiTreeOptions Options, bool Truncated);

/// <summary>Uses the shared styled paragraph shaper for wrapping, bidi and size-dependent Fluent typography.</summary>
internal sealed class UiFluentTreeLabel(string text, Func<UiTreeOptions> options) : UiTreeLabel(text, options)
{
    private UiInlineTextSource? _full, _display, _probe;
    private readonly UiVisualTextLines _fullLines = new(), _displayLines = new(), _probeLines = new();
    private UiTreeSize? _size;
    private int[]? _elements;
    private UiRetainedLayout<UiTreeLabelLayout>? _retained;
    private bool _truncated;
    internal string? AccessibilityIdentity;
    internal UiInlineTextSource Source => _display!;
    internal UiVisualTextLines Lines => _displayLines;
    internal string DisplayText => _display?.Source ?? string.Empty;
    public override bool IsTruncated => _truncated;
    protected override bool CanRetainMeasurement => true;
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();

    private void PrepareSources(UiTreeOptions value)
    {
        if (_size != value.Size)
        {
            _full?.Clear();
            _display?.Clear();
            _probe?.Clear();
            _fullLines.Clear();
            _displayLines.Clear();
            _probeLines.Clear();
            var flags = value.Size == UiTreeSize.Small ? UiStyledTextFlags.Caption : UiStyledTextFlags.None;
            _full = new(flags);
            _display = new(flags);
            _probe = new(flags);
            _full.Replace([new(Text)]);
            _size = value.Size;
        }
        if (_full!.RightToLeft != value.RightToLeft)
            _full.SetRightToLeft(value.RightToLeft);
        if (_display!.RightToLeft != value.RightToLeft)
            _display.SetRightToLeft(value.RightToLeft);
        if (_probe!.RightToLeft != value.RightToLeft)
            _probe.SetRightToLeft(value.RightToLeft);
    }

    internal UiColor Foreground(UiControlContext context)
    {
        var visual = !context.IsEnabled ? UiButtonVisualState.Disabled : Interaction.Held ? UiButtonVisualState.Pressed
            : Interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        return UiFluentTreePresentation.Colors(context, visual, Options.Appearance).Foreground;
    }

    private float Probe(UiControlContext context, string value)
    {
        _probe!.Replace([new(value)]);
        _ = context.MeasureInlineText(_probe, _probeLines, wrap: false);
        return _probeLines.Width;
    }

    private string Fit(UiControlContext context, float width)
    {
        const string suffix = "…";
        if (Probe(context, suffix) > width)
            return string.Empty;
        _elements ??= StringInfo.ParseCombiningCharacters(Text);
        var low = 0;
        var high = _elements.Length;
        while (low < high)
        {
            var count = low + (high - low + 1) / 2;
            var end = count == _elements.Length ? Text.Length : _elements[count];
            if (Probe(context, string.Concat(Text.AsSpan(0, end), suffix)) <= width)
                low = count;
            else
                high = count - 1;
        }
        var cut = low == _elements.Length ? Text.Length : _elements[low];
        return string.Concat(Text.AsSpan(0, cut), suffix);
    }

    internal UiTreeLabelLayout GetLayout(UiControlContext context, bool render)
    {
        var value = LayoutOptions(context);
        PrepareSources(value);
        if (_retained is { } previous && previous.CachedResult.Options == value
            && previous.TryGet(context, render, out var cached))
        {
            _truncated = cached.Truncated;
            return cached;
        }
        var retained = _retained ??= new();
        var attempt = retained.Begin(context);
        _ = context.MeasureInlineText(_full!, _fullLines, wrap: false);
        var width = context.LayoutConstraints.MaxWidth;
        var truncated = value.TextOverflow == UiTreeTextOverflow.Ellipsis && _fullLines.Width > width;
        var display = width <= 0 ? string.Empty : truncated ? Fit(context, width) : Text;
        _display!.Replace([new(display)]);
        if (display.Length == 0)
            _displayLines.Clear();
        else
            _ = context.MeasureInlineText(_display, _displayLines, value.TextOverflow == UiTreeTextOverflow.Wrap);
        var natural = new UiVector2(_displayLines.Width, MathF.Max(_displayLines.Height, _fullLines.Height));
        var result = new UiTreeLabelLayout(context.LayoutConstraints.Constrain(natural), value, truncated);
        retained.Commit(context, attempt, result, result.Size, retainMeasurement: true);
        _truncated = truncated;
        return result;
    }

    protected override UiVector2 MeasureContent(UiControlContext context) => GetLayout(context, render: false).Size;
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderFluentTreeLabel(this);
    private protected override void ReleaseOwnedResources()
    {
        _full?.Clear();
        _display?.Clear();
        _probe?.Clear();
        _full = _display = _probe = null;
        _fullLines.Clear();
        _displayLines.Clear();
        _probeLines.Clear();
        _retained = null;
        _elements = null;
        AccessibilityIdentity = null;
        base.ReleaseOwnedResources();
    }
}

public sealed partial class UiImmediateContext
{
    internal void RenderFluentTreeLabel(UiFluentTreeLabel label)
    {
        var context = new UiControlContext(this);
        var layout = label.GetLayout(context, render: true);
        using var typography = BeginStyledTypography(label.Source.BaseStyle);
        var origin = AllocateLayoutBox(layout.Size);
        var bounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var color = label.Foreground(context);
        PaintInlineVisualLines(label.Lines, origin, color, IntersectRect(CurrentClipRect, bounds),
            label.Lines.AlignmentOffsets(layout.Size.X, layout.Options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left));
        ExposePassiveText(ref label.AccessibilityIdentity, label.Text, bounds);
        FinishLayoutBox(origin, layout.Size);
    }
}
