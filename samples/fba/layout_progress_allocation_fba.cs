// FBA: pure Progress measurement, live values and actual parent allocations.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new ProgressAllocationPage();
var options = DuxelApp.Options<ProgressAllocationDesign>(
    Dux.App(page),
    title : "Duxel / Allocated Progress",
    width : 800,
    height : 500);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 680, MinHeight = 460 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct ProgressAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class ProgressAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var value = new UiState<float>(25);
        var dynamicBar = Fluent.ProgressBar(() =>
        {
            var current = value.Value;
            return new(current, 100, Color : current >= 75 ? UiProgressColor.Success : UiProgressColor.Brand);
        });
        var dynamicBox = Dux.SizedBox(dynamicBar, 80, 64);
        var staticBar = Fluent.ProgressBar(.5f);
        staticBar.Shape = UiProgressShape.Square;
        staticBar.Thickness = UiProgressThickness.Large;
        staticBar.Size = new(220, 4);
        var staticBox = Dux.SizedBox(staticBar, 80, 64);
        UiSizedBox[] boxes = [dynamicBox, staticBox];

        var width = Fluent.Button("Width : 80");
        width.Activated = () =>
        {
            var next = dynamicBox.Options.Width == 80 ? 240 : 80;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Width = next };
            }
            width.Label = $"Width : {next}";
        };

        var height = Fluent.Button("Height : 64");
        height.Activated = () =>
        {
            var next = dynamicBox.Options.Height == 64 ? 16 : 64;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Height = next };
            }
            height.Label = $"Height : {next}";
        };

        return Dux.Padding(
            new UiColumn(
            [
                Fluent.Title2("Allocated progress"),
                Fluent.Body1("The parent sets the real bar size. The getter is read only when drawing."),
                Dux.Row(
                    new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
                    new UiColumn(
                        [Fluent.Caption1("Live value / rounded"), dynamicBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("Static 50% / square"), staticBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min))),
                Fluent.Caption1(() => "Value : " + value.Value.ToString("0")),
                Fluent.Slider("Completed out of 100", value.Binding),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    width,
                    height,
                    Fluent.Button("Reset", () => value.Value = 25))
            ],
            new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)),
            new(24));
    });
}
