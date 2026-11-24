#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DefineConstants=$(DefineConstants);DUXEL_$(DuxelHost)
#:package Duxel.$(DuxelHost).App@0.4.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Duxel.App;
using Duxel.Core;
#if DUXEL_Windows
using Duxel.Windows.App;
#elif DUXEL_Linux
using Duxel.Linux.App;
#endif

const int ItemCount = 100_000;
const float RowHeight = 36;
var selected = new UiState<int>(0);
var id = new UiState<string>("");
var status = new UiState<string>("Enter an item number to jump to its row.");
var rows = new Rows(selected);
var list = Dux.VirtualList(rows, ItemCount, new UiVirtualListOptions(ItemExtent: RowHeight, Height: 320, Overscan: 3));
list.AccessibleName = "Inventory";
var root = Dux.Column(new UiFlexOptions(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    Dux.Row(new(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Fluent.Text(Text.Title2, "Inventory"), Fluent.Badge("100,000 items")),
    Dux.Row(new(Spacing: 8, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Fluent.Input("Item number", id.Binding, 8, 170), Fluent.Button("Find item", Find),
        Fluent.Button("First", () => Jump(0)), Fluent.Button("Last", () => Jump(ItemCount - 1))),
    Fluent.Text(Text.Caption1, () => status.Value),
    Fluent.Divider(),
    list,
    Fluent.Card("Selected item", Fluent.Text(Text.Body1, () => Rows.Label(selected.Value))),
    Fluent.Text(Text.Caption1, () => $"{list.RealizedCount:N0} row widgets for {ItemCount:N0} items"));
Run(Dux.ScrollView(Dux.SizedBox(root, width: 606), new(UiScrollAxis.Both)), "Duxel Inventory", 684, 616);

void Find()
{
    if (int.TryParse(id.Value, out var number) && number is >= 1 and <= ItemCount)
        Jump(number - 1);
    else
        status.Value = "Enter an item number from 1 to 100,000.";
}

void Jump(int index)
{
    selected.Value = index;
    list.Offset = index * (double)RowHeight;
    status.Value = $"Showing item {index + 1:N0}.";
}

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

sealed class Rows(UiState<int> selected) : IUiVirtualListSource
{
    private readonly ConditionalWeakTable<UiWidget, UiState<int>> _rowIndex = new();

    public string GetKey(int index) => $"inventory-{index}";

    public static string Label(int index) => $"INV-{index + 1:D6}    ·    Aisle {(index % 48) + 1:D2}    ·    Stock {(index * 37) % 220}";

    public UiWidget Create(int index)
    {
        var position = new UiState<int>(index);
        var row = Dux.Row(new(Spacing: 8, CrossAxisAlignment: UiCrossAxisAlignment.Center),
            Dux.SizedBox(Fluent.Text(Text.Body1, () => Label(position.Value)), width: 394),
            Fluent.Button("Select", () => selected.Value = position.Value));
        _rowIndex.Add(row, position);
        return row;
    }

    public void Bind(UiWidget widget, int index)
    {
        if (!_rowIndex.TryGetValue(widget, out var position))
            throw new InvalidOperationException("The row was not created by this source.");
        position.Value = index;
    }
}
