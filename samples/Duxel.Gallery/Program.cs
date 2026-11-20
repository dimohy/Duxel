using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;
using Duxel.Gallery;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);
if (args.Length != 0 && (args.Length != 2 || args[0] is not ("--verify" or "--verify-live-holidays")))
    throw new ArgumentException("Use no arguments, --verify <fresh output directory> or --verify-live-holidays <fresh output directory>.");
var session = new DuxelAppSession();
using var screen = new ObserverScreen(ObserverServices.CreateWindows(), session);
var verification = args.Length == 2
    ? new ObserverPresentationVerificationScreen(screen, session, args[1], liveHoliday : args[0] == "--verify-live-holidays") : null;
var options = DuxelApp.Options<ObserverDesign>(verification is null ? screen : verification,
    title: "Duxel Gallery", width: 1280, height: 900, vsync: false);
try
{
    DuxelWindowsApp.Run(options with
    {
        Window = options.Window with
        {
            MinWidth = 1120,
            MinHeight = 720,
            IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "duxel.ico")
        },
        Frame = options.Frame with
        {
            EnableIdleFrameSkip = true,
            UseEventDrivenPlatformLoop = true,
            IsAnimationActiveProvider = () => !screen.UpdateOnlyOnChange
        },
        Debug = new()
        {
            LogStartupTimings = true
        }
    }, session);
    verification?.Complete();
}
catch (Exception error)
{
    verification?.Fail(error);
    throw;
}

public readonly struct ObserverDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}
