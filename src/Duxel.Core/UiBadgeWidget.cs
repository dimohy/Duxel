using System.Globalization;

namespace Duxel.Core;

public enum UiBadgeSize
{
    Medium, Tiny, ExtraSmall, Small, Large, ExtraLarge
}
public enum UiBadgeShape
{
    Circular, Rounded, Square
}
public enum UiBadgeAppearance
{
    Filled, Ghost, Outline, Tint
}
public enum UiBadgeColor
{
    Brand, Danger, Important, Informative, Severe, Subtle, Success, Warning
}
public enum UiBadgeIconPosition
{
    Before, After
}
public enum UiBadgeColorScheme
{
    Light, Dark
}

public readonly record struct UiFluentBadgeOptions(UiBadgeSize Size = UiBadgeSize.Medium,
    UiBadgeShape Shape = UiBadgeShape.Circular, UiBadgeAppearance Appearance = UiBadgeAppearance.Filled,
    UiBadgeColor Color = UiBadgeColor.Brand, UiBadgeIconPosition IconPosition = UiBadgeIconPosition.Before)
{
    public void Validate()
    {
        if ((uint)Size > 5 || (uint)Shape > 2 || (uint)Appearance > 3 || (uint)Color > 7 || (uint)IconPosition > 1)
            throw new ArgumentOutOfRangeException(nameof(UiFluentBadgeOptions));
    }
}

public readonly record struct UiBadgeContent(string Text, bool Visible = true, bool Dot = false, string? AccessibleLabel = null);
public readonly record struct UiCounterBadgeValue(int Count = 0, int? OverflowCount = null, bool ShowZero = false, bool Dot = false, string? Text = null);

/// <summary>One bounded numeric-string memo per widget; no per-frame formatting of unchanged counts.</summary>
public struct UiBadgeTextCache
{
    private bool _valid, _overflow;
    private int _value;
    private string? _text;
    public string FormatCounter(int count, int overflow)
    {
        var overflowed = count > overflow;
        var value = overflowed ? overflow : count;
        if (_valid && _value == value && _overflow == overflowed)
            return _text!;
        var text = value.ToString(CultureInfo.InvariantCulture);
        _text = overflowed ? text + "+" : text;
        _value = value;
        _overflow = overflowed;
        _valid = true;
        return _text;
    }
    public void Clear() => this = default;
}

public interface IUiBadgeBehavior<TSource, TSelf> where TSelf : struct
{
    static virtual bool CanRetainMeasurement => false;
    static virtual TSource Normalize(TSource source) => source;
    static abstract UiBadgeContent Read(TSource source, ref TSelf state);
    static virtual void ValidateOptions(UiFluentBadgeOptions options) => options.Validate();
    static virtual void Release(ref TSelf state) => state = default;
}
public readonly struct UiTextBadgeBehavior : IUiBadgeBehavior<string, UiTextBadgeBehavior>
{
    public static bool CanRetainMeasurement => true;
    public static string Normalize(string source) => source ?? string.Empty;
    public static UiBadgeContent Read(string source, ref UiTextBadgeBehavior state) => new(source ?? string.Empty);
}
public readonly struct UiGetterBadgeBehavior : IUiBadgeBehavior<Func<string>, UiGetterBadgeBehavior>
{
    public static Func<string> Normalize(Func<string> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }
    public static UiBadgeContent Read(Func<string> source, ref UiGetterBadgeBehavior state) => new(source() ?? string.Empty);
}
public struct UiCounterBadgeBehavior : IUiBadgeBehavior<UiCounterBadgeValue, UiCounterBadgeBehavior>
{
    public static bool CanRetainMeasurement => true;
    private UiBadgeTextCache _cache;
    public static UiBadgeContent Read(UiCounterBadgeValue source, ref UiCounterBadgeBehavior state) => Resolve(source, ref state._cache);
    public static UiBadgeContent Resolve(UiCounterBadgeValue source, ref UiBadgeTextCache cache)
    {
        var text = !string.IsNullOrEmpty(source.Text) ? source.Text :
            (source.Count != 0 || source.ShowZero) && !source.Dot ? cache.FormatCounter(source.Count, source.OverflowCount ?? 99) : string.Empty;
        return new(text, text.Length != 0 || source.Dot, source.Dot,
            source.Dot ? cache.FormatCounter(source.Count, int.MaxValue) : null);
    }
    public static void ValidateOptions(UiFluentBadgeOptions options)
    {
        options.Validate();
        if (options.Shape == UiBadgeShape.Square || options.Appearance is not (UiBadgeAppearance.Filled or UiBadgeAppearance.Ghost)
            || options.Color is not (UiBadgeColor.Brand or UiBadgeColor.Danger or UiBadgeColor.Important or UiBadgeColor.Informative))
            throw new ArgumentException("CounterBadge supports circular/rounded, filled/ghost and brand/danger/important/informative.", nameof(options));
    }
}
public struct UiGetterCounterBadgeBehavior : IUiBadgeBehavior<Func<UiCounterBadgeValue>, UiGetterCounterBadgeBehavior>
{
    private UiCounterBadgeBehavior _counter;
    public static Func<UiCounterBadgeValue> Normalize(Func<UiCounterBadgeValue> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }
    public static UiBadgeContent Read(Func<UiCounterBadgeValue> source, ref UiGetterCounterBadgeBehavior state) => UiCounterBadgeBehavior.Read(source(), ref state._counter);
    public static void ValidateOptions(UiFluentBadgeOptions options) => UiCounterBadgeBehavior.ValidateOptions(options);
}

