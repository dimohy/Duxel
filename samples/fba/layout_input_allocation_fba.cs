// FBA: parent allocation, real editable text and owned prefix/suffix controls.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new InputAllocationPage();
var options = DuxelApp.Options<InputAllocationDesign>(Dux.App(page),
    title : "Duxel / Allocated Editable Input", width : 760, height : 440);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 640, MinHeight = 420 },
    Debug = new() { LogStartupTimings = true }
});
public readonly struct InputAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class InputAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var text = new UiState<string>("Edit me");
        var input = Fluent.Input("Editable input", text.Binding, 128);
        var help = Fluent.Button("?");
        help.Size = new(24, 24);
        help.Activated = () => text.Value = "Tab : help, input, clear";
        var clear = Fluent.Button("Clear", () => text.Value = string.Empty);
        clear.Size = new(48, 24);
        input.SetSlots(new("help", help, new(24, 24)), new("clear", clear, new(48, 24)));
        var box = Dux.SizedBox(input, 280, 64);
        var width = Fluent.Button("Width : 280");
        width.Activated = () =>
        {
            var next = box.Options.Width == 280 ? 160 : 280;
            box.Options = box.Options with { Width = next };
            width.Label = $"Width : {next}";
        };
        var height = Fluent.Button("Height : 64");
        height.Activated = () =>
        {
            var next = box.Options.Height == 64 ? 48 : 64;
            box.Options = box.Options with { Height = next };
            height.Label = $"Height : {next}";
        };
        var reset = Fluent.Button("Reset", () => text.Value = "Edit me");
        return Dux.Padding(new UiColumn(
        [
            Fluent.Title2("Constrained editable input"),
            Fluent.Body1("Click near the bottom of the field, type, resize, then Ctrl+Z."),
            box,
            Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), width, height, reset),
            Fluent.Caption1("Owned 24px help / 48px clear controls keep size, focus and callbacks.")
        ], new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min)), new(24));
    });
}
