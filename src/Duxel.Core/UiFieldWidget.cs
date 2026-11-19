namespace Duxel.Core;

public enum UiFieldValidationState
{
    None, Error, Warning, Success
}
public readonly record struct UiFieldOptions
{
    public string? Label
    {
        get; init;
    }
    public string? Hint
    {
        get; init;
    }
    public string? ValidationMessage
    {
        get; init;
    }
    public UiFieldValidationState? ValidationState
    {
        get; init;
    }
    public bool Required
    {
        get; init;
    }
    public bool Disabled
    {
        get; init;
    }
    public UiFieldValidationState EffectiveValidationState => ValidationState ?? (string.IsNullOrEmpty(ValidationMessage) ? UiFieldValidationState.None : UiFieldValidationState.Error);
    internal void Validate()
    {
        if (ValidationState is { } state && (uint)state > 3)
            throw new ArgumentOutOfRangeException(nameof(ValidationState));
    }
}

/// <summary>Control-facing field information. Platform accessibility providers must explicitly consume this contract.</summary>
public readonly record struct UiFieldContext(string? Label, string? Hint, string? ValidationMessage, bool Required, UiFieldValidationState ValidationState)
{
    public bool Invalid => ValidationState == UiFieldValidationState.Error;
}

public readonly record struct UiFieldLayout(UiVector2 Size, UiRect Control, UiRect Label, UiRect RequiredIndicator, UiRect Validation, UiRect ValidationIcon, UiRect Hint,
    UiControlTextBlock LabelText, UiControlTextBlock ValidationText, UiControlTextBlock HintText)
{
    public UiRect? LabelTarget
    {
        get; init;
    }
    public UiRect? LabelClip
    {
        get; init;
    }
}
public interface IUiFieldPresentation
{
    static virtual bool ReuseLayout => false;
    static virtual float ControlWidth(float fieldWidth) => fieldWidth;
    /// <summary>Natural outer width for an explicit control width, including label columns.</summary>
    static virtual float FieldWidth(float controlWidth) =>
        throw new NotSupportedException("This Field presentation must declare FieldWidth for constrained explicit control sizes.");
    /// <summary>Arranges a local copy after width-dependent natural text has been measured.</summary>
    static virtual UiFieldLayout Arrange(UiControlContext context, UiFieldOptions options, in UiFieldLayout natural, UiVector2 allocatedSize) =>
        throw new NotSupportedException("This Field presentation must implement Arrange to accept a changed parent allocation.");
    static abstract UiFieldLayout Measure(UiControlContext context, UiFieldOptions options, UiVector2 controlSize);
    static abstract void Paint(UiControlContext context, UiFieldOptions options, in UiFieldLayout layout, UiVector2 origin);
}
/// <summary>One bounded, weak-context layout cache for a logical field.</summary>
public sealed class UiFieldContent<TPresentation> : IDisposable where TPresentation : IUiFieldPresentation
{
    private readonly WeakReference<UiImmediateContext> _context = new(null!);
    private UiControlTextLayoutKey _key;
    private UiTypographyFonts? _fonts;
    private UiFieldOptions _options;
    private UiVector2 _size;
    private bool _valid, _disposed;
    internal UiFieldLayout Layout;
    internal int BuildCount
    {
        get;
        private set;
    }
    internal void Measure(UiImmediateContext ui, UiControlContext context, UiFieldOptions options, UiVector2 size, UiControlTextLayoutKey key, UiTypographyFonts? fonts, bool cacheable)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_valid && TPresentation.ReuseLayout && cacheable && _options == options && _size == size && _key == key && _fonts == fonts && _context.TryGetTarget(out var previous) && ReferenceEquals(previous, ui))
            return;
        Clear();
        Layout = TPresentation.Measure(context, options, size);
        BuildCount++;
        var chars = (long)(options.Label?.Length ?? 0) + (options.Hint?.Length ?? 0) + (options.ValidationMessage?.Length ?? 0);
        var derived = (long)Layout.LabelText.RetainedCharacters + Layout.ValidationText.RetainedCharacters + Layout.HintText.RetainedCharacters;
        var lines = (long)Layout.LabelText.LineCount + Layout.ValidationText.LineCount + Layout.HintText.LineCount;
        _valid = TPresentation.ReuseLayout && cacheable && chars <= 1024 && derived <= 4096 && lines <= 256 && BoundedFonts(fonts);
        if (_valid)
        {
            _context.SetTarget(ui);
            _key = key;
            _fonts = fonts;
            _options = options;
            _size = size;
        }
    }
    internal void EndFrame()
    {
        if (!_valid)
            Clear();
    }
    private static bool BoundedFonts(UiTypographyFonts? fonts) => fonts is null || fonts.Regular.Length <= 1024 && fonts.Semibold.Length <= 1024 && fonts.Bold.Length <= 1024
        && (fonts.SecondaryRegular?.Length ?? 0) <= 1024 && (fonts.SecondarySemibold?.Length ?? 0) <= 1024 && (fonts.SecondaryBold?.Length ?? 0) <= 1024;
    private void Clear()
    {
        _valid = false;
        _context.SetTarget(null!);
        _key = default;
        _fonts = null;
        _options = default;
        Layout = default;
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
    }
}
public interface IUiFieldBehavior
{
    static abstract bool FocusRequested(UiControlContext context, string id, UiRect labelBounds);
}
public readonly struct UiLabelFieldBehavior : IUiFieldBehavior
{
    public static bool FocusRequested(UiControlContext context, string id, UiRect labelBounds) => context.FieldLabelPressed(id, labelBounds);
}

