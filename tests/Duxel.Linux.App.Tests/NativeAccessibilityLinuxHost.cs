using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.App;
using Duxel.Core;
using Duxel.Linux.App;

internal static class NativeAccessibilityLinuxHost
{
    internal static int Run(string output, string primary, string secondary, UiScreen screen,
        string component, string clientMode, Action verify, Action? nextFrame = null)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("The native accessibility host requires Linux/X11.");
        if (Directory.Exists(output))
            throw new IOException("Use a fresh native accessibility result directory.");
        foreach (var font in new[] { primary, secondary })
            if (!File.Exists(font))
                throw new FileNotFoundException("The selected native accessibility font is missing.", font);
        Directory.CreateDirectory(output);
        using var firstFrame = new ManualResetEventSlim();
        var session = new DuxelAppSession();
        var view = new NativeScreen(screen, firstFrame, nextFrame);
        Task? driver = null;
        ExceptionDispatchInfo? failure = null;
        void Client(string mode, string destination, nuint? window = null)
        {
            var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("The owned executable path is missing."))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(mode);
            start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add(destination);
            if (window is { } handle)
                start.ArgumentList.Add(handle.ToString(CultureInfo.InvariantCulture));
            using var client = Process.Start(start) ?? throw new InvalidOperationException("The independent accessibility client did not start.");
            var stdout = client.StandardOutput.ReadToEndAsync();
            var stderr = client.StandardError.ReadToEndAsync();
            var exited = client.WaitForExit(20000);
            if (!exited)
            {
                client.Kill(entireProcessTree: true);
                client.WaitForExit();
            }
            File.WriteAllText(destination + ".stdout.log", stdout.GetAwaiter().GetResult());
            File.WriteAllText(destination + ".stderr.log", stderr.GetAwaiter().GetResult());
            if (!exited)
                throw new TimeoutException("The owned native accessibility client exceeded20seconds.");
            if (client.ExitCode != 0)
            {
                var record = NativeProbeFailure.Read(destination);
                if (record is not null)
                    throw new NativeProbeClientException(mode, client.ExitCode, record);
                throw new InvalidOperationException("The independent accessibility client exited : " + client.ExitCode);
            }
        }
        var options = DuxelApp.Options(view, $"Duxel owned Linux {component} fixture", 900, 600, vsync: false) with
        {
            Window = new()
            {
                Width = 900,
                Height = 600,
                Title = $"Duxel owned Linux {component} fixture",
                VSync = false,
                WindowCreated = handle => driver = Task.Run(() =>
                {
                    try
                    {
                        if (!firstFrame.Wait(8000))
                            throw new TimeoutException("The native accessibility surface did not render.");
                        Client(clientMode, Path.Combine(output, "client.json"), (nuint)handle);
                    }
                    catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
                    finally { session.Exit(); }
                })
            },
            Font = new()
            {
                PrimaryFontPath = primary,
                SecondaryFontPath = secondary,
                FontSize = 16,
                FastStartup = false,
                Typography = new(primary, primary, primary, secondary, secondary, secondary)
                {
                    Monospace = primary
                }
            },
            Frame = new()
            {
                UseEventDrivenPlatformLoop = true
            }
        };
        try
        {
            DuxelLinuxApp.Run(options, session);
        }
        finally { session.Exit(); driver?.GetAwaiter().GetResult(); }
        failure?.Throw();
        verify();
        Client("atspi-absent", Path.Combine(output, "retired.json"));
        return view.Frames;
    }
    private sealed class NativeScreen(UiScreen scene, ManualResetEventSlim firstFrame, Action? nextFrame) : UiScreen
    {
        private UiFloatTransition _probeClock = new(0);
        internal int Frames
        {
            get; private set;
        }
        public override void Render(UiImmediateContext ui)
        {
            Frames++;
            nextFrame?.Invoke();
            scene.Render(ui);
            // A finite test clock provides published-frame acknowledgements, not a throughput benchmark.
            ui.AnimateFloat(ref _probeClock, 1, new(30, UiAnimationEasing.Linear));
            firstFrame.Set();
        }
    }
}
