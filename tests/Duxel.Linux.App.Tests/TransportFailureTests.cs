using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Platform.Linux;

internal static class TransportFailureTests
{
    internal static void Run(string output, bool missingPango = false)
    {
        var address = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        if (missingPango)
            NativeLibrary.SetDllImportResolver(typeof(LinuxX11PlatformBackend).Assembly, (name, _, _) =>
                name == "libpango-1.0.so.0" ? throw new DllNotFoundException("Fixture Pango dependency unavailable.") : 0);
        else if (address is null || !address.StartsWith("unix:path=/tmp/duxel-fixture-no-bus-", StringComparison.Ordinal))
            throw new ArgumentException("This failure fixture requires its explicit unreachable private bus address.");
        var failures = 0;
        var elapsed = new List<double>();
        void Failure()
        {
            var watch = Stopwatch.StartNew();
            try
            {
                using var platform = new LinuxX11PlatformBackend(new(64, 64, "Owned AT-SPI failure fixture", Visible: false)
                {
                    Accessibility = new()
                    {
                        CallTimeoutMilliseconds = 200
                    }
                });
                throw new InvalidOperationException("The inaccessible bus unexpectedly produced a native provider.");
            }
            catch (IOException error) when (!missingPango && error.Message.Contains("AT-SPI connect session bus", StringComparison.Ordinal)) { failures++; }
            catch (DllNotFoundException error) when (missingPango && error.Message == "Fixture Pango dependency unavailable.") { failures++; }
            if (watch.Elapsed > TimeSpan.FromSeconds(2))
                throw new TimeoutException("AT-SPI startup failed to terminate promptly.");
            elapsed.Add(watch.Elapsed.TotalMilliseconds);
        }
        for (var i = 0; i < 3; i++)
            Failure();
        var before = Directory.GetFiles("/proc/self/fd").Length;
        for (var i = 0; i < 5; i++)
            Failure();
        var after = Directory.GetFiles("/proc/self/fd").Length;
        var threads = Directory.GetDirectories("/proc/self/task").Select(path => File.ReadAllText(Path.Combine(path, "comm")).Trim()).ToArray();
        if (after != before)
            throw new InvalidOperationException($"Failed AT-SPI startup leaked descriptors: {before} -> {after}");
        if (threads.Contains("DuxelAT-SPI", StringComparer.Ordinal))
            throw new InvalidOperationException("A failed AT-SPI owner thread was left running.");
        File.WriteAllText(output, JsonSerializer.Serialize(new LinuxTransportResult(RuntimeInformation.FrameworkDescription, missingPango ? "missing-pango" : "unreachable-bus", failures, 5, before, after, 0, elapsed), LinuxProbeResultsJson.Default.LinuxTransportResult));
    }
}
