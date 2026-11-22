using System.Globalization;
using System.Text;

namespace Duxel.Core;

/// <summary>Owns inline text spans and atomic widgets in one retained, wrapping line flow.</summary>
[UiComponent("InlineFlow", UiComponentExposure.Infrastructure)]
public class UiInlineFlowWidget : UiMultiChildWidget
{
    private readonly UiInlineFlowTextSource _source = new();
    private readonly UiVisualTextLines _lines = new();
    private UiSourceTextRange[] _ranges;
    private UiRect[] _rects;
    private bool[] _literals;
    private string[] _names;
    private UiRetainedLayout<UiVector2>? _retained;
    private bool _literalSourcePrepared;
    private UiItemHorizontalAlign _horizontalAlignment;
    private float[] _lineOffsets = [];
    public UiItemHorizontalAlign HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            ThrowIfDisposed();
            if ((uint)value > (uint)UiItemHorizontalAlign.Right)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_horizontalAlignment == value)
                return;
            ThrowIfBuilding();
            _horizontalAlignment = value;
            RequestRebuild();
        }
    }
    internal UiVisualTextLines VisualLines => _lines;
    internal UiRect ChildBounds(int index) => _rects[index];

    public bool RightToLeft
    {
        get
        {
            ThrowIfDisposed();
            return _source.RightToLeft;
        }
        set
        {
            ThrowIfDisposed();
            if (_source.RightToLeft == value)
                return;
            ThrowIfBuilding();
            _source.SetDirection(value);
            RequestRebuild();
        }
    }

    public UiInlineFlowWidget(UiWidget[] children, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(children, identity)
    {
        _ranges = new UiSourceTextRange[children.Length];
        _rects = new UiRect[children.Length];
        _literals = new bool[children.Length];
        _names = new string[children.Length];
    }
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement()
    {
        _retained?.Invalidate();
        _literalSourcePrepared = false;
    }

    private void PrepareSource(UiControlContext context, float width)
    {
        if (_literalSourcePrepared)
            return;
        var text = new StringBuilder();
        var runs = new List<UiStyledSourceRun>(Children.Length);
        var objects = new List<UiStyledTextObject>();
        for (var i = 0; i < Children.Length; i++)
        {
            var child = Children[i];
            var start = text.Length;
            _literals[i] = child is UiInlineTextSpan { IsInlineLiteral: true };
            if (_literals[i])
            {
                var span = (UiInlineTextSpan)child;
                _names[i] = span.Text;
                var value = span.DisplayText;
                var flags = span.Weight switch
                {
                    UiInlineTextWeight.Regular => UiStyledTextFlags.None,
                    UiInlineTextWeight.Semibold => UiStyledTextFlags.Semibold,
                    UiInlineTextWeight.Bold => UiStyledTextFlags.Bold,
                    _ => throw new InvalidOperationException("Inline span face was not validated.")
                };
                var style = new UiStyledTextStyle(Flags: flags);
                text.Append(value);
                if (value.Length > 0)
                {
                    if (runs.Count > 0 && runs[^1].Style == style)
                    {
                        var last = runs[^1];
                        runs[^1] = last with
                        {
                            Source = last.Source with
                            {
                                Length = checked(last.Source.Length + value.Length)
                            }
                        };
                    }
                    else
                        runs.Add(new(new(start, value.Length), string.Empty, style, Syntax: false));
                }
            }
            else
            {
                _names[i] = string.Empty;
                var size = context.MeasureConstrained(child, new(0, width, 0, float.PositiveInfinity));
                if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0 || size.X > width)
                    throw new InvalidOperationException("An inline widget must return finite extents within its available width.");
                objects.Add(new(size));
                text.Append('\uFFFC');
                runs.Add(new(new(start, 1), string.Empty, new(ObjectIndex: objects.Count), Syntax: false));
            }
            _ranges[i] = new(start, text.Length - start);
        }
        var literal = text.ToString();
        var boundaries = StringInfo.ParseCombiningCharacters(literal);
        for (var i = 1; i < _ranges.Length; i++)
            if (_ranges[i].Start < literal.Length && Array.BinarySearch(boundaries, _ranges[i].Start) < 0)
                throw new InvalidOperationException("Owned inline child boundaries cannot split a Unicode text element.");
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            runs[i] = run with
            {
                Display = literal.Substring(run.Source.Start, run.Source.Length)
            };
        }
        _source.Replace(literal, runs.ToArray(), objects.ToArray());
        _literalSourcePrepared = objects.Count == 0;
    }

    private UiVector2 PrepareLayout(UiControlContext context, bool render = false)
    {
        var allowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        if (_retained is { } previous && previous.TryGet(context, render, out var found, allowed))
            return found;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var constraints = context.LayoutConstraints.Validate();
        PrepareSource(context, constraints.MaxWidth);
        var textHeight = context.PrepareInlineFlow(_source, _lines, constraints.MaxWidth);
        var size = constraints.Constrain(new(_lines.Width, _lines.Height));
        if (_lines.Height > constraints.MaxHeight)
            throw new InvalidOperationException("Inline flow exceeds the available height.");
        if (_lineOffsets.Length != _lines.Count)
            _lineOffsets = new float[_lines.Count];
        for (var line = 0; line < _lines.Count; line++)
        {
            _lineOffsets[line] = UiVisualTextLines.AlignmentOffset(size.X, _lines[line].Width, _horizontalAlignment);
        }
        for (var i = 0; i < Children.Length; i++)
        {
            var range = _ranges[i];
            if (!_literals[i])
            {
                var line = _lines.FindLine(range.Start);
                var style = _source.Projection.StyleAt(range.Start);
                var extent = _source.Objects[style.ObjectIndex - 1].Size;
                if (_lines.NativeParagraph is not null)
                {
                    var allocation = _lines.NativeObjectBounds(range.Start);
                    _rects[i] = new(_lineOffsets[line] + allocation.X, allocation.Y, extent.X, extent.Y);
                }
                else
                    _rects[i] = new(_lineOffsets[line] + _lines.X(range.Start, line), _lines.Y(line) + (_lines.LineHeight(line) - extent.Y) / 2,
                        extent.X, extent.Y);
                continue;
            }
            UiRect? bounds = null;
            for (var line = 0; line < _lines.Count; line++)
            {
                var start = Math.Max(range.Start, _lines[line].Start);
                var end = Math.Min(range.End, _lines[line].End);
                if (start >= end)
                    continue;
                foreach (var selected in _lines.SelectionFragments(start, end, line))
                {
                    var fragment = new UiRect(_lineOffsets[line] + selected.Left, _lines.Y(line) + (_lines.LineHeight(line) - textHeight) / 2,
                        selected.Width, textHeight);
                    if (bounds is not { } old)
                        bounds = fragment;
                    else
                    {
                        var x = MathF.Min(old.X, fragment.X);
                        var y = MathF.Min(old.Y, fragment.Y);
                        bounds = new(x, y, MathF.Max(old.X + old.Width, fragment.X + fragment.Width) - x,
                            MathF.Max(old.Y + old.Height, fragment.Y + fragment.Height) - y);
                    }
                }
            }
            var at = _lines.FindLine(range.Start);
            _rects[i] = bounds ?? new(_lineOffsets[at] + _lines.X(range.Start, at), _lines.Y(at) + (_lines.LineHeight(at) - textHeight) / 2, 0, textHeight);
        }
        cache.Commit(context, attempt, size, size, allowed && ChildrenRetainMeasurement);
        return size;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => PrepareLayout(context);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var size = PrepareLayout(new(ui), render: true);
        var origin = ui.AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        ui.PaintInlineFlow(_lines, origin, root, _lineOffsets);
        for (var i = 0; i < Children.Length; i++)
        {
            var rect = _rects[i] with
            {
                X = origin.X + _rects[i].X,
                Y = origin.Y + _rects[i].Y
            };
            if (_literals[i])
                ui.RenderInlineSpanChild((UiInlineTextSpan)Children[i], _names[i], rect, root);
            else
                ui.RenderLayoutChild(Children[i], rect, root);
        }
        ui.FinishLayoutBox(origin, size);
    }
    private protected override void ReleaseOwnedResources()
    {
        _source.Clear();
        _lines.Clear();
        _ranges = [];
        _rects = [];
        _literals = [];
        _names = [];
        _retained = null;
        _lineOffsets = [];
        base.ReleaseOwnedResources();
    }
}
