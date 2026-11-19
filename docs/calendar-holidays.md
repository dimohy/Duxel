# Holiday providers

The Gallery uses the public [holidays-kr annual JSON files](https://github.com/hyunbinseo/holidays-kr/tree/main/public). No API key or account is required. Retrieval and rendering are separate. Other countries or company holidays implement the same provider interface.

```csharp
public interface IUiCalendarHolidayProvider
{
    ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(
        int year, int month, CancellationToken cancellationToken = default);
}
```

Year/month use the Gregorian DateOnly timeline. UiCalendarHolidaySet is an immutable prepared date/name snapshot, preserving multiple distinct names per day and removing duplicates. An authoritative empty response differs from network failure.

```csharp
using Duxel.App;

using var http = new HttpClient();
IUiCalendarHolidayProvider provider = new KoreanHolidayProvider(http);
var date = new UiState<DateOnly?>(null);
var calendar = Dux.Calendar(date.Binding, new(HolidayProvider : provider));
```

Open **Date and time → Holiday provider → Load visible month** in the Gallery. The initial New Year's Day record is offline example data. The public source publishes data by year. Availability depends on the years published by the source, and newly declared holidays appear when it updates.

## Execution and failure behavior

Calendar schedules work when its visible range or provider changes, or when explicitly refreshed. It loads the Gregorian months intersecting the six-week grid on a worker, then applies immutable results on the UI thread. Completion wakes the platform loop once. Month or provider changes and disposal cancel obsolete work. Color-only changes preserve pending requests.

Network failures, interrupted connections and timeouts omit holiday annotations with no on-screen error, including transport exceptions from custom providers. Selection, navigation and input continue. Rejected/malformed responses also have no display data; distinguish them through status and sanitized fixed codes. Diagnostics exclude raw server messages, request URLs and service keys.

The public provider downloads each year once and prepares all 12 month snapshots. It caches up to eight years, with a six-hour lifetime for successful data and 30 seconds for failures. A missing annual file or invalid JSON yields no annotations. Payloads are bounded to 64 KiB and date, year and name fields are validated before use.

The existing `KoreanPublicHolidayProvider` is available for applications that explicitly use a key for the [KASI Special Day Information API](https://www.data.go.kr/data/15012690/openapi.do). Its monthly cache, error reporting and XML validation remain available to those callers. `Calendar.RefreshHolidays` respects the selected provider's caching.

For prefetched data, set Options.Holidays instead of HolidayProvider; both together are rejected. Holidays use red dates, dots, name tooltips and accessible names. They remain selectable unless separately restricted by UiCalendarAvailability. Forced system colors win. DatePicker accepts the same Holidays/HolidayProvider options.

The focused `holiday-provider-only` and `calendar-holiday-loading-only` tests cover parsing, caching and failure behavior. Run `holiday-provider-live` in the graphics test executable to request the public source using the same provider as the Gallery.

Keep the caller-owned HttpClient and provider alive while the calendar uses them; dispose the calendar before releasing those resources.
## Gallery verification

The Windows live verification fetched the 2025 temporary holiday, the 2026 substitute holiday for Buddha's Birthday, and the 2026 local election day. It also activated the Gallery's load button and checked the substitute holiday on the calendar. The Ubuntu/WSLg Linux x64 NativeAOT Gallery passed 104 presentation scenarios, including the simulated outage and subsequent date selection. Evidence files: `W:/Cache/Duxel/gallery-source-20261117/keyless-holidays-1944/holiday-gallery-live-retry/result.json` and `W:/Cache/Duxel/gallery-source-20261117/keyless-holidays-1944/linux-presentation/result.json`.

Run the live Gallery check from the repository root. Each command requires a fresh output directory and writes `result.json` after the Gallery presents the calendar with the May 2026 substitute holiday and its live example code. The normal `--verify` scenarios use the simulated outage and require no network.

```powershell
.\run-gallery.ps1 -VerifyLiveHolidays -VerificationOutput .\artifacts\windows-live-holidays
.\run-gallery-linux.ps1 -SampleArguments @('--verify-live-holidays', '/tmp/duxel-linux-live-holidays')
```

The live check passed on .NET 11.0.0 in the Windows managed Gallery and Ubuntu/WSLg Linux x64 NativeAOT Gallery. The same Linux executable passed all 104 standard presentation scenarios. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/linux-keyless-holiday-1950/verification.json`.

On WSLg, a separate `libatspi` client read the offline New Year's Day name from the calendar cell, switched to the outage example, and selected the date through AT-SPI. It then invoked the live lookup and read the published name `1월 1일` on the same selected date. This verifies the name, action and selected state exposed to assistive software. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/atspi-holiday-1951/holiday-retry/result.json`.

On Windows, a separate UI Automation client performed the same flow on the managed and x64 NativeAOT Gallery. Both verified the accessible date name and `SelectionItem.IsSelected` after the lookup, then shut down normally. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/uia-holiday-1952/managed/result.json` and `native/result.json` under the same run root. Screen-reader speech requires a separate check.

