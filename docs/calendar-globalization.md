# Calendar globalization research and implementation contract

2026-11-17 user decision: support both single-date and date-range selection. Date/locale/month-grid preparation is separated from the completed single/range views, interaction, accessibility and Gallery wiring.

Research: CLDR separates week starts from weekends and standalone month names from formatted-date contexts ([CLDR](https://unicode.org/reports/tr35/tr35-dates.html)). .NET cultures have distinct default/optional calendars and reject unsupported formatter/calendar combinations ([Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/datetime/working-with-calendars)). NativeDigits does not automatically affect formatted output ([API](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.numberformatinfo.nativedigits)). Use the W3C calendar grid's single Tab stop and directional/Home/End/Page navigation as the interaction baseline ([APG](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/)). Official Fluent separates field and picker responsibilities; international range calendars demonstrate why alternative calendar systems must be architectural ([Fluent](https://github.com/microsoft/fluentui/blob/master/specs/Datepicker.md), [React Aria](https://react-aria.adobe.com/RangeCalendar)).

Contracts:

- Store absolute DateOnly values, with no timezone/DST conversion during selection.
- Support single and range selection, separate pending anchors from committed ranges, and normalize reverse selection.
- Separate arithmetic/formatting with IUiCalendarSystem. Default adapters snapshot read-only CultureInfo/System.Globalization.Calendar; do not silently substitute Gregorian or English.
- Capture CurrentCulture from the OS. Allow explicit culture, first-weekday and direction overrides. Use localized month/year/day labels, native or Latin digits and RTL geometry. Keep full accessible weekday names separately.
- Handle leap days/months,13-month years, Japanese era boundaries and supported-date limits, including partial boundary months.
- Separate weekend policy from week starts, and culture week numbering from ISO numbering.
- Define minimum/maximum dates, unavailable dates/ranges and availability inside a selected range.
- Separate focus from selection; expose full date names, today, selected and disabled states. Physical arrow movement must remain coherent in RTL.
- Snapshot locale data once and prepare grids when month/locale settings change, without per-frame XML, localization or month arithmetic.
- Gallery examples compare Korean, US/UK, Arabic, Persian, Hebrew, Thai and Japanese settings, with adjacent source for both selection modes.

Unsupported language/calendar combinations in .NET's optional set fail explicitly; IUiCalendarSystem allows additional explicit arithmetic/text adapters. Qualify actual combinations and date ranges rather than claiming all historical calendars, OSes or screen readers.

## Usage and implementation

Run `./run-gallery.ps1` and select **Duxel widgets → Date and time → Calendar / RangeCalendar**. Each sample owns adjacent live source. Change culture, week start, direction, day digits, weekend colors and availability.

```csharp
var date = new UiState<DateOnly?>(null);
var calendar = Dux.Calendar(date.Binding);
var range = new UiState<UiCalendarDateRange?>(null);
var rangeCalendar = Dux.RangeCalendar(range.Binding);
```

Click/Enter/Space selects; arrows move focus independently. Home/End respect the locale week. PageUp/PageDown changes months; Shift+Page changes years. Click year/month separately to open centered vertical wheels supporting scrolling, dragging, arrows, row activation, Enter and Escape. Period navigation changes the visible month without editing the committed value. Shared Popover owns layering, focus and dismissal.

Two activations commit a normalized inclusive range. Escape cancels only its pending anchor. Interior blackout dates reject a range. UiCalendarAvailability prepares merged intervals and bounded searches. The model supports Hebrew leap months/13month years, Japanese era transitions, partial boundary months, localized full names and RTL geometry. Latin day numerals leave headings/accessibility culture formatting intact.

Weekend defaults follow [CLDR48 weekData](https://github.com/unicode-org/cldr/blob/release-48/common/supplemental/supplementalData.xml): Korea Saturday/Sunday, Saudi Arabia Friday/Saturday, Iran Friday. Neutral/invariant cultures use its001 world rule. Override Weekend/DayColors; holidays are separate and forced system colors win. The Korean defaults are blue Saturday/red Sunday.

The Thai Gallery explicitly scopes bundled OFL Noto Sans Thai over calendar, selected-date summary and restriction text. FluentProvider.Fonts scopes typography while retaining inherited palette/geometry. Today is captured on construction; a host can update Options.Today at midnight. Static views request no perpetual frames.

UiCultureCalendar snapshots read-only culture/calendar data and rejects unsupported combinations. Custom immutable IUiCalendarSystem adapters add arithmetic/text; IUiCalendarPeriodSystem also enables period wheels. Week-number display, holiday calculation and timezone conversion are separate; week numbers are not implemented in this increment.

These are Duxel extensions outside the curated55 Fluent2 core entries, using Fluent presentation/date guidance without claiming full pixel conformance. Windows model/input/Vulkan/Gallery qualification: artifacts/calendar-widgets-2026-11-17/result.json. Actual Linux pixels, complete external assistive-technology acceptance and pinned RC3 NativeAOT remain unrun. See [editable pickers](date-time-pickers.md) and [holiday providers](calendar-holidays.md).

On Linux x64, run `atspi-gallery-range-calendar <gallery-pid> <fresh-result.json>` with the external `Duxel.Linux.App.Tests` client while Gallery is open. The client checks the pending endpoint, selection from end to start, preservation of both dates when switching to the Thai Buddhist calendar, and clearing the range. This check covers accessible text, actions and selection state.

Use `x11-gallery-range-calendar <gallery-pid> <fresh-result.json> <decimal-window-id>` for the same checks plus X11 keyboard input. The client checks that Enter starts another range at the focused date and that Escape cancels it and restores the previous range in the accessibility tree.

On a 1280×900 Gallery window, use `x11-gallery-range-calendar-pixels <gallery-pid> <fresh-result.json> <decimal-window-id>` to include keyboard confirmation and save viewport captures next to the result. The client sends Enter, Right, Enter to commit a new range and reads the selected dates through AT-SPI after switching to Thai Buddhist. It takes two X11 captures each after restoring the old range, committing the new range, and changing the calendar.

WSLg x64 managed and NativeAOT .NET 11.0.0 each passed all ten external checks and exited normally. The six cross-runtime screenshot pairs matched below the diagnostic header. The two captures per state within each runtime matched over the full viewport. The test covers synthetic X11 input and native AT-SPI state. Screen-reader speech and ARM64 hardware remain open.
