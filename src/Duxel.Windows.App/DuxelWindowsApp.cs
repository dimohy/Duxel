using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.Versioning;

namespace Duxel.Windows.App;

[SupportedOSPlatform("windows")]
public static class DuxelWindowsApp
{
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

        DuxelApp.Run(resolvedOptions);
    }
}
