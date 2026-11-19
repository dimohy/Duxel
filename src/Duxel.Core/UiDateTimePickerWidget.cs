namespace Duxel.Core;

public sealed record UiDateTimePickerOptions(UiDatePickerOptions? Date = null, UiTimePickerOptions? Time = null);

/// <summary>One nullable wall-clock date/time, composed from the shared date and time pickers.</summary>
public class UiDateTimePickerWidget : UiContainerBox<UiBoxContainerLayout, UiPlainContainerPresentation>
{
    private readonly State _state;
    public UiBinding<DateTime?> Value => _state.Value;
    public void Clear() { ThrowIfDisposed(); _state.Date.TrySetText(string.Empty); _state.Time.TrySetText(string.Empty); RequestRebuild(); }
    public UiDatePickerWidget DatePicker => _state.Date;
    public UiTimePickerWidget TimePicker => _state.Time;
    public new UiDateTimePickerOptions Options
    {
        get => new(_state.Date.Options, _state.Time.Options);
        set
        {
            ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); ThrowIfBuilding();
            var date = value.Date ?? new(); var time = value.Time ?? new();
            _state.Date.ValidatePickerOptions(date); _state.Time.ValidatePickerOptions(time);
            _state.Date.Options = date; _state.Time.Options = time;
            RequestRebuild();
        }
    }
    public UiDateTimePickerWidget(string label, UiBinding<DateTime?> value, UiDateTimePickerOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(new State(label, value, options ?? new()), identity) { }
    private UiDateTimePickerWidget(State state, UiWidgetIdentity identity) : base(state.Root, identity : identity) => _state = state;
    protected override UiVector2 MeasureContent(UiControlContext context) { _state.Synchronize(); return base.MeasureContent(context); }
    protected override void RenderControl(UiImmediateContext ui) { _state.Synchronize(); base.RenderControl(ui); }
    private sealed class State
    {
        internal UiBinding<DateTime?> Value { get; }
        internal UiDatePickerWidget Date { get; }
        internal UiTimePickerWidget Time { get; }
        internal UiWidget Root { get; }
        private DateTime? _last;
        private DateOnly? _date;
        private TimeOnly? _time;
        private bool _initialized;
        internal State(string label, UiBinding<DateTime?> value, UiDateTimePickerOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Synchronize();
            Date = Dux.DatePicker(label + " · " + (options.Date?.Labels?.ChooseDate ?? "Date"), new UiBinding<DateOnly?>(() => _date, SetDate), options.Date);
            Time = Dux.TimePicker(label + " · " + (options.Time?.Labels?.ChooseTime ?? "Time"), new UiBinding<TimeOnly?>(() => _time, SetTime), options.Time);
            Root = Dux.Wrap(new(Spacing : 12, RunSpacing : 12), Date, Time);
        }
        internal void Synchronize()
        {
            var actual = Value.Value;
            if (_initialized && actual == _last) return;
            _initialized = true; _last = actual;
            _date = actual is { } value ? DateOnly.FromDateTime(value) : null;
            _time = actual is { } time ? TimeOnly.FromDateTime(time) : null;
        }
        private bool Commit()
        {
            var next = _date is { } date && _time is { } time ? date.ToDateTime(time, Value.Value?.Kind ?? DateTimeKind.Unspecified) : (DateTime?)null;
            Value.Value = next;
            if (Value.Value != next) return false;
            _last = next; _initialized = true;
            return true;
        }
        private void SetDate(DateOnly? date)
        {
            var previous = _date; _date = date;
            if (!Commit()) _date = previous;
        }
        private void SetTime(TimeOnly? time)
        {
            var previous = _time; _time = time;
            if (!Commit()) _time = previous;
        }
    }
}
public static partial class Dux
{
    public static UiDateTimePickerWidget DateTimePicker(string label, UiBinding<DateTime?> value, UiDateTimePickerOptions? options = null) => new(label, value, options);
}
