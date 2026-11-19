using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

internal static unsafe partial class AtSpiProbe
{
    // Public libatspi/GObject LP64 prefixes; see the saved upstream headers.
    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectPrefix
    {
        internal nint Class;
        internal uint References;
        internal nint Data, Application, Path;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ApplicationPrefix
    {
        internal nint Class;
        internal uint References;
        internal nint Data, Objects, BusName;
    }

    private static (int ExitCode, string Output, string Error) GDbus(params string[] arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/gdbus")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The independent gdbus client did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(3000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("The independent gdbus request exceeded its bound.");
        }
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    private static bool RejectNumericWrite(nint accessible, double value, string reason = "InvalidArgument")
    {
        // libatspi's setter unrefs a null reply on a D-Bus error. Exercise error
        // replies with an independent CLI instead; never weaken product errors.
        var addressReply = GDbus("call", "--session", "--dest", "org.a11y.Bus", "--object-path", "/org/a11y/bus", "--method", "org.a11y.Bus.GetAddress");
        if (addressReply.ExitCode != 0)
            throw new InvalidOperationException("The independent accessibility-bus lookup failed: " + addressReply.Error);
        var address = Regex.Match(addressReply.Output.Trim(), @"^\('([^']+)',\)$");
        if (!address.Success)
            throw new InvalidDataException("The accessibility-bus lookup returned an unexpected tuple.");
        var obj = *(ObjectPrefix*)accessible;
        if (obj.Application == 0 || obj.Path == 0)
            throw new InvalidDataException("The native accessible has no live D-Bus reference.");
        var application = *(ApplicationPrefix*)obj.Application;
        var bus = Marshal.PtrToStringUTF8(application.BusName) ?? throw new InvalidDataException("Missing native bus name.");
        var path = Marshal.PtrToStringUTF8(obj.Path)!;
        var literal = double.IsNaN(value) ? "nan" : double.IsPositiveInfinity(value) ? "inf" : value.ToString("F1", CultureInfo.InvariantCulture);
        var reply = GDbus("call", "--address", address.Groups[1].Value, "--dest", bus, "--object-path", path,
            "--method", "org.freedesktop.DBus.Properties.Set", "org.a11y.atspi.Value", "CurrentValue", "<" + literal + ">");
        if (reply.ExitCode == 0)
            return false;
        if (!reply.Error.Contains("Numeric value request rejected: " + reason, StringComparison.Ordinal))
            throw new InvalidOperationException("The negative numeric request failed for an unexpected reason: " + reply.Error);
        return true;
    }
}
