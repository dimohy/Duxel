using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal sealed class CalendarPage(bool range, UiTypographyFonts? fonts) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new(range, fonts);
    public string Title => range ? "RangeCalendar" : "Calendar";
    public string Description => range ? "Choose start and end dates. Changing the locale preserves the dates you selected."
        : "Select a date and compare month names, weekdays, digits, and text direction across locales.";
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
        private string _culture = "gallery";
        private bool _latin;
        private readonly UiTypographyFonts? _fonts;
        private readonly List<UiThemeProvider> _localizedHosts = [];
        private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
        private readonly UiCalendarDateRange _blackout;
        internal UiCalendarView Preview { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        private readonly record struct Snapshot(string Culture, UiCalendarOptions Options,
            DateOnly? Single, UiCalendarDateRange? Range);
        internal Content(bool range, UiTypographyFonts? fonts)
        {
            _range = range;
            _fonts = fonts;
            _blackout = new(_today.AddDays(2), _today.AddDays(4));
            var options = new UiCalendarOptions(System : GalleryCalendarCulture.Create(_culture), Today : _today,
                Labels : GalleryCalendarCulture.CalendarLabels);
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
            var cultures = Fluent.Dropdown(GalleryText.Get("Calendar locale"), new UiBinding<string?>(() => _culture,
                value => ChangeCulture(value ?? throw new ArgumentNullException(nameof(value)))),
                [GalleryCalendarCulture.GalleryOption, GalleryCalendarCulture.SystemOption, new("ko-KR", GalleryText.Get("Korea · Gregorian")), new("en-US", GalleryText.Get("United States · Gregorian")),
                 new("en-GB", GalleryText.Get("United Kingdom · Gregorian")), new("ar-SA", GalleryText.Get("Saudi Arabia · Umm al-Qura")), new("fa-IR", GalleryText.Get("Iran · Persian calendar")),
                 new("he-IL", GalleryText.Get("Israel · Hebrew calendar")), new("th-TH", GalleryText.Get("Thailand · Buddhist calendar")), new("ja-JP", GalleryText.Get("Japan · Era calendar"))],
                new(Width : 260, ExpandLabel : GalleryText.Get("Choose locale")));
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                cultures,
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Toggle first day of week"), () => Preview.Options = Preview.Options with
                    { FirstDayOfWeek = (Preview.Options.FirstDayOfWeek ?? Preview.Options.System!.FirstDayOfWeek) == DayOfWeek.Sunday ? DayOfWeek.Monday : DayOfWeek.Sunday }),
                    Fluent.Button(GalleryText.Get("Toggle text direction"), () => Preview.Options = Preview.Options with
                    { RightToLeft = !(Preview.Options.RightToLeft ?? Preview.Options.System!.RightToLeft) }),
                    Fluent.Button(GalleryText.Get("Toggle date digits"), () => { _latin = !_latin; ChangeCulture(_culture); }),
                    Fluent.Button(GalleryText.Get("Toggle weekend colors"), () => Preview.Options = Preview.Options with
                    { DayColors = new(Enabled : Preview.Options.DayColors?.Enabled == false) }),
                    Fluent.Button(GalleryText.Get("Toggle unavailable dates"), ToggleAvailability),
                    Fluent.Button(GalleryText.Get("Clear selection"), () => { _single.Value = null; _selectedRange.Value = null;
                        if (Preview is UiRangeCalendarWidget calendar) calendar.CancelPendingSelection(); })),
                WrapCalendar(Fluent.Body1(() => GalleryText.Format("Selected dates: {0}", SelectionCaption()))),
                WrapCalendar(Fluent.Caption1(() => _status.Value)),
                Fluent.Body1(range ? GalleryText.Get("Choose two dates to set a range. Use the arrow keys to move, then press Enter to select. Escape cancels the pending start date.")
                    : GalleryText.Get("Use arrow keys to move focus. Select with Enter, Space, or a click. Change the month with Page Up or Page Down, and the year with Shift and either key. Select the year or month heading to open its wheel.")),
                sampleView);
        }
        private string SelectionCaption()
        {
            if (_range && _selectedRange.Value is { } range)
                return Full(range.Start) + " ~ " + Full(range.End);
            return _single.Value is { } date ? Full(date) : GalleryText.Get("None");
        }

        private UiWidget WrapCalendar(UiWidget child)
        {
            var host = Fluent.FluentProvider(child);
            _localizedHosts.Add(host);
            return host;
        }
        private UiTypographyFonts ThaiFonts() => GalleryCalendarCulture.ThaiFonts(_fonts);
        private string Full(DateOnly date) => Preview.Options.System!.FormatFullDate(date);
        private Snapshot Read() => new(_culture, Preview.Options, _single.Value, _selectedRange.Value);
        internal void ChangeCulture(string key)
        {
            var system = GalleryCalendarCulture.Create(key, _latin);
            var selected = _range ? _selectedRange.Value : _single.Value is { } date ? new UiCalendarDateRange(date, date) : null;
            if (selected is { } selection && (selection.Start < system.MinimumDate || selection.End > system.MaximumDate))
            {
                _status.Value = GalleryText.Get("The selected dates are outside this calendar's range. Clear the selection to change calendars.");
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
                _status.Value = GalleryText.Get("The restriction overlaps the selection. Clear the selection to enable it.");
                return;
            }
            Preview.Options = Preview.Options with { Availability = next };
            _status.Value = next is null ? string.Empty : GalleryText.Format("{0} through {1} are unavailable.", Full(_blackout.Start), Full(_blackout.End));
        }
        private string Source(Snapshot state, bool range)
        {
            var culture = GalleryCalendarCulture.SystemSource((UiCultureCalendar)state.Options.System!, state.Culture, includeDayNumbers : true);
            var initial = range ? state.Range is { } selected ? "new UiCalendarDateRange(" + Date(selected.Start) + ", " + Date(selected.End) + ")" : "null"
                : state.Single is { } single ? Date(single) : "null";
            var code = "var value = new UiState<" + (range ? "UiCalendarDateRange?" : "DateOnly?") + ">(" + initial + ");\n"
                + "var system = " + culture + ";\nvar options = new UiCalendarOptions(\n    System : system, Today : " + Date(_today)
                + ",\n    Labels : " + GalleryCalendarCulture.CalendarLabelsSource(state.Options.Labels!) + ","
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
