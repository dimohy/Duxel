using Duxel.Core;
using Duxel.Platform.Linux;

internal static class PresentationTestPlatform
{
    private static string[]? _fonts;
    internal static IReadOnlyList<string> Fonts => _fonts ?? throw new InvalidOperationException("Configure explicit Linux graphics fonts first.");

    internal static void SetFonts(string[] fonts)
    {
        if (fonts.Length != 5 || fonts.Any(path => !File.Exists(path)))
            throw new FileNotFoundException("All five explicit Linux graphics validation fonts must exist before opening a window.");
        _fonts = fonts;
        using var backend = new LinuxPlatformTextBackend();
        if (!backend.TryRasterize(new(fonts[0], fonts[2], "FPS 0", 16), out var raster))
            throw new InvalidOperationException("Explicit native font preflight did not rasterize.");
        using var pixels = raster.PixelOwner;
        var ink = 0;
        for (var i = 3; i < raster.RgbaPixels.Length; i += 4)
            if (raster.RgbaPixels.Span[i] > 100)
                ink++;
        Console.WriteLine($"native-font-preflight={raster.Width}x{raster.Height},ink={ink}");
        if (ink <= 30)
            throw new InvalidOperationException("Explicit native16px font preflight has insufficient visible ink.");
    }

    internal static IPlatformBackend Create() => Create(320, 240);

    internal static IPlatformBackend Create(int width, int height) => new LinuxX11PlatformBackend(new(width, height,
        "Duxel owned Linux presentation pixel fixture")
    {
        CenterOnScreen = false
    });

    internal static void ConfigureFonts(UiContext context, IPlatformBackend platform)
    {
        context.SetPlatformTextBackend(platform.TextBackend ?? throw new InvalidOperationException("Linux graphics validation requires native text."));
        ConfigureFonts(context);
    }

    internal static void ConfigureFonts(UiContext context)
    {
        var fonts = _fonts ?? throw new InvalidOperationException("Configure explicit Linux graphics fonts first.");
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(fonts[0]);
        context.SetDirectTextSecondaryFontPath(fonts[2]);
        context.SetTypographyFonts(new(fonts[0], fonts[1], fonts[1], fonts[2], fonts[3], fonts[3])
        {
            Monospace = fonts[4]
        });
    }
}
