using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Duxel.Windows.App;

[SupportedOSPlatform("windows")]
public static class DuxelWindowsApp
{
    [ModuleInitializer]
    internal static void RegisterPlatformRunner()
    {
        DuxelApp.RegisterRunner(Run);
    }

    public static void Run(DuxelAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resolvedOptions = options with
        {
            KeyRepeatSettingsProvider = options.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
            ImageDecoder = options.ImageDecoder ?? WindowsUiImageDecoder.Shared,
            ClipboardFactory = options.Clipboard is null && options.ClipboardFactory is null
                ? static platform => platform is IWin32PlatformBackend ? new WindowsClipboard() : null
                : options.ClipboardFactory,
            ImeHandlerFactory = options.ImeHandler is null && options.ImeHandlerFactory is null
                ? static platform => platform is IWin32PlatformBackend win32Platform
                    ? new WindowsImeHandler(win32Platform.WindowHandle)
                    : null
                : options.ImeHandlerFactory,
        };

        const int minWidth = 0;
        const int minHeight = 0;
        var initialWidth = resolvedOptions.Window.Width;
        var initialHeight = resolvedOptions.Window.Height;
        var enableDWriteText = ResolveDWriteTextEnabled(resolvedOptions.Renderer.EnableDWriteText);

        using var platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions(
            initialWidth,
            initialHeight,
            minWidth,
            minHeight,
            resolvedOptions.Window.Title,
            resolvedOptions.Window.VSync,
            enableDWriteText,
            resolvedOptions.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
            DuxelApp.RequestFrame
        ));

        DuxelApp.RunCore(resolvedOptions, platform);
    }

    private static bool ResolveDWriteTextEnabled(bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable("DUXEL_DIRECT_TEXT");
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        var normalized = value.Trim();
        if (normalized == "0")
        {
            return false;
        }

        return !normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("off", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("no", StringComparison.OrdinalIgnoreCase);
    }
}
