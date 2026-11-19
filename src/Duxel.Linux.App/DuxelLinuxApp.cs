using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Linux;

namespace Duxel.Linux.App;

/// <summary>Runs the shared application session with an owned native X11 platform.</summary>
[SupportedOSPlatform("linux")]
public static class DuxelLinuxApp
{
    [ModuleInitializer]
    internal static void RegisterPlatformRunner()
    {
        if (OperatingSystem.IsLinux())
            DuxelApp.RegisterRunner(Run);
    }

    public static void Run(DuxelAppOptions options) => Run(options, DuxelApp.PrimarySession);

    public static void Run(IUiView root, DuxelFontOptions fonts, string title = "Duxel", int width = 1280, int height = 720, bool vsync = false)
        => Run(DuxelApp.Options(root, title, width, height, vsync) with
        {
            Font = fonts,
            Frame = new()
            {
                UseEventDrivenPlatformLoop = true
            }
        });

    public static void Run(DuxelAppOptions options, DuxelAppSession session)
        => Run(options, session, new LinuxAtSpiOptions());

    public static void Run(DuxelAppOptions options, DuxelAppSession session, LinuxAtSpiOptions accessibility)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("DuxelLinuxApp requires Linux/X11.");
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(accessibility);
        ValidateFonts(options.Font);
        var window = options.Window;
        if (window.UseDuxelTitleBar || window.TitleBarMode is not (DuxelTitleBarMode.Default or DuxelTitleBarMode.System)
            || !window.IntegrateSystemChrome
            || window.OwnerWindowHandle != 0 || window.CenterOnOwner || window.Tray.Enabled || window.IconPath is not null || !window.IconData.IsEmpty
            || window.MinWidth != 0 || window.MinHeight != 0 || !window.Resizable
            || !window.ShowMinimizeButton || !window.ShowMaximizeButton)
            throw new NotSupportedException("Custom chrome, ownership, icons, tray and window sizing policies are not yet implemented by the X11 runner.");
        using var platform = new LinuxX11PlatformBackend(new(window.Width, window.Height, window.Title)
        {
            FrameInvalidated = session.RequestFrame,
            Accessibility = accessibility,
            CenterOnScreen = window.CenterOnScreen
        });
        using var clipboard = options.Clipboard is null && options.ClipboardFactory is null
            ? new LinuxX11Clipboard() : null;
        window.WindowCreated?.Invoke((nint)platform.WindowHandle);
        session.RunCore(options with
        {
            Clipboard = clipboard ?? options.Clipboard,
            ImageDecoder = options.ImageDecoder ?? LinuxUiImageDecoder.Shared
        }, platform);
    }

    private static void ValidateFonts(DuxelFontOptions font)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(font.Typography);
        var typography = font.Typography;
        ArgumentException.ThrowIfNullOrWhiteSpace(font.PrimaryFontPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(typography.Regular);
        ArgumentException.ThrowIfNullOrWhiteSpace(typography.Semibold);
        ArgumentException.ThrowIfNullOrWhiteSpace(typography.Bold);
        string?[] paths = [font.PrimaryFontPath, font.SecondaryFontPath, typography.Regular, typography.Semibold,
            typography.Bold, typography.SecondaryRegular, typography.SecondarySemibold, typography.SecondaryBold, typography.Monospace];
        foreach (var path in paths)
        {
            if (path is not null && !File.Exists(path))
                throw new FileNotFoundException("Select an explicit installed Linux font for every configured font role.", path);
        }
    }
}
