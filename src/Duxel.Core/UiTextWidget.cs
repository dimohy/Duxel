namespace Duxel.Core;

public readonly record struct UiTextLayout(UiVector2 Position, UiVector2 Size, UiRect ClipRect, bool Visible);

/// <summary>A compiled text layout/paint implementation, independent of the content source.</summary>
public interface IUiTextPresentation
{
    static virtual bool CanRetainMeasurement => false;
    /// <summary>Uses one common PlaceText/LayoutText/typography root and accepts the parent's assigned size.</summary>
    static virtual bool CanArrange => false;
    static abstract void Render(UiControlContext context, string text, UiColor? color);
    static virtual UiVector2 Measure(UiControlContext context, string text) => throw new NotSupportedException("The text presentation must provide pure measurement for layout containers.");
}

public readonly struct UiFluentTextPresentation : IUiTextPresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => context.MeasureTypography<UiRegularTypeface, UiFluentType300>(text, wrap: true);
    public static void Render(UiControlContext context, string text, UiColor? color) =>
        context.TypographyWrapped<UiRegularTypeface, UiFluentType300>(text, color);
}

public readonly struct UiFluentWrappedTextPresentation : IUiTextPresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => UiFluentTextPresentation.Measure(context, text);
    public static void Render(UiControlContext context, string text, UiColor? color) =>
        UiFluentTextPresentation.Render(context, text, color);
}

/// <summary>Explicit compatibility presentation using the surrounding context font and line metrics.</summary>
public readonly struct UiContextTextPresentation : IUiTextPresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => context.MeasureText(text);
    public static void Render(UiControlContext context, string text, UiColor? color)
    {
        var layout = context.LayoutText(text);
        if (layout.Visible)
            context.PaintText(text, layout, color ?? context.Theme.Text);
    }
}

public readonly struct UiContextWrappedTextPresentation : IUiTextPresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => context.MeasureText(context.WrapText(text));
    public static void Render(UiControlContext context, string text, UiColor? color) =>
        UiContextTextPresentation.Render(context, context.WrapText(text), color);
}

public interface IUiTextSource<T>
{
    /// <summary>Read depends only on the source replaced through the widget's Source property.</summary>
    static virtual bool CanRetainMeasurement => false;
    static abstract T Normalize(T source);
    static abstract string Read(T source);
}

public readonly struct UiStaticTextSource : IUiTextSource<string>
{
    public static bool CanRetainMeasurement => true;
    public static string Normalize(string source) => source ?? string.Empty;
    public static string Read(string source) => source;
}

public readonly struct UiGetterTextSource : IUiTextSource<Func<string>>
{
    public static Func<string> Normalize(Func<string> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }
    public static string Read(Func<string> source) => source();
}

/// <summary>Both content sourcing and presentation are concrete compile-time policies.</summary>
public class UiText<TSource, TSourcePolicy, TPresentation> : UiControlWidget
    where TSourcePolicy : IUiTextSource<TSource>
    where TPresentation : IUiTextPresentation
{
    private TSource _source;
    private UiColor? _color;
    private string? _accessibilityIdentity;
    public UiText(TSource source, UiColor? color = null, UiWidgetIdentity identity = UiWidgetIdentity.Parent) : base(identity)
    {
        _source = TSourcePolicy.Normalize(source);
        _color = color;
    }
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
    public UiColor? Color
    {
        get => _color;
        set
        {
            ThrowIfDisposed();
            if (_color == value)
                return;
            _color = value;
            RequestRebuild();
        }
    }
    protected virtual string ReadText() => TSourcePolicy.Read(_source);
    protected override bool CanRetainMeasurement => TSourcePolicy.CanRetainMeasurement && TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureText<TPresentation>(ReadText());
    protected override void RenderControl(UiImmediateContext ui) =>
        ui.RenderTextWidget<TPresentation>(ref _accessibilityIdentity, ReadText(), Color);
    private protected override void ReleaseOwnedResources()
    {
        _source = default!;
        _accessibilityIdentity = null;
        base.ReleaseOwnedResources();
    }
}

public class UiText<TPresentation> : UiText<string, UiStaticTextSource, TPresentation>
    where TPresentation : IUiTextPresentation
{
    public UiText(string text, UiColor? color = null, UiWidgetIdentity identity = UiWidgetIdentity.Parent) : base(text, color, identity) { }
    public string Text
    {
        get => Source; set => Source = value;
    }
}
