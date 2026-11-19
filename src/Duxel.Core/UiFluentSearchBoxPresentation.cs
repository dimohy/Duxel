namespace Duxel.Core;

[UiFluentIcon("search")]
internal readonly partial struct UiSearchBoxSearchIcon;
[UiFluentIcon("dismiss")]
internal readonly partial struct UiSearchBoxDismissIcon;

public readonly struct UiFluentSearchBoxPresentation : IUiSearchBoxPresentation
{
    private static readonly UiMenuIcon Search = UiMenuIcon.Create<UiSearchBoxSearchIcon, UiSearchBoxSearchIcon>();
    private static readonly UiMenuIcon Dismiss = UiMenuIcon.Create<UiSearchBoxDismissIcon, UiSearchBoxDismissIcon>();
    private static float Edge(UiSearchBoxOptions o) => o.Size == UiSearchBoxSize.Small ? 16 : o.Size == UiSearchBoxSize.Large ? 24 : 20;
    private static float Padding(UiSearchBoxOptions o) => o.Size == UiSearchBoxSize.Small ? 6 : o.Size == UiSearchBoxSize.Large ? 10 : 8;
    public static UiControlTypographyScope BeginText(UiControlContext context, UiSearchBoxOptions options) => options.Size switch
    {
        UiSearchBoxSize.Small => UiFluentSmallInputSize.BeginText(context),
        UiSearchBoxSize.Large => UiFluentLargeInputSize.BeginText(context),
        _ => UiFluentMediumInputSize.BeginText(context)
    };
    public static UiVector2 Measure(UiControlContext context, UiSearchBoxOptions options)
    {
        using var typography = BeginText(context, options);
        return new(MathF.Min(468, options.Width), MathF.Max(options.Size == UiSearchBoxSize.Small ? 24 : options.Size == UiSearchBoxSize.Large ? 40 : 32, context.TypographyLineHeight + 2));
    }
    public static UiInputLayout Arrange(UiControlContext context, UiRect bounds, UiSearchBoxOptions options, bool focused)
    {
        var edge = MathF.Min(Edge(options), bounds.Width);
        var padding = MathF.Min(Padding(options), MathF.Max(0, (bounds.Width - edge) / 2));
        var before = new UiRect(options.RightToLeft ? bounds.X + bounds.Width - padding - edge : bounds.X + padding,
            bounds.Y + (bounds.Height - edge) / 2, edge, edge);
        var showClear = focused && !options.ReadOnly;
        var clear = new UiRect(options.RightToLeft ? bounds.X + padding : bounds.X + bounds.Width - padding - edge,
            before.Y, edge, edge);
        var start = padding + edge + 6;
        var end = showClear ? padding + edge + 12 : Padding(options);
        var left = bounds.X + (options.RightToLeft ? end : start);
        left = MathF.Min(bounds.X + bounds.Width, left);
        var editing = new UiRect(left, bounds.Y, MathF.Max(0, bounds.Width - start - end), bounds.Height);
        return new(new(bounds.Width, bounds.Height), bounds,
            new(editing.X, bounds.Y + (bounds.Height - context.TypographyLineHeight) / 2, editing.Width, context.TypographyLineHeight),
            default, context.TypographyLineHeight)
        {
            Before = before,
            After = showClear ? clear : null,
            Editing = editing,
            Options = new(options.ReadOnly ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None, options.Placeholder, options.Invalid)
        };
    }
    public static void Paint(UiControlContext context, UiInputLayout layout, UiInputEditResult edit,
        UiSearchBoxOptions options, UiButtonInteraction clear, bool showClear)
    {
        var focused = context.IsEnabled && (edit.Active || edit.FocusWithin);
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : focused ? UiButtonVisualState.Pressed : edit.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var palette = UiFluentInputPalette.Resolve(context, state, options.Appearance, options.Invalid, edit.Held);
        UiFluentInputFrame.Paint(context, layout.Field, palette, focused, options.Appearance);
        var icon = UiFluentInputPalette.Adornment(context);
        Search.Paint(context, layout.Before!.Value, icon, default);
        using (context.Clip(layout.EditingBounds))
        {
            context.PaintEditableText(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text)
            {
                Typography = true
            });
        }
        UiFluentInputFrame.PaintPlaceholder(context, layout, edit, options.Placeholder, options.RightToLeft, palette.Placeholder);
        if (showClear && layout.After is { } clearBounds)
            Dismiss.Paint(context, clearBounds, icon, clear);
    }
}
