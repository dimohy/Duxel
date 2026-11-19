namespace Duxel.Core;

public readonly record struct UiInputOptions(UiInputTextFlags Flags = UiInputTextFlags.None, string? Placeholder = null, bool Invalid = false)
{
    public string? ContentBefore
    {
        get; init;
    }
    public string? ContentAfter
    {
        get; init;
    }
    internal void Validate()
    {
        if ((Flags & ~(UiInputTextFlags.ReadOnly | UiInputTextFlags.AutoSelectAll)) != 0)
            throw new ArgumentOutOfRangeException(nameof(Flags));
    }
}

public readonly record struct UiInputLayout(UiVector2 Size, UiRect Field, UiRect Content, UiVector2 LabelPosition, float LineHeight)
{
    public UiInputOptions Options
    {
        get; init;
    }
    public UiRect? Editing
    {
        get; init;
    }
    internal bool BeforeChild
    {
        get; init;
    }
    internal bool AfterChild
    {
        get; init;
    }
    public bool HasBeforeChild => BeforeChild;
    public bool HasAfterChild => AfterChild;
    public UiRect? Before
    {
        get; init;
    }
    public UiRect? After
    {
        get; init;
    }
    public UiRect EditingBounds => Editing ?? Field;
    internal void Validate()
    {
        static bool Rect(UiRect rect) => float.IsFinite(rect.X) && float.IsFinite(rect.Y) && float.IsFinite(rect.Width) && float.IsFinite(rect.Height);
        if (!float.IsFinite(Size.X) || !float.IsFinite(Size.Y) || Size.X < 0 || Size.Y < 0 || !Rect(Field) || Field.Width < 0 || Field.Height < 0
            || !Rect(Content) || Editing is { } editing && (!Rect(editing) || editing.Width < 0 || editing.Height < 0)
            || Before is { } before && (!Rect(before) || before.Width < 0 || before.Height < 0) || After is { } after && (!Rect(after) || after.Width < 0 || after.Height < 0)
            || !float.IsFinite(LabelPosition.X) || !float.IsFinite(LabelPosition.Y) || !float.IsFinite(LineHeight) || LineHeight <= 0)
            throw new InvalidOperationException("Input presentation returned invalid layout.");
    }
    public UiInputLayout At(UiVector2 origin) => this with
    {
        Field = Field with
        {
            X = Field.X + origin.X,
            Y = Field.Y + origin.Y
        },
        Content = Content with
        {
            X = Content.X + origin.X,
            Y = Content.Y + origin.Y
        },
        LabelPosition = new(LabelPosition.X + origin.X, LabelPosition.Y + origin.Y),
        Editing = Offset(Editing, origin),
        Before = Offset(Before, origin),
        After = Offset(After, origin)
    };
    private static UiRect? Offset(UiRect? rect, UiVector2 origin) => rect is { } value ? value with { X = value.X + origin.X, Y = value.Y + origin.Y } : null;
}
public readonly record struct UiInputEditResult(string Id, string Text, UiTextSelection Selection, int CaretIndex, float ScrollX,
    string? CompositionText, float CompositionWidth, bool Active, bool Hovered, bool Changed)
{
    internal UiSingleLineTextLayout? LineLayout
    {
        get; init;
    }
    internal UiSingleLineComposition? CompositionLayout
    {
        get; init;
    }
    public UiAccessibilityTextInfo? AccessibilityText
    {
        get; init;
    }
    public bool Held
    {
        get; init;
    }
    public bool FocusWithin
    {
        get; init;
    }
}
public readonly record struct UiInputTextPaint(UiColor Text, UiColor Selection, UiColor Caret)
{
    public bool Typography
    {
        get; init;
    }
}

public interface IUiTextInputBehavior
{
    static virtual bool AccessibilityValue => false;
    static virtual bool AccessibilityFocus => false;
    static abstract UiInputEditResult Process(UiControlContext context, string resolvedId, ref string value, int maxLength, in UiInputLayout layout);
}
public interface IUiInputPresentation
{
    /// <summary>Promises measurement from options, declared slot sizes and the text environment, independently of editor state.</summary>
    static virtual bool CanRetainMeasurement => false;
    static virtual UiControlTypographyScope BeginText(UiControlContext context) => default;
    static virtual bool SupportsChildren => false;
    static abstract UiInputLayout Measure(UiControlContext context, string label, float width);
    static virtual UiInputLayout MeasureContent(UiControlContext context, in UiInputLayout local, UiInputOptions options) => local;
    static virtual UiInputLayout Arrange(UiControlContext context, in UiInputLayout local, UiInputOptions options, UiVector2 allocatedSize)
        => throw new NotSupportedException("This Input presentation must implement Arrange to accept a different parent allocation.");
    static virtual UiInputLayout Place(UiControlContext context, in UiInputLayout local, UiVector2 origin) => local.At(origin);
    static virtual void PaintBeforeEdit(UiControlContext context, string label, in UiInputLayout layout)
    {
    }
    static abstract void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit);
}
public readonly struct UiStandardTextInputBehavior : IUiTextInputBehavior
{
    public static bool AccessibilityValue => true;
    public static bool AccessibilityFocus => true;
    public static UiInputEditResult Process(UiControlContext context, string resolvedId, ref string value, int maxLength, in UiInputLayout layout) =>
        context.EditSingleLine(resolvedId, ref value, maxLength, layout);
}
internal readonly struct UiLegacyTextInputBehavior : IUiTextInputBehavior
{
    public static bool AccessibilityValue => true;
    public static bool AccessibilityFocus => true;
    public static UiInputEditResult Process(UiControlContext context, string resolvedId, ref string value, int maxLength, in UiInputLayout layout) =>
        context.EditSingleLine(resolvedId, ref value, maxLength, layout, context.LegacyInputSelectAllOnFocus);
}
/// <summary>Compatibility presentation; compiled Fluent input presentations are implemented separately.</summary>
public readonly struct UiContextInputPresentation : IUiInputPresentation
{
    public static UiInputLayout Arrange(UiControlContext context, in UiInputLayout local, UiInputOptions options, UiVector2 allocatedSize) => context.ArrangeContextInput(local, allocatedSize);
    public static UiInputLayout Measure(UiControlContext context, string label, float width) => context.MeasureContextInput(label, width);
    public static UiInputLayout Place(UiControlContext context, in UiInputLayout local, UiVector2 origin) => context.PlaceContextInput(local, origin);
    public static void PaintBeforeEdit(UiControlContext context, string label, in UiInputLayout layout) => context.PaintContextInputLabel(label, layout);
    public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit) => context.PaintContextInput(layout, edit);
}

