using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Linux;

internal sealed partial class ObserverLinuxPresentationVerificationScreen : UiScreen
{
    private readonly LinuxX11PlatformBackend _platform;
    private readonly DuxelAppSession _session;
    private readonly string _output;
    private readonly ConcurrentDictionary<ulong, UiRendererFrameMetrics> _processed = new();
    private readonly List<string> _checks = [];
    private readonly Task _worker;
    private Published? _published;
    private int _version;
    private ExceptionDispatchInfo? _failure;
    private int _clears, _remaps;

    internal ObserverLinuxPresentationVerificationScreen(LinuxX11PlatformBackend platform,
        DuxelAppSession session, string output)
    {
        _platform = platform;
        _session = session;
        _output = Path.GetFullPath(output);
        if (Directory.Exists(_output))
            throw new IOException("Native presentation validation requires a fresh result directory.");
        Directory.CreateDirectory(_output);
        _worker = Task.Run(Run);
    }

    public override void Render(UiImmediateContext ui)
    {
        var metrics = ui.GetRendererFrameMetrics();
        if (metrics.DrawDataFrameId != 0)
            _processed.TryAdd(metrics.DrawDataFrameId, metrics);
        var version = Volatile.Read(ref _version);
        ui.EnableRootViewportContentLayout(contentPadding: 8);
        ui.GetWindowDrawList().AddRectFilled(new(24, 24, 100, 60),
            version == 0 ? new(210, 40, 40) : new(40, 210, 70), ui.WhiteTextureId);
        ui.SetCursorScreenPos(new(24, 100));
        ui.Text("Native surface " + version);
        Volatile.Write(ref _published, new(ui.GetDrawDataFrameId(), version));
    }

