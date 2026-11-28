using System.Diagnostics;
using System.Runtime.Versioning;
using Duxel.App;
using Duxel.Core;
using Duxel.Linux.App;

namespace Duxel.Samples;

internal static class DesktopSampleHost
{
    internal static void Run(DuxelAppOptions options)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("This example requires Linux.");

        var font = MatchDesktopFont();
        RunWithFont(options with { Design = ReadDesktopDesign() }, font);
    }

    internal static void Run(DuxelAppOptions options, string[] args, bool useEventDrivenLoop = false)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("This example requires Linux.");
        if (args.Length != 1 || !File.Exists(args[0]))
            throw new ArgumentException("Pass an installed font file as the first argument.", nameof(args));

        if (useEventDrivenLoop)
            options = options with { Frame = options.Frame with { UseEventDrivenPlatformLoop = true } };
        RunWithFont(options, args[0]);
    }

    [SupportedOSPlatform("linux")]
    private static void RunWithFont(DuxelAppOptions options, string font)
        => DuxelLinuxApp.Run(options with
        {
            Font = new() { PrimaryFontPath = font, SecondaryFontPath = null, Typography = new(font, font, font) }
        });

    private static string MatchDesktopFont()
    {
        using var match = Process.Start(new ProcessStartInfo("fc-match", ["-f", "%{file}", "sans-serif"])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Could not find a desktop font.");

        var font = match.StandardOutput.ReadToEnd().Trim();
        match.WaitForExit();
        if (match.ExitCode != 0 || !File.Exists(font))
            throw new InvalidOperationException("Install Fontconfig and a sans-serif desktop font.");
        return font;
    }

    private static UiCompiledDesign ReadDesktopDesign()
    {
        var request = new ProcessStartInfo("gdbus",
        [
            "call", "--session", "--timeout", "1",
            "--dest", "org.freedesktop.portal.Desktop",
            "--object-path", "/org/freedesktop/portal/desktop",
            "--method", "org.freedesktop.portal.Settings.Read",
            "org.freedesktop.appearance", "color-scheme"
        ])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var portal = Process.Start(request)!;
            var errors = portal.StandardError.ReadToEndAsync();
            var setting = portal.StandardOutput.ReadToEnd();
            portal.WaitForExit();
            errors.GetAwaiter().GetResult();
            return portal.ExitCode == 0 && setting.Contains("<uint32 1>", StringComparison.Ordinal)
                ? UiCompiledDesign.Windows11Dark
                : UiCompiledDesign.Windows11;
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 2)
        {
            // A minimal X11 session may omit the Settings portal.
            return UiCompiledDesign.Windows11;
        }
    }
}
