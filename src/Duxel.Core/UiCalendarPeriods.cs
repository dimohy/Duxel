using System.Globalization;

namespace Duxel.Core;

/// <summary>Optional native-period navigation for interactive year/month wheels.</summary>
public interface IUiCalendarPeriodSystem
{
    bool MonthBeforeYear { get; }
    string FormatMonth(DateOnly date);
    string FormatYear(DateOnly date);
    bool TryMovePeriods(DateOnly date, int offset, bool years, out DateOnly target);
}

internal sealed class UiCalendarWheelSequence(UiCalendarView owner, bool years) : IUiWheelSequence<DateOnly>
{
    public bool TryOffset(DateOnly value, int offset, out DateOnly target)
    {
        target = default;
        return owner.System is IUiCalendarPeriodSystem periods && periods.TryMovePeriods(value, offset, years, out var anchor)
            && owner.TryGetPeriodFocus(anchor, out target);
    }
    public string Format(DateOnly date) => years ? ((IUiCalendarPeriodSystem)owner.System).FormatYear(date)
        : ((IUiCalendarPeriodSystem)owner.System).FormatMonth(date);
    public string Key(DateOnly date) => date.DayNumber.ToString(CultureInfo.InvariantCulture);
}

internal readonly struct UiCalendarPeriodPresentation : IUiPopoverPresentation
{
    public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static UiControlTypographyScope BeginBodyText(UiControlContext context) => BeginTriggerText(context);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => new(context.MeasureText(label).X + 16, 32);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
    {
        if (interaction.Hovered || interaction.Expanded) context.Frame(bounds, context.Theme.ButtonHovered, default, 4, 0);
        context.TypographyButtonText(label, bounds, context.MeasureText(label), context.Theme.Text);
        if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
    }
    public static float Padding(UiPopoverOptions options) => 0;
    public static UiColor Foreground(UiControlContext context, UiPopoverOptions options) => context.Theme.Text;
    public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options)
        => UiFluentPopoverPresentation.PaintSurface(context, surface, layout, options);
}
