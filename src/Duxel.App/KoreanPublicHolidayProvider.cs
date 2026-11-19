using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using Duxel.Core;

namespace Duxel.App;

/// <summary>KASI getRestDeInfo adapter. The caller owns HttpClient; service keys and server messages are never returned in diagnostics.</summary>
public sealed class KoreanPublicHolidayProvider : IUiCalendarHolidayProvider
{
    private readonly HttpClient _http;
    private readonly string _serviceKey;
    private readonly TimeSpan _timeout, _successLifetime, _failureLifetime;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly Dictionary<(int Year, int Month), CacheEntry> _cache = [];
    private sealed record CacheEntry(UiCalendarHolidayResult Result, DateTimeOffset Expires, DateTimeOffset Stored);
    public KoreanPublicHolidayProvider(HttpClient http, string decodedServiceKey, TimeSpan? timeout = null,
        TimeSpan? successLifetime = null, TimeSpan? failureLifetime = null, TimeProvider? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentException.ThrowIfNullOrWhiteSpace(decodedServiceKey);
        _serviceKey = decodedServiceKey;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _successLifetime = successLifetime ?? TimeSpan.FromHours(6);
        _failureLifetime = failureLifetime ?? TimeSpan.FromSeconds(30);
        if (_timeout <= TimeSpan.Zero || _timeout.TotalMilliseconds > int.MaxValue || _successLifetime <= TimeSpan.Zero || _failureLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        _clock = clock ?? TimeProvider.System;
    }
    public async ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        _ = new DateOnly(year, month, 1);
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = (year, month);
            var now = _clock.GetUtcNow();
            if (_cache.TryGetValue(key, out var cached) && cached.Expires > now) return cached.Result;
            var result = await FetchAsync(year, month, cancellationToken).ConfigureAwait(false);
            now = _clock.GetUtcNow();
            if (_cache.Count >= 24 && !_cache.ContainsKey(key)) _cache.Remove(_cache.MinBy(item => item.Value.Stored).Key);
            _cache[key] = new(result, now + (result.Status == UiCalendarHolidayStatus.Available ? _successLifetime : _failureLifetime), now);
            return result;
        }
        finally { _serial.Release(); }
    }
    public async ValueTask InvalidateAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        _ = new DateOnly(year, month, 1);
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { _cache.Remove((year, month)); }
        finally { _serial.Release(); }
    }
    private static UiCalendarHolidayResult Failure(UiCalendarHolidayStatus status, string code) => new(status, UiCalendarHolidaySet.Empty, code);
    private async Task<UiCalendarHolidayResult> FetchAsync(int year, int month, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var token = timeout.Token;
        try
        {
            var entries = new List<UiCalendarHoliday>();
            var received = 0;
            int? total = null;
            for (var page = 1; page <= 10; page++)
            {
                var uri = new Uri("https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo"
                    + "?ServiceKey=" + Uri.EscapeDataString(_serviceKey) + "&solYear=" + year.ToString("D4", CultureInfo.InvariantCulture)
                    + "&solMonth=" + month.ToString("D2", CultureInfo.InvariantCulture)
                    + "&numOfRows=100&pageNo=" + page.ToString(CultureInfo.InvariantCulture));
                using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
                    return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KASI_HTTP_UNAVAILABLE");
                if (!response.IsSuccessStatusCode) return Failure(UiCalendarHolidayStatus.ServiceRejected, "KASI_HTTP_REJECTED");
                if (response.Content.Headers.ContentLength > 1_048_576) return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_RESPONSE_TOO_LARGE");
                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_048_576 });
                var xml = await XDocument.LoadAsync(reader, LoadOptions.None, token).ConfigureAwait(false);
                var gatewayCode = xml.Element("OpenAPI_ServiceResponse")?.Element("cmmMsgHeader")?.Element("returnReasonCode")?.Value;
                if (IsResultCode(gatewayCode) && gatewayCode != "00")
                    return Failure(UiCalendarHolidayStatus.ServiceRejected, "KASI_SERVICE_REJECTED");
                var root = xml.Element("response");
                var header = root?.Element("header");
                var code = header?.Element("resultCode")?.Value;
                if (root is null || !IsResultCode(code))
                    return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_RESPONSE_HEADER_INVALID");
                if (code != "00") return Failure(UiCalendarHolidayStatus.ServiceRejected, "KASI_SERVICE_REJECTED");
                var body = root.Element("body");
                if (!int.TryParse(body?.Element("totalCount")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                    || count is < 0 or > 1000 || total is { } previous && previous != count)
                    return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_TOTAL_COUNT_INVALID");
                total = count;
                var items = body?.Element("items")?.Elements("item").ToArray() ?? [];
                foreach (var item in items)
                {
                    if (!DateOnly.TryParseExact(item.Element("locdate")?.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        || date.Year != year || date.Month != month) return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_DATE_INVALID");
                    var holiday = item.Element("isHoliday")?.Value;
                    if (holiday is not ("Y" or "N")) return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_HOLIDAY_FLAG_INVALID");
                    var name = item.Element("dateName")?.Value;
                    if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)) return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_NAME_INVALID");
                    if (holiday == "Y") entries.Add(new(date, name));
                }
                received += items.Length;
                if (received == total) return new(UiCalendarHolidayStatus.Available, new(entries));
                if (received > total || items.Length == 0) return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_PAGE_COUNT_INVALID");
            }
            return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_PAGE_LIMIT");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KASI_TIMEOUT"); }
        catch (HttpRequestException) { return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KASI_NETWORK_UNAVAILABLE"); }
        catch (IOException) { return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KASI_CONNECTION_INTERRUPTED"); }
        catch (XmlException) { return Failure(UiCalendarHolidayStatus.InvalidResponse, "KASI_XML_INVALID"); }
    }

    private static bool IsResultCode(string? code) =>
        code is { Length: 2 } && char.IsAsciiDigit(code[0]) && char.IsAsciiDigit(code[1]);
}
