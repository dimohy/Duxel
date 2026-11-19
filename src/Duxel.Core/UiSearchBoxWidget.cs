namespace Duxel.Core;

public enum UiSearchBoxSize
{
    Medium, Small, Large
}

public readonly record struct UiSearchBoxOptions(float Width = 250, UiSearchBoxSize Size = UiSearchBoxSize.Medium,
    UiInputAppearance Appearance = UiInputAppearance.Outline, string? Placeholder = null,
    bool ReadOnly = false, bool Invalid = false, bool RightToLeft = false, string ClearLabel = "Clear search")
{
    public UiSearchBoxOptions() : this(250, UiSearchBoxSize.Medium, UiInputAppearance.Outline, null, false, false, false, "Clear search") { }
    internal UiSearchBoxOptions WithInheritedDirection(bool rightToLeft) =>
        rightToLeft && !RightToLeft ? this with { RightToLeft = true } : this;

    internal void Validate()
    {
        if (!float.IsFinite(Width) || Width <= 0 || (uint)Size > 2 || (uint)Appearance > 3)
            throw new ArgumentOutOfRangeException(nameof(UiSearchBoxOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(ClearLabel);
        if (UiAccessibilityTree.ContainsControl(ClearLabel, false) || Placeholder is { } placeholder && UiAccessibilityTree.ContainsControl(placeholder, false))
            throw new ArgumentException("Search labels must be single-line text.");
    }
}

public interface IUiSearchBoxPresentation
{
    static virtual UiAccessibilityRole EditorRole => UiAccessibilityRole.SearchBox;
    static virtual bool KeepClearForText => false;
    static abstract UiControlTypographyScope BeginText(UiControlContext context, UiSearchBoxOptions options);
    static abstract UiVector2 Measure(UiControlContext context, UiSearchBoxOptions options);
    static abstract UiInputLayout Arrange(UiControlContext context, UiRect bounds, UiSearchBoxOptions options, bool focused);
    static abstract void Paint(UiControlContext context, UiInputLayout layout, UiInputEditResult edit,
        UiSearchBoxOptions options, UiButtonInteraction clear, bool showClear);
}

/// <summary>A single-line search editor with a decorative search icon and a non-tab-stop clear action.</summary>
public class UiSearchBox<TPresentation> : UiControlWidget, IUiLabelTarget where TPresentation : IUiSearchBoxPresentation
{
    private UiBinding<string>? _text;
    private readonly UiTextInputOwner _editor = new();
    private UiSearchBoxOptions _options;
    public string Label
    {
        get;
    }
    public int MaxLength
    {
        get;
    }
    public UiBinding<string> Text => _text ?? throw new ObjectDisposedException(GetType().Name);
    public UiSearchBoxOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (value == _options)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiSearchBox(string label, UiBinding<string> text, int maxLength = 4096,
        UiSearchBoxOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(text);
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        _options = options ?? new();
        _options.Validate();
        Label = label;
        _text = text;
        MaxLength = maxLength;
    }
    internal static string ValidateText(string? text, int maxLength)
    {
        if (text is null || text.Length > maxLength || UiAccessibilityTree.ContainsControl(text, false))
            throw new InvalidOperationException("Search text must satisfy its non-null, single-line, maximum-length contract.");
        return text;
    }
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.LayoutConstraints.Constrain(TPresentation.Measure(context, _options.WithInheritedDirection(context.RightToLeft)));
    protected override void RenderControl(UiImmediateContext ui)
        => ui.RenderSearchBox<TPresentation>(PrimaryInputId(Label), Label, Text, MaxLength, _editor,
            _options.WithInheritedDirection(ui.RightToLeft));
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            _editor.Dispose();
        }
        finally { _text = null; base.ReleaseOwnedResources(); }
    }
}

[UiWidgetStyle(typeof(UiSearchBox<>), typeof(UiFluentSearchBoxPresentation))]
[UiComponent("SearchBox", UiComponentExposure.Fluent)]
public partial class UiFluentSearchBox;
