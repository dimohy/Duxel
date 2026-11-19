namespace Duxel.Core;

public readonly struct UiFluentMultilineInputBehavior : IUiMultilineInputBehavior
{
    public static bool AccessibilityValue => true;
    public static bool AccessibilityFocus => true;
    public static UiMultilineEditResult Process(UiControlContext context, string resolvedId,
        ref string value, int maxLength, in UiMultilineInputLayout layout) =>
        context.EditNavigableMultiline(resolvedId, ref value, maxLength, layout);
}

public interface IUiTextareaSize
{
    static virtual bool CanRetainMeasurement => false;
    static abstract float Height { get; }
    static abstract float HorizontalPadding { get; }
    static abstract float VerticalPadding { get; }
    static abstract UiControlTypographyScope BeginText(UiControlContext context);
}

public readonly struct UiFluentSmallTextareaSize : IUiTextareaSize
{
    public static bool CanRetainMeasurement => true;
    public static float Height => 44;
    public static float HorizontalPadding => 8;
    public static float VerticalPadding => 4;
    public static UiControlTypographyScope BeginText(UiControlContext context) =>
        context.BeginTypography<UiRegularTypeface, UiFluentType200>();
}

public readonly struct UiFluentMediumTextareaSize : IUiTextareaSize
{
    public static bool CanRetainMeasurement => true;
    public static float Height => 56;
    public static float HorizontalPadding => 12;
    public static float VerticalPadding => 6;
    public static UiControlTypographyScope BeginText(UiControlContext context) =>
        context.BeginTypography<UiRegularTypeface, UiFluentType300>();
}

public readonly struct UiFluentLargeTextareaSize : IUiTextareaSize
{
    public static bool CanRetainMeasurement => true;
    public static float Height => 68;
    public static float HorizontalPadding => 14;
    public static float VerticalPadding => 8;
    public static UiControlTypographyScope BeginText(UiControlContext context) =>
        context.BeginTypography<UiRegularTypeface, UiFluentType400>();
}

public readonly struct UiFluentTextareaPresentation<TSize, TAppearance> : IUiMultilineInputPresentation
    where TSize : IUiTextareaSize where TAppearance : IUiInputAppearance
{
    public static bool CanRetainMeasurement => TSize.CanRetainMeasurement && TAppearance.CanRetainMeasurement;
    public static UiControlTypographyScope BeginText(UiControlContext context) => TSize.BeginText(context);
    public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height)
    {
        if (TAppearance.Kind == UiInputAppearance.Underline)
            throw new NotSupportedException("Fluent Textarea supports outline and filled appearances.");
        return Layout(width > 0 ? width : MathF.Max(0, context.AvailableWidth),
            height > 0 ? height : TSize.Height, context.TypographyLineHeight);
    }
    public static UiMultilineInputLayout Arrange(UiControlContext context,
        in UiMultilineInputLayout natural, UiVector2 size) => Layout(size.X, size.Y, natural.LineHeight);
    private static UiMultilineInputLayout Layout(float width, float height, float lineHeight) =>
        new(new(0, 0, width, height), lineHeight)
        {
            Wrap = true,
            Content = new(1 + TSize.HorizontalPadding, 1 + TSize.VerticalPadding,
                MathF.Max(0, width - 2 - 2 * TSize.HorizontalPadding),
                MathF.Max(0, height - 4 - 2 * TSize.VerticalPadding))
        };
    public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : edit.Active ? UiButtonVisualState.Pressed
            : edit.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var palette = UiFluentInputPalette.Resolve(context, state, TAppearance.Kind, layout.Options.Invalid);
        TAppearance.Paint(context, layout.Field, palette, edit.Active && context.IsEnabled);
        context.PaintEditableMultiline(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text)
            { Typography = true }, palette.Placeholder);
    }
}

[UiWidgetStyle(typeof(UiMultilineInput<,>), typeof(UiFluentMultilineInputBehavior),
    typeof(UiFluentTextareaPresentation<UiFluentMediumTextareaSize, UiFluentOutlineInput>))]
[UiComponent("Textarea", UiComponentExposure.Fluent)]
public partial class UiFluentTextarea;

public sealed partial class UiImmediateContext
{
    internal UiMultilineEditResult EditNavigableMultiline(string id, ref string value, int maxLength,
        in UiMultilineInputLayout layout)
    {
        EnsureNotMeasuring();
        var hovered = ItemHoverable(id, layout.Field);
        _lastItemId = id;
        _lastItemFlags = _currentItemFlags;
        var navigation = ProcessControlKeyboard<RangeControlKeys>(layout.Field, new(false, hovered, false));
        var scrollbarCaptured = _state.ActiveId is { } active && active.StartsWith(id, StringComparison.Ordinal)
            && active.AsSpan(id.Length) is var suffix && (suffix.SequenceEqual("##scroll-v") || suffix.SequenceEqual("##scroll-h"));
        if (navigation.Focused && !scrollbarCaptured
            && (!_leftMousePressed || _fieldFocusedControlId == id || _accessibilityFocus == id) && _state.ActiveId != id)
        {
            _state.ActiveId = id;
            if ((layout.Options.Flags & UiInputTextFlags.AutoSelectAll) != 0)
            {
                _state.SetSelection(id, new(0, value.Length));
                _state.SetCursor(id, value.Length);
            }
            ResetCaretBlink();
        }
        else if (!navigation.Focused && !_leftMousePressed && _state.ActiveId == id)
        {
            _state.ActiveId = null;
            _imeHandler?.SetCompositionOwner(null);
        }
        if (hovered && IsControlEnabled)
            SetMouseCursor(UiMouseCursor.TextInput);
        return ProcessMultilineInput(id, ref value, maxLength, layout, navigable : true);
    }
}