/// <summary>Owns a real form control using the same keyed lifetime as user-composed widgets.</summary>
public class UiField<TBehavior, TPresentation> : UiWidget
    where TBehavior : IUiFieldBehavior where TPresentation : IUiFieldPresentation
{
    private UiWidget? _control;
    private UiVector2 _controlSize;
    private UiFieldOptions _options;
    private float _width;
    private bool _natural;
    private readonly FieldView _view;
    private readonly UiFieldContent<TPresentation> _content = new();
    private UiRetainedLayout<UiVector2>? _childMeasurement;
    internal override void InvalidateRetainedMeasurement()
    {
        _childMeasurement?.Invalidate();
        _childMeasurement?.Clear();
    }
    public UiField(UiWidget control, UiVector2 controlSize, UiFieldOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(control);
        ValidateSize(controlSize);
        options.Validate();
        OwnChild("control", control);
        _control = control;
        _controlSize = controlSize;
        _options = options;
        _view = new(this);
    }
    public UiField(UiWidget control) : base(UiWidgetIdentity.Instance)
    {
        ArgumentNullException.ThrowIfNull(control);
        OwnChild("control", control);
        _control = control;
        _natural = true;
        _view = new(this);
    }
    public UiWidget Control => _control ?? throw new ObjectDisposedException(nameof(UiField<TBehavior, TPresentation>));
    public bool UsesNaturalSize => _natural;
    /// <summary>Sets natural child measurement; zero uses the available field width.</summary>
    public float Width
    {
        get => _width;
        set
        {
            ThrowIfDisposed();
            ValidateWidth(value);
            if (_natural && _width == value)
                return;
            ThrowIfBuilding();
            _width = value;
            _natural = true;
            RequestRebuild();
        }
    }
    public UiVector2 ControlSize
    {
        get => _controlSize;
        set
        {
            ThrowIfDisposed();
            ValidateSize(value);
            if (!_natural && _controlSize == value)
                return;
            ThrowIfBuilding();
            _controlSize = value;
            _natural = false;
            RequestRebuild();
        }
    }
    public UiFieldOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    protected override IUiView Build(UiBuildContext context)
    {
        context.Child("control", () => Control);
        return _view;
    }
    protected override bool CanRetainMeasurement => TPresentation.ReuseLayout && CanRetainOwnedMeasurement(Control);
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureOwnedField<TPresentation>(
        this, Control, _natural ? null : ControlSize, Width, Options, _content, ref _childMeasurement);
    private sealed class FieldView(UiField<TBehavior, TPresentation> owner) : IUiView
    {
        public void Render(UiImmediateContext ui)
        {
            if (owner._natural)
                ui.AutoOwnedField<TBehavior, TPresentation>(owner, owner.Control, owner.Width, owner.Options, owner._content, ref owner._childMeasurement);
            else
                ui.Field<TBehavior, TPresentation>(owner.Control, owner.ControlSize, owner.Options, owner._content, constrainLayout : true);
        }
    }
    private static void ValidateWidth(float width)
    {
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
    }
    private static void ValidateSize(UiVector2 size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
    }
    private protected override void ReleaseOwnedResources()
    {
        _content.Dispose();
        _childMeasurement = null;
        _control = null;
        _options = default;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiField<,>), typeof(UiLabelFieldBehavior), typeof(UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>))]
[UiComponent("Field", UiComponentExposure.Fluent)]
public partial class UiFluentField;
