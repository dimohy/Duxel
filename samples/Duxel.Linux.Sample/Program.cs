using Duxel.App;
using Duxel.Core;
using Duxel.Linux.App;
using Duxel.Linux.Sample;

if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("Run this sample on Linux/X11.");
if (args.Length != 2)
    throw new ArgumentException("Specify explicit installed Latin and Korean OpenType font paths.");
var primary = args[0];
var secondary = args[1];
// An explicit API example; these are source-font selections, not final glyph geometry.
using (var fontMetadata = new Duxel.Platform.Linux.LinuxPlatformTextBackend())
{
    foreach (var run in fontMetadata.GetFontRuns(new(primary, secondary, "정A", 18), "en"))
        Console.WriteLine($"폰트 원문 [{run.StartUtf16}, {run.EndUtf16}) : {run.Font.FamilyName ?? "이 언어의 이름 없음"} / 굵기 : {run.Font.Weight?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "정보 없음"}");
}
var session = new DuxelAppSession();
using var screen = new LinuxDemoScreen(session);
DuxelLinuxApp.Run(DuxelApp.Options(screen, "Duxel Linux sample", LinuxDemoScreen.WindowWidth, LinuxDemoScreen.WindowHeight, vsync: false) with
{
    Font = new()
    {
        PrimaryFontPath = primary,
        SecondaryFontPath = secondary,
        // This compact demo explicitly chooses the supplied files for each role.
        Typography = new(primary, primary, primary, secondary, secondary, secondary)
        {
            Monospace = primary
        },
        FontSize = 18,
        FastStartup = false
    },
    Frame = new()
    {
        UseEventDrivenPlatformLoop = true
    }
}, session);
