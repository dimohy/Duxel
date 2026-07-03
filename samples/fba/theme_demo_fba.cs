// FBA: Windows 11 modern theme demo — loads .duxel-theme and applies a compiled design
#:property TargetFramework=net10.0
#:property OutputType=WinExe
#:property OptimizationPreference=Size
#:property InvariantGlobalization=true
#:property DebuggerSupport=false
#:property EventSourceSupport=false
#:property MetricsSupport=false
#:property MetadataUpdaterSupport=false
#:property StackTraceSupport=false
#:property UseSystemResourceKeys=true
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

// Load theme from .duxel-theme file
var themeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
var themeAsset = DuxelWindowsApp.GetSystemColorScheme() is UiSystemColorScheme.Dark
    ? "windows11-modern-dark.duxel-theme"
    : "windows11-modern.duxel-theme";
var themeFile = Path.Combine(themeDir, "assets", themeAsset);
if (!File.Exists(themeFile))
{
    throw new FileNotFoundException("Theme asset was not deployed.", themeFile);
}

var themeText = File.ReadAllText(themeFile);
var themeDef = UiThemeParser.Parse(themeText);
var design = UiThemeCompiler.ApplyDesign(themeDef);
var screen = CreateThemeDemoScreen(themeDef.Name);

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = $"Duxel Theme Demo — {themeDef.Name}",
        Width = 800,
        Height = 640,
        VSync = true
    },
    Design = design,
    Screen = screen
});

static UiScreen CreateThemeDemoScreen(string themeName)
{
    var checkA = true;
    var checkB = false;
    var clickCount = 0;
    var statusMessage = "Ready";
    var inputText = "Hello Windows 11";
    var slider = 0.6f;
    var drag = 42f;
    var combo = 0;
    var listIndex = 0;
    var comboItems = new[] { "Fluent", "Mica", "Acrylic", "Compact" };
    var listItems = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot" };

    return DuxelView.Screen(
        DuxelView.Windows.Window(
            $"Theme: {themeName}",
            DuxelView.Layout.Column(
                DuxelView.Custom(ui =>
                {
                    ui.PushFontSize(28f);
                    ui.Text($"{themeName} Design");
                    ui.PopFontSize();
                    ui.Text($"Last action: {statusMessage}");
                    ui.Separator();
                }),
                DuxelView.Text.Block("Actions"),
                DuxelView.Layout.Row(
                    DuxelView.Controls.Button("Primary Action", () =>
                    {
                        clickCount++;
                        statusMessage = $"Primary clicked ({clickCount})";
                    }),
                    DuxelView.Controls.Button("Secondary", () => statusMessage = "Secondary clicked")),
                DuxelView.Layout.Spacer(8f),
                DuxelView.Text.Block("Toggles"),
                DuxelView.Controls.Checkbox("Use Windows 11 spacing", () => checkA, value =>
                {
                    checkA = value;
                    statusMessage = $"Windows 11 spacing: {(value ? "ON" : "OFF")}";
                }),
                DuxelView.Controls.Checkbox("Show focus styling", () => checkB, value =>
                {
                    checkB = value;
                    statusMessage = $"Focus styling: {(value ? "ON" : "OFF")}";
                }),
                DuxelView.Layout.Spacer(8f),
                DuxelView.Custom(ui =>
                {
                    ui.SeparatorText("Immediate Extensions");
                    ui.InputText("Name", ref inputText, 128);
                    ui.SliderFloat("Accent strength", ref slider, 0f, 1f);
                    ui.DragFloat("Value", ref drag, 0.5f, 0f, 100f);
                    ui.ProgressBar(slider, new UiVector2(0f, 0f), $"{slider:P0}");
                    ui.Combo(ref combo, comboItems, 4, "Style");
                    ui.ListBox(ref listIndex, listItems, 4, "Channels");
                    if (ui.Button("Hover Me"))
                    {
                        statusMessage = "Hover button clicked";
                    }
                    if (ui.IsItemHovered())
                    {
                        ui.SetTooltip("Tooltip colors come from the compiled design theme.");
                    }
                })),
            new UiWindowOptions(
                Position: new UiVector2(16f, 16f),
                Size: new UiVector2(768f, 592f))));
}
