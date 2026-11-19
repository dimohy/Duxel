using System.Globalization;

namespace Duxel.Core;

public readonly record struct UiRatingDisplayValue(float? Value, int? Count = null,
    string? ValueText = null, string? CountText = null, string? AccessibleLabel = null)
{
    internal void Validate(int maximum)
    {
        if (Value is { } value && (!float.IsFinite(value) || value < 0 || value > maximum))
            throw new ArgumentOutOfRangeException(nameof(Value));
        if (Count < 0)
            throw new ArgumentOutOfRangeException(nameof(Count));
    }
}

public readonly record struct UiRatingDisplayOptions
{
    public int Maximum { get; init; } = 5;
    public UiRatingSize Size { get; init; } = UiRatingSize.Medium;
    public UiRatingShape Shape
    {
        get; init;
    }
    public UiRatingColor Color
    {
        get; init;
    }
    public bool Compact
    {
        get; init;
    }
    public bool RightToLeft
    {
        get; init;
    }
    public bool ShowValue { get; init; } = true;
    public bool ShowCount { get; init; } = true;
    public UiRatingDisplayOptions()
    {
    }
    internal int IconCount => Compact ? 1 : Maximum;
    internal void Validate()
    {
        if (Maximum < 2)
            throw new ArgumentOutOfRangeException(nameof(Maximum));
        new UiRatingOptions { Maximum = Maximum, Size = Size, Shape = Shape, Color = Color }.Validate();
    }
}

public readonly record struct UiRatingDisplayContent(float? Value, string ValueText, string CountText, string AccessibleLabel);
public readonly record struct UiRatingDisplayLayout(UiVector2 Size, UiControlTextBlock ValueText, UiControlTextBlock CountText);

public interface IUiRatingDisplayPresentation
{
    static virtual bool CanRetainMeasurement => false;
    /// <summary>Writes relative icon rectangles followed by value and count rectangles.</summary>
    static abstract UiRatingDisplayLayout Layout(UiControlContext context, UiRatingDisplayContent content,
        UiRatingDisplayOptions options, Span<UiRect> items);
    static abstract void Paint(UiControlContext context, UiRect bounds, UiRatingDisplayContent content,
        UiRatingDisplayOptions options, UiRatingDisplayLayout layout, ReadOnlySpan<UiRect> items);
}

internal sealed class UiRatingDisplayTextCache
{
    private bool _valid;
    private UiRatingDisplayValue _value;
    private UiRatingDisplayOptions _options;
    private string? _label;
    private CultureInfo? _culture;
    private UiRatingDisplayContent _content;
    internal bool UsesCurrentCulture => _valid && ReferenceEquals(_culture, CultureInfo.CurrentCulture);
    internal UiRatingDisplayContent Resolve(UiRatingDisplayValue value, string label, UiRatingDisplayOptions options)
    {
        var culture = CultureInfo.CurrentCulture;
        if (_valid && _value == value && _options == options && _label == label && ReferenceEquals(_culture, culture))
            return _content;
        var score = value.ValueText ?? value.Value?.ToString("0.##", culture) ?? string.Empty;
        var count = value.CountText ?? value.Count?.ToString("N0", culture) ?? string.Empty;
        var accessible = value.AccessibleLabel ?? string.Join(" ", new[] { label, score, count }.Where(static text => text.Length != 0));
        if (!options.ShowValue)
            score = string.Empty;
        if (!options.ShowCount)
            count = string.Empty;
        _content = new(value.Value, score, count.Length != 0 && score.Length != 0 ? "· " + count : count, accessible);
        (_valid, _value, _options, _label, _culture) = (true, value, options, label, culture);
        return _content;
    }
    internal void Clear()
    {
        _valid = false;
        _label = null;
        _culture = null;
        _value = default;
        _content = default;
    }
}

/// <summary>Noninteractive rating image; getter sources keep parent measurement live.</summary>
public class UiRatingDisplay<TPresentation> : UiControlWidget where TPresentation : IUiRatingDisplayPresentation
{
    private UiRatingDisplayValue _value;
    private Func<UiRatingDisplayValue>? _source;
    private UiRatingDisplayOptions _options;
    private string _label;
    private readonly UiRatingDisplayTextCache _text = new();
    private readonly UiRetainedLayout<UiRatingDisplayLayout> _retained = new();
    private UiRatingDisplayContent _content;
    private UiRect[] _items;
    public UiRatingDisplay(string label, UiRatingDisplayValue value, UiRatingDisplayOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        _options = options ?? new();
        _options.Validate();
        value.Validate(_options.Maximum);
        _label = label;
        _value = value;
        _items = new UiRect[checked(_options.IconCount + 2)];
    }
    public UiRatingDisplay(string label, Func<UiRatingDisplayValue> source, UiRatingDisplayOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(label, default(UiRatingDisplayValue), options, identity)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }
    public string Label
    {
        get => _label;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_label == value)
                return;
            _label = value;
            _retained.Invalidate();
            RequestRebuild();
        }
    }
    public UiRatingDisplayValue Value
    {
        get
        {
            ThrowIfDisposed();
            return ReadValue();
        }
        set
        {
            ThrowIfDisposed();
            value.Validate(_options.Maximum);
            if (_source is null && _value == value)
                return;
            _source = null;
            _value = value;
            _retained.Invalidate();
            RequestRebuild();
        }
    }
    public UiRatingDisplayOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (value == _options)
                return;
            ThrowIfBuilding();
            ReadValue().Validate(value.Maximum);
            if (value.IconCount != _options.IconCount)
                _items = new UiRect[checked(value.IconCount + 2)];
            _options = value;
            _retained.Invalidate();
            RequestRebuild();
        }
    }
    protected virtual UiRatingDisplayValue ReadValue() => _source is { } source ? source() : _value;
    protected override bool CanRetainMeasurement => _source is null && TPresentation.CanRetainMeasurement && _text.UsesCurrentCulture;
    private UiRatingDisplayLayout Layout(UiControlContext context, bool render)
    {
        var value = ReadValue();
        value.Validate(_options.Maximum);
        var content = _text.Resolve(value, Label, Options);
        if (content != _content)
        {
            _retained.Invalidate();
            _content = content;
        }
        var retain = context.CanRetainLayoutMeasurement && TPresentation.CanRetainMeasurement;
        if (_retained.TryGet(context, render, out var layout, retain))
            return layout;
        var attempt = _retained.Begin(context);
        layout = TPresentation.Layout(context, content, Options, _items);
        _retained.Commit(context, attempt, layout, layout.Size, retain);
        return layout;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => Layout(context, false).Size;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var context = new UiControlContext(ui);
        var layout = Layout(context, true);
        ui.RatingDisplay<TPresentation>(PrimaryInputId(Label), _content, Options, layout, _items);
    }
    private protected override void ReleaseOwnedResources()
    {
        _source = null;
        _value = default;
        _label = string.Empty;
        _content = default;
        _items = [];
        _text.Clear();
        _retained.Clear();
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiRatingDisplay<>), typeof(UiFluentRatingDisplayPresentation))]
[UiComponent("RatingDisplay", UiComponentExposure.Fluent)]
public partial class UiFluentRatingDisplay;
