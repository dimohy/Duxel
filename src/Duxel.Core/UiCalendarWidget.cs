namespace Duxel.Core;

public sealed record UiCalendarLabels(string PreviousMonth = "Previous month", string NextMonth = "Next month",
    string Today = "Today", string ChooseRangeEnd = "Choose the end date",
    string ChooseYear = "Choose year", string ChooseMonth = "Choose month");
public sealed record UiCalendarOptions(IUiCalendarSystem? System = null, DayOfWeek? FirstDayOfWeek = null,
    bool? RightToLeft = null, UiCalendarAvailability? Availability = null, DateOnly? Today = null,
    float CellSize = 40, UiCalendarLabels? Labels = null, string? AccessibleLabel = null,
    UiCalendarWeekend? Weekend = null, UiCalendarDayColors? DayColors = null,
    UiCalendarHolidaySet? Holidays = null, UiColor? HolidayColor = null, IUiCalendarHolidayProvider? HolidayProvider = null);

/// <summary>Shared view state for single dates and committed ranges. Locale/month data is prepared only on navigation.</summary>
public abstract partial class UiCalendarView : UiControlWidget
{
    private readonly UiCultureCalendar _defaultSystem = new();
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private UiCalendarOptions _options;
    private UiCalendarMonth? _month;
    private string[] _cellKeys = [];
    private string[] _cellNames = [];
    private UiControlTooltip?[] _holidayTips = [];
    private DateOnly? _focus;
    private int _preferredDay;
    internal DateOnly? PointerPressedDate;
    internal bool EmbeddedInPopup { get; set; }
    private readonly UiWheelPicker<DateOnly> _monthWheel, _yearWheel;
    internal UiPopover<UiCalendarPeriodPresentation> MonthPicker { get; }
    internal UiPopover<UiCalendarPeriodPresentation> YearPicker { get; }
    private readonly UiRetainedLayout<UiCalendarLayout> _layout = new();
    internal IUiCalendarSystem System => _options.System ?? _defaultSystem;
    internal DateOnly Minimum => _options.Availability is { } available && available.Minimum > System.MinimumDate ? available.Minimum : System.MinimumDate;
    internal DateOnly Maximum => _options.Availability is { } available && available.Maximum < System.MaximumDate ? available.Maximum : System.MaximumDate;
    internal UiCalendarWeekend Weekend => _options.Weekend ?? (System as IUiCalendarWeekendSystem)?.Weekend ?? UiCalendarWeekend.SaturdaySunday;
    internal DateOnly Today => _options.Today ?? _today;
    internal UiCalendarLabels Labels => _options.Labels ?? DefaultLabels;
    private static readonly UiCalendarLabels DefaultLabels = new();
    internal abstract UiCalendarDateRange? Selection { get; }
    internal virtual DateOnly? PendingStart => null;
    internal abstract bool Multiple { get; }
    internal abstract bool Commit(DateOnly date);
    internal abstract bool AccessibleSelect(DateOnly date, UiAccessibilityAction action);
    protected UiCalendarView(UiCalendarOptions? options, UiWidgetIdentity identity) : base(identity)
    {
        _options = options ?? new();
        ValidateOptions(_options);
        var focus = new UiBinding<DateOnly>(() => FocusedDate, date => SelectPeriod(date));
        _monthWheel = Dux.WheelPicker(focus, new UiCalendarWheelSequence(this, false), new(Labels.ChooseMonth));
        _yearWheel = Dux.WheelPicker(focus, new UiCalendarWheelSequence(this, true), new(Labels.ChooseYear));
        MonthPicker = new("Month", _monthWheel, new(Width : 160, MaximumHeight : 480));
        YearPicker = new("Year", _yearWheel, new(Width : 160, MaximumHeight : 480));
        _monthWheel.Chosen = _ => MonthPicker.Close();
        _yearWheel.Chosen = _ => YearPicker.Close();
        OwnChild("month-picker", MonthPicker);
        OwnChild("year-picker", YearPicker);
    }
    private void ValidateOptions(UiCalendarOptions options)
    {
        if (!float.IsFinite(options.CellSize) || options.CellSize < 24 || options.FirstDayOfWeek is { } day && (uint)day > 6)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.Holidays is not null && options.HolidayProvider is not null)
            throw new ArgumentException("Select either prepared holidays or a holiday provider.", nameof(options));
        var system = options.System ?? _defaultSystem;
        var minimum = options.Availability?.Minimum ?? system.MinimumDate;
        var maximum = options.Availability?.Maximum ?? system.MaximumDate;
        if (minimum > system.MaximumDate || maximum < system.MinimumDate || maximum < minimum)
            throw new ArgumentException("Calendar bounds do not intersect the supported calendar range.", nameof(options));
    }
    public UiCalendarOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); ValidateOptions(value);
            if (_options == value) return;
            ThrowIfBuilding();
            MonthPicker.Close(); YearPicker.Close();
            if (!ReferenceEquals(_options.HolidayProvider, value.HolidayProvider)) ResetHolidayLoading();
            var focus = _focus;
            _options = value;
            _monthWheel.Options = _monthWheel.Options with { Label = Labels.ChooseMonth };
            _yearWheel.Options = _yearWheel.Options with { Label = Labels.ChooseYear };
            _monthWheel.Refresh(); _yearWheel.Refresh();
            CancelPending();
            _focus = focus is { } previous && previous >= Minimum && previous <= Maximum ? previous : null;
            if (_focus is { } current) _preferredDay = System.GetParts(current).Day;
            _month = null;
            RequestRebuild();
        }
    }
    public DateOnly FocusedDate
    {
        get
        {
            if (_focus is null)
            {
                var date = Selection?.Start ?? Today;
                _focus = date < Minimum ? Minimum : date > Maximum ? Maximum : date;
                _preferredDay = System.GetParts(_focus.Value).Day;
            }
            return _focus.Value;
        }
    }
    internal UiCalendarMonth Month
    {
        get
        {
            if (_month is not null) return _month;
            _month = new(System, FocusedDate, _options.FirstDayOfWeek, _options.RightToLeft);
            _cellKeys = _month.Cells.ToArray().Select((cell, index) => "##day-" +
                (cell.Date?.DayNumber.ToString(global::System.Globalization.CultureInfo.InvariantCulture) ?? "blank-" + index)).ToArray();
            _cellNames = _month.Cells.ToArray().Select(cell => cell.IsSupported && cell.Date is { } date && EffectiveHolidays?.TryGetName(date, out var name) == true
                ? cell.AccessibleName + " · " + name : cell.AccessibleName).ToArray();
            return _month;
        }
    }
    internal ReadOnlyMemory<string> CellNames => _cellNames;
    internal UiControlTooltip HolidayTip(int index)
    {
        if (_holidayTips.Length < Month.Cells.Length) Array.Resize(ref _holidayTips, Month.Cells.Length);
        return _holidayTips[index] ??= new();
    }
    private protected override void ReleaseOwnedResources()
    {
        ResetHolidayLoading();
        foreach (var tooltip in _holidayTips) tooltip?.Dispose();
        _holidayTips = [];
        base.ReleaseOwnedResources();
    }
    internal ReadOnlyMemory<string> CellKeys => _cellKeys;
    internal bool WithinBounds(DateOnly date) => date >= Minimum && date <= Maximum;
    internal bool Available(DateOnly date) => WithinBounds(date) && (_options.Availability?.ContainsDate(date) ?? true);
    internal bool Available(UiCalendarDateRange range) => WithinBounds(range.Start) && WithinBounds(range.End)
        && (_options.Availability?.ContainsRange(range) ?? true);
    private void ValidateSelection()
    {
        if (Selection is { } range && !Available(range))
            throw new ArgumentOutOfRangeException(nameof(Selection), "The bound selection is outside the selectable dates.");
    }
    internal void Focus(DateOnly date, bool preservePreferredDay = false)
    {
        if (!WithinBounds(date)) return;
        if (_focus == date) return;
        var old = Month;
        _focus = date;
        if (!preservePreferredDay) _preferredDay = System.GetParts(date).Day;
        if (date.DayNumber < old.MonthStartDayNumber || date.DayNumber >= (long)old.MonthStartDayNumber + old.DaysInMonth)
        {
            _month = null;
            RequestRebuild();
        }
    }
    internal bool NavigationDate(int direction, out DateOnly date)
    {
        if (!Month.TryGetAdjacentMonth(direction, out var anchor)) { date = default; return false; }
        return FocusDayInMonth(anchor, out date);
    }
    private bool FocusDayInMonth(DateOnly anchor, out DateOnly date)
    {
        var parts = System.GetParts(anchor);
        var start = anchor.DayNumber - parts.Day + 1;
        var length = System.GetDaysInMonth(anchor);
        if ((long)start + length - 1 < Minimum.DayNumber || start > Maximum.DayNumber) { date = default; return false; }
        date = DateOnly.FromDayNumber((int)Math.Clamp((long)start + Math.Min(_preferredDay, length) - 1, Minimum.DayNumber, Maximum.DayNumber));
        return true;
    }
    public bool NavigateMonth(int direction)
    {
        ThrowIfDisposed();
        if (!NavigationDate(direction, out var date)) return false;
        Focus(date, preservePreferredDay : true);
        return true;
    }
    public bool NavigateYear(int direction)
    {
        ThrowIfDisposed();
        if (System is not IUiCalendarYearNavigation years || !years.TryMoveYear(FocusedDate, direction, out var anchor)
            || !FocusDayInMonth(anchor, out var date)) return false;
        Focus(date, preservePreferredDay : true);
        return true;
    }
    public void ShowDate(DateOnly date)
    {
        ThrowIfDisposed();
        if (!WithinBounds(date)) throw new ArgumentOutOfRangeException(nameof(date));
        Focus(date);
        RequestRebuild();
    }
    internal bool TryGetPeriodFocus(DateOnly anchor, out DateOnly date) => FocusDayInMonth(anchor, out date);
    internal bool SelectPeriod(DateOnly anchor)
    {
        if (!FocusDayInMonth(anchor, out var date)) return false;
        Focus(date, preservePreferredDay : true);
        RequestRebuild();
        return true;
    }
    internal virtual void CancelPending() { }
    protected override bool CanRetainMeasurement => true;
    internal override void InvalidateRetainedMeasurement() => _layout.Invalidate();
    private UiCalendarLayout Prepare(UiControlContext context, bool render = false)
    {
        ValidateSelection();
        _ = FocusedDate;
        if (_layout.TryGet(context, render, out var cached, context.CanRetainLayoutMeasurement)) return cached;
        var attempt = _layout.Begin(context);
        using var font = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        var edge = MathF.Max(_options.CellSize, font.LineHeight + 12);
        if (System is IUiCalendarPeriodSystem periods)
        {
            MonthPicker.Label = periods.FormatMonth(FocusedDate);
            YearPicker.Label = periods.FormatYear(FocusedDate);
        }
        var title = context.MeasureTextBlock(context.WrapText(Month.Title, edge * 7 - 80));
        var header = MathF.Max(32, title.Size.Y);
        var result = new UiCalendarLayout(context.ConstrainToLayout(new(edge * 7 + 16,
            16 + header + 8 + font.LineHeight + Month.WeekCount * edge + 8 + 32)), edge, header, font.LineHeight, title);
        _layout.Commit(context, attempt, result, result.Size, context.CanRetainLayoutMeasurement);
        return result;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => Prepare(context).Size;
    protected override void RenderControl(UiImmediateContext ui)
    {
        SynchronizeHolidayLoading(ui.CalendarFrameRequest);
        ui.RenderCalendar(this, PrimaryControlId, Prepare(new(ui), render : true));
    }
}

