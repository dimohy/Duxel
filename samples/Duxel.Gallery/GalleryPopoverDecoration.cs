using Duxel.Core;

namespace Duxel.Gallery;

internal readonly struct GalleryPopoverDecoration : IUiPopoverPresentation
{
    public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => UiFluentPopoverPresentation.BeginTriggerText(context);
    public static UiControlTypographyScope BeginBodyText(UiControlContext context) => UiFluentPopoverPresentation.BeginBodyText(context);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => UiFluentPopoverPresentation.MeasureTrigger(context, label);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
        => UiFluentPopoverPresentation.PaintTrigger(context, label, bounds, interaction);
    public static float Padding(UiPopoverOptions options) => UiFluentPopoverPresentation.Padding(options);
    public static UiColor Foreground(UiControlContext context, UiPopoverOptions options) => UiFluentPopoverPresentation.Foreground(context, options);
    public static UiRect PaintBounds(UiControlContext context, UiPopupLayout layout, UiPopoverOptions options)
    {
        var body = layout.Bounds;
        return new(body.X - 128, body.Y - 88, body.Width + 256, body.Height + 176);
    }
    public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options)
    {
        var body = layout.Bounds;
        context.Frame(new(body.X - 120, body.Y - 16, body.Width + 240, body.Height + 32),
            new(0, 120, 212, 28), new(0, 120, 212, 150), 12, 1);
        UiFluentPopoverPresentation.PaintSurface(context, surface, layout, options);
    }
}
