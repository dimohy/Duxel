// FBA: allocated RadioGroup roots, wrapping and start-aligned option hit regions.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new RadioAllocationPage();
var options = DuxelApp.Options<RadioAllocationDesign>(
    Dux.App(page),
    title : "Duxel / Allocated Radio Groups",
    width : 900,
    height : 600);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with
    {
        MinWidth = 760,
        MinHeight = 560
    },
    Debug = new()
    {
        LogStartupTimings = true
    }
});

public readonly struct RadioAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class RadioAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var selected = new UiState<string?>("a");
        UiRadioOption[] choices =
        [
            new("a", "Alpha option wraps with words", Description : "More context belongs to this choice."),
            new("b", "Beta")
        ];

        var vertical = Dux.SizedBox(
            new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
                string.Empty, selected.Binding, choices, 300),
            160, 180);
        var horizontal = Dux.SizedBox(
            new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>>(
                string.Empty, selected.Binding, choices, 300),
            160, 180);
        var stacked = Dux.SizedBox(
            new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>>(
                string.Empty, selected.Binding, choices, 300),
            160, 180);
        UiSizedBox[] boxes = [vertical, horizontal, stacked];

        var width = Fluent.Button("Width : 160");
        width.Activated = () =>
        {
            var next = vertical.Options.Width == 160 ? 240 : 160;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Width = next };
            }
            width.Label = $"Width : {next}";
        };

        var height = Fluent.Button("Height : 180");
        height.Activated = () =>
        {
            var next = vertical.Options.Height == 180 ? 64 : 180;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Height = next };
            }
            height.Label = $"Height : {next}";
        };

        return Dux.Padding(
            new UiColumn(
            [
                Fluent.Title2("Allocated radio groups"),
                Fluent.Body1("Three layouts share one selection. Tab, arrows and Space select options."),
                Dux.Row(
                    new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                    new UiColumn(
                        [Fluent.Caption1("Vertical"), vertical],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("Horizontal"), horizontal],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("Stacked"), stacked],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min))),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    width,
                    height,
                    Fluent.Button("Reset", () => selected.Value = "a")),
                Fluent.Caption1("Width changes wrap text. Short height clips input. Blank option space does not select.")
            ],
            new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)),
            new(24));
    });
}
