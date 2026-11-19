# Editable date and time pickers

Run `./run-gallery.ps1`, then **Duxel widgets → Date and time** for DatePicker, TimePicker and DateTimePicker. Adjacent sample source follows culture, hour cycle, seconds and current values.

The Gallery presentation check opens the date popup, selects the next month through its wheel, and picks a day. It also opens the time popup, selects a minute, presses **Done**, and verifies the combined binding for DateTimePicker. The check sends actions through the live Gallery's accessibility tree and waits for Vulkan presentation acknowledgements.

On Windows x64, `calendar-rendering-only` and `temporal-gallery-only` ran on both managed and NativeAOT .NET 11.0.0. The calendar suite passed 68 checks per runtime and matched 36 Vulkan PNG pairs. The picker suite passed 22 checks per runtime and matched 23 pairs, including popup, Thai and holiday outage examples. Evidence: `W:/Cache/Duxel/gallery-source-20261117/calendar-runtime-pixels-1937/comparison.json`.

With the Windows x64 .NET 11 NativeAOT Gallery running, run `Duxel.Graphics.Tests <fresh-output> uia-gallery-time-picker <gallery-pid>` from the built test output. The separate Windows UI Automation client passed three checks. It navigated to TimePicker, picked minute 05, read `15:05:05`, then pressed **Done** and read the same value after the popup closed. Both processes exited normally. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/uia-gallery-time-1930/native-probe/result.json`.

Use `win32-gallery-time-picker-wheel` in place of `uia-gallery-time-picker` to also exercise the Windows wheel message path. The client posts one wheel-down and one wheel-up message to the test Gallery window. The saved value went `15:05:05 → 15:06:05 → 15:05:05` through five checks. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/uia-gallery-time-1931/native-wheel/result.json`.

Use `uia-gallery-date-pickers` to verify DatePicker and DateTimePicker from the separate Windows client. In the Korean system locale, the client selected April from each month wheel and chose April 16 in each calendar. DateTimePicker kept the time during the date change and saved `2024-04-16T15:05:05` after the minute change. Evidence for the seven checks is in `W:/Cache/Duxel/gallery-source-20261117/uia-gallery-date-1932/native-date/result.json`.

Windows managed and Ubuntu/WSLg x64 NativeAOT each passed 104 presentation scenarios, including the three picker examples. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/temporal-gallery-1927-retry/verification.json`.

A separate .NET 11 AT-SPI client operated TimePicker in the Linux NativeAOT Gallery through D-Bus. Navigation, wheel selection and popup closure passed, and the client read back `15:05:05`. This checks external actions and state readback over AT-SPI. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/atspi-gallery-time-1928-retry/atspi-result.json`.

With the Gallery and client in the same D-Bus session, run `Duxel.Linux.App.Tests atspi-gallery-date-pickers <gallery-pid> <fresh-result.json>` to cover both date pickers. Under `C.UTF-8`, all seven checks passed on the x64 NativeAOT Gallery. DatePicker saved April 16 and DateTimePicker saved `2024-04-16T15:05:05` after the client closed the last popup. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/atspi-gallery-date-1933/date-fresh/result.json`.

The native wheel probe also sent one wheel-down and one wheel-up event through the X11 connection to the owned window. The saved time changed from `15:05:05` to `15:06:05` and back, with the seconds preserved. All five checks passed. These are injected X11 events. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/x11-gallery-wheel-1929/client-result.json`.

```csharp
var date = new UiState<DateOnly?>(null);
var datePicker = Dux.DatePicker("Date", date.Binding);
var time = new UiState<TimeOnly?>(null);
var timePicker = Dux.TimePicker("Time", time.Binding);
var appointment = new UiState<DateTime?>(null);
var dateTimePicker = Dux.DateTimePicker("Appointment", appointment.Binding);
```

DatePicker connects an editable localized date and the shared Calendar through one binding. Options.System accepts an immutable UiCultureCalendar. The default is the culture's short date; an explicit Format also requires exact parsing. Availability/Holidays/HolidayProvider flow into Calendar.

TimePicker combines input and hour/minute wheels. HourCycle.Culture follows the locale; Hours12/Hours24 override it. ShowSeconds adds seconds;12hour mode adds AM/PM. RTL cultures mirror column order. Scroll, drag, arrows and row activation work; Done closes. Hidden seconds/fractional precision are preserved. An empty picker starts its wheel at InitialTime or00:00.

```csharp
timePicker.Options = timePicker.Options with
{
    HourCycle = UiTimeHourCycle.Hours24,
    ShowSeconds = true
};
```

DateTimePicker reuses both controls. A partial component remains a draft until both are present. Preserve an existing DateTime.Kind; do not convert timezones. A new value is Unspecified. Clear removes partial drafts too. DateTimeOffset/timezone selection are outside this API.

Invalid text stays editable without overwriting the last valid binding; Field publishes validation. Empty text is null. Controlled refusal is not reported as an accepted typed value. External values synchronize text; culture changes reformat. Holiday network failure is separate from text validation and displays no error.

UiPickerLabels configures action/validation text independently of the date locale. Inherit parent fonts; Thai Gallery samples explicitly select bundled Noto Sans Thai.

## Shared wheels

Calendar and TimePicker share UiWheelPicker<T>. IUiWheelSequence<T> supplies neighbors, formatting and stable IDs. Prepared rows are reused while selection is unchanged. UiIntegerWheelSequence supports finite/cyclic integer ranges and native digits.

```csharp
var minute = new UiState<int>(30);
var wheel = Dux.WheelPicker(minute.Binding,
    new UiIntegerWheelSequence(0, 59, wrap : true),
    new UiWheelOptions("Minute", Width : 72));
```

Windows qualification/reuse: artifacts/calendar-widgets-2026-11-17/result.json. Actual Linux and pinned RC3 NativeAOT execution are separate acceptance items.

The DatePicker calendar uses a single popup surface. In TimePicker, the confirmation button fills the same content width as the wheels, with matching insets on both sides.