/// <summary>One logical input owns its edit history independently of its presentation.</summary>
public sealed class UiTextInputOwner : IDisposable
{
    internal Action<UiImmediateContext, string, string?>? FocusLost;
    private readonly WeakReference<UiImmediateContext> _context = new(null!);
    private string? _id;
    private bool _disposed;
    internal bool IsDisposed => _disposed;
    internal void Validate() => ObjectDisposedException.ThrowIf(_disposed, this);
    internal void Bind(UiImmediateContext context, string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_id == id && _context.TryGetTarget(out var previous) && ReferenceEquals(previous, context))
        {
            if (FocusLost is not null)
                context.RegisterTextFocusOwner(id, this);
            return;
        }
        Release();
        _id = id;
        _context.SetTarget(context);
        if (FocusLost is not null)
            context.RegisterTextFocusOwner(id, this);
    }
    private void Release()
    {
        var id = _id;
        _context.TryGetTarget(out var context);
        _id = null;
        _context.SetTarget(null!);
        if (id is not null && context is not null)
        {
            context.UnregisterTextFocusOwner(id, this);
            context.ReleaseOwnedTextInput(id);
        }
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        FocusLost = null;
        Release();
    }
}

public class UiInput<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiTextInputBehavior where TPresentation : IUiInputPresentation
{
    private string _label;
    private UiBinding<string>? _value;
    private readonly UiTextInputOwner _owner = new();
    private float _width;
    private int _maxLength;
    private UiInputOptions _options;
    private ChildSlots? _children;
    public UiInputSlot? BeforeSlot
    {
        get => _children?.Before; set => SetSlots(value, AfterSlot);
    }
    public UiInputSlot? AfterSlot
    {
        get => _children?.After; set => SetSlots(BeforeSlot, value);
    }
    public void SetSlots(UiInputSlot? before, UiInputSlot? after)
    {
        ThrowIfDisposed();
        before?.Validate();
        after?.Validate();
        if ((before.HasValue || after.HasValue) && !TPresentation.SupportsChildren)
            throw new NotSupportedException("This input presentation does not support child slots.");
        if (before is { } first && after is { } second && (first.Key == second.Key || ReferenceEquals(first.Content, second.Content)))
            throw new InvalidOperationException("Input slots require distinct keys and widget instances.");
        if (BeforeSlot == before && AfterSlot == after)
            return;
        ThrowIfBuilding();
        if (before is { } b)
            ValidateOwnedChild(b.Key, b.Content);
        if (after is { } a)
            ValidateOwnedChild(a.Key, a.Content);
        if (before is { } b2)
            OwnChild(b2.Key, b2.Content);
        if (after is { } a2)
            OwnChild(a2.Key, a2.Content);
        _children ??= new(this);
        _children.Before = before;
        _children.After = after;
        RequestRebuild();
    }
    protected override IUiView Build(UiBuildContext context)
    {
        var children = _children!;
        if (children.Before is { } before)
            context.Child(before.Key, () => before.Content);
        if (children.After is { } after)
            context.Child(after.Key, () => after.Content);
        return children;
    }
    protected override void RenderContent(UiImmediateContext ui)
    {
        if (_children is null)
            RenderControl(ui);
        else
            RenderBuiltContent(ui);
    }
    private sealed class ChildSlots(UiInput<TBehavior, TPresentation> owner) : UiInputChildren, IUiView
    {
        public void Render(UiImmediateContext ui) => owner.RenderControl(ui);
    }
    public UiInput(string label, UiBinding<string> value, int maxLength, float width = 0, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        _label = label;
        _value = value;
        _maxLength = maxLength;
        _width = width;
    }
    public string Label
    {
        get => _label; set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_label == value)
                return;
            _label = value;
            RequestRebuild();
        }
    }
    public UiBinding<string> Value => _value ?? throw new ObjectDisposedException(nameof(UiInput<TBehavior, TPresentation>));
    public UiInputOptions Options
    {
        get => _options; set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            _options = value;
            RequestRebuild();
        }
    }
    public float Width
    {
        get => _width; set
        {
            ThrowIfDisposed();
            if (!float.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_width == value)
                return;
            _width = value;
            RequestRebuild();
        }
    }
    public int MaxLength
    {
        get => _maxLength; set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_maxLength == value)
                return;
            _maxLength = value;
            RequestRebuild();
        }
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected virtual void OnChanged(string value) => Value.Value = value;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureInput<TPresentation>(Label, Width, Options, _children);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var value = Value.Value;
        if (ui.InputText<TBehavior, TPresentation>(PrimaryInputId(Label), Label, ref value, MaxLength, Width, _owner, Options, _children, constrainLayout: true))
            OnChanged(value);
    }
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            _owner.Dispose();
        }
        finally { _value = null; _label = string.Empty; _options = default; _children = null; base.ReleaseOwnedResources(); }
    }
}
