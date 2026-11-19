using Duxel.App;
using Duxel.Core;
using System.Net;
using System.Net.Http;

internal static class HolidayProviderTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunAsync(check).GetAwaiter().GetResult();
        RunPublicAsync(check).GetAwaiter().GetResult();
    }

    internal static void RunLive(Action<bool, string> check) => RunLiveAsync(check).GetAwaiter().GetResult();

    private static async Task RunLiveAsync(Action<bool, string> check)
    {
        using var http = new HttpClient();
        var provider = new KoreanHolidayProvider(http);
        async Task CheckDate(DateOnly date, string expected)
        {
            var result = await provider.GetHolidaysAsync(date.Year, date.Month);
            check(result.Holidays.TryGetName(date, out var name) && name == expected,
                $"Live, credential-free Korean holiday: {date:yyyy-MM-dd} {expected} ({result.Status} {result.DiagnosticCode})");
        }
        await CheckDate(new(2025, 1, 27), "임시공휴일");
        await CheckDate(new(2026, 5, 25), "대체공휴일(부처님 오신 날)");
        await CheckDate(new(2026, 6, 3), "전국동시지방선거");
    }

    private static async Task RunPublicAsync(Action<bool, string> check)
    {
        const string json = """{"2026-01-01":["1월 1일"],"2026-05-25":["대체공휴일","다른 이름","대체공휴일"]}""";
        var handler = new Handler((request, _) =>
        {
            check(request.Headers.Authorization is null && request.RequestUri?.Query.Length == 0
                && request.RequestUri.AbsolutePath.EndsWith("/public/2026.json", StringComparison.Ordinal),
                "annual requests send no key, query string or authorization header");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });
        using var http = new HttpClient(handler);
        var provider = new KoreanHolidayProvider(http);
        var may = await provider.GetHolidaysAsync(2026, 5);
        var june = await provider.GetHolidaysAsync(2026, 6);
        check(may.Holidays.Items.Count == 2 && june.Status == UiCalendarHolidayStatus.Available && june.Holidays.Items.Count == 0,
            "annual JSON keeps distinct names and treats a known empty month as available");
        check(handler.Calls == 1, "neighboring months share one cached annual request");

        using var missingHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var missing = await new KoreanHolidayProvider(missingHttp).GetHolidaysAsync(2027, 1);
        check(missing.DiagnosticCode == "KR_HOLIDAYS_YEAR_UNAVAILABLE" && missing.DisplayHolidays is null,
            "a missing annual dataset omits annotations without inventing holidays");

        using var invalidHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json) })));
        var invalid = await new KoreanHolidayProvider(invalidHttp).GetHolidaysAsync(2027, 1);
        check(invalid.Status == UiCalendarHolidayStatus.InvalidResponse && invalid.DisplayHolidays is null,
            "a wrong-year dataset is rejected before rendering");
        using var offlineHttp = new HttpClient(new Handler((_, _) => throw new HttpRequestException("offline")));
        var offline = await new KoreanHolidayProvider(offlineHttp).GetHolidaysAsync(2026, 1);
        check(offline.Status == UiCalendarHolidayStatus.NetworkUnavailable && offline.DisplayHolidays is null,
            "a network failure omits public data without annotations");
    }
    private static async Task RunAsync(Action<bool, string> check)
    {
        static string Xml(string items, int total = 1) => "<response><header><resultCode>00</resultCode></header><body><items>" + items
            + "</items><totalCount>" + total + "</totalCount></body></response>";
        const string item = "<item><locdate>20240506</locdate><isHoliday>Y</isHoliday><dateName>대체공휴일</dateName></item>";
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Xml(item)) }));
        using var http = new HttpClient(handler);
        var clock = new Clock();
        var provider = new KoreanPublicHolidayProvider(http, "fixture+/=", clock : clock);
        var first = await provider.GetHolidaysAsync(2024, 5);
        var second = await provider.GetHolidaysAsync(2024, 5);
        check(first.Status == UiCalendarHolidayStatus.Available && first.Holidays.TryGetName(new(2024, 5, 6), out var name) && name == "대체공휴일",
            "KASI adapter parses absolute dates and public holiday names from the documented XML shape");
        check(handler.Calls == 1 && ReferenceEquals(first, second), "repeated month reads reuse the prepared response instead of performing network/XML work");
        check(handler.LastUri!.Query.Contains("ServiceKey=fixture%2B%2F%3D", StringComparison.OrdinalIgnoreCase)
            && handler.LastUri.Query.Contains("solMonth=05", StringComparison.Ordinal), "KASI requests encode the decoded service key exactly once and address an explicit month");
        await provider.InvalidateAsync(2024, 5); await provider.GetHolidaysAsync(2024, 5);
        check(handler.Calls == 2, "explicit invalidation refreshes an already cached month");
        var network = new Handler((_, _) => throw new HttpRequestException("fixture secret URL must not escape"));
        using var networkHttp = new HttpClient(network);
        var offline = new KoreanPublicHolidayProvider(networkHttp, "fixture-key", clock : clock);
        var missing = await offline.GetHolidaysAsync(2024, 5);
        check(missing.Status == UiCalendarHolidayStatus.NetworkUnavailable && missing.DisplayHolidays is null
            && missing.DiagnosticCode == "KASI_NETWORK_UNAVAILABLE", "network errors produce no display annotations and only a sanitized diagnostic code");
        await offline.GetHolidaysAsync(2024, 5);
        check(network.Calls == 1, "brief negative caching prevents repeated requests during an outage");
        clock.Now += TimeSpan.FromSeconds(31); await offline.GetHolidaysAsync(2024, 5);
        check(network.Calls == 2, "outage results can be retried after their short cache lifetime");
        var slow = new Handler(async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(HttpStatusCode.OK); });
        using var slowHttp = new HttpClient(slow);
        var timed = await new KoreanPublicHolidayProvider(slowHttp, "fixture", timeout : TimeSpan.FromMilliseconds(20)).GetHolidaysAsync(2024, 5);
        check(timed.Status == UiCalendarHolidayStatus.NetworkUnavailable && timed.DisplayHolidays is null && timed.DiagnosticCode == "KASI_TIMEOUT",
            "provider timeout silently omits holiday annotations without blocking calendar interaction");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var cancelledAsRequested = false;
        try { await provider.GetHolidaysAsync(2024, 5, cancelled.Token); }
        catch (OperationCanceledException) { cancelledAsRequested = true; }
        check(cancelledAsRequested, "caller cancellation remains cancellation rather than being misreported as a network outage");
        var invalid = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("<!DOCTYPE response [<!ENTITY xxe SYSTEM 'file:///fixture'>]><response>&xxe;</response>") }));
        using var invalidHttp = new HttpClient(invalid);
        var malformed = await new KoreanPublicHolidayProvider(invalidHttp, "fixture").GetHolidaysAsync(2024, 5);
        check(malformed.Status == UiCalendarHolidayStatus.InvalidResponse && malformed.DisplayHolidays is null,
            "DTD/entity responses are rejected before they can enter a calendar snapshot");
        var emptyHandler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Xml("", 0)) }));
        using var emptyHttp = new HttpClient(emptyHandler);
        var empty = await new KoreanPublicHolidayProvider(emptyHttp, "fixture").GetHolidaysAsync(2024, 5);
        check(empty.Status == UiCalendarHolidayStatus.Available && empty.Holidays.Items.Count == 0,
            "an authoritative empty holiday month is distinct from a network failure");
        var rejectedHandler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));
        using var rejectedHttp = new HttpClient(rejectedHandler);
        var rejected = await new KoreanPublicHolidayProvider(rejectedHttp, "fixture").GetHolidaysAsync(2024, 5);
        check(rejected.Status == UiCalendarHolidayStatus.ServiceRejected && rejected.DiagnosticCode == "KASI_HTTP_REJECTED",
            "service rejection is classified separately without exposing keys, URLs or server messages");
        using var malformedHeaderHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("<response><header><resultCode>invalid</resultCode></header></response>") })));
        var malformedHeader = await new KoreanPublicHolidayProvider(malformedHeaderHttp, "fixture").GetHolidaysAsync(2024, 5);
        check(malformedHeader.Status == UiCalendarHolidayStatus.InvalidResponse && malformedHeader.DisplayHolidays is null
            && malformedHeader.DiagnosticCode == "KASI_RESPONSE_HEADER_INVALID",
            "well-formed XML with an invalid result header is not classified as a service rejection");
        const string gatewayError = "<OpenAPI_ServiceResponse><cmmMsgHeader><errMsg>fixture secret</errMsg>"
            + "<returnReasonCode>20</returnReasonCode></cmmMsgHeader></OpenAPI_ServiceResponse>";
        using var gatewayHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(gatewayError) })));
        var gateway = await new KoreanPublicHolidayProvider(gatewayHttp, "fixture").GetHolidaysAsync(2024, 5);
        check(gateway.Status == UiCalendarHolidayStatus.ServiceRejected && gateway.DisplayHolidays is null
            && gateway.DiagnosticCode == "KASI_SERVICE_REJECTED",
            "a portal error envelope is rejected even with HTTP 200, using a fixed diagnostic");
        var annotations = new UiCalendarHolidaySet([new(new(2024, 5, 6), "A"), new(new(2024, 5, 6), "B"), new(new(2024, 5, 6), "A")]);
        check(annotations.Items.Count == 2 && annotations.TryGetName(new(2024, 5, 6), out var joined) && joined == "A · B",
            "prepared holiday sets preserve distinct same-day names and remove exact duplicate annotations");
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls; internal Uri? LastUri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; LastUri = request.RequestUri; return send(request, cancellationToken); }
    }
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
