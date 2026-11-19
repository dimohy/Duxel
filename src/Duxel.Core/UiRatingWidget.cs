using System.Globalization;

namespace Duxel.Core;

public enum UiRatingSize
{
    Small, Medium, Large, ExtraLarge
}
public enum UiRatingShape
{
    Star, Circle, Square
}
public enum UiRatingColor
{
    Neutral, Brand, Marigold
}

public readonly record struct UiRatingOptions
{
    public int Maximum { get; init; } = 5;
    public float Step { get; init; } = 1;
    public UiRatingSize Size { get; init; } = UiRatingSize.ExtraLarge;
    public UiRatingShape Shape
    {
        get; init;
    }
    public UiRatingColor Color
    {
        get; init;
    }
    /// <summary>Formats accessible option names when the formatter or current culture changes. Null uses localized numeric values.</summary>
    public Func<float, string>? ItemLabel
    {
        get; init;
    }
    public bool RightToLeft
    {
        get; init;
    }
    public UiRatingOptions()
    {
    }
    internal void Validate()
    {
        if (Maximum <= 0 || Maximum > Array.MaxLength / 2)
            throw new ArgumentOutOfRangeException(nameof(Maximum));
        if (Step is not (1 or .5f))
            throw new ArgumentOutOfRangeException(nameof(Step));
        if ((uint)Size > (uint)UiRatingSize.ExtraLarge)
            throw new ArgumentOutOfRangeException(nameof(Size));
        UiFluentRatingGlyphs.Validate(Shape, Color);
    }
    internal void ValidateValue(float value)
    {
        if (!float.IsFinite(value) || value < 0 || value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public interface IUiRatingBehavior
{
    static abstract UiRadioGroupInteraction Process(UiControlContext context, string id, UiRect bounds,
        UiRatingOptions options, ReadOnlySpan<UiRadioOption> items, ReadOnlySpan<UiRadioOptionLayout> layouts,
        string? selected, UiRadioGroupInputState state);
}

public readonly struct UiFluentRatingBehavior : IUiRatingBehavior
{
    public static UiRadioGroupInteraction Process(UiControlContext context, string id, UiRect bounds,
        UiRatingOptions options, ReadOnlySpan<UiRadioOption> items, ReadOnlySpan<UiRadioOptionLayout> layouts,
        string? selected, UiRadioGroupInputState state)
        => context.RatingInput(id, bounds, options, items, layouts, selected, state);
}

public interface IUiRatingPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiVector2 Measure(UiControlContext context, UiRatingOptions options);
    /// <summary>Writes hit segments relative to the supplied root bounds.</summary>
    static abstract void Arrange(UiControlContext context, UiRect bounds, UiRatingOptions options, Span<UiRadioOptionLayout> layouts);
    static abstract void Paint(UiControlContext context, UiRect bounds, UiRatingOptions options, float value,
        float? preview, UiRadioGroupInteraction interaction);
}

internal sealed class UiRatingContent
{
    internal readonly UiRadioOption[] Items;
    internal readonly UiRadioOptionLayout[] Layouts;
    internal readonly UiRadioGroupInputState Input = new();
    private CultureInfo? _labelCulture;
    private Func<float, string>? _itemLabel;
    internal UiRatingContent(UiRatingOptions options)
    {
        Items = new UiRadioOption[checked(options.Maximum * (options.Step == .5f ? 2 : 1))];
        Layouts = new UiRadioOptionLayout[Items.Length];
        for (var i = 0; i < Items.Length; i++)
        {
            var label = ((i + 1) * options.Step).ToString("0.#", CultureInfo.InvariantCulture);
            Items[i] = new(label, label);
        }
        RefreshLabels(options);
    }
    internal void RefreshLabels(UiRatingOptions options, bool force = false)
    {
        var culture = CultureInfo.CurrentCulture;
        if (!force && ReferenceEquals(_labelCulture, culture) && ReferenceEquals(_itemLabel, options.ItemLabel))
            return;
        // Publish all names together so a failing formatter cannot leave partial labels.
        var labels = new string[Items.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var value = (i + 1) * options.Step;
            var label = options.ItemLabel is { } format ? format(value) : value.ToString("0.#", culture);
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Rating item labels must contain accessible text.", nameof(options));
            labels[i] = label;
        }
        for (var i = 0; i < labels.Length; i++)
            Items[i] = Items[i] with
            {
                Label = labels[i]
            };
        (_labelCulture, _itemLabel) = (culture, options.ItemLabel);
    }
    internal string? Selected(float value, float step)
    {
        var rounded = MathF.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
        var ordinal = rounded / step;
        return ordinal >= 1 && ordinal <= Items.Length && ordinal == MathF.Truncate(ordinal) ? Items[(int)ordinal - 1].Value : null;
    }
    internal float ValueOf(string token, float step)
    {
        for (var i = 0; i < Items.Length; i++)
            if (Items[i].Value == token)
                return (i + 1) * step;
        throw new InvalidOperationException("Rating input returned an unknown option identity.");
    }
}

/// <summary>Typed rating value using the shared radio input and accessibility kernel.</summary>
public class UiRating<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiRatingBehavior where TPresentation : IUiRatingPresentation
{
    private UiBinding<float>? _value;
    private UiRatingContent? _content;
    private UiRatingOptions _options;
    private string _label;
    public UiRating(string label, UiBinding<float> value, UiRatingOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);
        _options = options ?? new();
        _options.Validate();
        _label = label;
        _value = value;
        _content = new(_options);
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
            RequestRebuild();
        }
    }
    public UiBinding<float> Value => _value ?? throw new ObjectDisposedException(nameof(UiRating<TBehavior, TPresentation>));
    /// <summary>Refreshes names after external localization data changes without replacing the formatter or input state.</summary>
    public void RefreshItemLabels()
    {
        ThrowIfDisposed();
        ThrowIfBuilding();
        _content!.RefreshLabels(Options, force: true);
        RequestRebuild();
    }
    public UiRatingOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (value == _options)
                return;
            ThrowIfBuilding();
            value.ValidateValue(Value.Value);
            if (value.Maximum != _options.Maximum || value.Step != _options.Step)
            {
                var next = new UiRatingContent(value);
                foreach (var item in next.Items)
                    if (item.Value == _content!.Input.PressedValue)
                        next.Input.PressedValue = item.Value;
                _content!.Input.Release();
                _content = next;
            }
            else
                _content!.RefreshLabels(value);
            _options = value;
            RequestRebuild();
        }
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) => TPresentation.Measure(context, Options);
    protected virtual void OnChanged(float value) => Value.Value = value;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var content = _content!;
        content.RefreshLabels(Options);
        var value = Value.Value;
        _options.ValidateValue(value);
        ui.Rating<TBehavior, TPresentation>(PrimaryInputId(Label), Label, content, Options, value);
        foreach (var change in content.Input.Changes)
        {
            OnChanged(content.ValueOf(change, _options.Step));
            if (!ReferenceEquals(content, _content))
                break;
        }
    }
    private protected override void ReleaseOwnedResources()
    {
        _content?.Input.Release();
        _content = null;
        _value = null;
        _options = default;
        _label = string.Empty;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiRating<,>), typeof(UiFluentRatingBehavior), typeof(UiFluentRatingPresentation))]
[UiComponent("Rating", UiComponentExposure.Fluent)]
public partial class UiFluentRating;
