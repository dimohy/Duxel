using Duxel.App;
using Duxel.Core;
using System.Net.Http;

namespace Duxel.Gallery;

internal sealed class HolidayProviderPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "공휴일 제공자";
    public string Description => "공휴일 데이터는 달력과 분리해 받아옵니다. 연결이 끊기면 공휴일 표시만 생략하고 날짜 선택은 계속 동작합니다.";
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
        internal UiCalendarWidget Calendar { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content()
        {
            var year = DateTime.Today.Year;
            _offline = new([new(new(year, 1, 1), "신정")]);
            _provider = new(_http);
            var sample = GallerySample.Create(() => Dux.Calendar(_value.Binding, new(System : GalleryCalendarCulture.Create("ko-KR"),
                Holidays : _offline, Labels : GalleryCalendarCulture.CalendarLabels)));
            Calendar = sample.Preview; Calendar.ShowDate(new(year, 1, 1));
            sample.BindSource(() => (_mode, Calendar.FocusedDate, _value.Value), snapshot => Source(snapshot._mode, snapshot.FocusedDate, snapshot.Value));
            Samples = [sample];
            var live = Fluent.Field(Fluent.Button("현재 달 공휴일 조회", Load));
            live.Width = 176;
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1("처음에는 신정 한 건을 넣은 오프라인 예제입니다. 조회 버튼을 누르면 공개된 한국 공휴일 데이터를 불러옵니다."),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8), Fluent.Button("오프라인 예제", Offline), Fluent.Button("연결 끊김 예제", Outage),
                    live), sample);
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
            var code = "var value = new UiState<DateOnly?>(" + value + ");\nvar system = new UiCultureCalendar(System.Globalization.CultureInfo.GetCultureInfo(\"ko-KR\"));\n"
                + "var calendar = Dux.Calendar(value.Binding, new(System : system,\n    Labels : new(\"이전 달\", \"다음 달\", \"오늘\", \"종료일을 선택하세요\", \"연도 선택\", \"월 선택\")));\n"
                + $"calendar.ShowDate(new DateOnly({focus.Year}, {focus.Month}, {focus.Day}));\n";
            if (mode == "offline") return code + "var holidays = new UiCalendarHolidaySet([new(new DateOnly(" + _offline.Items[0].Date.Year
                + ", 1, 1), \"신정\")]);\ncalendar.Options = calendar.Options with { Holidays = holidays };";
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
