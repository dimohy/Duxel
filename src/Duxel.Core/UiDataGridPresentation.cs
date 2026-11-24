namespace Duxel.Core;

/// <summary>Paints the grid without owning its layout, input, selection, or edit state.</summary>
public interface IUiDataGridPresentation
{
    static virtual UiControlTypographyScope BeginHeaderText(UiControlContext context)
        => context.BeginTypography<UiSemiboldTypeface, UiFluentType300>();

    static virtual UiControlTypographyScope BeginBodyText(UiControlContext context)
        => context.BeginTypography<UiRegularTypeface, UiFluentType300>();

    static virtual void PaintHeader(UiControlContext context, UiRect bounds)
        => context.Frame(bounds, context.Theme.FrameBg, default, 0, 0);

    static virtual void PaintHoveredHeader(UiControlContext context, UiRect bounds)
        => context.Frame(bounds, context.Theme.FrameBgHovered, default, 0, 0);

    static virtual void PaintSortIndicator(UiControlContext context, UiRect bounds, bool descending)
        => context.PaintDataGridSortIndicator(bounds, descending);

    static virtual void PaintRow(UiControlContext context, UiRect bounds, bool selected, bool hovered)
    {
        if (selected || hovered)
            context.Frame(bounds, selected ? context.Theme.Header : context.Theme.FrameBgHovered, default, 0, 0);
    }

    static virtual void PaintDivider(UiControlContext context, UiRect bounds)
        => context.Frame(new(bounds.X, bounds.Y + bounds.Height - 1, bounds.Width, 1), context.Theme.Border, default, 0, 0);

    static virtual void PaintSelection(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool? selected)
        => UiFluentListPresentation.PaintCheckbox(context, bounds, interaction, selected);

    static virtual void PaintFocus(UiControlContext context, UiRect bounds)
        => context.FocusOutline(bounds, 4);

    static virtual void PaintText(UiControlContext context, string label, UiRect cell, UiItemHorizontalAlign alignment)
    {
        const float padding = 10;
        if (cell.Width <= 2 * padding || string.IsNullOrEmpty(label))
            return;
        var text = context.MeasureText(label);
        var available = cell.Width - 2 * padding;
        var inset = MathF.Max(0, available - text.X);
        var left = cell.X + padding + (alignment switch
        {
            UiItemHorizontalAlign.Center => inset / 2,
            UiItemHorizontalAlign.Right => inset,
            _ => 0
        });
        using var clip = context.Clip(new(cell.X + padding, cell.Y, available, cell.Height));
        context.TypographyButtonText(label, new(left, cell.Y, text.X, cell.Height), text,
            context.IsEnabled ? context.Theme.Text : context.Theme.TextDisabled);
    }
}

public readonly struct UiFluentDataGridPresentation : IUiDataGridPresentation;

public readonly partial struct UiControlContext
{
    internal void PaintDataGridSortIndicator(UiRect bounds, bool descending)
        => _ui.PaintDataGridSortIndicator(bounds, descending);
}
