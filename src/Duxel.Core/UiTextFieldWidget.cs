namespace Duxel.Core;

public readonly record struct UiTextFieldOptions(float Width = 320, string? Placeholder = null,
    bool ReadOnly = false, bool RightToLeft = false, string ClearLabel = "Clear text")
{
    public UiTextFieldOptions() : this(320) { }

    internal UiSearchBoxOptions EditorOptions => new(Width, Placeholder: Placeholder, ReadOnly: ReadOnly,
        RightToLeft: RightToLeft, ClearLabel: ClearLabel);
}

/// <summary>A short-text field combining a label, helper or validation message, and a clearable editor.</summary>
public class UiTextFieldWidget
    : UiField<UiLabelFieldBehavior, UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>>
{
    private UiTextFieldOptions _inputOptions;
    private Editor Input => (Editor)Control;

    public UiTextFieldWidget(string label, UiBinding<string> text, int maxLength = 4096,
        UiTextFieldOptions? inputOptions = null, UiFieldOptions field = default)
        : base(new Editor(label, text, maxLength, inputOptions ?? new()))
    {
        _inputOptions = inputOptions ?? new();
        Options = field with { Label = label };
        Width = _inputOptions.Width;
    }

    public UiBinding<string> Text => Input.Text;

    public UiTextFieldOptions InputOptions
    {
        get => _inputOptions;
        set
        {
            ThrowIfDisposed();
            value.EditorOptions.Validate();
            if (_inputOptions == value)
                return;
            ThrowIfBuilding();
            Input.Options = value.EditorOptions;
            _inputOptions = value;
            Width = value.Width;
        }
    }

    private sealed class Editor(string label, UiBinding<string> text, int maxLength, UiTextFieldOptions options)
        : UiSearchBox<UiFluentTextFieldPresentation>(label, text, maxLength, options.EditorOptions, UiWidgetIdentity.Parent);
}

[UiWidgetStyle(typeof(UiTextFieldWidget))]
[UiComponent("TextField", UiComponentExposure.Fluent)]
public partial class UiFluentTextField;

internal readonly struct UiFluentTextFieldPresentation : IUiSearchBoxPresentation
{
    public static UiAccessibilityRole EditorRole => UiAccessibilityRole.Edit;
    public static bool KeepClearForText => true;

    public static UiControlTypographyScope BeginText(UiControlContext context, UiSearchBoxOptions options)
        => UiFluentMediumInputSize.BeginText(context);

    public static UiVector2 Measure(UiControlContext context, UiSearchBoxOptions options)
    {
        using var typography = BeginText(context, options);
        return new(options.Width, MathF.Max(32, context.TypographyLineHeight + 2));
    }

    public static UiInputLayout Arrange(UiControlContext context, UiRect bounds, UiSearchBoxOptions options, bool clearSlot)
    {
        var padding = MathF.Min(10, bounds.Width / 2);
        var clearWidth = clearSlot && !options.ReadOnly ? MathF.Min(24, MathF.Max(0, bounds.Width - padding * 2)) : 0;
        var end = padding + clearWidth;
        var edit = new UiRect(bounds.X + (options.RightToLeft ? end : padding), bounds.Y,
            MathF.Max(0, bounds.Width - padding - end), bounds.Height);
        var lineHeight = context.TypographyLineHeight;
        return new(new(bounds.Width, bounds.Height), bounds,
            new(edit.X, bounds.Y + (bounds.Height - lineHeight) / 2, edit.Width, lineHeight), default, lineHeight)
        {
            Editing = edit,
            After = clearWidth <= 0 ? null : new UiRect(options.RightToLeft ? bounds.X + padding : edit.X + edit.Width,
                bounds.Y, clearWidth, bounds.Height),
            Options = new(options.ReadOnly ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None, options.Placeholder, options.Invalid)
        };
    }

    private static readonly UiMenuIcon ClearIcon = UiMenuIcon.Create<UiSearchBoxDismissIcon, UiSearchBoxDismissIcon>();

    public static void Paint(UiControlContext context, UiInputLayout layout, UiInputEditResult edit,
        UiSearchBoxOptions options, UiButtonInteraction clear, bool showClear)
    {
        var focused = context.IsEnabled && (edit.Active || edit.FocusWithin) && !options.ReadOnly;
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : focused ? UiButtonVisualState.Pressed
            : edit.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var palette = UiFluentInputPalette.Resolve(context, state, options.Appearance, options.Invalid, edit.Held);
        var border = focused && !options.Invalid && context.Theme.ForcedColors is null ? palette.Focus : palette.Border;
        context.Frame(layout.Field, palette.Background, border, 4, focused ? 2 : 1);
        using (context.Clip(layout.EditingBounds))
        {
            context.PaintEditableText(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text) { Typography = true });
            if (edit.Text.Length == 0 && string.IsNullOrEmpty(edit.CompositionText) && options.Placeholder is { } placeholder)
                context.TypographyTextBlock(context.MeasureTextBlock(placeholder), layout.Content, palette.Placeholder);
        }
        if (showClear && layout.After is { } slot)
        {
            var edge = MathF.Min(16, MathF.Min(slot.Width, slot.Height));
            ClearIcon.Paint(context, new(slot.X + (slot.Width - edge) / 2, slot.Y + (slot.Height - edge) / 2, edge, edge),
                UiFluentInputPalette.Adornment(context), clear);
        }
    }
}
