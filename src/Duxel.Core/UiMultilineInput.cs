namespace Duxel.Core;

public readonly record struct UiMultilineInputOptions
{
    public UiInputTextFlags Flags
    {
        get; init;
    }
    public string? Placeholder
    {
        get; init;
    }
    public bool Invalid
    {
        get; init;
    }
    public bool? Wrap
    {
        get; init;
    }
    internal bool ReadOnly => (Flags & UiInputTextFlags.ReadOnly) != 0;
    internal void Validate()
    {
        if ((Flags & ~(UiInputTextFlags.ReadOnly | UiInputTextFlags.AutoSelectAll)) != 0)
            throw new ArgumentOutOfRangeException(nameof(Flags));
    }
}

public readonly record struct UiMultilineInputLayout(UiRect Field, float LineHeight)
{
    public UiMultilineInputOptions Options
    {
        get; init;
    }
    public UiRect? Content
    {
        get; init;
    }
    public bool Wrap
    {
        get; init;
    }
    internal UiVisualTextLines? VisualLines
    {
        get; init;
    }
    internal UiMultilineInputOwner? InputOwner
    {
        get; init;
    }
    internal UiStyledTextSource? StyledSource
    {
        get; init;
    }
    internal string? StyledEditorId
    {
        get; init;
    }
    public UiRect ContentBounds => Content ?? new(Field.X + 6, Field.Y + 4,
        MathF.Max(0, Field.Width - 12), MathF.Max(0, Field.Height - 8));
    public UiVector2 Size => new(Field.Width, Field.Height);
    public UiMultilineInputLayout At(UiVector2 origin) => this with
    {
        Field = Field with
        {
            X = Field.X + origin.X,
            Y = Field.Y + origin.Y
        },
        Content = Content is { } content
            ? content with
            {
                X = content.X + origin.X,
                Y = content.Y + origin.Y
            } : null
    };

    internal void Validate()
    {
        Options.Validate();
        static bool Valid(UiRect rect) => float.IsFinite(rect.X) && float.IsFinite(rect.Y)
            && float.IsFinite(rect.Width) && float.IsFinite(rect.Height) && rect.Width >= 0 && rect.Height >= 0;
        if (!Valid(Field) || !Valid(ContentBounds) || !float.IsFinite(LineHeight) || LineHeight <= 0)
            throw new InvalidOperationException("Multiline input requires finite geometry and a positive line height.");
    }
}

public readonly record struct UiMultilineEditResult(
    string Id,
    string Text,
    UiTextSelection Selection,
    int CaretIndex,
    float ScrollX,
    float ScrollY,
    float MaximumScrollX,
    float MaximumScrollY,
    float ContentWidth,
    float ContentHeight,
    string? CompositionText,
    float CompositionWidth,
    bool Active,
    bool Hovered,
    bool Changed)
{
    public UiAccessibilityTextInfo? AccessibilityText
    {
        get; init;
    }
    internal UiVisualTextLines? VisualLines
    {
        get; init;
    }
    internal int VisualVersion
    {
        get; init;
    }
    internal UiRect? VisualContent
    {
        get; init;
    }
    internal UiVisualTextLines? CompositionLines
    {
        get; init;
    }
    internal int CompositionVersion
    {
        get; init;
    }
}

public interface IUiMultilineInputBehavior
{
    static virtual bool AccessibilityFocus => false;
    static virtual bool AccessibilityValue => false;
    static abstract UiMultilineEditResult Process(UiControlContext context, string resolvedId,
        ref string value, int maxLength, in UiMultilineInputLayout layout);
}

public interface IUiMultilineInputPresentation
{
    /// <summary>Promises measurement from declared extents, options and the text environment, independently of editor values.</summary>
    static virtual bool CanRetainMeasurement => false;
    static virtual UiControlTypographyScope BeginText(UiControlContext context) => default;
    static abstract UiMultilineInputLayout Measure(UiControlContext context, float width, float height);
    static virtual UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
        throw new NotSupportedException("This multiline presentation must implement Arrange to accept parent allocation.");
    static abstract void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit);
}

