using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static partial class GalleryCloseLatencyProbe
{
    internal static void Run(string output, string executable, bool openTimePopup = false, bool verifyHolidays = false)
    {
        executable = Path.GetFullPath(executable);
        if (!File.Exists(executable)) throw new FileNotFoundException("Gallery executable is required.", executable);
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh close-latency output directory.");
        Directory.CreateDirectory(output);
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        using var process = new Process { StartInfo = start };
        using var ready = new ManualResetEventSlim();
        var stdout = new ConcurrentQueue<string>();
        var stderr = new ConcurrentQueue<string>();
        process.OutputDataReceived += (_, data) =>
        {
            if (data.Data is not { } line) return;
            stdout.Enqueue(line);
            if (line.Contains("StartupTiming[FirstFramePresented]", StringComparison.Ordinal)) ready.Set();
        };
        process.ErrorDataReceived += (_, data) => { if (data.Data is { } line) stderr.Enqueue(line); };
        if (!process.Start()) throw new InvalidOperationException("Gallery process did not start.");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        var id = process.Id;
        nint window = 0;
        double? hidden = null, detached = null, exited = null;
        bool killed = false;
        var preparation = new List<string>();
        string? failure = null;
        var timer = new Stopwatch();
        try
        {
            if (!ready.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Gallery did not present its first frame within the probe budget.");
            // Settle first-frame work without executing unrelated Gallery pages.
            Thread.Sleep(250);
            process.Refresh(); window = process.MainWindowHandle;
            if (window == 0 || !OwnsWindow(window, id) || !IsWindowVisible(window))
                throw new InvalidOperationException("The owned Gallery window is not visible after first presentation.");
            if (openTimePopup || verifyHolidays)
            {
                AccessibilityClientTests.Run(window, (condition, name) =>
                {
                    if (!condition) throw new InvalidOperationException(name);
                    preparation.Add(name);
                }, galleryTimePicker : openTimePopup, leaveGalleryTimePopupOpen : openTimePopup, galleryHolidays : verifyHolidays);
                if (!OwnsWindow(window, id) || !IsWindowVisible(window))
                    throw new InvalidOperationException("The owned Gallery window disappeared during UI Automation.");
            }
            timer.Start();
            if (!PostMessageW(window, 0x0010, 0, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            while (timer.Elapsed < TimeSpan.FromSeconds(15))
            {
                var ownsWindow = OwnsWindow(window, id);
                if (hidden is null && (!ownsWindow || !IsWindowVisible(window))) hidden = timer.Elapsed.TotalMilliseconds;
                if (detached is null && !ownsWindow) detached = timer.Elapsed.TotalMilliseconds;
                if (process.HasExited) { exited = timer.Elapsed.TotalMilliseconds; break; }
                Thread.Sleep(1);
            }
            if (exited is null) throw new TimeoutException("Gallery did not exit within the close probe budget.");
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Gallery exited with code : " + process.ExitCode);
            if (hidden is null || detached is null)
                throw new InvalidOperationException("The close probe did not observe both visibility loss and window retirement.");
        }
        catch (Exception error)
        {
            failure = error.ToString();
        }
        finally
        {
            if (!process.HasExited)
            {
                if (window != 0 && OwnsWindow(window, id)) _ = PostMessageW(window, 0x0010, 0, 0);
                if (!process.WaitForExit(3000)) { process.Kill(entireProcessTree : true); killed = true; }
            }
            process.WaitForExit();
            File.WriteAllLines(Path.Combine(output, "stdout.log"), stdout);
            File.WriteAllLines(Path.Combine(output, "stderr.log"), stderr);
            using var file = File.Create(Path.Combine(output, "result.json"));
            using var json = new Utf8JsonWriter(file, new() { Indented = true });
            json.WriteStartObject();
            json.WriteString("observedAtUtc", DateTimeOffset.UtcNow);
            json.WriteString("executable", executable);
            json.WriteNumber("ownedProcessId", id);
            json.WriteBoolean("firstFramePresented", ready.IsSet);
            json.WriteString("scenario", verifyHolidays ? "holiday-lookup" : openTimePopup ? "time-picker-popup-open" : "overview");
            json.WriteStartArray("preparationChecks");
            foreach (var name in preparation) json.WriteStringValue(name);
            json.WriteEndArray();
            Number("closeToHiddenMilliseconds", hidden);
            Number("closeToWindowNoLongerOwnedMilliseconds", detached);
            Number("closeToExitMilliseconds", exited);
            json.WriteNumber("exitCode", process.ExitCode);
            json.WriteBoolean("killedOwnedProcess", killed);
            json.WriteString("failure", failure);
            json.WriteString("visibilityScope", "Win32 WS_VISIBLE and owning process; not DWM compositor latency.");
            json.WriteEndObject();
            void Number(string name, double? value)
            {
                if (value is { } number) json.WriteNumber(name, number); else json.WriteNull(name);
            }
        }
        if (failure is not null) throw new InvalidOperationException("Gallery close probe failed; see its preserved logs.");
        Console.WriteLine($"Gallery close : hidden={hidden:F2}ms, HWND retired={detached:F2}ms, exit={exited:F2}ms");
    }
    private static bool OwnsWindow(nint window, int processId)
        => GetWindowThreadProcessId(window, out var owner) != 0 && owner == (uint)processId;
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint window);
    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);
}
