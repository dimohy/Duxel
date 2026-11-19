using System.Globalization;

namespace Duxel.Core;

public sealed record UiDatePickerOptions(UiCultureCalendar? System = null, UiCalendarAvailability? Availability = null,
    float Width = 280, string? Format = null, UiPickerLabels? Labels = null, UiCalendarLabels? CalendarLabels = null, UiCalendarHolidaySet? Holidays = null, IUiCalendarHolidayProvider? HolidayProvider = null);

/// <summary>Culture-aware editable date field and the shared Calendar surface.</summary>
public class UiDatePickerWidget : UiContainerBox<UiBoxContainerLayout, UiPlainContainerPresentation>
{
    private readonly UiDatePickerState _state;
    public UiBinding<DateOnly?> Value => _state.Value;
    public string Text => _state.Text;
    public string? ValidationMessage => _state.Error;
    public new UiDatePickerOptions Options
    {
        get => _state.Options;
        set { ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); ThrowIfBuilding(); _state.Configure(value); RequestRebuild(); }
    }
    internal void ValidatePickerOptions(UiDatePickerOptions options) => _state.ValidateChange(options);
    public UiCalendarWidget Calendar => _state.Calendar;
    public bool IsOpen => _state.Popup.IsOpen;
    public bool TrySetText(string text) { ThrowIfDisposed(); var accepted = _state.Edit(text); RequestRebuild(); return accepted; }
    public void ShowCalendar() { ThrowIfDisposed(); _state.Synchronize(); _state.Popup.Show(); RequestRebuild(); }
    public bool Close() => _state.Popup.Close();
    public UiDatePickerWidget(string label, UiBinding<DateOnly?> value, UiDatePickerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(new UiDatePickerState(label, value, options ?? new()), identity) { }
    private UiDatePickerWidget(UiDatePickerState state, UiWidgetIdentity identity) : base(state.Field, identity : identity) => _state = state;
    protected override UiVector2 MeasureContent(UiControlContext context) { _state.Synchronize(); return base.MeasureContent(context); }
    protected override void RenderControl(UiImmediateContext ui) { _state.Synchronize(); base.RenderControl(ui); }
}
internal sealed class UiDatePickerState : UiTemporalPickerState<DateOnly>
{
    private readonly UiCultureCalendar _defaultSystem = new();
    internal UiDatePickerOptions Options { get; private set; }
    private UiCultureCalendar System => Options.System ?? _defaultSystem;
    protected override CultureInfo Culture => System.Culture;
    protected override UiPickerLabels Labels => Options.Labels ?? Defaults;
    private static readonly UiPickerLabels Defaults = new();
    protected override string InvalidMessage => Labels.InvalidDate;
    internal UiCalendarWidget Calendar { get; }
    internal UiPopover<UiPickerTrigger<UiDatePickerIcon>> Popup { get; }
    internal UiDatePickerState(string label, UiBinding<DateOnly?> value, UiDatePickerOptions options) : base(label, value)
    {
        Validate(options);
        Options = options;
        Calendar = Dux.Calendar(new UiBinding<DateOnly?>(() => Value.Value, date => { if (Accept(date)) Popup!.Close(); }),
            new(System : System, Availability : options.Availability, Labels : options.CalendarLabels, Holidays : options.Holidays, HolidayProvider : options.HolidayProvider));
        Calendar.EmbeddedInPopup = true;
        Popup = new(Labels.ChooseDate, Calendar, new(Width : 296, MaximumHeight : 440) { ContentPadding = 0 });
        Initialize(Popup, options.Width);
    }
    private void Validate(UiDatePickerOptions options)
    {
        UiPickerText.Width(options.Width);
        if (options.Holidays is not null && options.HolidayProvider is not null) throw new ArgumentException("Select either prepared holidays or a holiday provider.", nameof(options));
        UiPickerText.ValidateLabels(options.Labels ?? Defaults);
        var system = options.System ?? _defaultSystem;
        if (options.Format is { } format)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(format);
            _ = system.MinimumDate.ToDateTime(TimeOnly.MinValue).ToString(format, system.Culture);
        }
        if (options.Availability is { } bounds && (bounds.Minimum > system.MaximumDate || bounds.Maximum < system.MinimumDate))
            throw new ArgumentException("Date picker bounds do not intersect the calendar range.", nameof(options));
    }
    internal void ValidateChange(UiDatePickerOptions options)
    {
        Validate(options);
        var system = options.System ?? _defaultSystem;
        if (Value.Value is { } date && (date < system.MinimumDate || date > system.MaximumDate || options.Availability?.ContainsDate(date) == false))
            throw new ArgumentOutOfRangeException(nameof(options), "The current date is outside the new picker options.");
    }
    internal void Configure(UiDatePickerOptions options)
    {
        ValidateChange(options);
        if (Options == options) return;
        Popup.Close(); Options = options;
        Calendar.Options = Calendar.Options with { System = System, Availability = options.Availability, Labels = options.CalendarLabels, Holidays = options.Holidays, HolidayProvider = options.HolidayProvider };
        Popup.Label = Labels.ChooseDate; Input.Width = options.Width; Field.Width = options.Width;
        Synchronize(force : true);
    }
    protected override bool Allowed(DateOnly date) => date >= System.MinimumDate && date <= System.MaximumDate
        && (Options.Availability?.ContainsDate(date) ?? true);
    protected override string Format(DateOnly date) => UiPickerText.ShapeDigits(date.ToDateTime(TimeOnly.MinValue).ToString(Options.Format ?? "d", Culture), Culture);
    protected override bool TryParse(string text, out DateOnly date) => Options.Format is { } format
        ? DateOnly.TryParseExact(text, format, Culture, DateTimeStyles.None, out date)
        : DateOnly.TryParse(text, Culture, DateTimeStyles.None, out date);
    protected override void ExternalValueChanged(DateOnly? date) { if (date is { } value) Calendar.ShowDate(value); }
}
public static partial class Dux
{
    public static UiDatePickerWidget DatePicker(string label, UiBinding<DateOnly?> value, UiDatePickerOptions? options = null) => new(label, value, options);
}
