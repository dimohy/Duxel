namespace Duxel.Core;

public enum UiProgressShape
{
    Rounded, Square
}
public enum UiProgressThickness
{
    Medium, Large
}
public enum UiProgressColor
{
    Brand, Success, Warning, Error, Neutral
}

public readonly record struct UiProgressValue(float? Value, float Maximum = 1,
    UiProgressShape Shape = UiProgressShape.Rounded, UiProgressThickness Thickness = UiProgressThickness.Medium,
    UiProgressColor Color = UiProgressColor.Brand, bool ReducedMotion = false)
{
    public bool RightToLeft { get; init; }
    /// <summary>Keep a moving Fluent bar visible until the end of its current indeterminate cycle.</summary>
    public bool FinishIndeterminateCycle { get; init; }
    public float? Fraction => Value is { } value ? Math.Clamp(value / Maximum, 0, 1) : null;
    public UiProgressValue Validate()
    {
        if (Value is { } value && !float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(Value));
        if (!float.IsFinite(Maximum) || Maximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(Maximum));
        if (Shape is not (UiProgressShape.Rounded or UiProgressShape.Square))
            throw new ArgumentOutOfRangeException(nameof(Shape));
        if (Thickness is not (UiProgressThickness.Medium or UiProgressThickness.Large))
            throw new ArgumentOutOfRangeException(nameof(Thickness));
        if (Color is not (UiProgressColor.Brand or UiProgressColor.Success or UiProgressColor.Warning or UiProgressColor.Error or UiProgressColor.Neutral))
            throw new ArgumentOutOfRangeException(nameof(Color));
        return this;
    }
}

public interface IUiProgressSource<T>
{
    /// <summary>Measure depends only on the declared source, requested size and current layout environment.</summary>
    static virtual bool CanRetainMeasurement => false;
    static abstract T Normalize(T source);
    static abstract UiProgressValue Read(T source);
    /// <summary>Measures declared geometry without invoking the render-time value source.</summary>
    static virtual UiVector2 Measure<TPresentation>(UiControlContext context, T source, UiVector2 requestedSize)
        where TPresentation : IUiProgressPresentation => context.MeasureProgress<TPresentation>(requestedSize);
}
public readonly struct UiStaticProgressSource : IUiProgressSource<UiProgressValue>
{
    public static bool CanRetainMeasurement => true;
    public static UiProgressValue Normalize(UiProgressValue source) => source.Validate();
    public static UiProgressValue Read(UiProgressValue source) => source;
    public static UiVector2 Measure<TPresentation>(UiControlContext context, UiProgressValue source, UiVector2 requestedSize)
        where TPresentation : IUiProgressPresentation => context.MeasureProgress<TPresentation>(source, requestedSize);
}
public readonly struct UiGetterProgressSource : IUiProgressSource<Func<UiProgressValue>>
{
    public static bool CanRetainMeasurement => true;
    public static Func<UiProgressValue> Normalize(Func<UiProgressValue> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }
    public static UiProgressValue Read(Func<UiProgressValue> source) => source().Validate();
}
public interface IUiProgressPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 requestedSize);
    /// <summary>Measures source-independent geometry; the default requires an explicit presentation implementation.</summary>
    static virtual UiVector2 Measure(UiControlContext context, UiVector2 requestedSize)
        => throw new NotSupportedException("This Progress presentation must implement source-independent measurement for getter sources.");
    static virtual UiVector2 Arrange(UiControlContext context, UiVector2 naturalSize, UiVector2 allocatedSize)
        => throw new NotSupportedException("This Progress presentation must implement Arrange to accept a different parent allocation.");
    static abstract void Paint(UiControlContext context, UiProgressValue value, UiRect bounds);
}
public class UiProgress<TSource, TSourcePolicy, TPresentation> : UiControlWidget
    where TSourcePolicy : IUiProgressSource<TSource> where TPresentation : IUiProgressPresentation
{
    private TSource _source;
    private UiVector2 _size;
    public UiProgress(TSource source, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity) =>
        _source = TSourcePolicy.Normalize(source);
    public TSource Source
    {
        get => _source;
        set
        {
            ThrowIfDisposed();
            var source = TSourcePolicy.Normalize(value);
            if (EqualityComparer<TSource>.Default.Equals(_source, source))
                return;
            _source = source;
            RequestRebuild();
        }
    }
    public UiVector2 Size
    {
        get => _size;
        set
        {
            ThrowIfDisposed();
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X < 0 || value.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_size == value)
                return;
            ThrowIfBuilding();
            _size = value;
            RequestRebuild();
        }
    }
    protected override bool CanRetainMeasurement => TSourcePolicy.CanRetainMeasurement && TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) => TSourcePolicy.Measure<TPresentation>(context, _source, Size);
    protected virtual UiProgressValue ReadValue() => TSourcePolicy.Read(_source);
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderProgress<TPresentation>(ReadValue(), Size, constrainLayout: true,
        accessibilityId: PrimaryControlId);
    private protected override void ReleaseOwnedResources()
    {
        _source = default!;
        base.ReleaseOwnedResources();
    }
}
public class UiProgress<TPresentation> : UiProgress<UiProgressValue, UiStaticProgressSource, TPresentation>
    where TPresentation : IUiProgressPresentation
{
    public UiProgress(float? value, float maximum = 1, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(new(value, maximum), identity) { }
    public float? Value
    {
        get => Source.Value; set => Source = Source with { Value = value };
    }
    public float Maximum
    {
        get => Source.Maximum; set => Source = Source with { Maximum = value };
    }
    public UiProgressShape Shape
    {
        get => Source.Shape; set => Source = Source with { Shape = value };
    }
    public UiProgressThickness Thickness
    {
        get => Source.Thickness; set => Source = Source with { Thickness = value };
    }
    public UiProgressColor Color
    {
        get => Source.Color; set => Source = Source with { Color = value };
    }
    public bool RightToLeft
    {
        get => Source.RightToLeft; set => Source = Source with { RightToLeft = value };
    }
    public bool ReducedMotion
    {
        get => Source.ReducedMotion; set => Source = Source with { ReducedMotion = value };
    }
    public bool FinishIndeterminateCycle
    {
        get => Source.FinishIndeterminateCycle; set => Source = Source with { FinishIndeterminateCycle = value };
    }
}

public class UiProgress<TBehavior, TPresentation> : UiProgress<TPresentation>
    where TBehavior : struct, IUiProgressBehavior<TBehavior> where TPresentation : IUiProgressPresentation
{
    private TBehavior _behavior;
    public UiProgress(float? value, float maximum = 1, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(value, maximum, identity) { }
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderProgress<TBehavior, TPresentation>(ref _behavior, ReadValue(), Size, constrainLayout: true,
        accessibilityId: PrimaryControlId);
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            TBehavior.Release(ref _behavior);
        }
        finally
        {
            _behavior = default;
            base.ReleaseOwnedResources();
        }
    }
}

