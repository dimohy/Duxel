namespace Duxel.Core;

public readonly record struct UiSegmentOption(string Value, string Label, bool Enabled = true,
    UiMenuIcon Icon = default, bool Unread = false);

public sealed record UiSegmentedControlOptions(float Width = 320, bool EqualWidth = true,
    bool RightToLeft = false, bool ReducedMotion = false,
    UiSegmentedControlStyle Style = UiSegmentedControlStyle.NeutralOverNavBarPill, float HorizontalInset = 16,
    string UnreadLabel = "Unread", bool IsFixedWidth = true)
{
    internal void Validate()
    {
        if (!float.IsFinite(Width) || Width < 0) throw new ArgumentOutOfRangeException(nameof(Width));
        if (!float.IsFinite(HorizontalInset) || HorizontalInset < 0) throw new ArgumentOutOfRangeException(nameof(HorizontalInset));
        if ((uint)Style > 3) throw new ArgumentOutOfRangeException(nameof(Style));
        ArgumentException.ThrowIfNullOrWhiteSpace(UnreadLabel);
    }
}

/// <summary>Single selection with a shared pill surface and an owned finite selection timeline.</summary>
public partial class UiSegmentedControlWidget : UiControlWidget
{
    private UiSegmentedControlOptions _options;
    private UiSegmentOption[] _items;
    internal UiRadioOption[] RadioItems;
    internal UiRadioOptionLayout[] Layouts;
    internal readonly UiRadioGroupInputState Input = new();
    internal readonly UiHorizontalScrollState Scroll = new();
    internal Action BeforePublication { get; }
    internal UiRadioOptionLayout[] InputLayouts = [];
    public float ScrollOffset => Scroll.Offset;
    public float MaximumScrollOffset => Scroll.Maximum;
    internal float ContentWidth { get; private set; }
    internal UiFloatTransition ThumbX, ThumbWidth;
    private UiControlTextLayoutKey _layoutKey;
    private bool _layoutRightToLeft;
    private readonly WeakReference<UiImmediateContext> _layoutContext = new(null!);
    private float _layoutDesiredWidth;
    private bool _layoutValid;
    private UiVector2 _size;
    internal int LayoutBuildCount { get; private set; }
    public ulong ContentVersion { get; private set; }
    public string Label { get; }
    public UiBinding<string?> Value { get; }
    internal UiBinding<string?> Selection { get; }
    public ReadOnlySpan<UiSegmentOption> Items => _items;
    public UiSegmentedControlOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            if (value == _options) return;
            ThrowIfBuilding();
            var previous = _options;
            _options = value;
            if (previous.Width != value.Width || previous.EqualWidth != value.EqualWidth
                || previous.RightToLeft != value.RightToLeft || previous.HorizontalInset != value.HorizontalInset
                || previous.IsFixedWidth != value.IsFixedWidth)
                _layoutValid = false;
            if (previous.UnreadLabel != value.UnreadLabel) RadioItems = ToRadios(_items);
            RequestRebuild();
        }
    }
    public UiSegmentedControlWidget(string label, UiBinding<string?> value, ReadOnlyMemory<UiSegmentOption> items,
        UiSegmentedControlOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label;
        BeforePublication = SynchronizeSelectionViewport;
        PaintContent = PaintCachedContent;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Selection = new(() => Value.Value, next =>
        {
            Value.Value = next;
            if (next is not null && Value.Value == next) Commit(next);
        });
        _options = options ?? new();
        _options.Validate();
        _items = CopyItems(items.Span);
        RadioItems = ToRadios(_items);
        Layouts = new UiRadioOptionLayout[_items.Length];
        InputLayouts = new UiRadioOptionLayout[_items.Length];
    }
    private static UiSegmentOption[] CopyItems(ReadOnlySpan<UiSegmentOption> items)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
            if (string.IsNullOrEmpty(item.Value) || item.Label is null || !keys.Add(item.Value))
                throw new ArgumentException("Segment values must be nonempty and distinct; labels cannot be null.", nameof(items));
        return items.ToArray();
    }
    private UiRadioOption[] ToRadios(ReadOnlySpan<UiSegmentOption> items)
    {
        var result = new UiRadioOption[items.Length];
        for (var i = 0; i < items.Length; i++)
            result[i] = new(items[i].Value, items[i].Label, items[i].Enabled, items[i].Unread ? _options.UnreadLabel : null);
        return result;
    }
    public void SetItems(ReadOnlyMemory<UiSegmentOption> items)
    {
        ThrowIfDisposed();
        ThrowIfBuilding();
        if (_items.AsSpan().SequenceEqual(items.Span)) return;
        var copy = CopyItems(items.Span);
        _items = copy;
        RadioItems = ToRadios(copy);
        Layouts = new UiRadioOptionLayout[copy.Length];
        InputLayouts = new UiRadioOptionLayout[copy.Length];
        Scroll.Candidate = Scroll.Dragging = Scroll.ThumbDrag = false;
        Scroll.Selected = null;
        // Replacing options cancels the in-progress gesture and rebuilds stable value-based accessibility IDs.
        Input.Release();
        ContentVersion++;
        _layoutValid = false;
        RequestRebuild();
    }
    internal void Commit(string value)
    {
        if (Value.Value != value) return;
        for (var i = 0; i < _items.Length; i++)
            if (_items[i].Value == value && _items[i].Unread)
            {
                _items[i] = _items[i] with { Unread = false };
                RadioItems[i] = RadioItems[i] with { Description = null };
                ContentVersion++;
            }
    }
    internal UiVector2 Prepare(UiImmediateContext ui, UiControlContext context, float width)
    {
        var key = ui.CurrentControlTextLayoutKey;
        var rightToLeft = _options.RightToLeft || context.RightToLeft;
        var sameContext = _layoutContext.TryGetTarget(out var prior) && ReferenceEquals(prior, ui);
        var directionChanged = _layoutRightToLeft != rightToLeft;
        if (_layoutValid && sameContext && _layoutKey == key && !directionChanged
            && _size.X == MathF.Min(_layoutDesiredWidth, width))
            return _size;
        LayoutBuildCount++;
        Scroll.Selected = null;
        if (!sameContext || directionChanged)
        {
            ThumbX = default;
            ThumbWidth = default;
        }
        var maximum = 0f;
        var total = 0f;
        var height = 32f;
        for (var i = 0; i < _items.Length; i++)
        {
            var item = _items[i];
            var block = item.Icon.IsEmpty ? context.MeasureTextBlock(item.Label) : default;
            var size = item.Icon.IsEmpty ? block.Size : new UiVector2(20, 20);
            var natural = size.X + 32;
            Layouts[i] = new(new(0, 0, natural, 0), default, default, default, item.Label, null) { LabelBlock = block };
            maximum = MathF.Max(maximum, natural);
            total += natural;
            height = MathF.Max(height, size.Y + 12);
        }
        var desired = _options.Width > 0 ? _options.Width
            : (_options.EqualWidth ? maximum * _items.Length : total) + _options.HorizontalInset * 2;
        var actual = _items.Length == 0 ? 0 : MathF.Min(desired, width);
        ContentWidth = _options.IsFixedWidth || _items.Length == 0 ? actual
            : MathF.Max(actual, (_options.EqualWidth ? maximum * _items.Length : total) + _options.HorizontalInset * 2);
        var inset = MathF.Min(_options.HorizontalInset, ContentWidth / 2);
        var available = MathF.Max(0, ContentWidth - inset * 2);
        var wasScrollable = Scroll.Maximum > 0;
        Scroll.Maximum = MathF.Max(0, ContentWidth - actual);
        Scroll.ContentWidth = ContentWidth;
        Scroll.SetOffset(directionChanged || !wasScrollable ? rightToLeft ? Scroll.Maximum : 0 : Scroll.Offset);
        var x = 0f;
        for (var i = 0; i < Layouts.Length; i++)
        {
            var w = _options.EqualWidth ? available / Layouts.Length : total > 0 ? available * Layouts[i].Bounds.Width / total : 0;
            var left = inset + (rightToLeft ? available - x - w : x);
            var block = _items[i].Icon.IsEmpty && w > 0
                ? UiLimitedTextLayout.Measure<UiRegularTypeface, UiSegmentedControlTypography>(context, _items[i].Label,
                    MathF.Max(1, w - 32), 1, 1).Block : default;
            Layouts[i] = Layouts[i] with { Bounds = new(left, 0, w, height), LabelBlock = block };
            x += w;
        }
        _size = new(actual, _items.Length == 0 ? 0 : height);
        _layoutKey = key;
        _layoutRightToLeft = rightToLeft;
        _layoutDesiredWidth = _items.Length == 0 ? 0 : desired;
        _layoutContext.SetTarget(ui);
        _layoutValid = ui.CanRetainControlTextLayout;
        return _size;
    }
    private void SynchronizeSelectionViewport()
    {
        if (Scroll.Selected == Value.Value) return;
        Reveal(Value.Value, _size.X);
        Scroll.Selected = Value.Value;
        ArrangeInput();
    }
    internal void ArrangeInput()
    {
        for (var i = 0; i < Layouts.Length; i++)
            InputLayouts[i] = Layouts[i] with { Bounds = Layouts[i].Bounds with { X = Layouts[i].Bounds.X - Scroll.Offset } };
    }
    internal void Reveal(string? value, float viewportWidth)
    {
        if (value is null || Scroll.Maximum <= 0) return;
        for (var index = 0; index < _items.Length; index++)
            if (_items[index].Value == value)
            {
                var rect = Layouts[index].Bounds;
                var inset = MathF.Min(_options.HorizontalInset, viewportWidth / 2);
                if (rect.X < Scroll.Offset + inset) Scroll.SetOffset(rect.X - inset);
                else if (rect.X + rect.Width > Scroll.Offset + viewportWidth - inset)
                    Scroll.SetOffset(rect.X + rect.Width - viewportWidth + inset);
                return;
            }
    }
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureSegments(this);
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSegments(this, PrimaryControlId);
    private protected override void ReleaseOwnedResources()
    {
        Input.Release();
        PaintGroup.Dispose();
        _iconVersions = [];
        _items = [];
        RadioItems = [];
        Layouts = [];
        InputLayouts = [];
        Scroll.Context = null;
        _layoutContext.SetTarget(null!);
        base.ReleaseOwnedResources();
    }
}

public readonly partial struct UiControlContext
{
    internal UiVector2 MeasureSegments(UiSegmentedControlWidget widget)
    {
        using var font = BeginTypography<UiRegularTypeface, UiSegmentedControlTypography>();
        var width = ConstrainToLayout(new(widget.Options.Width > 0 ? widget.Options.Width : float.MaxValue, 32)).X;
        return ConstrainToLayout(widget.Prepare(_ui, this, width));
    }
}

[UiWidgetStyle(typeof(UiSegmentedControlWidget))]
[UiComponent("SegmentedControl", UiComponentExposure.Fluent)]
public partial class UiFluentSegmentedControl;
