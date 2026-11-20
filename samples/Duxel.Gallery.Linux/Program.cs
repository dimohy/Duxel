using Duxel.App;
using Duxel.Core;
using Duxel.Gallery;
using Duxel.Linux.App;
using Duxel.Platform.Linux;
using Duxel.Svg;

if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Run the Linux Gallery on Linux/X11.");
var (fontPaths, verifyMode, verifyOutput) = GalleryLinuxArguments.Parse(args);

var fonts = new DuxelFontOptions
{
    PrimaryFontPath = fontPaths[0],
    SecondaryFontPath = fontPaths[2],
    Typography = new(fontPaths[0], fontPaths[1], fontPaths[1], fontPaths[2], fontPaths[3], fontPaths[3]) { Monospace = fontPaths[4] },
    FontSize = 16,
    FastStartup = false
};
var services = new ObserverServices(LinuxUiImageDecoder.Shared, new SvgFontOptions("Observer UI",
[
    new("Observer UI", fontPaths[0]), new("Observer UI", fontPaths[1]),
    new("Observer Korean", fontPaths[2]), new("Observer Korean", fontPaths[3])
]), fonts.Typography);
var session = new DuxelAppSession();
using var observer = new ObserverScreen(services, session);
if (verifyMode == "--verify-presentation-reuse")
{
    using var platform = new LinuxX11PlatformBackend(new(640, 360, "Duxel / owned native presentation probe")
    {
        FrameInvalidated = session.RequestFrame,
        Accessibility = new()
    });
    var probe = new ObserverLinuxPresentationVerificationScreen(platform, session, verifyOutput!);
    var probeOptions = DuxelApp.Options<ObserverLinuxDesign>(probe,
        title: "Duxel / owned native presentation probe", width: 640, height: 360, vsync: false);
    session.RunCore(probeOptions with
    {
        Font = fonts,
        ImageDecoder = LinuxUiImageDecoder.Shared,
        Frame = probeOptions.Frame with
        {
            UseEventDrivenPlatformLoop = true
        }
    }, platform);
    probe.Complete();
    return;
}
if (verifyMode is "--verify-idle-pointer" or "--verify-stream-pointer")
{
    using var platform = new LinuxX11PlatformBackend(new(1280, 900, "Duxel / owned finite idle pointer probe")
    {
        FrameInvalidated = session.RequestFrame,
        Accessibility = new()
    });
    using var clipboard = new LinuxX11Clipboard();
    using var idle = new ObserverLinuxIdleVerificationScreen(observer, platform, session, verifyOutput!, verifyMode == "--verify-stream-pointer");
    var idleOptions = DuxelApp.Options<ObserverLinuxDesign>(idle, title: "Duxel / owned finite idle pointer probe", width: 1280, height: 900, vsync: false);
    session.RunCore(idleOptions with
    {
        Font = fonts,
        Clipboard = clipboard,
        ImageDecoder = LinuxUiImageDecoder.Shared,
        Frame = idleOptions.Frame with
        {
            UseEventDrivenPlatformLoop = true
        }
    }, platform);
    idle.Complete();
    return;
}
if (verifyMode == "--verify-pointer-performance")
{
    using var platform = new LinuxX11PlatformBackend(new(1280, 900, "Duxel / owned finite native pointer probe")
    {
        FrameInvalidated = session.RequestFrame,
        Accessibility = new()
    });
    using var clipboard = new LinuxX11Clipboard();
    using var pointer = new ObserverLinuxPointerVerificationScreen(observer, platform, session, verifyOutput!);
    var probeOptions = DuxelApp.Options<ObserverLinuxDesign>(pointer, title: "Duxel / owned finite native pointer probe", width: 1280, height: 900, vsync: false);
    try
    {
        session.RunCore(probeOptions with
        {
            Font = fonts,
            Clipboard = clipboard,
            ImageDecoder = LinuxUiImageDecoder.Shared,
            Frame = probeOptions.Frame with
            {
                UseEventDrivenPlatformLoop = true
            }
        }, platform);
        pointer.Complete();
    }
    catch (Exception error) { pointer.Fail(error); throw; }
    return;
}
var sliderVerification = verifyMode == "--verify-slider-performance"
    ? new ObserverLinuxSliderVerificationScreen(observer, session, verifyOutput!) : null;
var verification = verifyMode is not null && sliderVerification is null
    ? new ObserverPresentationVerificationScreen(observer, session, verifyOutput!,
        gpuCamera : verifyMode is "--verify-gpu-camera" or "--verify-gpu-camera-reuse", liveHoliday : verifyMode == "--verify-live-holidays",
        highResolutionCamera : verifyMode != "--verify-gpu-camera-reuse") : null;
var options = DuxelApp.Options<ObserverLinuxDesign>(sliderVerification ?? verification ?? (UiScreen)observer,
    title: "Duxel Gallery (Linux)", width: 1280, height: 900, vsync: false);
try
{
    DuxelLinuxApp.Run(options with
    {
        Font = fonts,
        Frame = options.Frame with
        {
            EnableIdleFrameSkip = sliderVerification is null,
            UseEventDrivenPlatformLoop = true,
            IsAnimationActiveProvider = () => sliderVerification is not null || !observer.UpdateOnlyOnChange
        }
    }, session);
    verification?.Complete();
    sliderVerification?.Complete();
}
catch (Exception error)
{
    verification?.Fail(error);
    sliderVerification?.Fail(error);
    throw;
}

public readonly struct ObserverLinuxDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}
