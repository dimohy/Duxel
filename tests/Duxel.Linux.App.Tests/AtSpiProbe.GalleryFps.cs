using System.Globalization;

internal static partial class AtSpiProbe
{
    private static void VerifyGalleryFps(nint window, NativeObject eventWait, nuint nativeWindow, List<string> checks)
    {
        var profile = Environment.GetEnvironmentVariable("DUXEL_APP_PROFILE_OUT")
            ?? throw new InvalidOperationException("The FPS probe requires the Gallery app profile.");
        using var peer = new NativePeer(nativeWindow);

        string ReadStatus()
        {
            atspi_accessible_clear_cache(window);
            using var label = Find(window, value => Name(value).Contains(" FPS · ", StringComparison.Ordinal));
            return label is null ? "" : Name(label.Value);
        }

        long ReadFrame()
        {
            const string prefix = "[duxel-app-prof] frame=";
            long frame = 0;
            foreach (var line in File.ReadLines(profile))
            {
                if (!line.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var end = line.IndexOf(' ', prefix.Length);
                if (end > prefix.Length)
                    frame = long.Parse(line.AsSpan(prefix.Length, end - prefix.Length), CultureInfo.InvariantCulture);
            }
            return frame;
        }

        InvokeNamed(eventWait, "toggle");
        string first = "";
        Wait(() => (first = ReadStatus()).Length > 0, "continuous Gallery FPS sample");
        string latest = "";
        Wait(() => (latest = ReadStatus()).Length > 0 && latest != first, "a second Gallery FPS sample");
        checks.Add("The live Gallery header refreshes FPS and presentation statistics in continuous mode.");

        InvokeNamed(eventWait, "toggle");
        Wait(() =>
        {
            var frame = ReadFrame();
            Thread.Sleep(250);
            return frame > 0 && ReadFrame() == frame;
        }, "Gallery frame counter settles in event-wait mode");
        var idleFrame = ReadFrame();
        var idleStatus = ReadStatus();
        Thread.Sleep(2200);
        if (ReadFrame() != idleFrame || ReadStatus() != idleStatus)
            throw new InvalidOperationException("The Gallery emitted a frame or refreshed diagnostics while idle.");
        checks.Add("Event-wait mode holds the app frame counter and visible FPS text for 2.2 seconds.");

        peer.Pointer(6, 1000, 750);
        Wait(() => ReadFrame() > idleFrame, "an X11 pointer event wakes the Gallery");
        if (ReadStatus() != idleStatus)
            throw new InvalidOperationException("The first native input changed the FPS sample after idle.");
        checks.Add("The first pointer-driven frame resumes rendering without adding the idle gap to its FPS sample.");

        InvokeNamed(eventWait, "toggle");
        Wait(() => (latest = ReadStatus()).Length > 0 && latest != idleStatus, "Gallery FPS refresh resumes");
        InvokeNamed(eventWait, "toggle");
        checks.Add("Leaving event-wait mode resumes FPS sampling.");
    }
}
