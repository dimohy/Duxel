using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ObserverTemporalPickerControlProbe
{
    private enum Stage { OpenDate, OpenMonth, SelectMonth, SelectDate, OpenTime, SelectMinute, CloseTime, Done }
    private static readonly DateOnly TargetDate = new(2024, 4, 16);
    private static readonly TimeOnly TargetTime = new(15, 5, 5);
    private readonly UiDatePickerWidget? _date;
    private readonly UiTimePickerWidget? _time;
    private readonly UiDateTimePickerWidget? _combined;
    private Stage _stage;

    internal ObserverTemporalPickerControlProbe(TemporalPickerPage page)
    {
        page.SelectCulture("ko-KR");
        _combined = page.Preview as UiDateTimePickerWidget;
        _date = _combined?.DatePicker ?? page.Preview as UiDatePickerWidget;
        _time = _combined?.TimePicker ?? page.Preview as UiTimePickerWidget;
        if (_date is null && _time is null)
            throw new InvalidOperationException("The gallery temporal probe requires an actual picker.");
        _stage = _date is null ? Stage.OpenTime : Stage.OpenDate;
    }

    internal bool Ready => _stage == Stage.Done
        && (_date is null || !_date.IsOpen && _date.Value.Value == TargetDate)
        && (_time is null || !_time.IsOpen && _time.Value.Value == TargetTime)
        && (_combined is null || _combined.Value.Value == TargetDate.ToDateTime(TargetTime));
    internal string StageName => _stage.ToString();

    internal void Advance(UiAccessibilityTree tree)
    {
        var elements = tree.CaptureSnapshot().Elements;
        bool Invoke(UiAccessibilityRole role, string name, string? parent = null)
        {
            var matches = elements.Where(element => element.Node.Role == role && element.Node.Name == name
                && (parent is null || element.Node.ParentId == parent) && !element.Node.Offscreen).ToArray();
            if (matches.Length == 0) return false;
            var button = matches.Single();
            if (tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait: false)
                != UiAccessibilityResult.Success)
                throw new InvalidOperationException($"The gallery temporal probe could not invoke '{name}'.");
            return true;
        }

        switch (_stage)
        {
            case Stage.OpenDate:
                if (Invoke(UiAccessibilityRole.Button, "날짜 선택")) _stage = Stage.OpenMonth;
                break;
            case Stage.OpenMonth:
                var monthCalendar = _date!.Calendar;
                if (_date.IsOpen && monthCalendar.Options.System is IUiCalendarPeriodSystem periods
                    && Invoke(UiAccessibilityRole.Button, periods.FormatMonth(monthCalendar.FocusedDate)))
                    _stage = Stage.SelectMonth;
                break;
            case Stage.SelectMonth:
                var month = elements.SingleOrDefault(element => element.Node.Role == UiAccessibilityRole.ListBox
                    && element.Node.Name == "월 선택" && !element.Node.Offscreen);
                if (month.Node.Id is null) break;
                var options = elements.Where(element => element.Node.Role == UiAccessibilityRole.Option
                    && element.Node.ParentId == month.Node.Id && !element.Node.Offscreen).OrderBy(element => element.Node.Bounds.Y).ToArray();
                var selected = Array.FindIndex(options, element => element.Node.Selected == true);
                if (selected < 0 || selected == options.Length - 1)
                    throw new InvalidOperationException("The month wheel does not show the next month.");
                if (Invoke(UiAccessibilityRole.Option, options[selected + 1].Node.Name, month.Node.Id)) _stage = Stage.SelectDate;
                break;
            case Stage.SelectDate:
                var calendar = _date!.Calendar;
                if (calendar.FocusedDate.Month == TargetDate.Month && Invoke(UiAccessibilityRole.GridCell,
                    calendar.Options.System!.FormatFullDate(TargetDate)))
                    _stage = _time is null ? Stage.Done : Stage.OpenTime;
                break;
            case Stage.OpenTime:
                if (_date is { IsOpen: true }) break;
                if (Invoke(UiAccessibilityRole.Button, "시각 선택")) _stage = Stage.SelectMinute;
                break;
            case Stage.SelectMinute:
                var minutes = elements.SingleOrDefault(element => element.Node.Role == UiAccessibilityRole.ListBox
                    && element.Node.Name == "분" && !element.Node.Offscreen);
                if (!_time!.IsOpen || minutes.Node.Id is null) break;
                var label = new UiIntegerWheelSequence(0, 59, _time.Options.Culture).Format(TargetTime.Minute);
                if (Invoke(UiAccessibilityRole.Option, label, minutes.Node.Id)) _stage = Stage.CloseTime;
                break;
            case Stage.CloseTime:
                if (_time!.Value.Value == TargetTime && Invoke(UiAccessibilityRole.Button, "확인")) _stage = Stage.Done;
                break;
        }
    }
}
