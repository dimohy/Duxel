using Duxel.App;
using Duxel.Windows.App;

namespace Duxel.Samples;

internal static class DesktopSampleHost
{
    internal static void Run(DuxelAppOptions options)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This example requires Windows.");

        DuxelWindowsApp.Run(options);
    }

    internal static void Run(DuxelAppOptions options, string[] args, bool useEventDrivenLoop = false)
        => Run(options);
}
