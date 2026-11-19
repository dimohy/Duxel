// FBA: actual parent allocation drives the compiled control layout, paint and input.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new ConstrainedPage();
var options = DuxelApp.Options<ConstrainedDesign>(Dux.App(page),
    title : "Duxel / Constrained Fluent Controls", width : 760, height : 440);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 640, MinHeight = 420 },
    Debug = new() { LogStartupTimings = true }
});
public readonly struct ConstrainedDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class ConstrainedPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var value = 50f;
        var reset = Fluent.Button("Reset (50)");
        var binding = new UiBinding<float>(() => value, next =>
        {
            value = next;
            reset.Label = $"Reset ({next:0})";
        });
        reset.Activated = () =>
        {
            value = 50;
            reset.Label = "Reset (50)";
        };
        var slider = Fluent.Slider("Level", binding);
        var sliderBox = Dux.SizedBox(slider, 80, 64);
        var resize = Fluent.Button("Width : 80");
        resize.Activated = () =>
        {
            var width = sliderBox.Options.Width == 80 ? 240 : 80;
            sliderBox.Options = new(width, 64);
            resize.Label = $"Width : {width}";
        };
        var enabled = new UiState<bool>(false);
        var selected = new UiState<bool?>(false);
        var controls = Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.SizedBox(Fluent.Switch("Enabled", enabled.Binding), 240, 64),
            Dux.SizedBox(Fluent.Checkbox("Selected", selected.Binding), 240, 64));
        var actions = Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), reset, resize);
        return Dux.Padding(new UiColumn(
        [
            Fluent.Title2("Constrained Fluent controls"),
            Fluent.Body1("Slider : drag across the full height; use Tab, arrows, Home / End."),
            sliderBox, actions, controls,
            Fluent.Caption1("Switch / Checkbox : click anywhere in their allocated row.")
        ], new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min)), new(24));
    });
}