public class UiProgress<TSource, TSourcePolicy, TBehavior, TPresentation> : UiProgress<TSource, TSourcePolicy, TPresentation>
    where TSourcePolicy : IUiProgressSource<TSource> where TBehavior : struct, IUiProgressBehavior<TBehavior>
    where TPresentation : IUiProgressPresentation
{
    private TBehavior _behavior;
    public UiProgress(TSource source, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(source, identity) { }
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderProgress<TBehavior, TPresentation>(ref _behavior, ReadValue(), Size, constrainLayout: true,
        accessibilityId: PrimaryControlId);
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            TBehavior.Release(ref _behavior);
        }
        finally
        {
            _behavior = default;
            base.ReleaseOwnedResources();
        }
    }
}

public readonly struct UiFluentProgressPresentation : IUiProgressPresentation
{
    internal const double IndeterminateCycleSeconds = 3;
    public static bool CanRetainMeasurement => true;
    public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 size) =>
        new(size.X > 0 ? size.X : context.AvailableWidth, size.Y > 0 ? size.Y : value.Thickness == UiProgressThickness.Large ? 4 : 2);
    public static UiVector2 Measure(UiControlContext context, UiVector2 size)
    {
        var height = size.Y;
        if (height == 0)
        {
            var constraints = context.LayoutConstraints;
            if (constraints.MinHeight != constraints.MaxHeight)
                throw new NotSupportedException("Getter-driven Progress measurement requires Size.Y or a tight parent height; it does not read the getter to infer thickness.");
            height = constraints.MaxHeight;
        }
        return new(size.X > 0 ? size.X : context.AvailableWidth, height);
    }
    public static UiVector2 Arrange(UiControlContext context, UiVector2 naturalSize, UiVector2 allocatedSize) => allocatedSize;
    public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds)
    {
        var radius = value.Shape == UiProgressShape.Rounded ? 4 : 0;
        var forced = context.Theme.ForcedColors;
        var background = forced?.CanvasText ?? context.Theme.ProgressBarBg;
        var fill = forced?.Highlight
            ?? (value.Fraction is null ? context.Theme.ProgressBarFill : ResolveColor(context, value.Color));
        if (value.Fraction is { } fraction)
        {
            context.Frame(bounds, background, background, radius, 0);
            if (fraction > 0)
                context.Frame(bounds with
                {
                    X = value.RightToLeft ? bounds.X + bounds.Width * (1 - fraction) : bounds.X,
                    Width = bounds.Width * fraction
                }, fill, fill, radius, 0);
            return;
        }
        context.RequestAnimation(bounds);
        var phase = (float)(context.TimeSeconds % IndeterminateCycleSeconds / IndeterminateCycleSeconds);
        if (value.ReducedMotion)
        {
            context.Frame(bounds, background, background, radius, 0);
            var opacity = .2f + .8f * (1 - MathF.Abs(phase * 2 - 1));
            var pulsed = new UiColor((fill.Rgba & 0x00ffffff) | ((uint)MathF.Round((fill.Rgba >> 24) * opacity) << 24));
            context.Frame(bounds, pulsed, pulsed, radius, 0);
        }
        else
        {
            var width = bounds.Width * .33f;
            var offset = width * (-1 + 4 * phase);
            var x = value.RightToLeft ? bounds.X + bounds.Width - width - offset : bounds.X + offset;
            context.ProgressGradient(new(x, bounds.Y, width, bounds.Height), bounds, background, fill, radius);
        }
    }

    internal static UiColor ResolveColor(UiControlContext context, UiProgressColor color) => color switch
    {
        UiProgressColor.Brand => context.Theme.ProgressBarFill,
        UiProgressColor.Success => new(0x10, 0x7c, 0x10),
        UiProgressColor.Warning => new(0xda, 0x3b, 0x01),
        UiProgressColor.Error => new(0xd1, 0x34, 0x38),
        UiProgressColor.Neutral => NeutralColor(context),
        _ => throw new ArgumentOutOfRangeException(nameof(color))
    };

    private static UiColor NeutralColor(UiControlContext context) => context.FluentColorScheme switch
    {
        UiFluentColorScheme.Light => new(143, 143, 143),
        UiFluentColorScheme.Dark => new(184, 184, 184),
        _ => throw new ArgumentOutOfRangeException(nameof(context))
    };
}