    private static async Task Wait(Func<bool> condition, string name)
    {
        var start = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(start).TotalSeconds > 8)
                throw new TimeoutException(name);
            await Task.Delay(2);
        }
    }

    private async Task<UiRendererFrameMetrics> Flush()
    {
        async Task<Published> Publish()
        {
            var before = Volatile.Read(ref _published)?.FrameId ?? 0;
            _session.RequestFrame();
            await Wait(() => (Volatile.Read(ref _published)?.FrameId ?? 0) > before, "native UI frame publication");
            return Volatile.Read(ref _published)!;
        }
        var version = Volatile.Read(ref _version);
        var target = await Publish();
        while (target.Version != version)
            target = await Publish();
        while (!_processed.ContainsKey(target.FrameId))
            await Publish();
        return _processed[target.FrameId];
    }

    private void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        _checks.Add(name);
    }

    private async Task Run()
    {
        nint display = 0;
        try
        {
            display = XOpenDisplay(0);
            if (display == 0)
                throw new InvalidOperationException("Native presentation XOpenDisplay failed.");
            var start = Stopwatch.GetTimestamp();
            var stable = await Flush();
            while (!stable.PresentationReused)
            {
                if (Stopwatch.GetElapsedTime(start).TotalSeconds > 8)
                    throw new TimeoutException("unchanged native surface did not become reusable");
                stable = await Flush();
            }
            var sequence = stable.PresentationSequence;
            Check(sequence > 0, "initial real presentation precedes native reuse");
            Check(Pixel(display) == 0xd22828, "actual native window contains the expected initial rectangle");
            for (var i = 0; i < 8; i++)
            {
                var frame = await Flush();
                Check(frame.PresentationReused && frame.PresentationSequence == sequence,
                    "explicit unchanged CPU frame accepted without another native presentation " + i);
            }

            async Task NativeInvalidate(bool remap, nuint expected)
            {
                var before = Volatile.Read(ref _published)!.FrameId;
                var token = _platform.PresentationInvalidationSequence;
                var present = _processed.Values.Max(x => x.PresentationSequence);
                if (remap)
                {
                    XUnmapWindow(display, _platform.WindowHandle);
                    XMapWindow(display, _platform.WindowHandle);
                    _remaps++;
                }
                else
                {
                    XClearArea(display, _platform.WindowHandle, 0, 0, 0, 0, 1);
                    _clears++;
                }
                XSync(display, 0);
                // No UI request is issued until the real native event has woken it.
                await Wait(() => _platform.PresentationInvalidationSequence > token
                    && (Volatile.Read(ref _published)?.FrameId ?? 0) > before,
                    "native expose/remap wakes the event-waiting producer");
                await Flush();
                Check(_processed.Values.Any(x => x.DrawDataFrameId > before && !x.PresentationReused
                    && x.PresentationSequence > present), "native invalidation causes an actual fresh presentation");
                await Wait(() => Pixel(display) == expected, "native pixels restored after expose/remap");
                Check(true, remap ? "actual mapped-window pixels restored" : "actual cleared-window pixels restored");
                var settled = await Flush();
                Check(settled.PresentationReused, "native surface returns to reuse after restoration");
            }

            await NativeInvalidate(false, 0xd22828);
            await NativeInvalidate(true, 0xd22828);
            sequence = _processed.Values.Max(x => x.PresentationSequence);
            Volatile.Write(ref _version, 1);
            var changed = await Flush();
            Check(!changed.PresentationReused && changed.PresentationSequence > sequence,
                "changed model reaches a new actual presentation");
            await Wait(() => Pixel(display) == 0x28d246, "changed native pixel color");
            Check(true, "changed model is visible in the real native window");
            await NativeInvalidate(false, 0x28d246);
        }
        catch (Exception error) { _failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            if (display != 0)
                XCloseDisplay(display);
            _session.Exit();
        }
    }

    private nuint Pixel(nint display)
    {
        var image = XGetImage(display, _platform.WindowHandle, 50, 50, 1, 1, nuint.MaxValue, 2);
        if (image == 0)
            throw new InvalidOperationException("Native window pixel readback failed.");
        try
        {
            return XGetPixel(image, 0, 0);
        }
        finally { XDestroyImage(image); }
    }

    internal void Complete()
    {
        _worker.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(_output, "result.json"), JsonSerializer.Serialize(
            new NativePresentationResult(_failure is null, _failure?.SourceException.ToString(),
                _checks.Count, _checks.ToArray(), _clears, _remaps,
                _platform.PresentationInvalidationSequence,
                _processed.Values.OrderBy(x => x.DrawDataFrameId).Select(x =>
                    new NativePresentationFrame(x.DrawDataFrameId, x.PresentationSequence, x.PresentationReused)).ToArray(),
                "Actual X11 window pixels, explicit frame acceptance, native clear/expose and unmap/map. Not physical monitor/compositor latency."),
            NativePresentationJson.Default.NativePresentationResult));
        _failure?.Throw();
    }

    private sealed record Published(ulong FrameId, int Version);
    [LibraryImport("libX11.so.6")] private static partial nint XOpenDisplay(nint name);
    [LibraryImport("libX11.so.6")] private static partial int XCloseDisplay(nint display);
    [LibraryImport("libX11.so.6")] private static partial int XSync(nint display, int discard);
    [LibraryImport("libX11.so.6")] private static partial int XClearArea(nint display, nuint window, int x, int y, uint width, uint height, int exposures);
    [LibraryImport("libX11.so.6")] private static partial int XUnmapWindow(nint display, nuint window);
    [LibraryImport("libX11.so.6")] private static partial int XMapWindow(nint display, nuint window);
    [LibraryImport("libX11.so.6")] private static partial nint XGetImage(nint display, nuint drawable, int x, int y, uint width, uint height, nuint planeMask, int format);
    [LibraryImport("libX11.so.6")] private static partial nuint XGetPixel(nint image, int x, int y);
    [LibraryImport("libX11.so.6")] private static partial int XDestroyImage(nint image);
}

internal sealed record NativePresentationFrame(ulong FrameId, ulong ActualPresentSequence, bool Reused);
internal sealed record NativePresentationResult(bool Completed, string? Failure, int Passed, string[] Checks,
    int Clears, int Remaps, long NativeInvalidationSequence, NativePresentationFrame[] Frames, string Scope);
[JsonSerializable(typeof(NativePresentationResult))]
internal partial class NativePresentationJson : JsonSerializerContext;
