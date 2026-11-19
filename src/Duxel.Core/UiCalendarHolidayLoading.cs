namespace Duxel.Core;

public abstract partial class UiCalendarView
{
    private Task<UiCalendarHolidayResult>? _holidayTask;
    private CancellationTokenSource? _holidayCancellation;
    private (IUiCalendarHolidayProvider Provider, int First, int Last)? _holidayRequest;
    private UiCalendarHolidaySet? _providerHolidays;
    private int _holidayGeneration;
    public bool IsHolidayLoading => _holidayTask is { IsCompleted: false };
    public UiCalendarHolidayStatus? HolidayStatus { get; private set; }
    internal UiCalendarHolidaySet? EffectiveHolidays => Options.Holidays ?? _providerHolidays;
    internal Task<UiCalendarHolidayResult>? PendingHolidayLoad => _holidayTask;
    public void RefreshHolidays()
    {
        ThrowIfDisposed(); ThrowIfBuilding(); ResetHolidayLoading(); RequestRebuild();
    }
    private void ResetHolidayLoading()
    {
        Interlocked.Increment(ref _holidayGeneration);
        var cancellation = _holidayCancellation; var pending = _holidayTask;
        _holidayCancellation = null;
        if (cancellation is not null)
        {
            cancellation.Cancel();
            if (pending is null || pending.IsCompleted) cancellation.Dispose();
            else _ = pending.ContinueWith(_ => cancellation.Dispose(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        _holidayTask = null; _holidayRequest = null; _providerHolidays = null; HolidayStatus = null;
        _month = null;
    }
    private void SynchronizeHolidayLoading(Action? wake)
    {
        if (_holidayTask is { IsCompleted: true } completed)
        {
            _holidayTask = null;
            _holidayCancellation?.Dispose(); _holidayCancellation = null;
            var result = completed.GetAwaiter().GetResult();
            _providerHolidays = result.DisplayHolidays; HolidayStatus = result.Status;
            _month = null;
        }
        if (Options.HolidayProvider is not { } provider) return;
        var month = Month;
        var gridStart = month.MonthStartDayNumber - month.LeadingDays;
        var firstDay = Math.Max(System.MinimumDate.DayNumber, gridStart);
        var lastDay = Math.Min(System.MaximumDate.DayNumber, gridStart + month.Cells.Length - 1);
        var request = (provider, firstDay, lastDay);
        if (_holidayRequest is { } prior && ReferenceEquals(prior.Provider, provider) && prior.First == firstDay && prior.Last == lastDay) return;
        ResetHolidayLoading(); _holidayRequest = request;
        var cancellation = new CancellationTokenSource(); _holidayCancellation = cancellation;
        var generation = Volatile.Read(ref _holidayGeneration);
        var token = cancellation.Token;
        // The render thread only schedules work. HTTP/XML executes on the worker and publishes an immutable result.
        var task = Task.Run(async () =>
        {
            try
            {
                var first = DateOnly.FromDayNumber(firstDay); var last = DateOnly.FromDayNumber(lastDay);
                var rows = new List<UiCalendarHoliday>();
                var current = new DateOnly(first.Year, first.Month, 1);
                while (true)
                {
                    var result = await provider.GetHolidaysAsync(current.Year, current.Month, token).ConfigureAwait(false);
                    if (result.Status != UiCalendarHolidayStatus.Available) return result with { Holidays = UiCalendarHolidaySet.Empty };
                    rows.AddRange(result.Holidays.Items);
                    if (current.Year == last.Year && current.Month == last.Month) break;
                    current = current.AddMonths(1);
                }
                return new UiCalendarHolidayResult(UiCalendarHolidayStatus.Available, new(rows));
            }
            catch (System.Net.Http.HttpRequestException)
            { return new(UiCalendarHolidayStatus.NetworkUnavailable, UiCalendarHolidaySet.Empty, "HOLIDAY_PROVIDER_NETWORK_UNAVAILABLE"); }
            catch (System.IO.IOException)
            { return new(UiCalendarHolidayStatus.NetworkUnavailable, UiCalendarHolidaySet.Empty, "HOLIDAY_PROVIDER_CONNECTION_INTERRUPTED"); }
            catch (TimeoutException)
            { return new(UiCalendarHolidayStatus.NetworkUnavailable, UiCalendarHolidaySet.Empty, "HOLIDAY_PROVIDER_TIMEOUT"); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { return new(UiCalendarHolidayStatus.NetworkUnavailable, UiCalendarHolidaySet.Empty, "HOLIDAY_PROVIDER_TIMEOUT"); }
        });
        _holidayTask = task;
        _ = task.ContinueWith(finished =>
        {
            if (generation == Volatile.Read(ref _holidayGeneration)) wake?.Invoke();
            else if (finished.IsFaulted) _ = finished.Exception; // Observe obsolete failures after the owner has moved or closed.
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
public sealed partial class UiImmediateContext
{
    internal Action? CalendarFrameRequest => _requestFrame;
}
