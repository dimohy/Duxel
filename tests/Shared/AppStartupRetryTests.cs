using Duxel.App;
using Duxel.Core;

internal static class AppStartupRetryTests
{
    internal static void Run(string primaryFont, string secondaryFont, Action<DuxelAppOptions, DuxelAppSession> run,
        Action<nint>? verifyWindowClosed = null)
    {
        var session = new DuxelAppSession();
        var previousDiagnostics = UiFontAtlasBuilder.DiagnosticsLog;
        Action<string> originalDiagnostics = _ => { };
        UiFontAtlasBuilder.DiagnosticsLog = originalDiagnostics;
        var windows = 0;
        nint lastWindow = 0;
        var presented = false;
        var options = DuxelApp.Options(new ProbeScreen(), "Duxel startup retry fixture", 320, 240, vsync: false) with
        {
            Font = new()
            {
                PrimaryFontPath = primaryFont,
                SecondaryFontPath = secondaryFont,
                Typography = new(primaryFont, primaryFont, primaryFont, secondaryFont, secondaryFont, secondaryFont),
                FastStartup = false
            },
            Window = new()
            {
                Title = "Duxel startup retry fixture",
                Width = 320,
                Height = 240,
                VSync = false,
                WindowCreated = hwnd =>
                {
                    lastWindow = hwnd;
                    windows++;
                }
            },
            Frame = new() { UseEventDrivenPlatformLoop = true }
        };
        try
        {
            var rendererError = new InvalidOperationException("Injected startup Vulkan failure.");
            ExpectFailure(options with
            {
                Debug = new()
                {
                    LogStartupTimings = true,
                    Log = line =>
                    {
                        if (line.StartsWith("VulkanInit[Surface]", StringComparison.Ordinal))
                            throw rendererError;
                    }
                }
            }, rendererError);

            var clipboardError = new InvalidOperationException("Injected clipboard factory failure.");
            ExpectFailure(options with
            {
                ClipboardFactory = _ => throw clipboardError,
                Debug = new() { LogStartupTimings = true, Log = _ => { } }
            }, clipboardError);

            var shutdownError = new InvalidOperationException("Injected shutdown diagnostics failure.");
            ExpectFailure(options with
            {
                Debug = new()
                {
                    LogStartupTimings = true,
                    Log = line =>
                    {
                        CloseAfterFirstFrame(line);
                        if (presented && line.StartsWith("ShutdownTiming[Begin]", StringComparison.Ordinal))
                            throw shutdownError;
                    }
                }
            }, shutdownError);
            if (!presented)
                throw new InvalidOperationException("The shutdown failure was not preceded by a Vulkan presentation.");

            presented = false;
            run(options with
            {
                Debug = new() { LogStartupTimings = true, Log = CloseAfterFirstFrame }
            }, session);
            verifyWindowClosed?.Invoke(lastWindow);
            if (!presented || windows != 4 || session.IsExitRequested
                || !ReferenceEquals(UiFontAtlasBuilder.DiagnosticsLog, originalDiagnostics))
                throw new InvalidOperationException("The same session did not present and close cleanly on retry.");

            Console.WriteLine("The same session presented a Vulkan frame and closed after three injected failures.");
        }
        finally
        {
            UiFontAtlasBuilder.DiagnosticsLog = previousDiagnostics;
        }

        void ExpectFailure(DuxelAppOptions candidate, Exception expected)
        {
            try
            {
                run(candidate, session);
                throw new InvalidOperationException("The expected run failure was not triggered.");
            }
            catch (InvalidOperationException error) when (ReferenceEquals(error, expected))
            {
                verifyWindowClosed?.Invoke(lastWindow);
                if (!ReferenceEquals(UiFontAtlasBuilder.DiagnosticsLog, originalDiagnostics))
                    throw new InvalidOperationException("Startup left the session font diagnostics installed.", error);
                session.RequestFrame();
                session.Exit();
                Console.WriteLine(expected.Message + " The original exception and font diagnostics were preserved.");
            }
        }

        void CloseAfterFirstFrame(string line)
        {
            if (line.StartsWith("RenderThreadException:", StringComparison.Ordinal))
                Console.Error.WriteLine(line);
            if (!line.StartsWith("StartupTiming[FirstFramePresented]", StringComparison.Ordinal))
                return;
            presented = true;
            session.Exit();
        }
    }

    private sealed class ProbeScreen : UiScreen
    {
        public override void Render(UiImmediateContext ui) => ui.Text("Startup retry");
    }
}
