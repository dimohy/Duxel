using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ControlFontFixture
{
    internal static void ConfigureEmoji(UiContext context, bool korean = false)
    {
        Configure(context, korean : korean);
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var emoji = Path.Combine(fonts, "seguiemj.ttf");
        if (!File.Exists(emoji))
            throw new FileNotFoundException("Supplementary-character fixtures require Segoe UI Emoji.", emoji);
        var regular = Path.Combine(fonts, korean ? "malgun.ttf" : "segoeui.ttf");
        var semibold = Path.Combine(fonts, korean ? "malgunbd.ttf" : "seguisb.ttf");
        var bold = Path.Combine(fonts, korean ? "malgunbd.ttf" : "segoeuib.ttf");
        context.SetDirectTextPrimaryFontPath(regular);
        context.SetDirectTextSecondaryFontPath(emoji);
        context.SetTypographyFonts(new(regular, semibold, bold, emoji, emoji, emoji)
        {
            Monospace = Path.Combine(fonts, "consola.ttf")
        });
    }

    internal static void Configure(UiContext context, bool backend = true, bool korean = true)
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular = Path.Combine(directory, "segoeui.ttf");
        var semibold = Path.Combine(directory, "seguisb.ttf");
        var bold = Path.Combine(directory, "segoeuib.ttf");
        if (!File.Exists(regular) || !File.Exists(semibold) || !File.Exists(bold))
            throw new FileNotFoundException("Control font fixtures require Segoe UI regular,semibold and bold.");
        var secondary = korean ? Path.Combine(directory, "malgun.ttf") : null;
        var secondaryBold = korean ? Path.Combine(directory, "malgunbd.ttf") : null;
        if (korean && (!File.Exists(secondary) || !File.Exists(secondaryBold)))
            throw new FileNotFoundException("Korean control font fixtures require Malgun regular and bold, matching the application defaults.");
        if (backend)
        {
            context.SetPlatformTextBackend(WindowsPlatformTextBackend.Instance);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(regular);
            context.SetDirectTextSecondaryFontPath(secondary);
        }
        var monospace = Path.Combine(directory, "consola.ttf");
        if (!File.Exists(monospace))
            throw new FileNotFoundException("Code typography fixtures require Consolas.");
        context.SetTypographyFonts(new(regular, semibold, bold, secondary, secondaryBold, secondaryBold)
        {
            Monospace = monospace
        });
    }
}
