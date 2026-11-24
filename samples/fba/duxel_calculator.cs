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
using System.Globalization;

const int MaxInputLength = 17;
var display = new UiState<string>("0");
var operation = new UiState<string>("Ready");
var memory = new UiState<decimal?>(null);
decimal? accumulator = null;
string? pending = null;
var replace = true;
string[] keys =
[
    "MC", "MR", "M+", "M-",
    "Clear", "+/-", "Back", "/",
    "7", "8", "9", "*",
    "4", "5", "6", "-",
    "1", "2", "3", "+",
    "0", ".", "%", "="
];

const float Width = 356;
var displayCard = Fluent.Card("Standard", Dux.Column(new UiFlexOptions(Spacing: 3, MainAxisSize: UiMainAxisSize.Min,
    CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    Fluent.Text(Text.Caption1, () => operation.Value),
    Dux.Align(Fluent.Text(Text.Display, () => display.Value), new UiAlignment(1, 0))),
    new UiCardOptions(Width: Width, Appearance: UiCardAppearance.FilledAlternative));
var root = Dux.Column(new UiFlexOptions(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
    Fluent.Text(Text.Title2, "Calculator"),
    displayCard,
    Dux.SizedBox(Dux.UniformGrid(new(4, CrossAxisSpacing: 7, MainAxisSpacing: 7, MainAxisExtent: 40),
        [.. keys.Select(Key)]), width: Width),
    Fluent.Text(Text.Caption1, () => memory.Value is { } value ? $"Memory  {Format(value)}" : "Memory is empty"));
Run(Dux.ScrollView(Dux.SizedBox(root, width: Width), new(UiScrollAxis.Both)), "Duxel Calculator", 428, 566);

UiWidget Key(string key) => Fluent.Button(key, () => Press(key));

void Press(string key)
{
    switch (key)
    {
        case "Clear":
            Reset("Ready");
            return;
        case "+" or "-" or "*" or "/" or "=":
            Calculate(key);
            return;
        case "MC":
            memory.Value = null;
            return;
        case "MR":
            if (memory.Value is { } recalled)
            {
                display.Value = Format(recalled);
                replace = false;
            }
            return;
        case "M+" or "M-":
            if (Read(out var current))
            {
                try { memory.Value = (memory.Value ?? 0) + (key == "M+" ? current : -current); }
                catch (OverflowException) { operation.Value = "Memory limit reached"; }
            }
            return;
        case "%":
            if (Read(out var percent))
            {
                display.Value = Format(percent / 100);
                replace = false;
            }
            return;
        case "Back":
            if (!replace)
            {
                var shorter = display.Value[..^1];
                display.Value = shorter is "" or "-" ? "0" : shorter;
            }
            return;
        case "+/-":
            if (replace && pending is not null)
                display.Value = "0";
            display.Value = display.Value.StartsWith('-') ? display.Value[1..] : "-" + display.Value;
            replace = false;
            return;
    }

    if (replace)
    {
        display.Value = "0";
        replace = false;
    }
    if (key == "." && !display.Value.Contains('.'))
        display.Value += ".";
    else if (char.IsAsciiDigit(key[0]) && display.Value.Length < MaxInputLength)
        display.Value = display.Value switch { "0" => key, "-0" => "-" + key, _ => display.Value + key };
}

void Calculate(string key)
{
    if (!Read(out var current))
        return;
    try
    {
        var result = (accumulator, pending) switch
        {
            (decimal left, "+") when !replace => left + current,
            (decimal left, "-") when !replace => left - current,
            (decimal left, "*") when !replace => left * current,
            (decimal left, "/") when !replace => left / current,
            _ => current
        };
        operation.Value = key == "="
            ? pending is null ? "Result" : $"{Format(accumulator!.Value)} {pending} {Format(current)} ="
            : $"{Format(result)} {key}";
        display.Value = Format(result);
        accumulator = key == "=" ? null : result;
        pending = key == "=" ? null : key;
        replace = true;
    }
    catch (Exception error) when (error is DivideByZeroException or OverflowException)
    {
        Reset(error is DivideByZeroException ? "Cannot divide by zero" : "Result is too large");
    }
}

void Reset(string status)
{
    display.Value = "0";
    operation.Value = status;
    accumulator = null;
    pending = null;
    replace = true;
}

bool Read(out decimal value) => decimal.TryParse(display.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
static string Format(decimal value) => value.ToString("G17", CultureInfo.InvariantCulture);

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
