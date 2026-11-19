namespace Duxel.Core;

public enum UiDividerAlignment
{
    Center, Start, End
}
public enum UiDividerAppearance
{
    Default, Subtle, Strong, Brand
}
public readonly record struct UiDividerOptions(bool Vertical = false, UiDividerAlignment Alignment = UiDividerAlignment.Center,
    UiDividerAppearance Appearance = UiDividerAppearance.Default, bool Inset = false, float Length = 0, bool RightToLeft = false)
{
    public void Validate()
    {
        if (Alignment is < UiDividerAlignment.Center or > UiDividerAlignment.End)
            throw new ArgumentOutOfRangeException(nameof(Alignment));
        if (Appearance is < UiDividerAppearance.Default or > UiDividerAppearance.Brand)
            throw new ArgumentOutOfRangeException(nameof(Appearance));
        if (!float.IsFinite(Length) || Length < 0)
            throw new ArgumentOutOfRangeException(nameof(Length));
    }
}
public interface IUiDividerPresentation
{
    static virtual bool CanRetainMeasurement => false;
    /// <summary>Accepts parent allocation by placing exactly one common text root and clipping paint to that root.</summary>
    static virtual bool CanArrange => false;
    static virtual UiVector2 Measure(UiControlContext context, string text, UiDividerOptions options) =>
        throw new NotSupportedException("This Divider presentation must provide pure measurement.");
    static abstract void Render(UiControlContext context, string text, UiDividerOptions options);
}
public readonly struct UiFluentDividerPresentation : IUiDividerPresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text, UiDividerOptions options) =>
        context.MeasureFluentDivider(text, options);
    public static void Render(UiControlContext context, string text, UiDividerOptions options) => context.FluentDivider(text, options);
}
public class UiDivider<TSource, TSourcePolicy, TPresentation> : UiControlWidget
    where TSourcePolicy : IUiTextSource<TSource> where TPresentation : IUiDividerPresentation
{
    private TSource _source;
    private UiDividerOptions _options;
    public UiDivider(TSource source, UiDividerOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        options.Validate();
        _source = TSourcePolicy.Normalize(source);
        _options = options;
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
    public UiDividerOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            _options = value;
            RequestRebuild();
        }
    }
    protected override bool CanRetainMeasurement => TSourcePolicy.CanRetainMeasurement && TPresentation.CanRetainMeasurement;
    protected virtual string ReadText() => TSourcePolicy.Read(_source);
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureDivider<TPresentation>(ReadText(), Options);
    protected override void RenderControl(UiImmediateContext ui) => ui.Divider<TPresentation>(ReadText(), Options, constrainLayout : true);
    private protected override void ReleaseOwnedResources()
    {
        _source = default!;
        base.ReleaseOwnedResources();
    }
}
public class UiDivider<TPresentation> : UiDivider<string, UiStaticTextSource, TPresentation> where TPresentation : IUiDividerPresentation
{
    public UiDivider(string text = "", UiDividerOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(text, options, identity)
    {
    }
    public string Text
    {
        get => Source;
        set => Source = value;
    }
}
[UiWidgetStyle(typeof(UiDivider<>), typeof(UiFluentDividerPresentation))]
[UiComponent("Divider", UiComponentExposure.Fluent)]
public partial class UiFluentDivider;
[UiWidgetStyle(typeof(UiDivider<,,>), typeof(Func<string>), typeof(UiGetterTextSource), typeof(UiFluentDividerPresentation))]
[UiComponent("Divider", UiComponentExposure.Fluent)]
public partial class UiFluentDynamicDivider;

public readonly record struct UiDividerPaint(UiColor Line, UiColor Content);
public static class UiFluentDividerPalette
{
    // Exact brandWeb and neutral tokens from Microsoft Fluent UI8830c10b.
    public static UiDividerPaint Resolve(UiFluentColorScheme scheme, UiDividerAppearance appearance) => (scheme, appearance) switch
    {
        (UiFluentColorScheme.Light, UiDividerAppearance.Default) => new(new(0xe0, 0xe0, 0xe0), new(0x42, 0x42, 0x42)),
        (UiFluentColorScheme.Light, UiDividerAppearance.Subtle) => new(new(0xf0, 0xf0, 0xf0), new(0x61, 0x61, 0x61)),
        (UiFluentColorScheme.Light, UiDividerAppearance.Strong) => new(new(0xd1, 0xd1, 0xd1), new(0x24, 0x24, 0x24)),
        (UiFluentColorScheme.Light, UiDividerAppearance.Brand) => new(new(0x0f, 0x6c, 0xbd), new(0x0f, 0x6c, 0xbd)),
        (UiFluentColorScheme.Dark, UiDividerAppearance.Default) => new(new(0x52, 0x52, 0x52), new(0xd6, 0xd6, 0xd6)),
        (UiFluentColorScheme.Dark, UiDividerAppearance.Subtle) => new(new(0x3d, 0x3d, 0x3d), new(0xad, 0xad, 0xad)),
        (UiFluentColorScheme.Dark, UiDividerAppearance.Strong) => new(new(0x66, 0x66, 0x66), new(0xff, 0xff, 0xff)),
        (UiFluentColorScheme.Dark, UiDividerAppearance.Brand) => new(new(0x47, 0x9e, 0xf5), new(0x47, 0x9e, 0xf5)),
        _ => throw new ArgumentOutOfRangeException(nameof(scheme))
    };
}
