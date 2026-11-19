using Duxel.Core;
using System.Globalization;

internal static class CalendarHolidayLoadingTests
{
    internal static void Run(Action<bool, string> check) => RunAsync(check).GetAwaiter().GetResult();
    private static async Task RunAsync(Action<bool, string> check)
    {
        // UI frames stay on one render writer; awaited worker completion is observed synchronously between frames.
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var wakes = 0; context.SetRequestFrameCallback(() => Interlocked.Increment(ref wakes));
        var provider = new Provider();
        using var calendar = Dux.Calendar(new UiState<DateOnly?>(new(2024, 3, 15)).Binding,
            new(System : new UiCultureCalendar(CultureInfo.GetCultureInfo("en-US")), HolidayProvider : provider));
        void Frame()
        {
            context.NewFrame(new(.016f, new(600, 500), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 600, 500), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); calendar.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        Frame();
        provider.Started.Task.GetAwaiter().GetResult();
        check(calendar.IsHolidayLoading && !context.State.HasActiveAnimations,
            "pending holiday I/O leaves the event-driven calendar idle instead of requesting animation frames");
        var pending = calendar.PendingHolidayLoad!;
        var focus = calendar.FocusedDate;
        calendar.Options = calendar.Options with { DayColors = new(Enabled : false) }; Frame();
        check(calendar.FocusedDate == focus && ReferenceEquals(pending, calendar.PendingHolidayLoad) && provider.Calls == 1,
            "paint-only settings preserve the displayed month and do not restart pending holiday requests");
        var before = Volatile.Read(ref wakes);
        provider.Release.SetResult(true); pending.GetAwaiter().GetResult();
        check(SpinWait.SpinUntil(() => Volatile.Read(ref wakes) > before, 1000),
            "completed holiday work explicitly wakes the idle platform loop");
        Frame(); var calls = provider.Calls; Frame(); Frame();
        check(calendar.HolidayStatus == UiCalendarHolidayStatus.Available && provider.Calls == calls && calls == 3
            && tree.CaptureSnapshot().Elements.Any(item => item.Node.Name.Contains("Fixture holiday")),
            "visible Gregorian boundary months load once and publish prepared accessible annotations on the render thread");
        var unavailable = new ImmediateProvider();
        calendar.Options = calendar.Options with { HolidayProvider = unavailable }; Frame();
        calendar.PendingHolidayLoad!.GetAwaiter().GetResult(); Frame();
        check(calendar.HolidayStatus == UiCalendarHolidayStatus.NetworkUnavailable
            && !tree.CaptureSnapshot().Elements.Any(item => item.Node.Name.Contains("Fixture holiday") || item.Node.Invalid)
            && tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Grid),
            "network-unavailable results remove holiday annotations without displaying an error or disabling the grid");
        foreach (var failure in new Exception[]
        {
            new System.Net.Http.HttpRequestException("Fixture connection failed"),
            new System.IO.IOException("Fixture connection interrupted"),
            new TimeoutException("Fixture request timed out"),
            new OperationCanceledException("Fixture transport timeout")
        })
        {
            var failing = new ThrowingProvider(failure);
            calendar.Options = calendar.Options with { HolidayProvider = failing }; Frame();
            calendar.PendingHolidayLoad!.GetAwaiter().GetResult(); Frame(); Frame();
            var nodes = tree.CaptureSnapshot().Elements;
            check(calendar.HolidayStatus == UiCalendarHolidayStatus.NetworkUnavailable
                && calendar.EffectiveHolidays is null && !calendar.IsHolidayLoading
                && nodes.Any(item => item.Node.Role == UiAccessibilityRole.Grid)
                && !nodes.Any(item => item.Node.Invalid || item.Node.Name.Contains("Fixture connection")
                    || item.Node.Name.Contains("Fixture request") || item.Node.Name.Contains("Fixture transport"))
                && failing.Calls == 1 && !context.State.HasActiveAnimations,
                $"{failure.GetType().Name} silently omits holidays without error UI, repeat requests or animation polling");
            var priorFocus = calendar.FocusedDate;
            calendar.NavigateMonth(1);
            check(calendar.FocusedDate != priorFocus, "calendar navigation remains available after a provider transport failure");
        }
        calendar.ShowDate(new(2024, 3, 15));
        var slow = new Provider(); calendar.Options = calendar.Options with { HolidayProvider = slow }; Frame();
        slow.Started.Task.GetAwaiter().GetResult(); var obsolete = calendar.PendingHolidayLoad!;
        calendar.NavigateMonth(1); Frame(); var current = calendar.PendingHolidayLoad!;
        var cancelled = false;
        try { obsolete.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
        slow.Release.SetResult(true); current.GetAwaiter().GetResult(); Frame();
        check(cancelled && calendar.FocusedDate.Month == 4 && calendar.HolidayStatus == UiCalendarHolidayStatus.Available,
            "month navigation cancels obsolete holiday work and applies only the current visible range");
        calendar.RefreshHolidays(); Frame(); calendar.PendingHolidayLoad!.GetAwaiter().GetResult(); Frame();
        check(calendar.HolidayStatus == UiCalendarHolidayStatus.Available,
            "explicit refresh remains safe after a prior request completed and released its cancellation resources");
        await Task.CompletedTask;
    }
    private sealed class Provider : IUiCalendarHolidayProvider
    {
        internal readonly TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        public async ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(int year, int month, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls); Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new(UiCalendarHolidayStatus.Available, new([new(new(year, month, 1), "Fixture holiday")]));
        }
    }
    private sealed class ThrowingProvider(Exception failure) : IUiCalendarHolidayProvider
    {
        internal int Calls;
        public ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(int year, int month, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return ValueTask.FromException<UiCalendarHolidayResult>(failure);
        }
    }
    private sealed class ImmediateProvider : IUiCalendarHolidayProvider
    {
        public ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(int year, int month, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new UiCalendarHolidayResult(UiCalendarHolidayStatus.NetworkUnavailable, UiCalendarHolidaySet.Empty));
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