public readonly struct UiStandardMultilineInputBehavior : IUiMultilineInputBehavior
{
    public static bool AccessibilityValue => true;
    public static UiMultilineEditResult Process(UiControlContext context, string resolvedId,
        ref string value, int maxLength, in UiMultilineInputLayout layout) =>
        context.EditMultiline(resolvedId, ref value, maxLength, layout);
}

public sealed class UiMultilineInputOwner : IDisposable
{
    private readonly UiTextInputOwner _editor = new();
    private readonly UiScrollOwner _scrollbars = new();
    private UiVisualTextLines? _lines;
    internal UiVisualTextLines Lines => _lines ??= new();
    internal void UsePreparedLines(UiVisualTextLines lines) => _lines = lines;
    internal void ClearLines() => _lines?.Clear();
    internal UiScrollOwner Scrollbars => _scrollbars;
    internal void Validate() => _editor.Validate();
    internal void Bind(UiImmediateContext context, string id)
    {
        Validate();
        if (!_scrollbars.IsBoundTo(context, id))
        {
            _scrollbars.ReleaseAuxiliary("##dragAutoScrollTime");
            ClearLines();
        }
        _editor.Bind(context, id);
        _scrollbars.Bind(context, id);
    }
    public void Dispose()
    {
        _scrollbars.ReleaseAuxiliary("##dragAutoScrollTime");
        ClearLines();
        _lines = null;
        _scrollbars.Release();
        _editor.Dispose();
    }
}

/// <summary>Owns one multiline editor while statically selecting its behavior and presentation.</summary>
[UiComponent("MultilineInput", UiComponentExposure.Infrastructure)]
public class UiMultilineInput<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiMultilineInputBehavior where TPresentation : IUiMultilineInputPresentation
{
    private string _label;
    private UiBinding<string>? _value;
    private readonly UiMultilineInputOwner _owner = new();
    private float _width;
    private float _height;
    private int _maxLength;
    private UiMultilineInputOptions _options;

    public UiMultilineInput(string label, UiBinding<string> value, int maxLength = 4096,
        float width = 0, float height = 0, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);
        ValidateExtent(width, nameof(width));
        ValidateExtent(height, nameof(height));
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        _label = label;
        _value = value;
        _width = width;
        _height = height;
        _maxLength = maxLength;
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
    public UiBinding<string> Value => _value ?? throw new ObjectDisposedException(nameof(UiMultilineInput<TBehavior, TPresentation>));
    public UiMultilineInputOptions Options
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
    public float Width
    {
        get => _width;
        set
        {
            ThrowIfDisposed();
            ValidateExtent(value, nameof(value));
            if (_width == value)
                return;
            _width = value;
            RequestRebuild();
        }
    }
    public float Height
    {
        get => _height;
        set
        {
            ThrowIfDisposed();
            ValidateExtent(value, nameof(value));
            if (_height == value)
                return;
            _height = value;
            RequestRebuild();
        }
    }
    public int MaxLength
    {
        get => _maxLength;
        set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _maxLength = value;
            RequestRebuild();
        }
    }

    protected virtual void OnChanged(string value) => Value.Value = value;
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) =>
        context.MeasureMultiline<TPresentation>(Width, Height, Options);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var value = Value.Value;
        if (ui.InputTextMultiline<TBehavior, TPresentation>(PrimaryInputId(Label), Label,
            ref value, MaxLength, Width, Height, _owner, constrainLayout: true, Options))
            OnChanged(value);
    }

    private static void ValidateExtent(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name);
    }
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            _owner.Dispose();
        }
        finally
        {
            _value = null;
            _label = string.Empty;
            _options = default;
            base.ReleaseOwnedResources();
        }
    }
}
