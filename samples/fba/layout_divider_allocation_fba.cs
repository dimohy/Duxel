// FBA: Divider parent allocations, orientation, wrapping and clipping.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new DividerAllocationPage();
var options = DuxelApp.Options<DividerAllocationDesign>(
    Dux.App(page),
    title : "Duxel / Allocated Divider",
    width : 860,
    height : 600);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 560 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct DividerAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class DividerAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        const string paragraph = "Section details\nWrapped at the assigned width";
        var content = new UiState<string>(paragraph);
        var fixedDivider = Fluent.Divider(paragraph, new(Appearance : UiDividerAppearance.Brand, Length : 220));
        var dynamicDivider = Fluent.Divider(() => content.Value, new(Length : 220));
        var fixedBox = Dux.SizedBox(fixedDivider, 100, 120);
        var dynamicBox = Dux.SizedBox(dynamicDivider, 100, 120);
        UiSizedBox[] boxes = [fixedBox, dynamicBox];

        var width = Fluent.Button("Width : 100");
        width.Activated = () =>
        {
            var next = fixedBox.Options.Width == 100 ? 240 : 100;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Width = next };
            }
            width.Label = $"Width : {next}";
        };
        var height = Fluent.Button("Height : 120");
        height.Activated = () =>
        {
            var next = fixedBox.Options.Height == 120 ? 40 : 120;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Height = next };
            }
            height.Label = $"Height : {next}";
        };

        return Dux.Padding(
            new UiColumn(
            [
                Fluent.Title2("Allocated Divider roots"),
                Fluent.Body1("Change width, height and orientation. Lines and captions use the assigned area."),
                Dux.Row(
                    new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
                    new UiColumn(
                        [Fluent.Caption1("Static content"), fixedBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("Getter content"), dynamicBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min))),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    width,
                    height,
                    Fluent.Button("Orientation", () =>
                    {
                        var vertical = !fixedDivider.Options.Vertical;
                        fixedDivider.Options = fixedDivider.Options with { Vertical = vertical };
                        dynamicDivider.Options = dynamicDivider.Options with { Vertical = vertical };
                    })),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Button("Short", () => content.Value = "Section"),
                    Fluent.Button("Empty", () => content.Value = string.Empty),
                    Fluent.Button("Reset", () => content.Value = paragraph))
            ],
            new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)),
            new(24));
    });
}
