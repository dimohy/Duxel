using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal enum TemporalPickerKind { Date, Time, DateTime }
internal sealed class TemporalPickerPage(TemporalPickerKind kind, UiTypographyFonts? fonts) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new(kind, fonts);
    public string Title => kind + "Picker";
    public string Description => kind switch
    {
        TemporalPickerKind.Date => "Enter a date or select it from the calendar to try regional formats and validation.",
        TemporalPickerKind.Time => "Enter a time or use the hour and minute wheels. Switch between 12- and 24-hour time and choose whether to show seconds.",
        _ => "Enter a date and a time to store them as one value. You can also use the calendar and time wheels."
    };
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    internal UiWidget Preview => State.Preview;
    internal void SelectCulture(string key) => State.ChangeCulture(key);
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        private readonly TemporalPickerKind _kind;
        private readonly UiTypographyFonts? _fonts;
        private string _culture = "gallery";
        private UiCultureCalendar _system = GalleryCalendarCulture.Create("gallery");
        private readonly UiPickerLabels _labels = GalleryCalendarCulture.Labels;
        private readonly string _label;
        private readonly UiState<DateOnly?> _date = new(new(2024, 3, 15));
        private readonly UiState<TimeOnly?> _time = new(new(15, 4, 5));
        private readonly UiState<DateTime?> _combined = new(new(2024, 3, 15, 15, 4, 5));
        private readonly UiThemeProvider _fontHost;
        internal UiWidget Preview { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        private readonly record struct Snapshot(string Culture, UiCultureCalendar System, UiDatePickerOptions Date, UiTimePickerOptions Time,
            DateOnly? DateValue, TimeOnly? TimeValue, DateTime? CombinedValue);
        internal Content(TemporalPickerKind kind, UiTypographyFonts? fonts)
        {
            _kind = kind;
            _fonts = fonts;
            _label = kind switch
            {
                TemporalPickerKind.Date => GalleryText.Get("Date"),
                TemporalPickerKind.Time => GalleryText.Get("Time"),
                _ => GalleryText.Get("Appointment")
            };
            var dateOptions = new UiDatePickerOptions(System : _system, Labels : _labels, CalendarLabels : GalleryCalendarCulture.CalendarLabels);
            var timeOptions = new UiTimePickerOptions(Culture : _system.Culture, Labels : _labels);
            UiThemeProvider? host = null;
            UiWidget Layout(UiWidget preview) => host = Fluent.FluentProvider(preview);
            IGallerySample sample; UiWidget view;
            if (kind == TemporalPickerKind.Date)
            {
                var typed = GallerySample.Create(() => Dux.DatePicker(_label, _date.Binding, dateOptions), layout : Layout);
                Preview = typed.Preview; typed.BindSource(Read, Source); sample = typed; view = typed;
            }
            else if (kind == TemporalPickerKind.Time)
            {
                var typed = GallerySample.Create(() => Dux.TimePicker(_label, _time.Binding, timeOptions), layout : Layout);
                Preview = typed.Preview; typed.BindSource(Read, Source); sample = typed; view = typed;
            }
            else
            {
                var typed = GallerySample.Create(() => Dux.DateTimePicker(_label, _combined.Binding, new(dateOptions, timeOptions)), layout : Layout);
                Preview = typed.Preview; typed.BindSource(Read, Source); sample = typed; view = typed;
            }
            _fontHost = host!; Samples = [sample];
            var culture = Fluent.Dropdown(GalleryText.Get("Display locale"), new UiBinding<string?>(() => _culture, value => ChangeCulture(value!)),
                [GalleryCalendarCulture.GalleryOption, GalleryCalendarCulture.SystemOption, new("ko-KR", GalleryText.Get("South Korea")), new("en-US", GalleryText.Get("United States")), new("en-GB", GalleryText.Get("United Kingdom")),
                 new("ar-SA", GalleryText.Get("Saudi Arabia")), new("he-IL", GalleryText.Get("Israel · Hebrew calendar")), new("th-TH", GalleryText.Get("Thailand · Buddhist calendar")), new("ja-JP", GalleryText.Get("Japan · Era calendar"))], new(Width : 240, ExpandLabel : GalleryText.Get("Choose locale")));
            var controls = new List<UiWidget> { culture, Fluent.Button(GalleryText.Get("Clear value"), Clear) };
            if (kind != TemporalPickerKind.Date)
            {
                controls.Add(Fluent.Button(GalleryText.Get("Toggle 12- or 24-hour time"), () => SetTime(TimeOptions with
                { HourCycle = TimeOptions.HourCycle == UiTimeHourCycle.Hours24 ? UiTimeHourCycle.Hours12 : UiTimeHourCycle.Hours24 })));
                controls.Add(Fluent.Button(GalleryText.Get("Toggle seconds"), () => SetTime(TimeOptions with { ShowSeconds = !TimeOptions.ShowSeconds })));
            }
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8), controls.ToArray()),
                Fluent.Body1(() => GalleryText.Format("Stored value: {0}", (kind == TemporalPickerKind.Date ? _date.Value?.ToString("O", CultureInfo.InvariantCulture)
                    : kind == TemporalPickerKind.Time ? _time.Value?.ToString("O", CultureInfo.InvariantCulture)
                    : _combined.Value?.ToString("O", CultureInfo.InvariantCulture)) ?? GalleryText.Get("None"))), view);
        }
        private UiDatePickerOptions DateOptions => Preview is UiDatePickerWidget date ? date.Options
            : Preview is UiDateTimePickerWidget combined ? combined.DatePicker.Options : new();
        private UiTimePickerOptions TimeOptions => Preview is UiTimePickerWidget time ? time.Options
            : Preview is UiDateTimePickerWidget combined ? combined.TimePicker.Options : new();
        private void SetTime(UiTimePickerOptions options)
        {
            if (Preview is UiTimePickerWidget time) time.Options = options;
            else ((UiDateTimePickerWidget)Preview).TimePicker.Options = options;
        }
        private Snapshot Read() => new(_culture, _system, DateOptions, TimeOptions, _date.Value, _time.Value, _combined.Value);
        internal void ChangeCulture(string key)
        {
            var system = GalleryCalendarCulture.Create(key);
            if (Preview is UiDatePickerWidget date) date.Options = date.Options with { System = system };
            else if (Preview is UiTimePickerWidget time) time.Options = time.Options with { Culture = system.Culture };
            else
            {
                var combined = (UiDateTimePickerWidget)Preview;
                combined.Options = new(combined.DatePicker.Options with { System = system }, combined.TimePicker.Options with { Culture = system.Culture });
            }
            _fontHost.Fonts = key == "th-TH" ? GalleryCalendarCulture.ThaiFonts(_fonts) : null;
            _system = system;
            _culture = key;
        }
        private void Clear()
        {
            if (Preview is UiDateTimePickerWidget combined) combined.Clear();
            else if (Preview is UiDatePickerWidget date) date.TrySetText(string.Empty);
            else ((UiTimePickerWidget)Preview).TrySetText(string.Empty);
        }
        private string Source(Snapshot state)
        {
            var dateValue = state.DateValue is { } date ? $"new DateOnly({date.Year}, {date.Month}, {date.Day})" : "null";
            var timeValue = state.TimeValue is { } time ? $"new TimeOnly({time.Hour}, {time.Minute}, {time.Second})" : "null";
            var combinedValue = state.CombinedValue is { } value ? $"new DateTime({value.Year}, {value.Month}, {value.Day}, {value.Hour}, {value.Minute}, {value.Second})" : "null";
            var code = "var system = " + GalleryCalendarCulture.SystemSource(state.System, state.Culture) + ";\n"
                + "var labels = " + GalleryCalendarCulture.PickerLabelsSource(_labels) + ";\n";
            var dateOptions = _kind == TemporalPickerKind.Time ? string.Empty
                : "new UiDatePickerOptions(System : system, Labels : labels,\n    CalendarLabels : "
                    + GalleryCalendarCulture.CalendarLabelsSource(state.Date.CalendarLabels!) + ")";
            var timeOptions = "new UiTimePickerOptions(Culture : system.Culture, HourCycle : UiTimeHourCycle." + state.Time.HourCycle
                + ", ShowSeconds : " + GallerySample.Boolean(state.Time.ShowSeconds) + ", Labels : labels)";
            code += _kind switch
            {
                TemporalPickerKind.Date => "var value = new UiState<DateOnly?>(" + dateValue + ");\nvar picker = Dux.DatePicker(" + GallerySample.String(_label) + ", value.Binding, " + dateOptions + ");",
                TemporalPickerKind.Time => "var value = new UiState<TimeOnly?>(" + timeValue + ");\nvar picker = Dux.TimePicker(" + GallerySample.String(_label) + ", value.Binding, " + timeOptions + ");",
                _ => "var value = new UiState<DateTime?>(" + combinedValue + ");\nvar picker = Dux.DateTimePicker(" + GallerySample.String(_label) + ", value.Binding,\n    new(Date : " + dateOptions + ", Time : " + timeOptions + "));"
            };
            if (state.Culture == "th-TH")
                code += "\nvar thaiFont = Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Fonts\", \"NotoSansThai\", \"NotoSansThai.ttf\");\nvar example = Fluent.FluentProvider(picker);\nexample.Fonts = new UiTypographyFonts(thaiFont, thaiFont, thaiFont, "
                    + GallerySample.String(_fonts?.SecondaryRegular) + ", " + GallerySample.String(_fonts?.SecondarySemibold) + ", " + GallerySample.String(_fonts?.SecondaryBold) + ");";
            return code;
        }
    }
}