internal readonly record struct UiCalendarLayout(UiVector2 Size, float Edge, float HeaderHeight,
    float WeekdayHeight, UiControlTextBlock Title);

public class UiCalendarWidget : UiCalendarView
{
    public UiBinding<DateOnly?> Value { get; }
    public UiCalendarWidget(UiBinding<DateOnly?> value, UiCalendarOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(options, identity)
        => Value = value ?? throw new ArgumentNullException(nameof(value));
    internal override UiCalendarDateRange? Selection => Value.Value is { } date ? new(date, date) : null;
    internal override bool Multiple => false;
    internal override bool Commit(DateOnly date)
    {
        if (!Available(date)) return false;
        Value.Value = date;
        return Value.Value == date;
    }
    internal override bool AccessibleSelect(DateOnly date, UiAccessibilityAction action)
    {
        if (action == UiAccessibilityAction.Deselect)
        {
            if (Value.Value != date) return true;
            Value.Value = null;
            return Value.Value is null;
        }
        if (action == UiAccessibilityAction.AddToSelection && Value.Value is { } selected && selected != date) return false;
        return Commit(date);
    }
}
public class UiRangeCalendarWidget : UiCalendarView
{
    public UiBinding<UiCalendarDateRange?> Value { get; }
    private DateOnly? _pending;
    public DateOnly? PendingStartDate => _pending;
    public UiRangeCalendarWidget(UiBinding<UiCalendarDateRange?> value, UiCalendarOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(options, identity)
        => Value = value ?? throw new ArgumentNullException(nameof(value));
    internal override UiCalendarDateRange? Selection => Value.Value;
    internal override bool Multiple => true;
    internal override DateOnly? PendingStart => _pending;
    public void CancelPendingSelection() { ThrowIfDisposed(); if (_pending is null) return; _pending = null; RequestRebuild(); }
    internal override void CancelPending() => CancelPendingSelection();
    internal override bool Commit(DateOnly date)
    {
        if (!Available(date)) return false;
        if (_pending is not { } start) { _pending = date; return true; }
        var range = UiCalendarDateRange.FromEndpoints(start, date);
        if (!Available(range)) return false;
        Value.Value = range;
        if (Value.Value != range) return false;
        _pending = null;
        return true;
    }
    internal override bool AccessibleSelect(DateOnly date, UiAccessibilityAction action)
    {
        UiCalendarDateRange? target;
        var previous = Value.Value;
        if (action == UiAccessibilityAction.Select) target = new(date, date);
        else if (action == UiAccessibilityAction.AddToSelection)
            target = previous is { } existing ? new(date < existing.Start ? date : existing.Start,
                date > existing.End ? date : existing.End) : new(date, date);
        else
        {
            if (_pending == date) { _pending = null; return true; }
            if (previous is not { } old || !old.Contains(date)) return true;
            if (old.Start == old.End) target = null;
            else if (date == old.Start) target = new(old.Start.AddDays(1), old.End);
            else if (date == old.End) target = new(old.Start, old.End.AddDays(-1));
            else return false;
        }
        if (target is { } range && !Available(range)) return false;
        Value.Value = target;
        if (Value.Value != target) return false;
        _pending = null;
        return true;
    }
}

public static partial class Dux
{
    public static UiCalendarWidget Calendar(UiBinding<DateOnly?> value, UiCalendarOptions? options = null) => new(value, options);
    public static UiRangeCalendarWidget RangeCalendar(UiBinding<UiCalendarDateRange?> value, UiCalendarOptions? options = null) => new(value, options);
}
