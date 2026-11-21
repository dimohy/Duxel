using Duxel.App;
using Duxel.Core;
using System.Net.Http;

namespace Duxel.Gallery;

internal sealed class HolidayProviderPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "공휴일 제공자";
    public string Description => "Holiday data loads separately from the calendar. You can still select dates when the holiday source is offline.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    internal UiCalendarWidget Preview => State.Calendar;
    internal void SimulateOutage() => State.Outage();
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private readonly UiState<DateOnly?> _value = new(null);
        private readonly HttpClient _http = new();

        private readonly KoreanHolidayProvider _provider;

        private bool _disposed;
        private string _mode = "offline";
        private readonly UiCalendarHolidaySet _offline;
        private readonly UiCultureCalendar _system = GalleryCalendarCulture.Create("gallery");
        internal UiCalendarWidget Calendar { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content()
        {
            var year = DateTime.Today.Year;
            _offline = new([new(new(year, 1, 1), GalleryText.Get("New Year's Day"))]);
            _provider = new(_http);
            var sample = GallerySample.Create(() => Dux.Calendar(_value.Binding, new(System : _system,
                Holidays : _offline, Labels : GalleryCalendarCulture.CalendarLabels)));
            Calendar = sample.Preview; Calendar.ShowDate(new(year, 1, 1));
            sample.BindSource(() => (_mode, Calendar.FocusedDate, _value.Value), snapshot => Source(snapshot._mode, snapshot.FocusedDate, snapshot.Value));
            Samples = [sample];
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("The offline example starts with New Year's Day. Choose Load to see the Korean public holiday names from the provider.")),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8), Fluent.Button(GalleryText.Get("Offline example"), Offline), Fluent.Button(GalleryText.Get("Connection failure example"), Outage),
                    Fluent.Button(GalleryText.Get("Load holidays for this month"), Load)), sample);
        }
        private void Offline() { _mode = "offline"; Calendar.Options = Calendar.Options with { Holidays = _offline, HolidayProvider = null }; }
        internal void Outage()
        {
            _mode = "outage";
            var result = new UiCalendarHolidayResult(UiCalendarHolidayStatus.NetworkUnavailable, UiCalendarHolidaySet.Empty);
            Calendar.Options = Calendar.Options with { Holidays = result.DisplayHolidays, HolidayProvider = null };
        }
        private void Load()
        {
            _mode = "live";
            Calendar.Options = Calendar.Options with { Holidays = null, HolidayProvider = _provider };
            Calendar.RefreshHolidays();
        }
        private string Source(string mode, DateOnly focus, DateOnly? selected)
        {
            var value = selected is { } date ? $"new DateOnly({date.Year}, {date.Month}, {date.Day})" : "null";
            var code = "var value = new UiState<DateOnly?>(" + value + ");\nvar system = "
                + GalleryCalendarCulture.SystemSource(_system, "gallery") + ";\n"
                + "var calendar = Dux.Calendar(value.Binding, new(System : system,\n    Labels : "
                + GalleryCalendarCulture.CalendarLabelsSource(Calendar.Options.Labels!) + "));\n"
                + $"calendar.ShowDate(new DateOnly({focus.Year}, {focus.Month}, {focus.Day}));\n";
            if (mode == "offline") return code + "var holidays = new UiCalendarHolidaySet([new(new DateOnly(" + _offline.Items[0].Date.Year
                + ", 1, 1), " + GallerySample.String(_offline.Items[0].Name) + ")]);\ncalendar.Options = calendar.Options with { Holidays = holidays };";
            if (mode == "outage") return code + "// A network failure intentionally produces no on-screen error.\n"
                + "var result = new UiCalendarHolidayResult(UiCalendarHolidayStatus.NetworkUnavailable, UiCalendarHolidaySet.Empty);\n"
                + "calendar.Options = calendar.Options with { Holidays = result.DisplayHolidays };";
            return "using Duxel.App;\n\n" + code + "\nusing var http = new HttpClient();\n"
                + "IUiCalendarHolidayProvider provider = new KoreanHolidayProvider(http);\n"
                + "calendar.Options = calendar.Options with { Holidays = null, HolidayProvider = provider };\n"
                + "// Visible months load asynchronously; results are cached and applied on the UI thread.";
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; Root.Dispose(); _http.Dispose();
        }
    }
}
