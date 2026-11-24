#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DefineConstants=$(DefineConstants);DUXEL_$(DuxelHost)
#:package Duxel.$(DuxelHost).App@0.4.0

using System.Diagnostics;
using Duxel.App;
using Duxel.Core;
#if DUXEL_Windows
using Duxel.Windows.App;
#elif DUXEL_Linux
using Duxel.Linux.App;
#endif

var running = new UiState<bool>(true);
var count = new UiState<float>(2000);
var time = Stopwatch.StartNew();
var renderTimeMs = new UiState<float>(0);
var lastSample = 0L;
var random = new Random(23);
var seeds = Enumerable.Range(0, 5000)
    .Select(_ => (X: random.NextSingle(), Y: random.NextSingle(), Speed: .06f + .4f * random.NextSingle())).ToArray();
UiColor[] colors = [new(23, 111, 190), new(15, 143, 117), new(133, 84, 179), new(225, 115, 45)];
var viewport = new UiViewHost(new UiView(ui =>
{
    var origin = ui.GetCursorScreenPos();
    var size = ui.GetContentRegionAvail();
    var clip = new UiRect(origin.X, origin.Y, size.X, size.Y);
    var draw = ui.GetWindowDrawList();
    draw.AddRectFilled(clip, ui.GetColorU32(UiStyleColor.FrameBg), ui.WhiteTextureId, clip);
    var seconds = (float)time.Elapsed.TotalSeconds;
    if (time.ElapsedMilliseconds - lastSample >= 250)
    {
        renderTimeMs.Value = ui.GetRenderTimeMs();
        lastSample = time.ElapsedMilliseconds;
    }
    for (var index = 0; index < (int)count.Value; index++)
    {
        var seed = seeds[index];
        var x = origin.X + size.X * ((seed.X + seconds * seed.Speed) % 1);
        var y = origin.Y + size.Y * seed.Y;
        draw.AddCircleFilled(new(x, y), 2.5f, colors[index % colors.Length], ui.WhiteTextureId, clip);
    }
}));

var root = Dux.Column(new UiFlexOptions(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    Dux.Row(new(Spacing: 10, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Fluent.Text(Text.Title2, "Motion canvas"), Fluent.Badge("Vulkan")),
    Dux.Row(new(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Fluent.Switch("Animate", new UiBinding<bool>(() => running.Value, value =>
        {
            running.Value = value;
            if (value) time.Start();
            else time.Stop();
        })),
        Dux.SizedBox(Fluent.Slider("Shapes", count.Binding, 500, 5000, 100), width: 268)),
    Fluent.Text(Text.Caption1, () => $"{count.Value:N0} circles · previous render {renderTimeMs.Value:F2} ms"),
    Dux.SizedBox(viewport, height: 330));
Run(Dux.ScrollView(Dux.SizedBox(root, width: 688), new(UiScrollAxis.Both)), "Duxel Motion Canvas", 764, 500, () => running.Value);

static void Run(UiWidget root, string title, int width, int height, Func<bool>? animationActive = null)
{
    var options = DuxelApp.Options(Dux.App(root), title: title, width: width, height: height);
    options = options with
    {
        Debug = new() { LogStartupTimings = true },
        Frame = options.Frame with
        {
            UseEventDrivenPlatformLoop = true,
            EnableIdleFrameSkip = true,
            IsAnimationActiveProvider = animationActive
        }
    };
#if DUXEL_Windows
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("This build requires Windows.");
    DuxelWindowsApp.Run(options);
#elif DUXEL_Linux
    if (!OperatingSystem.IsLinux())
        throw new PlatformNotSupportedException("This build requires Linux.");
    using var match = Process.Start(new ProcessStartInfo("fc-match", ["-f", "%{file}", "sans-serif"])
    {
        RedirectStandardOutput = true,
        UseShellExecute = false
    }) ?? throw new InvalidOperationException("Could not find a desktop font.");
    var font = match.StandardOutput.ReadToEnd().Trim();
    match.WaitForExit();
    if (match.ExitCode != 0 || !File.Exists(font))
        throw new InvalidOperationException("Install Fontconfig and a sans-serif desktop font.");

    DuxelLinuxApp.Run(options with
    {
        Design = ReadDesktopDesign(),
        Font = new() { PrimaryFontPath = font, SecondaryFontPath = null, Typography = new(font, font, font) }
    });
#else
#error This example requires the Windows or Linux Duxel host.
#endif
}

#if DUXEL_Linux
static UiCompiledDesign ReadDesktopDesign()
{
    var request = new ProcessStartInfo("gdbus",
    [
        "call", "--session", "--timeout", "1",
        "--dest", "org.freedesktop.portal.Desktop",
        "--object-path", "/org/freedesktop/portal/desktop",
        "--method", "org.freedesktop.portal.Settings.Read",
        "org.freedesktop.appearance", "color-scheme"
    ])
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    try
    {
        using var portal = Process.Start(request)!;
        var errors = portal.StandardError.ReadToEndAsync();
        var setting = portal.StandardOutput.ReadToEnd();
        portal.WaitForExit();
        errors.GetAwaiter().GetResult();
        return portal.ExitCode == 0 && setting.Contains("<uint32 1>", StringComparison.Ordinal)
            ? UiCompiledDesign.Windows11Dark
            : UiCompiledDesign.Windows11;
    }
    catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 2)
    {
        // The Settings portal is optional in a minimal X11 session.
        return UiCompiledDesign.Windows11;
    }
}
#endif
