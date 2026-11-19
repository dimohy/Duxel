using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class PresentationTestPlatform
{
    internal static IPlatformBackend Create() => Create(320, 240);

    internal static WindowsPlatformBackend Create(int width, int height, bool orderedInput = false) => new(new()
    {
        Width = width,
        Height = height,
        Title = "Duxel owned presentation pixel fixture",
        VSync = false,
        EnableOrderedInput = orderedInput,
        KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
        TrayOptions = new()
    });

    internal static void ConfigureFonts(UiContext context, IPlatformBackend platform)
        => ConfigureFonts(context);

    internal static void ConfigureFonts(UiContext context) => ControlFontFixture.Configure(context);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
}
