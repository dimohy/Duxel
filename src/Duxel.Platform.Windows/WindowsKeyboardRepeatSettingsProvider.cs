using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed class WindowsKeyRepeatSettingsProvider : IKeyRepeatSettingsProvider
{
    private readonly UiKeyRepeatSettings _settings = Win32KeyboardRepeat.GetKeyboardRepeatTimings();

    public UiKeyRepeatSettings GetSettings() => _settings;
}

internal static partial class Win32KeyboardRepeat
{
    private const uint SpiGetKeyboardDelay = 0x0016;
    private const uint SpiGetKeyboardSpeed = 0x000A;

    internal static UiKeyRepeatSettings GetKeyboardRepeatTimings()
    {
        if (!SystemParametersInfoW(SpiGetKeyboardDelay, 0, out var delay, 0))
        {
            throw new InvalidOperationException($"SystemParametersInfoW(SPI_GETKEYBOARDDELAY) failed: {Marshal.GetLastPInvokeError()}");
        }

        if (!SystemParametersInfoW(SpiGetKeyboardSpeed, 0, out var speed, 0))
        {
            throw new InvalidOperationException($"SystemParametersInfoW(SPI_GETKEYBOARDSPEED) failed: {Marshal.GetLastPInvokeError()}");
        }

        var delaySeconds = (delay + 1) * 0.25;
        var cps = 2.5 + (speed / 31.0) * 27.5;
        var repeatIntervalSeconds = 1.0 / cps;
        return new UiKeyRepeatSettings(delaySeconds, repeatIntervalSeconds);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint uiAction, uint uiParam, out uint pvParam, uint fWinIni);
}

