namespace Duxel.Core;

internal static class UiSegmentedControlPainter
{
    internal static void Paint(UiControlContext context, UiSegmentedControlWidget widget, UiRect bounds,
        UiRect thumb, int selected, UiRadioGroupInteraction interaction)
    {
        var options = widget.Options;
        var theme = context.Theme;
        var forced = theme.ForcedColors is not null;
        var paint = UiSegmentedControlPalette.Resolve(options.Style, context.FluentColorScheme, context.IsEnabled);
        var background = forced ? theme.WindowBg : paint.Surface;
        var inset = MathF.Min(options.HorizontalInset, bounds.Width / 2);
        var pill = bounds with { X = bounds.X + inset, Width = MathF.Max(0, widget.ContentWidth - inset * 2) };
        context.Frame(pill, background, forced ? theme.Border : default, 16, forced ? 1 : 0);
        if (selected >= 0)
        {
            var selectedPaint = UiSegmentedControlPalette.Resolve(options.Style, context.FluentColorScheme,
                context.IsEnabled && widget.Items[selected].Enabled);
            context.Frame(thumb, theme.ForcedColors?.Highlight ?? selectedPaint.SelectedSurface, default, 16, 0);
        }
        for (var i = 0; i < widget.Items.Length; i++)
        {
            var item = widget.Items[i];
            var local = widget.Layouts[i].Bounds;
            var rect = new UiRect(bounds.X + local.X, bounds.Y, local.Width, bounds.Height);
            var enabled = context.IsEnabled && item.Enabled;
            var itemInteraction = new UiButtonInteraction(false, i == interaction.HoveredIndex, i == interaction.HeldIndex);
            var itemPaint = UiSegmentedControlPalette.Resolve(options.Style, context.FluentColorScheme, enabled);
            var color = forced ? enabled ? theme.Text : theme.TextDisabled : itemPaint.Foreground;
            void Content(UiColor ink)
            {
                using var itemClip = context.Clip(rect);
                if (item.Icon.IsEmpty)
                {
                    var block = widget.Layouts[i].LabelBlock;
                    context.TypographyTextBlock(block, rect with { Y = rect.Y + (rect.Height - block.Size.Y) / 2, Height = block.Size.Y },
                        ink, UiItemHorizontalAlign.Center);
                }
                else item.Icon.Paint(context, new(rect.X + (rect.Width - 20) / 2, rect.Y + (rect.Height - 20) / 2, 20, 20), ink, itemInteraction);
            }
            Content(color);
            if (selected >= 0)
            {
                using var selectedClip = context.Clip(thumb);
                Content(forced ? enabled ? theme.ForcedColors!.Value.HighlightText : theme.TextDisabled : itemPaint.SelectedForeground);
            }
            if (item.Unread)
            {
                var contentSize = item.Icon.IsEmpty ? widget.Layouts[i].LabelBlock.Size : new UiVector2(20, 20);
                var contentX = rect.X + (rect.Width - contentSize.X) / 2;
                var dotX = options.RightToLeft ? contentX - 9 : contentX + contentSize.X + 9;
                var dotY = rect.Y + (rect.Height - contentSize.Y) / 2 + 6;
                using var dotClip = context.Clip(rect);
                context.Circle(new(dotX, dotY), 3, forced ? enabled ? theme.Text : theme.TextDisabled : itemPaint.Unread);
            }
            if (interaction.Control.FocusVisible && interaction.FocusedIndex == i) context.FocusOutline(rect, 16);
        }
    }
}
