// FBA: generated Wrap and Grid with retained child instances and real Fluent button input.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new FlowPage();
var options = DuxelApp.Options<FlowDesign>(Dux.App(page),
    title : "Duxel / Layout Containers / Wrap and Grid", width : 760, height : 620);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 480, MinHeight = 560 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct FlowDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class FlowPage : UiWidget
{
    private readonly int[] _counts = new int[6];
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var cells = new UiWidget[_counts.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            var index = i;
            var button = Fluent.Button($"Cell {i + 1} : {_counts[i]}");
            button.Activated = () =>
            {
                _counts[index]++;
                button.Label = $"Cell {index + 1} : {_counts[index]}";
            };
            cells[i] = button;
        }
        var grid = Dux.UniformGrid(new(3, CrossAxisSpacing : 12, MainAxisSpacing : 12, MainAxisExtent : 40), cells);
        var columns = Fluent.Button("Columns : 3");
        columns.Activated = () =>
        {
            var count = grid.Options.CrossAxisCount == 3 ? 2 : 3;
            grid.Options = grid.Options with { CrossAxisCount = count };
            columns.Label = $"Columns : {count}";
        };
        var controls = new UiWidget[5];
        controls[0] = columns;
        for (var i = 1; i < controls.Length; i++)
        {
            controls[i] = Fluent.Button($"Wrap item {i}");
        }
        foreach (var child in controls)
        {
            ((UiFluentButton)child).Size = new(130, 32);
        }
        var wrap = Dux.Wrap(new(Spacing : 12, RunSpacing : 12), controls);
        var content = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Title2("Wrap and uniform Grid"),
            Fluent.Body1("Change the column count; cell counters keep their state."),
            wrap, grid,
            Fluent.Caption1("Shared generated layout, ownership and input; no internal Window or Panel."));
        return Dux.Container(content, new(Padding : new(24)));
    });
}
