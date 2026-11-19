// FBA: nested wheel ownership, boundary propagation and caller clipping.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

var clipped = false;
var rows = 24;
var clicks = 0;
var options = DuxelApp.Options<ScrollRoutingDesign>(
    Dux.App(Dux.View(Render)),
    title : "Duxel / Nested Scroll Routing",
    width : 800,
    height : 620);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 580 },
    Debug = new() { LogStartupTimings = true }
});

void Render(UiImmediateContext ui)
{
    ui.EnableRootViewportContentLayout(contentPadding : 24);
    ui.SetCursorScreenPos(new(24, 24));
    if (ui.Button(clipped ? "Clip : On" : "Clip : Off"))
        clipped = !clipped;
    ui.SameLine();
    if (ui.Button($"Rows : {rows}"))
        rows = rows == 24 ? 6 : 24;
    ui.SameLine();
    ui.Text($"Clicks : {clicks}");
    ui.Text("Wheel over the inner list. At its boundary, the outer view scrolls.");
    ui.Text("Horizontal wheel uses the same ownership rule. Toggle Clip to restrict input.");
    ui.SetCursorScreenPos(new(24, 130));
    ui.PushClipRect(clipped ? new(24, 130, 350, 200) : new(24, 130, 680, 400), true);
    try
    {
        ui.BeginChild("outer-scroll", new(660, 380), border : true);
        ui.Text("Outer viewport");
        ui.BeginChild("inner-scroll", new(300, 180), border : true);
        for (var row = 0; row < rows; row++)
        {
            ui.PushID(row);
            if (ui.Button($"Inner row : {row + 1}"))
                clicks++;
            ui.PopID();
        }
        ui.Dummy(new(560, 20));
        ui.EndChild();
        for (var row = 0; row < rows; row++)
            ui.Text($"Outer row : {row + 1}");
        ui.Dummy(new(900, 20));
        ui.EndChild();
    }
    finally
    {
        ui.PopClipRect();
    }
}

public readonly struct ScrollRoutingDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}
