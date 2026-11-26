#:property TargetFramework=net11.0
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DefineConstants=$(DefineConstants);DUXEL_$(DuxelHost)
#:project ../../src/Duxel.Core/Duxel.Core.csproj
#:project ../../src/Duxel.App/Duxel.App.csproj
#:project ../../src/Duxel.$(DuxelHost).App/Duxel.$(DuxelHost).App.csproj

using Duxel.App;
using Duxel.Core;
#if DUXEL_Windows
using Duxel.Windows.App;
#elif DUXEL_Linux
using Duxel.Linux.App;
#endif

var content = Dux.Padding(Fluent.Text(Text.Title2, "Hello, Duxel"), new UiEdgeInsets(24));
var options = DuxelApp.Options(Dux.App(content), title: "Duxel Hello", width: 360, height: 180)
    with { Debug = new() { LogStartupTimings = true } };
#if DUXEL_Windows
if (OperatingSystem.IsWindows())
    DuxelWindowsApp.Run(options);
else
    throw new PlatformNotSupportedException();
#elif DUXEL_Linux
if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException();
if (args.Length != 1 || !File.Exists(args[0]))
    throw new ArgumentException("Pass an installed font file as the first argument.");

var font = args[0];
DuxelLinuxApp.Run(options with
{
    Font = new() { PrimaryFontPath = font, SecondaryFontPath = null, Typography = new(font, font, font) }
});
#else
#error This example requires the Windows or Linux Duxel host.
#endif