public interface IUiBadgePresentation
{
    static virtual bool CanRetainMeasurement => false;
    /// <summary>Accepts allocation through one common text root; paint must respect that root's clip.</summary>
    static virtual bool CanArrange => false;
    static virtual UiVector2 Measure(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) =>
        throw new NotSupportedException("This Badge presentation must provide pure measurement.");
    static abstract void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options);
}
public interface IUiBadgeIcon
{
    static virtual bool CanRetainMeasurement => false;
    static abstract bool IsPresent
    {
        get;
    }
    static abstract void Paint(UiControlContext context, UiRect bounds, UiColor color);
}
public readonly struct UiNoBadgeIcon : IUiBadgeIcon
{
    public static bool CanRetainMeasurement => true;
    public static bool IsPresent => false;
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color)
    {
    }
}
public readonly struct UiFluentBadgePresentation : IUiBadgePresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) =>
        context.MeasureFluentBadge<UiNoBadgeIcon>(content, options);
    public static void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) =>
        context.FluentBadge<UiNoBadgeIcon>(content, options);
}
public readonly struct UiFluentBadgePresentation<TIcon> : IUiBadgePresentation where TIcon : IUiBadgeIcon
{
    public static bool CanRetainMeasurement => TIcon.CanRetainMeasurement;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) =>
        context.MeasureFluentBadge<TIcon>(content, options);
    public static void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) =>
        context.FluentBadge<TIcon>(content, options);
}

/// <summary>Default and user-derived badges share source behavior, retained identity and presentation lifetime.</summary>
public class UiBadge<TSource, TBehavior, TPresentation> : UiControlWidget
    where TBehavior : struct, IUiBadgeBehavior<TSource, TBehavior> where TPresentation : IUiBadgePresentation
{
    private TBehavior _behavior;
    private TSource _source;
    private UiFluentBadgeOptions _options;
    private string? _accessibleLabel;
    public UiBadge(TSource source, UiFluentBadgeOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        TBehavior.ValidateOptions(options);
        _source = TBehavior.Normalize(source);
        _options = options;
    }
    public TSource Source
    {
        get => _source;
        set
        {
            ThrowIfDisposed();
            var source = TBehavior.Normalize(value);
            if (EqualityComparer<TSource>.Default.Equals(_source, source))
                return;
            _source = source;
            RequestRebuild();
        }
    }
    public UiFluentBadgeOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            TBehavior.ValidateOptions(value);
            if (_options == value)
                return;
            _options = value;
            RequestRebuild();
        }
    }
    public string? AccessibleLabel
    {
        get => _accessibleLabel;
        set
        {
            ThrowIfDisposed();
            if (string.Equals(_accessibleLabel, value, StringComparison.Ordinal))
                return;
            _accessibleLabel = value;
            RequestRebuild();
        }
    }
    protected virtual UiBadgeContent ReadContent() => TBehavior.Read(Source, ref _behavior);
    protected override bool CanRetainMeasurement => TBehavior.CanRetainMeasurement && TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        TBehavior.ValidateOptions(Options);
        var content = ReadContent();
        return context.MeasureBadge<TPresentation>(content with
        {
            AccessibleLabel = AccessibleLabel ?? content.AccessibleLabel
        }, Options);
    }
    protected override void RenderControl(UiImmediateContext ui)
    {
        TBehavior.ValidateOptions(Options);
        var content = ReadContent();
        ui.Badge<TPresentation>(content with
        {
            AccessibleLabel = AccessibleLabel ?? content.AccessibleLabel
        }, Options, constrainLayout : true);
    }
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            TBehavior.Release(ref _behavior);
        }
        finally
        {
            _behavior = default;
            _source = default!;
            _accessibleLabel = null;
            base.ReleaseOwnedResources();
        }
    }
}

public class UiCounterBadge<TPresentation> : UiBadge<UiCounterBadgeValue, UiCounterBadgeBehavior, TPresentation>
    where TPresentation : IUiBadgePresentation
{
    public UiCounterBadge(int count = 0, int overflowCount = 99, bool showZero = false, bool dot = false,
        UiFluentBadgeOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(new(count, overflowCount, showZero, dot), options, identity)
    {
    }
    public int Count
    {
        get => Source.Count;
        set => Source = Source with { Count = value };
    }
    public int OverflowCount
    {
        get => Source.OverflowCount ?? 99;
        set => Source = Source with { OverflowCount = value };
    }
    public bool ShowZero
    {
        get => Source.ShowZero;
        set => Source = Source with { ShowZero = value };
    }
    public bool Dot
    {
        get => Source.Dot;
        set => Source = Source with { Dot = value };
    }
    public string? Text
    {
        get => Source.Text;
        set => Source = Source with { Text = value };
    }
}

[UiWidgetStyle(typeof(UiBadge<,,>), typeof(string), typeof(UiTextBadgeBehavior), typeof(UiFluentBadgePresentation))]
[UiComponent("Badge", UiComponentExposure.Fluent)]
public partial class UiFluentBadge;

[UiWidgetStyle(typeof(UiBadge<,,>), typeof(Func<string>), typeof(UiGetterBadgeBehavior), typeof(UiFluentBadgePresentation))]
[UiComponent("Badge", UiComponentExposure.Fluent)]
public partial class UiFluentDynamicBadge;

[UiWidgetStyle(typeof(UiCounterBadge<>), typeof(UiFluentBadgePresentation))]
[UiComponent("CounterBadge", UiComponentExposure.Fluent)]
public partial class UiFluentCounterBadge;

[UiWidgetStyle(typeof(UiBadge<,,>), typeof(Func<UiCounterBadgeValue>), typeof(UiGetterCounterBadgeBehavior), typeof(UiFluentBadgePresentation))]
[UiComponent("CounterBadge", UiComponentExposure.Fluent)]
public partial class UiFluentDynamicCounterBadge;
