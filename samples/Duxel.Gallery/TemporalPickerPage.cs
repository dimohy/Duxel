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
        TemporalPickerKind.Date => "날짜를 직접 입력하거나 달력에서 선택하세요. 지역별 표기와 입력 검증을 확인할 수 있습니다.",
        TemporalPickerKind.Time => "시각을 직접 입력하거나 시·분 휠로 선택하세요. 12·24시간제와 초 표시도 바꿀 수 있습니다.",
        _ => "날짜와 시각을 따로 입력해 하나의 값으로 저장합니다. 달력과 시간 휠도 함께 사용할 수 있습니다."
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
        private string _culture = "system";
        private readonly UiState<DateOnly?> _date = new(new(2024, 3, 15));
        private readonly UiState<TimeOnly?> _time = new(new(15, 4, 5));
        private readonly UiState<DateTime?> _combined = new(new(2024, 3, 15, 15, 4, 5));
        private readonly UiThemeProvider _fontHost;
        internal UiWidget Preview { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        private readonly record struct Snapshot(string Culture, UiDatePickerOptions Date, UiTimePickerOptions Time,
            DateOnly? DateValue, TimeOnly? TimeValue, DateTime? CombinedValue);
        internal Content(TemporalPickerKind kind, UiTypographyFonts? fonts)
        {
            _kind = kind; _fonts = fonts;
            var system = GalleryCalendarCulture.Create("system");
            var dateOptions = new UiDatePickerOptions(System : system, Labels : GalleryCalendarCulture.Labels, CalendarLabels : GalleryCalendarCulture.CalendarLabels);
            var timeOptions = new UiTimePickerOptions(Culture : system.Culture, Labels : GalleryCalendarCulture.Labels);
            UiThemeProvider? host = null;
            UiWidget Layout(UiWidget preview) => host = Fluent.FluentProvider(preview);
            IGallerySample sample; UiWidget view;
            if (kind == TemporalPickerKind.Date)
            {
                var typed = GallerySample.Create(() => Dux.DatePicker("날짜", _date.Binding, dateOptions), layout : Layout);
                Preview = typed.Preview; typed.BindSource(Read, Source); sample = typed; view = typed;
            }
            else if (kind == TemporalPickerKind.Time)
            {
                var typed = GallerySample.Create(() => Dux.TimePicker("시각", _time.Binding, timeOptions), layout : Layout);
                Preview = typed.Preview; typed.BindSource(Read, Source); sample = typed; view = typed;
            }
            else
            {
                var typed = GallerySample.Create(() => Dux.DateTimePicker("예약", _combined.Binding, new(dateOptions, timeOptions)), layout : Layout);
                Preview = typed.Preview; typed.BindSource(Read, Source); sample = typed; view = typed;
            }
            _fontHost = host!; Samples = [sample];
            var culture = Fluent.Dropdown("표시 지역", new UiBinding<string?>(() => _culture, value => ChangeCulture(value!)),
                [new("system", "시스템 설정"), new("ko-KR", "한국"), new("en-US", "미국"), new("en-GB", "영국"),
                 new("ar-SA", "사우디아라비아"), new("he-IL", "이스라엘 · 히브리력"), new("th-TH", "태국 · 불기"), new("ja-JP", "일본 · 연호")], new(Width : 240));
            var controls = new List<UiWidget> { culture, Fluent.Button("값 지우기", Clear) };
            if (kind != TemporalPickerKind.Date)
            {
                controls.Add(Fluent.Button("12·24시간제 전환", () => SetTime(TimeOptions with
                { HourCycle = TimeOptions.HourCycle == UiTimeHourCycle.Hours24 ? UiTimeHourCycle.Hours12 : UiTimeHourCycle.Hours24 })));
                controls.Add(Fluent.Button("초 표시 전환", () => SetTime(TimeOptions with { ShowSeconds = !TimeOptions.ShowSeconds })));
            }
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8), controls.ToArray()),
                Fluent.Body1(() => "저장 값 : " + ((kind == TemporalPickerKind.Date ? _date.Value?.ToString("O", CultureInfo.InvariantCulture)
                    : kind == TemporalPickerKind.Time ? _time.Value?.ToString("O", CultureInfo.InvariantCulture)
                    : _combined.Value?.ToString("O", CultureInfo.InvariantCulture)) ?? "없음")), view);
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
        private Snapshot Read() => new(_culture, DateOptions, TimeOptions, _date.Value, _time.Value, _combined.Value);
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
            var code = "var system = " + GalleryCalendarCulture.SystemSource(state.Culture) + ";\n";
            code += "var labels = new UiPickerLabels(ChooseDate : \"날짜 선택\", ChooseTime : \"시각 선택\", Hour : \"시\", Minute : \"분\", Second : \"초\", Period : \"오전·오후\", Done : \"확인\",\n    InvalidDate : \"올바른 날짜를 입력하세요.\", InvalidTime : \"올바른 시각을 입력하세요.\", Rejected : \"값을 적용할 수 없습니다.\");\n";
            var dateOptions = "new UiDatePickerOptions(System : system, Labels : labels,\n    CalendarLabels : new(\"이전 달\", \"다음 달\", \"오늘\", \"종료일을 선택하세요\", \"연도 선택\", \"월 선택\"))";
            var timeOptions = "new UiTimePickerOptions(Culture : system.Culture, HourCycle : UiTimeHourCycle." + state.Time.HourCycle
                + ", ShowSeconds : " + GallerySample.Boolean(state.Time.ShowSeconds) + ", Labels : labels)";
            code += _kind switch
            {
                TemporalPickerKind.Date => "var value = new UiState<DateOnly?>(" + dateValue + ");\nvar picker = Dux.DatePicker(\"날짜\", value.Binding, " + dateOptions + ");",
                TemporalPickerKind.Time => "var value = new UiState<TimeOnly?>(" + timeValue + ");\nvar picker = Dux.TimePicker(\"시각\", value.Binding, " + timeOptions + ");",
                _ => "var value = new UiState<DateTime?>(" + combinedValue + ");\nvar picker = Dux.DateTimePicker(\"예약\", value.Binding,\n    new(Date : " + dateOptions + ", Time : " + timeOptions + "));"
            };
            if (state.Culture == "th-TH")
                code += "\nvar thaiFont = Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Fonts\", \"NotoSansThai\", \"NotoSansThai.ttf\");\nvar example = Fluent.FluentProvider(picker);\nexample.Fonts = new UiTypographyFonts(thaiFont, thaiFont, thaiFont, "
                    + GallerySample.String(_fonts?.SecondaryRegular) + ", " + GallerySample.String(_fonts?.SecondarySemibold) + ", " + GallerySample.String(_fonts?.SecondaryBold) + ");";
            return code;
        }
    }
}
