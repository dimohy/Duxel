using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal sealed class CalendarPage(bool range, UiTypographyFonts? fonts) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new(range, fonts);
    public string Title => range ? "RangeCalendar" : "Calendar";
    public string Description => range ? "시작일과 종료일을 선택하세요. 문화권을 바꿔도 선택한 날짜의 의미는 유지됩니다."
        : "날짜를 선택하고 문화권별 월·요일·숫자와 글 방향을 비교해 보세요.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    internal UiCalendarView Preview => State.Preview;
    internal void SelectCulture(string key) => State.ChangeCulture(key);
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        private readonly bool _range;
        private readonly UiState<DateOnly?> _single = new(null);
        private readonly UiState<UiCalendarDateRange?> _selectedRange = new(null);
        private readonly UiState<string> _status = new(string.Empty);
        private string _culture = "system";
        private bool _latin;
        private readonly UiTypographyFonts? _fonts;
        private readonly List<UiThemeProvider> _localizedHosts = [];
        private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
        private readonly UiCalendarDateRange _blackout;
        internal UiCalendarView Preview { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        private readonly record struct Snapshot(string Culture, bool Latin, UiCalendarOptions Options,
            DateOnly? Single, UiCalendarDateRange? Range);
        internal Content(bool range, UiTypographyFonts? fonts)
        {
            _range = range;
            _fonts = fonts;
            _blackout = new(_today.AddDays(2), _today.AddDays(4));
            var options = new UiCalendarOptions(System : CreateSystem("system", false), Today : _today,
                Labels : new("이전 달", "다음 달", "오늘", "종료일을 선택하세요", "연도 선택", "월 선택"));
            IGallerySample sample;
            UiWidget sampleView;
            if (range)
            {
                var typed = GallerySample.Create(() => Dux.RangeCalendar(_selectedRange.Binding, options), layout : WrapCalendar);
                Preview = typed.Preview;
                typed.BindSource(Read, state => Source(state, true));
                sample = typed;
                sampleView = typed;
            }
            else
            {
                var typed = GallerySample.Create(() => Dux.Calendar(_single.Binding, options), layout : WrapCalendar);
                Preview = typed.Preview;
                typed.BindSource(Read, state => Source(state, false));
                sample = typed;
                sampleView = typed;
            }
            Samples = [sample];
            var cultures = Fluent.Dropdown("달력 문화권", new UiBinding<string?>(() => _culture,
                value => ChangeCulture(value ?? throw new ArgumentNullException(nameof(value)))),
                [new("system", "시스템 설정"), new("ko-KR", "한국 · 양력"), new("en-US", "미국 · 양력"),
                 new("en-GB", "영국 · 양력"), new("ar-SA", "사우디 · 움알쿠라력"), new("fa-IR", "이란 · 페르시아력"),
                 new("he-IL", "이스라엘 · 히브리력"), new("th-TH", "태국 · 불기"), new("ja-JP", "일본 · 연호")],
                new(Width : 260, ExpandLabel : "문화권 선택"));
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                cultures,
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("주 시작 요일 전환", () => Preview.Options = Preview.Options with
                    { FirstDayOfWeek = (Preview.Options.FirstDayOfWeek ?? Preview.Options.System!.FirstDayOfWeek) == DayOfWeek.Sunday ? DayOfWeek.Monday : DayOfWeek.Sunday }),
                    Fluent.Button("글 방향 전환", () => Preview.Options = Preview.Options with
                    { RightToLeft = !(Preview.Options.RightToLeft ?? Preview.Options.System!.RightToLeft) }),
                    Fluent.Button("날짜 숫자 전환", () => { _latin = !_latin; ChangeCulture(_culture); }),
                    Fluent.Button("주말 색상 전환", () => Preview.Options = Preview.Options with
                    { DayColors = new(Enabled : Preview.Options.DayColors?.Enabled == false) }),
                    Fluent.Button("선택 불가 날짜 전환", ToggleAvailability),
                    Fluent.Button("선택 지우기", () => { _single.Value = null; _selectedRange.Value = null;
                        if (Preview is UiRangeCalendarWidget calendar) calendar.CancelPendingSelection(); })),
                WrapCalendar(Fluent.Body1(() => _range ? "선택 : " + (_selectedRange.Value is { } value
                    ? Full(value.Start) + " ~ " + Full(value.End) : "없음") : "선택 : " + (_single.Value is { } date ? Full(date) : "없음"))),
                WrapCalendar(Fluent.Caption1(() => _status.Value)),
                Fluent.Body1(range ? "날짜를 두 번 선택해 기간을 정합니다. 방향키로 이동한 뒤 Enter로 선택할 수도 있습니다. Escape는 지정 중인 시작점만 취소합니다."
                    : "방향키는 포커스만 옮깁니다. Enter·Space·클릭으로 선택하고 PageUp/PageDown으로 달을, Shift+PageUp/PageDown으로 연도를 이동합니다. 위의 연도·월을 누르면 휠로 고를 수 있습니다."),
                sampleView);
        }
        private UiWidget WrapCalendar(UiWidget child)
        {
            var host = Fluent.FluentProvider(child);
            _localizedHosts.Add(host);
            return host;
        }
        private UiTypographyFonts ThaiFonts() => GalleryCalendarCulture.ThaiFonts(_fonts);
        private string Full(DateOnly date) => Preview.Options.System!.FormatFullDate(date);
        private Snapshot Read() => new(_culture, _latin, Preview.Options, _single.Value, _selectedRange.Value);
        internal void ChangeCulture(string key)
        {
            var system = CreateSystem(key, _latin);
            var selected = _range ? _selectedRange.Value : _single.Value is { } date ? new UiCalendarDateRange(date, date) : null;
            if (selected is { } selection && (selection.Start < system.MinimumDate || selection.End > system.MaximumDate))
            {
                _status.Value = "선택한 날짜가 이 달력의 지원 범위를 벗어납니다. 선택을 지운 뒤 바꿔 주세요.";
                return;
            }
            var fonts = key == "th-TH" ? ThaiFonts() : null;
            foreach (var host in _localizedHosts) host.Fonts = fonts;
            _culture = key;
            Preview.Options = Preview.Options with { System = system, FirstDayOfWeek = null, RightToLeft = null };
            _status.Value = string.Empty;
        }
        private void ToggleAvailability()
        {
            var next = Preview.Options.Availability is null ? new UiCalendarAvailability(blackouts : [_blackout]) : null;
            var selection = _range ? _selectedRange.Value : _single.Value is { } date ? new UiCalendarDateRange(date, date) : null;
            if (next is not null && selection is { } selected && !next.ContainsRange(selected))
            {
                _status.Value = "선택한 날짜에 제한이 겹칩니다. 선택을 지운 뒤 제한을 켜 주세요.";
                return;
            }
            Preview.Options = Preview.Options with { Availability = next };
            _status.Value = next is null ? string.Empty : Full(_blackout.Start) + " ~ " + Full(_blackout.End) + "은 선택할 수 없습니다.";
        }
        private static UiCultureCalendar CreateSystem(string key, bool latin) => GalleryCalendarCulture.Create(key, latin);
        private string Source(Snapshot state, bool range)
        {
            var digits = "UiCalendarDayNumbers." + (state.Latin ? "Latin" : "Culture");
            var culture = state.Culture == "system" ? "new UiCultureCalendar(dayNumbers : " + digits + ")"
                : "new UiCultureCalendar(System.Globalization.CultureInfo.GetCultureInfo(" + GallerySample.String(state.Culture) + ")"
                    + (state.Culture == "he-IL" ? ", new System.Globalization.HebrewCalendar()" : state.Culture == "ja-JP"
                        ? ", new System.Globalization.JapaneseCalendar()" : ", calendar : null") + ", dayNumbers : " + digits + ")";
            var initial = range ? state.Range is { } selected ? "new UiCalendarDateRange(" + Date(selected.Start) + ", " + Date(selected.End) + ")" : "null"
                : state.Single is { } single ? Date(single) : "null";
            var code = "var value = new UiState<" + (range ? "UiCalendarDateRange?" : "DateOnly?") + ">(" + initial + ");\n"
                + "var system = " + culture + ";\nvar options = new UiCalendarOptions(\n    System : system, Today : " + Date(_today)
                + ",\n    Labels : new UiCalendarLabels(\"이전 달\", \"다음 달\", \"오늘\", \"종료일을 선택하세요\", \"연도 선택\", \"월 선택\"),"
                + "\n    FirstDayOfWeek : " + (state.Options.FirstDayOfWeek is { } first ? "DayOfWeek." + first : "null")
                + ", RightToLeft : " + (state.Options.RightToLeft is { } rtl ? GallerySample.Boolean(rtl) : "null")
                + ",\n    DayColors : new UiCalendarDayColors(Enabled : " + GallerySample.Boolean(state.Options.DayColors?.Enabled != false) + "),"
                + "\n    Availability : " + (state.Options.Availability is null ? "null" : "new UiCalendarAvailability(blackouts : [new("
                    + Date(_blackout.Start) + ", " + Date(_blackout.End) + ")])")
                + ");\nvar calendar = Dux." + (range ? "RangeCalendar" : "Calendar") + "(value.Binding, options);";
            if (state.Culture == "th-TH")
                code += "\nvar thaiFont = Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Fonts\", \"NotoSansThai\", \"NotoSansThai.ttf\");"
                    + "\nvar example = Fluent.FluentProvider(calendar);\nexample.Fonts = new UiTypographyFonts(thaiFont, thaiFont, thaiFont, "
                    + GallerySample.String(_fonts?.SecondaryRegular) + ", " + GallerySample.String(_fonts?.SecondarySemibold) + ", "
                    + GallerySample.String(_fonts?.SecondaryBold) + ");";
            return code;
        }
        private static string Date(DateOnly date) => "new DateOnly(" + date.Year.ToString(CultureInfo.InvariantCulture) + ", "
            + date.Month.ToString(CultureInfo.InvariantCulture) + ", " + date.Day.ToString(CultureInfo.InvariantCulture) + ")";
    }
}
