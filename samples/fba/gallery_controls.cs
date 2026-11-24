#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DefineConstants=$(DefineConstants);DUXEL_$(DuxelHost)
#:package Duxel.$(DuxelHost).App@0.4.0

using Duxel.App;
using Duxel.Core;
#if DUXEL_Windows
using Duxel.Windows.App;
#elif DUXEL_Linux
using Duxel.Linux.App;
#endif

var enabled = new UiState<bool>(false);
var clicks = new UiState<int>(0);
var root = Dux.Column(new UiFlexOptions(Spacing: 12),
    Fluent.Text(Text.Title2, "Controls and state"),
    Fluent.Switch("Enable the counter", enabled.Binding),
    Fluent.Button("Count", () =>
    {
        if (enabled.Value)
            clicks.Value++;
    }),
    Fluent.Text(Text.Body1, () => enabled.Value ? $"Count: {clicks.Value}" : "The counter is off."));

var options = DuxelApp.Options<GalleryDesign>(Dux.App(root), title: "Duxel · Controls", width: 480, height: 320)
    with { Debug = new() { LogStartupTimings = true } };
#if DUXEL_Windows
if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("This build requires Windows.");
DuxelWindowsApp.Run(options);
#elif DUXEL_Linux
if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("This build requires Linux.");
if (args.Length != 1 || !File.Exists(args[0]))
    throw new ArgumentException("Provide an installed font file as the first argument.");
var font = args[0];
DuxelLinuxApp.Run(options with
{
    Font = new() { PrimaryFontPath = font, SecondaryFontPath = null, Typography = new(font, font, font) },
    Frame = new() { UseEventDrivenPlatformLoop = true }
});
#else
#error This example requires the Windows or Linux Duxel host.
#endif

readonly struct GalleryDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11;
}
