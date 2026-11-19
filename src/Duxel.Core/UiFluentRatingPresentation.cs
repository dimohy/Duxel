namespace Duxel.Core;

public readonly struct UiFluentRatingPresentation : IUiRatingPresentation
{
    public static bool CanRetainMeasurement => true;
    private static float IconSize(UiRatingOptions options) => UiFluentRatingGlyphs.Size(options.Size);
    private static int Columns(float width, UiRatingOptions options)
        => !float.IsFinite(width) ? options.Maximum : Math.Max(1, Math.Min(options.Maximum, (int)MathF.Min(options.Maximum, MathF.Floor(width / IconSize(options)))));
    public static UiVector2 Measure(UiControlContext context, UiRatingOptions options)
    {
        var columns = Columns(MathF.Min(context.AvailableWidth, context.LayoutConstraints.MaxWidth), options);
        return new(IconSize(options) * columns, IconSize(options) * MathF.Ceiling((float)options.Maximum / columns));
    }
    public static void Arrange(UiControlContext context, UiRect bounds, UiRatingOptions options, Span<UiRadioOptionLayout> layouts)
    {
        var width = IconSize(options);
        var columns = Columns(bounds.Width, options);
        for (var i = 0; i < layouts.Length; i++)
        {
            var ordinal = i * options.Step;
            var logicalX = (ordinal % columns) * width;
            var segmentWidth = width * options.Step;
            var x = options.RightToLeft ? bounds.Width - logicalX - segmentWidth : logicalX;
            layouts[i] = new(new(x, MathF.Floor(ordinal / columns) * width, segmentWidth, width), default, default, default, string.Empty, null);
        }
    }
    public static void Paint(UiControlContext context, UiRect bounds, UiRatingOptions options, float value,
        float? preview, UiRadioGroupInteraction interaction)
    {
        var displayed = MathF.Round((preview ?? value) * 2, MidpointRounding.AwayFromZero) / 2;
        var width = IconSize(options);
        var size = width;
        var columns = Columns(bounds.Width, options);
        if (context.IsHovered(bounds))
            context.SetMouseCursor(context.IsEnabled ? UiMouseCursor.Hand : UiMouseCursor.NotAllowed);
        for (var i = 0; i < options.Maximum; i++)
        {
            var logicalX = i % columns * width;
            var x = bounds.X + (options.RightToLeft ? bounds.Width - logicalX - width : logicalX);
            var y = bounds.Y + i / columns * width;
            var icon = new UiRect(x, y, size, size);
            var fill = Math.Clamp(displayed - i, 0, 1);
            UiFluentRatingGlyphs.Paint(context, icon, fill, options.Shape, options.Color, options.RightToLeft, false, interaction.Control);
            var focused = interaction.FocusedIndex * options.Step;
            if (interaction.Control.FocusVisible && focused >= i && focused < i + 1)
                context.FocusOutline(new(x, y, width, width), 0);
        }
    }
}
