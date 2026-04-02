// FBA: Theme file loading demo — loads .duxel-theme and applies at runtime
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
var themeFile = Path.Combine(themeDir, "assets", "cyberpunk.duxel-theme");
if (!File.Exists(themeFile))
{
    // Fallback: try relative path from working directory
    themeFile = Path.Combine("samples", "fba", "assets", "cyberpunk.duxel-theme");
}

var themeText = File.ReadAllText(themeFile);
var themeDef = UiThemeParser.Parse(themeText);
var theme = UiThemeCompiler.Apply(themeDef);

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = $"Duxel Theme Demo — {themeDef.Name}",
        Width = 960,
        Height = 640,
        VSync = true
    },
    Theme = theme,
    Screen = new ThemeDemoScreen(themeDef.Name)
});

public sealed class ThemeDemoScreen(string themeName) : UiScreen
{
    private bool _checkA = true;
    private bool _checkB;
    private int _radio;
    private float _slider = 0.6f;
    private float _drag = 42f;
    private int _combo;
    private readonly string[] _comboItems = ["Neon", "Vapor", "Retro", "Synth"];
    private int _listIndex;
    private readonly string[] _listItems = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot"];
    private string _inputText = "Hello Cyberpunk";
    private int _clickCount;
    private string _statusMessage = "";
    private readonly bool[] _selectables = [false, false, false];

    public override void Render(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + 16f, viewport.Pos.Y + 16f));
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - 32f, viewport.Size.Y - 32f));
        ui.BeginWindow($"Theme: {themeName}");

        // ── Header ──
        ui.PushFontSize(28f);
        ui.Text($"{themeName} Theme");
        ui.PopFontSize();
        ui.Separator();

        // ── Status ──
        if (_statusMessage.Length is not 0)
        {
            ui.Text($"Last action: {_statusMessage}");
            ui.Separator();
        }

        // ── Buttons ──
        ui.SeparatorText("Buttons");
        if (ui.Button("Primary Action"))
        {
            _clickCount++;
            _statusMessage = $"Primary clicked ({_clickCount})";
        }
        ui.SameLine();
        if (ui.SmallButton("Small"))
        {
            _statusMessage = "Small button clicked";
        }
        ui.SameLine();
        if (ui.ArrowButton("arrow_r", UiDir.Right))
        {
            _radio = (_radio + 1) % 3;
            _statusMessage = $"Radio → Option {(char)('A' + _radio)}";
        }

        // ── Checkbox / Radio ──
        ui.SeparatorText("Toggles");
        if (ui.Checkbox("Enable Neon Glow", ref _checkA))
            _statusMessage = $"Neon Glow: {(_checkA ? "ON" : "OFF")}";
        if (ui.Checkbox("Wireframe Mode", ref _checkB))
            _statusMessage = $"Wireframe: {(_checkB ? "ON" : "OFF")}";
        ui.RadioButton("Option A", ref _radio, 0);
        ui.SameLine();
        ui.RadioButton("Option B", ref _radio, 1);
        ui.SameLine();
        ui.RadioButton("Option C", ref _radio, 2);

        // ── Input ──
        ui.SeparatorText("Input");
        ui.InputText("Name", ref _inputText, 128);

        // ── Sliders / Drag ──
        ui.SeparatorText("Sliders & Drags");
        ui.SliderFloat("Intensity", ref _slider, 0f, 1f);
        ui.DragFloat("Value", ref _drag, 0.5f, 0f, 100f);
        ui.ProgressBar(_slider, new UiVector2(0f, 0f), $"{_slider:P0}");

        // ── Combo / ListBox ──
        ui.SeparatorText("Selection");
        ui.Combo(ref _combo, _comboItems, 4, "Style");
        ui.ListBox(ref _listIndex, _listItems, 4, "Channels");

        // ── Tree ──
        ui.SeparatorText("Tree & Collapsing");
        if (ui.CollapsingHeader("System Info", true))
        {
            if (ui.TreeNode("GPU"))
            {
                ui.Text("Vendor: Cyberdyne");
                ui.Text("VRAM: 16 GB");
                ui.TreePop();
            }
            if (ui.TreeNode("Display"))
            {
                ui.Text("Resolution: 3840×2160");
                ui.Text("Refresh: 144 Hz");
                ui.TreePop();
            }
        }

        // ── Tabs ──
        ui.SeparatorText("Tabs");
        if (ui.BeginTabBar("DemoTabs"))
        {
            if (ui.BeginTabItem("Overview"))
            {
                ui.Text("Theme overview panel.");
                ui.EndTabItem();
            }
            if (ui.BeginTabItem("Settings"))
            {
                ui.Text("Adjust theme parameters here.");
                ui.EndTabItem();
            }
            ui.EndTabBar();
        }

        // ── Selectable ──
        ui.SeparatorText("Selectable");
        for (var i = 0; i < 3; i++)
        {
            if (ui.Selectable($"Profile {i + 1}", ref _selectables[i]))
                _statusMessage = $"Profile {i + 1}: {(_selectables[i] ? "selected" : "deselected")}";
        }

        // ── Tooltip ──
        if (ui.Button("Hover Me"))
            _statusMessage = "Hover button clicked!";
        if (ui.IsItemHovered())
        {
            ui.SetTooltip("This tooltip uses TooltipBg & TooltipText tokens!");
        }

        // ── Menu ──
        if (ui.BeginMenuBar())
        {
            if (ui.BeginMenu("File"))
            {
                if (ui.MenuItem("New"))
                {
                    _inputText = "";
                    _clickCount = 0;
                    _statusMessage = "Reset — New file";
                }
                if (ui.MenuItem("Open"))
                    _statusMessage = "Open selected";
                if (ui.MenuItem("Save", selected: true))
                    _statusMessage = "Saved!";
                ui.EndMenu();
            }
            if (ui.BeginMenu("Edit"))
            {
                if (ui.MenuItem("Undo"))
                    _statusMessage = "Undo";
                ui.MenuItem("Redo", enabled: false);
                ui.EndMenu();
            }
            ui.EndMenuBar();
        }

        ui.EndWindow();
    }
}
